import { describe, expect, it } from 'vitest';
import { applyAction, commandOptions, IllegalActionError } from '../actions';
import { computeDamage } from '../combat';
import { createBattleEngine } from '../engine';
import { computeMoveField } from '../movement';
import { GameSession } from '../session';
import { cloneState, createState } from '../state';
import { StatusBehaviorRegistry, addStatus } from '../statuses';
import { makeLevel, u } from './fixtures';

describe('formal tactical statuses', () => {
  it('applies typed combat modifiers outside Unit.meta', () => {
    const baseline = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'knight', 2)] }),
    );
    const affected = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'knight', 2)] }),
    );
    addStatus(affected.units[1], 'armor_down', 2);
    expect(computeDamage(affected, affected.units[0], affected.units[1]).damage).toBeGreaterThan(
      computeDamage(baseline, baseline.units[0], baseline.units[1]).damage,
    );
    expect(affected.units[1].meta).toEqual({});
  });

  it('uses shaken for movement and capture restrictions', () => {
    const s = createState(
      makeLevel(['v....'], {
        units: [u(0, 0, 'soldier', 1), u(4, 0, 'soldier', 2)],
      }),
    );
    addStatus(s.units[0], 'shaken', 1);
    const field = computeMoveField(s, s.units[0]);
    expect(Math.max(...[...field.tiles.values()].map((tile) => tile.cost))).toBe(2);
    expect(commandOptions(s, s.units[0], { x: 0, y: 0 }).map((option) => option.ability)).not.toContain(
      'capture',
    );
  });

  it('ticks and expires poison at the owner turn start without killing', () => {
    const s = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1, 3), u(1, 0, 'soldier', 2)] }),
    );
    addStatus(s.units[0], 'poisoned', 1);
    applyAction(s, { kind: 'endTurn' });
    const events = applyAction(s, { kind: 'endTurn' });
    expect(s.units[0].hp).toBe(1);
    expect(s.units[0].statuses).toEqual([]);
    expect(events.some((event) => event.type === 'statusTick')).toBe(true);
    expect(events.some((event) => event.type === 'statusRemoved')).toBe(true);
  });

  it('deep-clones status instances for AI and undo simulations', () => {
    const s = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    addStatus(s.units[0], 'inspired', 2);
    const clone = cloneState(s);
    clone.units[0].statuses[0].remaining = 1;
    expect(s.units[0].statuses[0].remaining).toBe(2);
  });

  it('uses status tags to block arcane weapons in menus and authoritative validation', () => {
    const state = createState(
      makeLevel(['..'], { units: [u(0, 0, 'mage', 1), u(1, 0, 'soldier', 2)] }),
    );
    addStatus(state.units[0], 'silenced', 2);
    const options = commandOptions(state, state.units[0], { x: 0, y: 0 });
    expect(options.some((option) => option.ability === 'attack')).toBe(false);
    expect(() => applyAction(state, {
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
      createBattleEngine({ statusBehaviors: behaviors }),
    );
    addStatus(session.state.units[0], 'inspired', 3);
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
