import { describe, expect, it } from 'vitest';
import { IllegalActionError } from '../domain/errors';
import { createBattleEngine } from '../plugins/default';
import { GameSession } from '../session';
import { cloneState } from '../state';
import { StatusBehaviorRegistry } from '../statuses';
import { makeLevel, testAddStatus, testApply, testCommands, testDamage, testMoveField, testState, TEST_CONTENT, u } from './fixtures';

describe('formal tactical statuses', () => {
  it('applies combat modifiers through typed status state', () => {
    const baseline = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'knight', 2)] }),
    );
    const affected = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'knight', 2)] }),
    );
    testAddStatus(affected.units[1], 'armor_down', 2);
    expect(testDamage(affected, affected.units[0], affected.units[1]).damage).toBeGreaterThan(
      testDamage(baseline, baseline.units[0], baseline.units[1]).damage,
    );
    expect(affected.units[1].statuses).toEqual([
      expect.objectContaining({ id: 'armor_down', remaining: 2 }),
    ]);
  });

  it('uses shaken for movement and capture restrictions', () => {
    const s = testState(
      makeLevel(['v....'], {
        units: [u(0, 0, 'soldier', 1), u(4, 0, 'soldier', 2)],
      }),
    );
    testAddStatus(s.units[0], 'shaken', 1);
    const field = testMoveField(s, s.units[0]);
    expect(Math.max(...[...field.tiles.values()].map((tile) => tile.cost))).toBe(2);
    expect(testCommands(s, s.units[0], { x: 0, y: 0 }).map((option) => option.ability)).not.toContain(
      'capture',
    );
  });

  it('ticks and expires poison at the owner turn start without killing', () => {
    const s = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1, 3), u(1, 0, 'soldier', 2)] }),
    );
    testAddStatus(s.units[0], 'poisoned', 1);
    testApply(s, { kind: 'endTurn' });
    const events = testApply(s, { kind: 'endTurn' });
    expect(s.units[0].hp).toBe(1);
    expect(s.units[0].statuses).toEqual([]);
    expect(events.some((event) => event.type === 'statusTick')).toBe(true);
    expect(events.some((event) => event.type === 'statusRemoved')).toBe(true);
  });

  it('deep-clones status instances for AI and undo simulations', () => {
    const s = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    testAddStatus(s.units[0], 'inspired', 2);
    const clone = cloneState(s);
    clone.units[0].statuses[0].remaining = 1;
    expect(s.units[0].statuses[0].remaining).toBe(2);
  });

  it('uses status tags to block arcane weapons in menus and authoritative validation', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 2)] }),
    );
    testAddStatus(state.units[0], 'silenced', 2);
    const options = testCommands(state, state.units[0], { x: 0, y: 0 });
    expect(options.some((option) => option.ability === 'attack')).toBe(false);
    expect(() => testApply(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: 'mage_bolt', target: { x: 1, y: 0 } },
    })).toThrow(IllegalActionError);
  });

  it('injects a custom lifecycle behavior through BattleEngine', () => {
    const behaviors = new StatusBehaviorRegistry().register({
      id: 'inspired',
      onOwnerTurnStart: (context) => {
        context.damage(7);
      },
    });
    const session = new GameSession(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
      createBattleEngine({ content: TEST_CONTENT, statusBehaviors: behaviors }),
    );
    testAddStatus(session.state.units[0], 'inspired', 3);
    session.dispatch({ kind: 'endTurn' });
    const events = session.dispatch({ kind: 'endTurn' });

    expect(session.state.units[0].hp).toBe(93);
    expect(events).toContainEqual(expect.objectContaining({
      type: 'statusTick',
      status: 'inspired',
      amount: 7,
    }));
  });
});
