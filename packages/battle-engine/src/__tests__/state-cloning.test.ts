import { describe, expect, it } from 'vitest';
import { createBattleEngine } from '../plugins/default';
import { cloneState } from '../state';
import { TEST_CONTENT, skirmishLevel } from './fixtures';
import type { GameState } from '../types';

const engine = () => createBattleEngine({ content: TEST_CONTENT });

/** A battle with some history: markers, spent resources, changed ground. */
function playedOut(): GameState {
  const battle = engine();
  const state = battle.createState(
    skirmishLevel({
      owners: [{ x: 0, y: 0, owner: 1 }, { x: 5, y: 3, owner: 2 }, { x: 3, y: 0, owner: 0 }],
      scenario: {
        zones: [{ id: 'ridge', cells: [{ x: 2, y: 1 }] }],
        triggers: [{
          id: 'raise',
          timing: 'turnStart',
          condition: { type: 'turnAtLeast', turn: 2 },
          effects: [{ type: 'addElevation', zone: 'ridge', amount: 1 }],
        }],
      },
    }),
    { seed: 5 },
  );
  for (const player of state.players) player.controller = 'ai';
  for (let index = 0; index < 40 && state.phase === 'playing'; index++) {
    battle.dispatch(state, battle.chooseAiAction(state));
  }
  return state;
}

/** Every object the two graphs have in common, named by where it sits. */
function sharedPaths(
  left: unknown,
  right: unknown,
  path = 'state',
  seen = new Set<unknown>(),
): string[] {
  if (!left || !right || typeof left !== 'object' || typeof right !== 'object') return [];
  if (left === right) return [path];
  if (seen.has(left)) return [];
  seen.add(left);
  const keys = new Set([...Object.keys(left as object), ...Object.keys(right as object)]);
  return [...keys].flatMap((key) =>
    sharedPaths((left as never)[key], (right as never)[key], `${path}.${key}`, seen));
}

/**
 * What a clone is allowed to share with its original.
 *
 * All three are *authored* declarations: the level said them once and no rule
 * writes them back. Copying them per clone would be pure cost — the AI clones a
 * state for every candidate it scores. They are listed rather than assumed,
 * because sharing anything a rule *does* write is the one isolation bug that
 * undo and AI simulation cannot survive: the simulated future would edit the
 * present, and the symptom would appear a turn later, somewhere else.
 */
const AUTHORED = [
  /** The level's ruleset, including its resource-grant tables. */
  /^state\.rules\b/,
  /** Trigger declarations. Their firing ledgers are runtime, and are copied. */
  /^state\.scenario\.triggers\b/,
  /** Objective declarations; their runtime lives in `objectiveStates`. */
  /^state\.players\.\d+\.objectives\.\d+\b/,
];

describe('cloning a battle', () => {
  /**
   * `cloneState` is written out field by field, which means a new mutable field
   * is *shared* until someone remembers to add a line — and a shared field is
   * invisible until an AI simulation edits the live battle. Enumerating what may
   * be shared turns "remembered" into "the build fails".
   */
  it('shares nothing a rule can write', () => {
    const state = playedOut();
    const unexpected = sharedPaths(state, cloneState(state))
      .filter((path) => !AUTHORED.some((allowed) => allowed.test(path)));

    expect(unexpected).toEqual([]);
  });

  it('is worth having: the state it walked really was a played-out one', () => {
    // A clone check against an empty opening state proves nothing about markers,
    // spent resources or a battlefield the scenario has changed.
    const state = playedOut();
    expect(state.turn).toBeGreaterThan(1);
    expect(state.map.elevation.some((height) => height > 0)).toBe(true);
    expect(state.units.length + state.markers.length).toBeGreaterThan(0);
  });

  it('keeps a simulated future out of the present', () => {
    const state = playedOut();
    const future = cloneState(state);
    future.units[0].hp = 1;
    future.units[0].statuses.push({ id: 'shaken', remaining: 2, stacks: 1 });
    future.players[0].objectiveStates[Object.keys(future.players[0].objectiveStates)[0]].status = 'failed';
    future.scenario.firedTriggerIds.push('imagined');
    future.map.tiles[0] = 'water';

    expect(state.units[0].hp).not.toBe(1);
    expect(state.units[0].statuses).toEqual([]);
    expect(Object.values(state.players[0].objectiveStates).some((runtime) => runtime.status === 'failed'))
      .toBe(false);
    expect(state.scenario.firedTriggerIds).not.toContain('imagined');
    expect(state.map.tiles[0]).not.toBe('water');
  });
});
