import { describe, expect, it } from 'vitest';
import { TEST_RULES, makeLevel, testValidate, u } from './fixtures';
import { validateLevel } from '../level-validation';
import { emptyLevel } from '../level/defaults';
import { PayloadReferences } from '../payload-references';
import { DomainInvariantError } from '../domain/errors';
import type {
  LevelData,
  LevelScenario,
  Objective,
  ScenarioCondition,
  ScenarioEffect,
} from '../types';

/**
 * What the level linter refuses.
 *
 * Six hundred lines of lint had one negative test, in another package, about
 * one check. Everything else was asserted the only way an always-true test
 * looks: shipped levels produce no errors. That says nothing about whether a
 * *broken* level produces one.
 *
 * Every case here is a level that is clean except for one planted fault, and it
 * asserts exactly one error — which pins down both halves: the linter finds the
 * fault, and it does not invent a second complaint about the same thing.
 */

declare module '../types' {
  interface ScenarioEffectKindMap {
    /** A rule pack's own effect, to prove the linter needs no list of kinds. */
    lintProbe: { type: 'lintProbe'; zone: string };
  }
}

const errorsOf = (level: LevelData): string[] =>
  testValidate(level).filter((issue) => issue.severity === 'error').map((issue) => issue.message);

const baseLevel = (scenario: LevelScenario | undefined, victory?: Objective[]): LevelData =>
  makeLevel(['....', '....', '....', '....'], {
    units: [u(0, 0, 'soldier', 1), u(3, 3, 'soldier', 2)],
    structures: [{ id: 'keep', type: 'gate', x: 1, y: 1 }],
    composites: [{ id: 'fortress', parts: ['keep'] }],
    scenario,
    victory,
  });

/** A landing zone, so a case can plant an *unknown* zone against a known one. */
const KNOWN_SCENARIO: LevelScenario = { zones: [{ id: 'landing', cells: [{ x: 0, y: 1 }] }] };

const withEffect = (effect: ScenarioEffect): LevelData => baseLevel({
  ...KNOWN_SCENARIO,
  triggers: [{
    id: 'planted',
    timing: 'turnStart',
    condition: { type: 'turnAtLeast', turn: 2 },
    effects: [effect],
  }],
});

const withCondition = (condition: ScenarioCondition): LevelData => baseLevel({
  ...KNOWN_SCENARIO,
  triggers: [{
    id: 'planted',
    timing: 'turnStart',
    condition,
    effects: [{ type: 'emitSignal', signal: 'planted' }],
  }],
});

const withObjective = (objective: Objective): LevelData => baseLevel(KNOWN_SCENARIO, [objective]);

describe('level lint', () => {
  it('passes a level whose only fault is nothing', () => {
    expect(errorsOf(baseLevel(KNOWN_SCENARIO))).toEqual([]);
  });

  /**
   * A blank document opens clean.
   *
   * `emptyLevel`'s own comment said "blank, valid level" and it shipped two errors:
   * both sides had nothing to move and nothing to build, which this lint calls
   * 开局即败. So the editor opened on a red card against a document nobody had
   * touched, and its default victory condition — rout the enemy — had no enemy.
   * The blank document now gives each side the first terrain in the catalog that
   * produces anything, which is the same kind of question the blank ground already
   * asked of the catalog.
   */
  it('passes the blank document the editor starts from', () => {
    const blank = emptyLevel(TEST_RULES.content);
    expect(errorsOf(blank)).toEqual([]);
    // Each side owns somewhere to build, and the two are not the same cell.
    const homes = blank.owners.filter((entry) => entry.owner !== 0);
    expect(homes).toHaveLength(2);
    expect(new Set(homes.map((home) => `${home.x},${home.y}`)).size).toBe(2);
    expect(new Set(blank.players.map((player) => player.id)))
      .toEqual(new Set(homes.map((home) => home.owner)));
  });

  it('reports an unreadable terrain document but never hides a catalog defect', () => {
    const malformed = baseLevel(KNOWN_SCENARIO);
    malformed.terrain[0] = '?...';
    expect(errorsOf(malformed)).toEqual(['unknown terrain char "?" at 0,0']);

    const brokenEncoding = new Proxy(TEST_RULES.content.terrainEncoding, {
      get(encoding, member) {
        if (member === 'terrain') {
          return () => { throw new DomainInvariantError('catalog defect'); };
        }
        const value = Reflect.get(encoding, member, encoding);
        return typeof value === 'function' ? value.bind(encoding) : value;
      },
    });
    const brokenContent = { ...TEST_RULES.content, terrainEncoding: brokenEncoding };

    expect(() => validateLevel({ ...TEST_RULES, content: brokenContent }, baseLevel(KNOWN_SCENARIO)))
      .toThrow(DomainInvariantError);
  });

  /**
   * Each case: one planted fault, one error, and the error names the thing that
   * is wrong. Asserting the *name* rather than the sentence keeps the corpus
   * honest about semantics instead of about wording.
   */
  const cases: ReadonlyArray<readonly [string, LevelData, string]> = [
    /* ------------------------------------------------------------ effects */
    ['an effect naming an unknown status', withEffect({
      type: 'addStatus', selector: {}, status: 'blessed', duration: 2,
    }), 'blessed'],
    ['an effect selecting by an unknown zone', withEffect({
      type: 'removeStatus', selector: { zone: 'nowhere' }, status: 'poisoned',
    }), 'nowhere'],
    ['reinforcements of an unknown unit type', withEffect({
      type: 'spawnUnits', units: [{ x: 2, y: 2, unit: 'wyvern', owner: 1 }],
    }), 'wyvern'],
    ['reinforcements for a player the level never declared', withEffect({
      type: 'spawnUnits', units: [{ x: 2, y: 2, unit: 'soldier', owner: 7 }],
    }), '7'],
    ['reinforcements arriving off the map', withEffect({
      type: 'spawnUnits', units: [{ x: 9, y: 2, unit: 'soldier', owner: 1 }],
    }), '9,2'],
    ['a teleport into an unknown zone', withEffect({
      type: 'teleportUnits', selector: {}, zone: 'nowhere',
    }), 'nowhere'],
    ['an overlay of an unknown kind', withEffect({
      type: 'addOverlay', id: 'planted', overlay: 'moonlight', zone: 'landing',
    }), 'moonlight'],
    ['an overlay over an unknown zone', withEffect({
      type: 'addOverlay', id: 'planted', overlay: 'flooded', zone: 'nowhere',
    }), 'nowhere'],
    ['terrain replaced by an unknown terrain', withEffect({
      type: 'replaceTerrain', zone: 'landing', terrain: 'lava',
    }), 'lava'],
    ['a fractional elevation', withEffect({
      type: 'setElevation', zone: 'landing', value: 1.5,
    }), '整数'],
    ['a fractional elevation change', withEffect({
      type: 'addElevation', zone: 'landing', amount: 0.5,
    }), '整数'],
    ['a shove of negative distance', withEffect({
      type: 'forceMove', selector: {}, mode: 'push', source: { x: 1, y: 1 }, distance: -1,
    }), '非负整数'],
    ['a shove from off the map', withEffect({
      type: 'forceMove', selector: {}, mode: 'push', source: { x: 9, y: 9 }, distance: 1,
    }), '9,9'],
    ['a cliff between two cells that do not touch', withEffect({
      type: 'setCliffs', edges: [{ from: { x: 0, y: 0 }, to: { x: 2, y: 2 } }], blocked: true,
    }), '0,0'],
    ['directional cover off the map', withEffect({
      type: 'setDirectionalCover', covers: [{ at: { x: 8, y: 0 }, sides: { north: 'half' } }],
    }), '8,0'],
    ['damage to an unknown structure', withEffect({
      type: 'damageStructure', id: 'watchtower', amount: 10,
    }), 'watchtower'],
    ['a move of an unknown composite', withEffect({
      type: 'moveComposite', id: 'armada', dx: 1, dy: 0,
    }), 'armada'],
    ['an objective steered that nobody declared', withEffect({
      type: 'activateObjective', player: 1, id: 'secret',
    }), 'secret'],
    ['a team change for an unknown player', withEffect({
      type: 'setPlayerTeam', player: 7, team: 3,
    }), '7'],
    ['an engagement rule with no id', withEffect({
      type: 'addEngagementRule', rule: { id: '  ', zone: 'landing', mode: 'no-attacks' },
    }), 'id'],
    ['an engagement rule over an unknown zone', withEffect({
      type: 'addEngagementRule', rule: { id: 'truce', zone: 'nowhere', mode: 'no-attacks' },
    }), 'nowhere'],
    ['an engagement rule for an unknown player', withEffect({
      type: 'addEngagementRule', rule: { id: 'truce', zone: 'landing', mode: 'no-attacks', players: [7] },
    }), '7'],
    ['a standing order over an unknown zone', withEffect({
      type: 'setUnitDirective', selector: {}, directive: { mode: 'guard', zone: 'nowhere' },
    }), 'nowhere'],
    ['a patrol route leaving the map', withEffect({
      type: 'setUnitDirective', selector: {}, directive: { mode: 'patrol', waypoints: [{ x: 9, y: 1 }] },
    }), '9,1'],
    ['a rescue into an unknown zone', withEffect({
      type: 'restoreWithdrawnUnits', selector: {}, zone: 'nowhere',
    }), 'nowhere'],
    ['a revival filtered by an unknown zone', withEffect({
      type: 'reviveMarkers', selector: { zone: 'nowhere' },
    }), 'nowhere'],

    /* --------------------------------------------------------- conditions */
    ['a condition watching an unknown zone', withCondition({
      type: 'unitInZone', zone: 'nowhere',
    }), 'nowhere'],
    ['a condition counting units in an unknown zone', withCondition({
      type: 'unitCount', selector: { zone: 'nowhere' }, op: 'gte', value: 1,
    }), 'nowhere'],
    ['a condition watching an unknown structure', withCondition({
      type: 'structure', id: 'watchtower', state: 'destroyed',
    }), 'watchtower'],
    ['a condition watching an unknown composite', withCondition({
      type: 'composite', id: 'armada', state: 'neutralized',
    }), 'armada'],
    ['a condition naming an unknown player', withCondition({
      type: 'currentPlayer', player: 7,
    }), '7'],
    ['a condition watching an objective nobody declared', withCondition({
      type: 'objective', player: 1, id: 'secret', status: 'completed',
    }), 'secret'],
    ['a cycle of no length', withCondition({
      type: 'turnCycle', every: 0,
    }), '正整数'],
    ['a fault nested inside a compound condition', withCondition({
      type: 'all',
      conditions: [{ type: 'not', condition: { type: 'unitInZone', zone: 'nowhere' } }],
    }), 'nowhere'],

    /* --------------------------------------------------------- objectives */
    ['an objective naming an unknown structure', withObjective({
      type: 'destroy', structures: ['watchtower'],
    }), 'watchtower'],
    ['an objective naming an unknown composite', withObjective({
      type: 'neutralizeComposite', composite: 'armada',
    }), 'armada'],
    ['an escort with no destination zone', withObjective({
      type: 'escort', selector: {}, zone: 'nowhere', count: 1,
    }), 'nowhere'],
    ['an escort nobody can complete', withObjective({
      type: 'escort', selector: {}, zone: 'landing', count: 0,
    }), '1'],
    ['a guard duty nobody can fail', withObjective({
      type: 'protect', selector: {}, minimumAlive: 0, untilTurn: 5,
    }), '1'],
    ['an empty compound objective', withObjective({
      type: 'all', objectives: [],
    }), '空'],
    ['a fault nested inside a compound objective', withObjective({
      type: 'all', objectives: [{ type: 'destroy', structures: ['watchtower'] }],
    }), 'watchtower'],

    /* ----------------------------------------------------------- triggers */
    ['a repeat of no cadence', baseLevel({
      ...KNOWN_SCENARIO,
      triggers: [{
        id: 'planted',
        timing: 'turnStart',
        condition: { type: 'turnAtLeast', turn: 2 },
        effects: [{ type: 'emitSignal', signal: 'planted' }],
        repeat: { everyRounds: 0 },
      }],
    }), '正整数'],
    ['a repeat that ends before it starts', baseLevel({
      ...KNOWN_SCENARIO,
      triggers: [{
        id: 'planted',
        timing: 'turnStart',
        condition: { type: 'turnAtLeast', turn: 2 },
        effects: [{ type: 'emitSignal', signal: 'planted' }],
        repeat: { everyRounds: 2, startTurn: 6, endTurn: 3 },
      }],
    }), '起始'],
    ['a declared overlay over an unknown zone', baseLevel({
      ...KNOWN_SCENARIO,
      overlays: [{ id: 'fog', type: 'flooded', zone: 'nowhere' }],
    }), 'nowhere'],
    ['a declared overlay that expires before it appears', baseLevel({
      ...KNOWN_SCENARIO,
      overlays: [{ id: 'fog', type: 'flooded', zone: 'landing', remainingRounds: 0 }],
    }), '1'],
  ];

  for (const [name, level, named] of cases) {
    it(`refuses ${name}`, () => {
      const errors = errorsOf(level);
      expect(errors, errors.join('\n')).toHaveLength(1);
      expect(errors[0]).toContain(named);
    });
  }

  it('follows a losing condition hidden inside an objective', () => {
    // Neither walker used to reach it: the objective tree stops at children, and
    // the condition tree starts at triggers.
    const errors = errorsOf(withObjective({
      type: 'failOn',
      condition: { type: 'unitInZone', zone: 'nowhere' },
      objective: { type: 'routEnemies' },
    }));

    expect(errors, errors.join('\n')).toHaveLength(1);
    expect(errors[0]).toContain('nowhere');
  });

  it('lints a rule pack\'s own effect kind as thoroughly as a built-in one', () => {
    const level = withEffect({ type: 'lintProbe', zone: 'nowhere' });

    // Unregistered, the kind itself is the finding — nothing claims to know
    // what it points at.
    expect(errorsOf(level)).toEqual(['触发器 planted 引用了未注册的场景效果「lintProbe」']);

    const rules = {
      ...TEST_RULES,
      scenarioEffects: TEST_RULES.scenarioEffects.clone().register({
        kind: 'lintProbe' as const,
        references: (effect) => new PayloadReferences().zone(effect.zone),
        apply: () => {},
      }),
    };
    const errors = validateLevel(rules, level)
      .filter((issue) => issue.severity === 'error')
      .map((issue) => issue.message);

    expect(errors, errors.join('\n')).toHaveLength(1);
    expect(errors[0]).toContain('nowhere');
  });
});
