import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../engine';
import { DeterministicOnlyRandom, SplitMixRandom, createRandomState } from '../random';
import { BattleRecorder, hashState, replayBattle } from '../replay';
import { TEST_CONTENT, makeLevel, u } from './fixtures';
import type { Action, GameState, LevelData } from '../types';

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
  const recorder = new BattleRecorder(level.id, seed);

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

  it('ignores unit array order, which is not part of the outcome', () => {
    const battle = engine();
    const state = battle.createState(skirmish(), { seed: 3 });
    const before = hashState(state);
    state.units.reverse();
    expect(hashState(state)).toBe(before);
  });
});

describe('replay', () => {
  it('reproduces a full AI battle exactly', () => {
    const { state, recorder } = playRecorded(2024);
    const replay = recorder.replay();
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
    const { recorder } = playRecorded(2024);
    const tampered = {
      ...recorder.replay(),
      actions: [
        { kind: 'command', unit: 999, path: [{ x: 0, y: 0 }], command: { ability: 'wait' } } as Action,
      ],
    };
    const outcome = replayBattle(engine(), skirmish(), tampered);
    expect(outcome.divergedAt).toBe(0);
    expect(outcome.reason).not.toBe('');
  });
});
