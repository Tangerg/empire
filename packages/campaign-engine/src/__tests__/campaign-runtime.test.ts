import { describe, expect, it } from 'vitest';
import { cloneContentCatalog, GlobalContentCatalog } from '@empire/battle-engine';

/** This suite composes its own catalog instead of relying on ambient state. */
const TEST_CATALOG = cloneContentCatalog(GlobalContentCatalog);
import { createState } from '@empire/battle-engine';
import { makeLevel, u } from '@empire/battle-engine/__tests__/fixtures';
import { CampaignBattleBridge } from '../battle-bridge';
import { CampaignRuntime } from '../runtime';
import { CampaignSaveMigrator, createCampaignSave } from '../save';
import type { CampaignDefinition } from '../types';

function campaign(): CampaignDefinition {
  return {
    schema: 1,
    id: 'test-campaign',
    version: 1,
    start: 'prologue',
    contentPacks: { 'empire.common': 1, 'empire.ancient-empires': 1 },
    initial: {
      flags: ['rescued-scout'],
      variables: { trust: 1 },
      roster: [{ id: 'hero', unitType: 'soldier', owner: 1 }],
    },
    nodes: [
      { id: 'prologue', type: 'story', next: 'decision', effects: [{ type: 'addVariable', key: 'trust', amount: 1 }] },
      {
        id: 'decision',
        type: 'choice',
        choices: [
          { id: 'advance', next: 'battle', condition: { type: 'flag', flag: 'rescued-scout' } },
          { id: 'withdraw', next: 'failure' },
        ],
      },
      {
        id: 'battle',
        type: 'battle',
        level: 'test-level',
        perspectivePlayer: 1,
        rosterBindings: [{ campaignUnit: 'hero', levelUnitKey: 'campaign-hero' }],
        next: { victory: 'ending', defeat: 'failure', retreat: 'decision' },
        outcomeEffects: { victory: [{ type: 'setFlag', flag: 'first-victory' }] },
      },
      { id: 'ending', type: 'ending', outcome: 'completed' },
      { id: 'failure', type: 'ending', outcome: 'failed' },
    ],
  };
}

const level = () => makeLevel(['...'], {
  units: [
    { ...u(0, 0, 'soldier', 1), key: 'campaign-hero' },
    { ...u(2, 0, 'soldier', 2), key: 'enemy' },
  ],
});

describe('generic campaign framework', () => {
  it('runs story, conditional choice, battle bridge and result projection end-to-end', () => {
    const runtime = new CampaignRuntime(campaign());
    expect(runtime.advance().id).toBe('decision');
    expect(runtime.state.variables.trust).toBe(2);
    expect(runtime.choices().map((choice) => choice.id)).toContain('advance');
    runtime.choose('advance');

    const bridge = new CampaignBattleBridge(() => level(), TEST_CATALOG);
    const request = runtime.beginBattle(bridge);
    expect(request.level.units.find((unit) => unit.key === 'campaign-hero')).toMatchObject({ rank: 0, hp: 100 });
    const battle = createState(request.level, TEST_CATALOG);
    const hero = battle.units.find((unit) => unit.key === 'campaign-hero')!;
    hero.rank = 1;
    hero.rankProgress = 42;
    hero.hp = 55;
    battle.phase = 'over';
    battle.winnerTeam = 1;
    battle.endReason = 'objective';
    const result = bridge.result(request, battle, [{ type: 'scenarioSignal', signal: 'ally.joined' }]);
    expect(result.outcome).toBe('victory');
    expect(runtime.completeBattle(result)?.id).toBe('ending');
    expect(runtime.state.roster.hero).toMatchObject({ rank: 1, rankProgress: 42, hpRatio: 0.55 });
    expect(runtime.state.flags).toContain('first-victory');
    runtime.advance();
    expect(runtime.state.status).toBe('completed');
  });

  it('locks save data to campaign and content-pack versions', () => {
    const definition = campaign();
    const runtime = new CampaignRuntime(definition);
    const save = createCampaignSave(definition, runtime.snapshot(), '2026-08-12T00:00:00.000Z');
    const loaded = new CampaignSaveMigrator().load(save, definition);
    expect(loaded.state.currentNode).toBe('prologue');
    expect(() => new CampaignSaveMigrator().load({
      ...save,
      contentPacks: { ...save.contentPacks, 'empire.common': 2 },
    }, definition)).toThrow(/content pack mismatch/);
  });

});
