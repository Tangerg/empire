import { describe, expect, it } from 'vitest';
import { applyAction, IllegalActionError } from '../actions';
import { commanderAuraFor, tacticOptions } from '../commanders';
import { computeMoveField } from '../movement';
import { cloneState } from '../state';
import { TEST_CONTENT, TEST_RULES, makeLevel, testDamage, testState, u } from './fixtures';
import { COMMAND_POINTS_RESOURCE, MOMENTUM_RESOURCE } from '../resources';

function commandLevel() {
  const leader = { ...u(0, 0, 'soldier', 1), key: 'leader' };
  const troop = { ...u(1, 0, 'soldier', 1), key: 'troop', commander: 'alpha' };
  return makeLevel(['.......'], {
    units: [leader, troop, u(6, 0, 'ogre', 2)],
    commanders: [
      {
        id: 'alpha',
        unitKey: 'leader',
        radius: 1,
        aura: { attackMultiplier: 1.2, defenseDelta: 0.1, movementDelta: 1 },
        turnGrants: [{ resource: COMMAND_POINTS_RESOURCE, amount: 1 }],
        tactics: ['rally', 'steady'],
      },
    ],
  });
}

describe('commanders and formation resources', () => {
  it('applies a local aura only to linked units inside command range', () => {
    const state = testState(commandLevel());
    const troop = state.units[1];
    const enemy = state.units[2];
    expect(commanderAuraFor(state, troop)).toMatchObject({
      attackMultiplier: 1.2,
      defenseDelta: 0.1,
      movementDelta: 1,
    });
    const commanded = testDamage(state, troop, enemy).damage;
    expect(Math.max(...[...computeMoveField(state, troop, TEST_CONTENT).tiles.values()].map((tile) => tile.cost))).toBe(4);

    troop.x = 2;
    expect(commanderAuraFor(state, troop).attackMultiplier).toBe(1);
    expect(testDamage(state, troop, enemy).damage).toBeLessThan(commanded);
  });

  it('spends command points on a data-defined area tactic', () => {
    const level = commandLevel();
    level.players[0].resources[COMMAND_POINTS_RESOURCE] = { current: 2, capacity: 4 };
    const state = testState(level);
    expect(tacticOptions(TEST_RULES, state, 'alpha').map((option) => option.id)).toContain('rally');

    const events = applyAction(state, {
      kind: 'tactic',
      commander: 'alpha',
      tactic: 'rally',
      target: { x: 1, y: 0 },
    }, TEST_RULES);
    expect(state.players[0].resources[COMMAND_POINTS_RESOURCE].current).toBe(0);
    expect(state.units[1].statuses).toContainEqual(
      expect.objectContaining({ id: 'inspired', sourceUnitId: state.units[0].id }),
    );
    expect(events).toContainEqual(
      expect.objectContaining({ type: 'tacticUsed', commander: 'alpha', tactic: 'rally' }),
    );
    expect(() =>
      applyAction(state, {
        kind: 'tactic',
        commander: 'alpha',
        tactic: 'rally',
        target: { x: 1, y: 0 },
      }, TEST_RULES),
    ).toThrow(IllegalActionError);
  });

  it('regenerates command points and resets tactic use on the owner turn', () => {
    const level = commandLevel();
    level.players[0].resources[COMMAND_POINTS_RESOURCE] = { current: 2, capacity: 4 };
    const state = testState(level);
    applyAction(state, {
      kind: 'tactic',
      commander: 'alpha',
      tactic: 'steady',
      target: { x: 1, y: 0 },
    }, TEST_RULES);
    expect(state.players[0].resources[COMMAND_POINTS_RESOURCE].current).toBe(1);
    applyAction(state, { kind: 'endTurn' }, TEST_RULES);
    const events = applyAction(state, { kind: 'endTurn' }, TEST_RULES);
    expect(state.players[0].resources[COMMAND_POINTS_RESOURCE].current).toBe(2);
    expect(state.commanders[0].usedTactics).toEqual([]);
    expect(events).toContainEqual(
      expect.objectContaining({
        type: 'resourceChanged',
        resource: COMMAND_POINTS_RESOURCE,
        subject: { kind: 'player', id: 1 },
        amount: 1,
      }),
    );
  });

  it('deep-clones commander and hero runtime state', () => {
    const level = commandLevel();
    level.units[0].resources = { [MOMENTUM_RESOURCE]: { current: 0, capacity: 5 } };
    const state = testState(level);
    const copy = cloneState(state);
    copy.commanders[0].aura.attackMultiplier = 9;
    copy.commanders[0].usedTactics.push('rally');
    copy.units[0].resources[MOMENTUM_RESOURCE].current = 4;
    copy.players[0].resources[COMMAND_POINTS_RESOURCE].current = 4;
    expect(state.commanders[0].aura.attackMultiplier).toBe(1.2);
    expect(state.commanders[0].usedTactics).toEqual([]);
    expect(state.units[0].resources[MOMENTUM_RESOURCE].current).toBe(0);
    expect(state.players[0].resources[COMMAND_POINTS_RESOURCE].current).toBe(0);
  });

  it('removes the aura and shocks linked troops when the commander falls', () => {
    const state = testState(
      makeLevel(['...'], {
        units: [
          u(0, 0, 'knight', 1),
          { ...u(1, 0, 'mage', 2, 5), key: 'enemy-leader' },
          { ...u(2, 0, 'soldier', 2), commander: 'enemy-command' },
        ],
        commanders: [
          {
            id: 'enemy-command',
            unitKey: 'enemy-leader',
            radius: 2,
            aura: { attackMultiplier: 1.2 },
          },
        ],
      }),
    );
    const leader = state.units[1];
    const linked = state.units[2];
    expect(commanderAuraFor(state, linked).attackMultiplier).toBe(1.2);
    const events = applyAction(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', target: { x: 1, y: 0 } },
    }, TEST_RULES);
    expect(state.units.some((unit) => unit.id === leader.id)).toBe(false);
    expect(commanderAuraFor(state, linked).attackMultiplier).toBe(1);
    expect(linked.statuses).toContainEqual(expect.objectContaining({ id: 'shaken' }));
    expect(events).toContainEqual(
      expect.objectContaining({ type: 'commanderDefeated', commander: 'enemy-command' }),
    );
  });
});
