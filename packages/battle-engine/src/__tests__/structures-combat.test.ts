import { describe, expect, it } from 'vitest';
import { applyAction } from '../actions';
import { TEST_RULES, makeLevel, testCommands, testForecastStructure, testState, u } from './fixtures';

describe('structure combat', () => {
  it('targets, forecasts and destroys a hostile structure through a normal attack action', () => {
    const level = makeLevel(['....'], {
      units: [u(0, 0, 'ballista', 1), u(3, 0, 'ogre', 2)],
      structures: [{ id: 'gate-a', type: 'gate', owner: 2, x: 2, y: 0, hp: 20 }],
      victory: [{ id: 'break-gate', type: 'destroy', structures: ['gate-a'] }],
    });
    level.players[1].objectives = [{ type: 'routEnemies' }];
    const state = testState(level);
    const ballista = state.units[0];
    const gate = state.structures[0];
    const option = testCommands(state, ballista, ballista).find(
      (entry) => entry.weapon === 'ballista_bolt',
    );
    expect(option?.targets).toContainEqual({ x: 2, y: 0 });
    const fc = testForecastStructure(state, ballista, gate, 'ballista_bolt');
    expect(fc.targetBonusMultiplier).toBe(1.5);
    expect(fc.destroyed).toBe(true);

    const events = applyAction(state, {
      kind: 'command',
      unit: ballista.id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: 'ballista_bolt', target: { x: 2, y: 0 } },
    }, TEST_RULES);
    const attack = events.findIndex((event) => event.type === 'attackStructure');
    const damage = events.findIndex((event) => event.type === 'structureDamaged');
    const destroyed = events.findIndex((event) => event.type === 'structureDestroyed');
    expect(attack).toBeGreaterThanOrEqual(0);
    expect(damage).toBeGreaterThan(attack);
    expect(destroyed).toBeGreaterThan(damage);
    expect(gate.hp).toBe(0);
    expect(state.players[0].objectiveStates['break-gate'].status).toBe('completed');
  });

  it('does not offer allied structures as attack targets', () => {
    const state = testState(
      makeLevel(['...'], {
        units: [u(0, 0, 'ballista', 1), u(1, 0, 'soldier', 2)],
        structures: [{ id: 'own-gate', type: 'gate', owner: 1, x: 2, y: 0 }],
      }),
    );
    const attacks = testCommands(state, state.units[0], { x: 0, y: 0 }).filter(
      (entry) => entry.ability === 'attack',
    );
    expect(attacks.flatMap((entry) => entry.targets)).not.toContainEqual({ x: 2, y: 0 });
  });
});
