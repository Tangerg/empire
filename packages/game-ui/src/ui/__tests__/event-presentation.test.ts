// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { ANCIENT_EMPIRES_LEVELS as BUILTIN_LEVELS } from '@empire/content-ancient-empires';
import { createState, type GameEvent, type GameState } from '@empire/battle-engine';
import {
  DefaultBattleEventPresenters,
  type BattleLogContext,
  type BattleStage,
} from '../event-presentation';
import type { BoardView } from '../board';

/**
 * How an event looks and how it reads used to be two `switch` statements a
 * hundred lines apart in the controller. These are the invariants of the table
 * that replaced them.
 */

const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const state = (): GameState => createState(TEST_CATALOG, BUILTIN_LEVELS[0]);

function logContext(current: GameState): BattleLogContext {
  return {
    state: current,
    content: TEST_CATALOG,
    unitName: (id) => {
      const unit = current.units.find((candidate) => candidate.id === id);
      return unit ? TEST_CATALOG.units.get(unit.type).name : '单位';
    },
    playerName: (id) => current.players.find((player) => player.id === id)?.name ?? '？',
  };
}

/** Records what an animation asked the board to do, without a real board. */
function recordingStage(current: GameState): { stage: BattleStage; calls: string[] } {
  const calls: string[] = [];
  const board = new Proxy({} as BoardView, {
    get: (_target, method) => (...args: unknown[]) => {
      calls.push(`${String(method)}(${args.map((value) => JSON.stringify(value)).join(',')})`);
      return Promise.resolve();
    },
  });
  return {
    calls,
    stage: {
      board,
      state: current,
      unit: (id) => current.units.find((candidate) => candidate.id === id) ?? null,
      structure: (id) => current.structures.find((candidate) => candidate.id === id) ?? null,
      lastSeen: () => ({ x: 9, y: 9 }),
      repaint: () => calls.push('repaint()'),
    },
  };
}

describe('battle event presentation', () => {
  it('reads a strike the way the battle log always has', () => {
    const current = state();
    const attacker = current.units[0];
    const defender = current.units[current.units.length - 1];
    const line = DefaultBattleEventPresenters.describe(logContext(current), {
      type: 'attack',
      attacker: attacker.id,
      defender: defender.id,
      damage: 30,
      killed: true,
      weapon: TEST_CATALOG.units.get(attacker.type).weapons[0],
    });

    expect(line).toBe(`${TEST_CATALOG.units.get(attacker.type).name} 造成 30 点伤害，目标阵亡`);
  });

  it('lets an event have a line without a picture, and a picture without a line', () => {
    const current = state();
    const rankUp: GameEvent = { type: 'rankChanged', unit: current.units[0].id, from: 0, to: 1 };
    const death: GameEvent = { type: 'death', unit: current.units[0].id, at: { x: 1, y: 1 } };

    expect(DefaultBattleEventPresenters.describe(logContext(current), rankUp)).toContain('晋升');
    expect(DefaultBattleEventPresenters.tryGet('rankChanged')?.animate).toBeUndefined();
    expect(DefaultBattleEventPresenters.describe(logContext(current), death)).toBe('');
    expect(DefaultBattleEventPresenters.tryGet('death')?.animate).toBeDefined();
  });

  it('lands a blow on the tile a unit died in', async () => {
    const current = state();
    const attacker = current.units[0];
    const { stage, calls } = recordingStage(current);
    await DefaultBattleEventPresenters.animate(stage, {
      type: 'attack',
      attacker: attacker.id,
      defender: 9999, // already gone
      weapon: TEST_CATALOG.units.get(attacker.type).weapons[0],
      damage: 12,
      killed: true,
    });

    // The damage number still has to appear somewhere the player was looking.
    expect(calls.some((call) => call.startsWith('animateHit({"x":9,"y":9}'))).toBe(true);
  });

  it('passes over an event nobody presents, in silence', async () => {
    const current = state();
    const unread: GameEvent = { type: 'roundStart', turn: 3 };

    expect(DefaultBattleEventPresenters.describe(logContext(current), unread)).toBe('');
    const { stage, calls } = recordingStage(current);
    await DefaultBattleEventPresenters.animate(stage, unread);
    expect(calls).toEqual([]);
  });

  it('keeps a replaced presenter out of the shared default', () => {
    const themed = DefaultBattleEventPresenters.clone().replace({
      type: 'gameOver',
      describe: () => '幕落',
    });
    const current = state();

    expect(themed.describe(logContext(current), { type: 'gameOver', team: 1, reason: '敌军已被全歼' }))
      .toBe('幕落');
    expect(DefaultBattleEventPresenters.describe(logContext(current), { type: 'gameOver', team: 1, reason: '敌军已被全歼' }))
      .toBe('敌军已被全歼');
  });
});
