import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { DeterministicOnlyRandom, SplitMixRandom, createRandomState } from '../random';
import { BattleRecorder, hashState, replayBattle } from '../replay';
import { CoreActionHandlers } from '../actions';
import { DomainInvariantError } from '../domain/errors';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import type { Action, GameState, LevelData } from '../types';
import type { EnginePlugin } from '../kernel';
import { requirePersistentRuleset } from '../ruleset-manifest';

const engine = () => createBattleEngine({ content: TEST_CONTENT });

const skirmish = (): LevelData =>
  makeLevel(['C..v..', '.T..T.', '..h...', 'v....C'], {
    units: [
      u(1, 0, 'soldier', 1),
      u(0, 2, 'archer', 1),
      u(4, 3, 'soldier', 2),
      u(5, 2, 'knight', 2),
    ],
    owners: [
      { x: 0, y: 0, owner: 1 },
      { x: 5, y: 3, owner: 2 },
      { x: 3, y: 0, owner: 0 },
      { x: 0, y: 3, owner: 0 },
    ],
    funds: [200, 200],
  });

/** Plays a whole battle with the AI on both sides, recording every action. */
function playRecorded(seed: number): { state: GameState; recorder: BattleRecorder } {
  const battle = engine();
  const level = skirmish();
  const state = battle.createState(level, { seed });
  for (const player of state.players) player.controller = 'ai';
  const recorder = new BattleRecorder(battle, state);

  for (let guard = 0; guard < 1500 && state.phase === 'playing'; guard += 1) {
    const action: Action = battle.chooseAiAction(state);
    recorder.record(action);
    battle.dispatch(state, action);
  }
  return { state, recorder };
}

describe('seeded randomness', () => {
  it('produces the same sequence for the same seed', () => {
    const first = { random: createRandomState(1234) } as GameState;
    const second = { random: createRandomState(1234) } as GameState;
    const draw = (state: GameState) =>
      Array.from({ length: 8 }, () => SplitMixRandom.next(state, 'test'));
    expect(draw(first)).toEqual(draw(second));
  });

  it('produces a different sequence for a different seed', () => {
    const a = { random: createRandomState(1) } as GameState;
    const b = { random: createRandomState(2) } as GameState;
    expect(SplitMixRandom.next(a, 'test')).not.toBe(SplitMixRandom.next(b, 'test'));
  });

  it('keeps streams independent so a new consumer cannot shift an old one', () => {
    const alone = { random: createRandomState(99) } as GameState;
    const shared = { random: createRandomState(99) } as GameState;

    const expected = Array.from({ length: 4 }, () => SplitMixRandom.next(alone, 'combat'));
    // A second consumer draws in between; the first stream must be unaffected.
    const actual: number[] = [];
    for (let index = 0; index < 4; index += 1) {
      SplitMixRandom.next(shared, 'weather');
      actual.push(SplitMixRandom.next(shared, 'combat'));
    }
    expect(actual).toEqual(expected);
  });

  it('peeks without advancing the stream', () => {
    const state = { random: createRandomState(7) } as GameState;
    const peeked = SplitMixRandom.peek(state, 'test');
    expect(SplitMixRandom.peek(state, 'test')).toBe(peeked);
    expect(SplitMixRandom.next(state, 'test')).toBe(peeked);
    expect(SplitMixRandom.next(state, 'test')).not.toBe(peeked);
  });

  it('stays uniform enough to be usable as a chance roll', () => {
    const state = { random: createRandomState(4242) } as GameState;
    let hits = 0;
    for (let index = 0; index < 4000; index += 1) {
      if (SplitMixRandom.chance(state, 'roll', 30)) hits += 1;
    }
    expect(hits / 4000).toBeGreaterThan(0.26);
    expect(hits / 4000).toBeLessThan(0.34);
  });

  it('travels with the state, so a clone draws the same numbers', () => {
    const battle = engine();
    const state = battle.createState(skirmish(), { seed: 5 });
    SplitMixRandom.next(state, 'combat');
    const copy = battle.cloneState(state);
    expect(SplitMixRandom.next(copy, 'combat')).toBe(SplitMixRandom.next(state, 'combat'));
  });

  it('lets a ruleset forbid variance outright', () => {
    const state = { random: createRandomState(1) } as GameState;
    expect(() => DeterministicOnlyRandom.next(state, 'combat')).toThrow(/exact forecasts/);
  });

  it('exposes a seeded scenario condition that is reproducible', () => {
    const battle = engine();
    const roll = (seed: number) => {
      const state = battle.createState(skirmish(), { seed });
      return Array.from({ length: 6 }, () =>
        battle.rules.scenarioConditions.evaluate(
          state,
          { type: 'chance', percent: 50 },
          TEST_CONTENT,
        ));
    };
    expect(roll(11)).toEqual(roll(11));
    expect(roll(11)).not.toEqual(roll(12));
  });
});

describe('state hashing', () => {
  it('is stable across a clone and sensitive to a real change', () => {
    const battle = engine();
    const state = battle.createState(skirmish(), { seed: 3 });
    expect(hashState(battle.cloneState(state))).toBe(hashState(state));

    const before = hashState(state);
    state.units[0].hp -= 1;
    expect(hashState(state)).not.toBe(before);
  });

  it('covers state the old projection had grown past', () => {
    // The digest used to be a hand-written list of about twenty fields, and it
    // had drifted: these six changes each left the hash untouched.
    const battle = engine();
    const state = battle.createState(skirmish(), { seed: 3 });
    const changes: Array<[string, () => void]> = [
      ['a passenger', () => state.embarkedUnits.push({ carrier: 1, unit: { ...state.units[0] } })],
      ['a spent tactic', () => state.commanders.push({
        id: 'c', unitId: 1, owner: 1, radius: 2,
        aura: { attackMultiplier: 1, defenseDelta: 0, movementDelta: 0 },
        turnGrants: [], tactics: [], usedTactics: ['rally'],
      })],
      ['a no-fight zone', () => state.scenario.engagementRules.push({ id: 'truce', zone: 'z', mode: 'no-attacks' })],
      ['a blocked edge', () => state.map.cliffs.push({ from: { x: 0, y: 0 }, to: { x: 1, y: 0 } })],
      ['directional cover', () => state.map.directionalCover.push({ at: { x: 0, y: 0 }, sides: { north: 'full' } })],
      ['a patrol route', () => { state.units[0].directive.waypoints.push({ x: 2, y: 2 }); }],
    ];
    const unnoticed = changes.filter(([, change]) => {
      const before = hashState(state);
      change();
      return hashState(state) === before;
    });

    expect(unnoticed.map(([name]) => name)).toEqual([]);
  });

  it('reads the unit list as an order, because a side turn does', () => {
    // The digest used to sort the unit list on the grounds that order was
    // bookkeeping. It is not: a side turn's actor list *is* the unit list, and
    // the AI keeps the first of two equally-scored candidates — so the same
    // board with two units swapped can be ordered around differently from the
    // next action on. A deterministic engine reproduces the order too.
    const battle = engine();
    const state = battle.createState(skirmish(), { seed: 3 });
    const before = hashState(state);
    state.units.reverse();
    expect(hashState(state)).not.toBe(before);
  });

  it('reads an absent field and an undefined one alike, so a save round-trips', () => {
    // JSON drops `key: undefined`; `structuredClone` keeps it. A digest that
    // told the two apart would report every reloaded battle as diverged.
    const battle = engine();
    const state = battle.createState(skirmish(), { seed: 3 });
    const before = hashState(state);
    expect(hashState(JSON.parse(JSON.stringify(state)) as GameState)).toBe(before);
  });

  it('ignores session fields by domain path, not every same-named extension value', () => {
    const battle = engine();
    const state = battle.createState(skirmish(), { seed: 3 });
    const before = hashState(state);
    state.players[0].controller = 'human';
    expect(hashState(state)).toBe(before);

    state.scenario.variables.controller = 'mind-control-rule';
    expect(hashState(state)).not.toBe(before);
    const afterController = hashState(state);
    state.scenario.variables.levelName = 'semantic-plugin-state';
    expect(hashState(state)).not.toBe(afterController);
  });
});

describe('replay', () => {
  it('freezes the composed identity and refuses persistence without authored pack versions', () => {
    const manifest = engine().rulesetManifest;
    expect(Object.isFrozen(manifest)).toBe(true);
    expect(Object.isFrozen(manifest.plugins)).toBe(true);
    expect(Object.isFrozen(manifest.contentPacks)).toBe(true);
    expect(() => requirePersistentRuleset({ plugins: { engine: 1 }, contentPacks: {} }))
      .toThrow(/no versioned content pack/);
  });

  it('reproduces a full AI battle exactly', () => {
    const { state, recorder } = playRecorded(2024);
    const replay = recorder.replay(state);
    expect(replay.actions.length).toBeGreaterThan(10);

    const outcome = replayBattle(engine(), skirmish(), replay);
    expect(outcome.divergedAt).toBeNull();
    expect(hashState(outcome.state)).toBe(hashState(state));
    expect(outcome.state.turn).toBe(state.turn);
    expect(outcome.state.winnerTeam).toBe(state.winnerTeam);
  });

  it('reproduces the same battle twice from the same seed', () => {
    expect(hashState(playRecorded(77).state)).toBe(hashState(playRecorded(77).state));
  });

  it('reports where a recording stops applying instead of failing silently', () => {
    const { state, recorder } = playRecorded(2024);
    const tampered = {
      ...recorder.replay(state),
      actions: [
        { kind: 'command', unit: 999, path: [{ x: 0, y: 0 }], command: { ability: 'wait' } } as Action,
      ],
    };
    const outcome = replayBattle(engine(), skirmish(), tampered);
    expect(outcome.divergedAt).toBe(0);
    expect(outcome.reason).not.toBe('');
  });

  it('reports a refused recorded order but propagates an engine defect', () => {
    const handlers = CoreActionHandlers.clone().replace({
      kind: 'endTurn',
      execute: () => { throw new DomainInvariantError('broken turn lifecycle'); },
    });
    const brokenRules: EnginePlugin = {
      id: 'test.broken-turn',
      version: 1,
      overrides: ['actionHandlers'],
      install: (context) => context.replace('actionHandlers', handlers),
    };
    const battle = createBattleEngine({ content: TEST_CONTENT, plugins: [brokenRules] });
    const level = skirmish();
    const state = battle.createState(level, { seed: 5 });
    const recorder = new BattleRecorder(battle, state);
    recorder.record({ kind: 'endTurn' });

    expect(() => replayBattle(battle, level, recorder.replay(state)))
      .toThrow(DomainInvariantError);
  });

  it('refuses a compatible-looking action stream when its ruleset or hashes differ', () => {
    const { state, recorder } = playRecorded(2024);
    const replay = recorder.replay(state);

    expect(replayBattle(engine(), skirmish(), {
      ...replay,
      initialStateHash: '00000000',
    }).reason).toMatch(/初始状态摘要/);

    expect(replayBattle(engine(), skirmish(), {
      ...replay,
      finalStateHash: '00000000',
    }).reason).toMatch(/最终状态摘要/);

    expect(replayBattle(engine(), skirmish(), {
      ...replay,
      ruleset: {
        ...replay.ruleset,
        contentPacks: { ...replay.ruleset.contentPacks, 'empire.common': 999 },
      },
    }).reason).toMatch(/content pack "empire\.common" version/);
  });
});
