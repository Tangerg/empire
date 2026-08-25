import { describe, expect, it } from 'vitest';
import {
  createBattleEngine,
  normaliseLevel,
  StoredDocumentError,
  type LevelData,
} from '@empire/battle-engine';
import { CampaignBattleBridge, DEFAULT_MARKER_DISPOSITIONS } from '../battle-bridge';
import { CampaignRuntime } from '../runtime';
import {
  CAMPAIGN_SAVE_SCHEMA,
  createCampaignSave,
  loadCampaignSave,
} from '../save';
import type { CampaignDefinition } from '../types';
import { CampaignInvariantError } from '../errors';
import { createTestCatalog } from '@empire/test-content';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog();
const engine = () => createBattleEngine({ content: TEST_CATALOG });

const level = (): LevelData =>
  normaliseLevel({
    schema: 2,
    id: 'save-arena',
    name: 'save arena',
    width: 4,
    height: 2,
    terrain: ['C...', '...C'],
    owners: [{ x: 0, y: 0, owner: 1 }, { x: 3, y: 1, owner: 2 }],
    units: [
      { key: 'hero', x: 1, y: 0, unit: 'soldier', owner: 1 },
      { key: 'ally', x: 2, y: 0, unit: 'archer', owner: 1 },
      { key: 'foe', x: 3, y: 0, unit: 'soldier', owner: 2 },
    ],
    players: [
      {
        id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human',
        resources: { funds: { current: 0, capacity: null } },
      },
      {
        id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'ai',
        resources: { funds: { current: 0, capacity: null } },
      },
    ],
    rules: {},
    victory: [{ type: 'routEnemies' }],
  });

const definition = (): CampaignDefinition => ({
  schema: 1,
  id: 'save-campaign',
  version: 3,
  start: 'fight',
  contentPacks: { 'empire.common': 1 },
  nodes: [
    {
      id: 'fight',
      type: 'battle',
      level: 'save-arena',
      rosterBindings: [
        { campaignUnit: 'hero', levelUnitKey: 'hero' },
        { campaignUnit: 'ally', levelUnitKey: 'ally' },
      ],
      next: { victory: 'won', defeat: 'lost', retreat: 'lost' },
    },
    { id: 'won', type: 'ending', outcome: 'completed', presentation: 'test/won' },
    { id: 'lost', type: 'ending', outcome: 'failed', presentation: 'test/lost' },
  ],
  initial: {
    roster: [
      { id: 'hero', unitType: 'soldier', owner: 1, rank: 1, rankProgress: 30 },
      { id: 'ally', unitType: 'archer', owner: 1 },
    ],
  },
});

describe('campaign save', () => {
  it('round-trips through the current save reader', () => {
    const runtime = new CampaignRuntime(definition());
    const save = createCampaignSave(definition(), runtime.state, '2026-01-01T00:00:00.000Z');
    const loaded = loadCampaignSave(JSON.parse(JSON.stringify(save)), definition());

    expect(loaded.schema).toBe(CAMPAIGN_SAVE_SCHEMA);
    expect(loaded.state).toEqual(runtime.state);
    expect(loaded.savedAt).toBe('2026-01-01T00:00:00.000Z');
  });

  it('copies state so later play cannot mutate an existing save', () => {
    const runtime = new CampaignRuntime(definition());
    const save = createCampaignSave(definition(), runtime.state);
    runtime.state.flags.push('after-save');
    expect(save.state.flags).not.toContain('after-save');
  });

  it('refuses a save from a different campaign or version', () => {
    const runtime = new CampaignRuntime(definition());
    const save = createCampaignSave(definition(), runtime.state);
    expect(() => loadCampaignSave(save, { ...definition(), version: 4 }))
      .toThrow(/identity\/version mismatch/);
    expect(() => loadCampaignSave(save, { ...definition(), id: 'other' }))
      .toThrow(/identity\/version mismatch/);
  });

  it('refuses a save whose content packs no longer match', () => {
    const runtime = new CampaignRuntime(definition());
    const save = createCampaignSave(definition(), runtime.state);
    expect(() => loadCampaignSave(save, {
      ...definition(),
      contentPacks: { 'empire.common': 2 },
    })).toThrow(/content pack mismatch/);
  });

  it('refuses extra pack identities instead of checking only the expected subset', () => {
    const runtime = new CampaignRuntime(definition());
    const save = createCampaignSave(definition(), runtime.state);
    expect(() => loadCampaignSave({
      ...save,
      contentPacks: { ...save.contentPacks, 'foreign.rules': 1 },
    }, definition())).toThrow(/content pack mismatch: "foreign\.rules"/);
  });

  it('classifies a malformed raw state as a stored-document problem', () => {
    const runtime = new CampaignRuntime(definition());
    const save = createCampaignSave(definition(), runtime.state) as unknown as {
      state: Record<string, unknown>;
    };
    delete save.state.flags;

    expect(() => loadCampaignSave(save, definition()))
      .toThrow(StoredDocumentError);
    expect(() => loadCampaignSave(save, definition()))
      .toThrow(/state\.flags is missing or invalid/);
  });

  it('refuses an invalid live aggregate before writing it', () => {
    const runtime = new CampaignRuntime(definition());
    runtime.state.roster.hero.hpRatio = 2;
    expect(() => createCampaignSave(definition(), runtime.state)).toThrow(CampaignInvariantError);

    const malformed = new CampaignRuntime(definition());
    (malformed.state.variables as Record<string, unknown>).bad = {};
    expect(() => createCampaignSave(definition(), malformed.state)).toThrow(CampaignInvariantError);

    expect(() => createCampaignSave({
      ...definition(),
      contentPacks: { common: 0 },
    }, new CampaignRuntime(definition()).state)).toThrow(CampaignInvariantError);
  });

  it('accepts only the current save schema', () => {
    const runtime = new CampaignRuntime(definition());
    const save = { ...createCampaignSave(definition(), runtime.state), schema: 0 };
    expect(() => loadCampaignSave(save, definition())).toThrow(/unsupported campaign save schema 0/);
  });

  it('rejects non-object payloads', () => {
    expect(() => loadCampaignSave('nope', definition())).toThrow(/must be an object/);
    expect(() => loadCampaignSave([], definition())).toThrow(/must be an object/);
  });
});

describe('campaign battle bridge', () => {
  const bridge = () => new CampaignBattleBridge(() => level(), TEST_CATALOG);

  it('seeds level units from persistent roster state', () => {
    const runtime = new CampaignRuntime(definition());
    runtime.state.roster.hero.hpRatio = 0.5;
    runtime.state.roster.hero.rank = 2;
    runtime.state.roster.hero.rankProgress = 44;

    const request = runtime.beginBattle(bridge());
    const hero = request.level.units.find((unit) => unit.key === 'hero')!;

    expect(hero.hp).toBe(Math.round(TEST_CATALOG.units.get('soldier').maxHp * 0.5));
    expect(hero.rank).toBe(2);
    expect(hero.rankProgress).toBe(44);
  });

  it('removes a fallen roster member from the battlefield and from the bindings', () => {
    const runtime = new CampaignRuntime(definition());
    runtime.state.roster.ally.disposition = 'fallen';

    const request = runtime.beginBattle(bridge());
    expect(request.level.units.some((unit) => unit.key === 'ally')).toBe(false);
    expect(request.rosterBindings.map((binding) => binding.campaignUnit)).toEqual(['hero']);
  });

  it('carries battle growth back into the campaign roster', () => {
    const runtime = new CampaignRuntime(definition());
    const bridgeInstance = bridge();
    const request = runtime.beginBattle(bridgeInstance);

    const battle = engine();
    const state = battle.createState(request.level);
    const hero = state.units.find((unit) => unit.key === 'hero')!;
    hero.rank = 2;
    hero.rankProgress = 77;
    hero.hp = Math.round(TEST_CATALOG.units.get('soldier').maxHp * 0.4);
    state.units = state.units.filter((unit) => unit.owner === 1);
    state.phase = 'over';
    state.winnerTeam = 1;

    const result = bridgeInstance.result(request, state, []);
    runtime.completeBattle(result);

    expect(result.outcome).toBe('victory');
    expect(runtime.state.roster.hero.rank).toBe(2);
    expect(runtime.state.roster.hero.rankProgress).toBe(77);
    expect(runtime.state.roster.hero.hpRatio).toBeCloseTo(0.4, 2);
    expect(runtime.state.roster.hero.disposition).toBe('available');
  });

  it('classifies an unfinished battle as a retreat', () => {
    const runtime = new CampaignRuntime(definition());
    const bridgeInstance = bridge();
    const request = runtime.beginBattle(bridgeInstance);
    const state = engine().createState(request.level);

    expect(bridgeInstance.result(request, state, []).outcome).toBe('retreat');
  });

  it('reports scenario signals and event counts to the campaign layer', () => {
    const runtime = new CampaignRuntime(definition());
    const bridgeInstance = bridge();
    const request = runtime.beginBattle(bridgeInstance);
    const state = engine().createState(request.level);
    state.phase = 'over';
    state.winnerTeam = 1;
    state.scenario.eventCounts.attack = 4;

    const result = bridgeInstance.result(request, state, [
      { type: 'scenarioSignal', signal: 'village-saved' },
      { type: 'turnEnd', player: 1 },
    ]);

    expect(result.signals).toEqual(['village-saved']);
    expect(result.eventCounts.attack).toBe(4);
  });

  it('refuses to prepare a second battle while one is pending', () => {
    const runtime = new CampaignRuntime(definition());
    runtime.beginBattle(bridge());
    expect(() => runtime.beginBattle(bridge())).toThrow(/pending battle/);
  });

  it('fails loudly when a binding names a level key that does not exist', () => {
    const broken: CampaignDefinition = {
      ...definition(),
      nodes: definition().nodes.map((node) =>
        node.type === 'battle'
          ? { ...node, rosterBindings: [{ campaignUnit: 'hero', levelUnitKey: 'ghost' }] }
          : node),
    };
    const runtime = new CampaignRuntime(broken);
    expect(() => runtime.beginBattle(bridge())).toThrow(/no unit key "ghost"/);
  });
});

describe('how leaving the field reads to a roster', () => {
  it('calls an unrecognised departure missing, not dead', () => {
    // The translation was a ternary whose fallthrough answered `fallen` —
    // permanently dead — for every marker kind it did not know, and marker kinds
    // are an open string. A story pack's own way off the field would have
    // silently killed the unit.
    expect(DEFAULT_MARKER_DISPOSITIONS['corpse']).toBe('fallen');
    expect(DEFAULT_MARKER_DISPOSITIONS['transport-loss']).toBe('fallen');
    expect(DEFAULT_MARKER_DISPOSITIONS['withdrawn']).toBe('missing');
    expect(DEFAULT_MARKER_DISPOSITIONS['test.captured']).toBeUndefined();
  });
});
