import { describe, expect, it } from 'vitest';
import { makeLevel, testApply, testCombatPlan, testState, u } from './fixtures';

describe('weapon hit effects', () => {
  it('applies a data-defined status after a surviving target takes damage', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'rogue', 1), u(1, 0, 'ogre', 2)] }),
    );
    const events = testApply(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 0 }],
      command: { ability: 'attack', weapon: 'rogue_blades', target: { x: 1, y: 0 } },
    });

    expect(state.units.find((unit) => unit.owner === 2)?.statuses).toContainEqual(
      expect.objectContaining({ id: 'poisoned', remaining: 2, sourceUnitId: 1 }),
    );
    expect(events).toContainEqual(expect.objectContaining({ type: 'statusApplied', status: 'poisoned' }));
  });

  it('forecasts and applies one effect to every surviving area target', () => {
    const state = testState(
      makeLevel(['...', '...'], {
        units: [
          u(0, 1, 'mage', 1),
          { ...u(1, 1, 'ogre', 2), reaction: 'counter' },
          { ...u(1, 0, 'ogre', 2), reaction: 'counter' },
        ],
      }),
    );
    const plan = testCombatPlan(state, state.units[0], { x: 1, y: 1 }, { x: 0, y: 1 }, 'mage_overcharge');
    expect(plan.unitHits.every((hit) => hit.effects.some(
      (effect) => effect.type === 'addStatus' && effect.status === 'armor_down',
    ))).toBe(true);

    testApply(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 1 }],
      command: { ability: 'attack', weapon: 'mage_overcharge', target: { x: 1, y: 1 } },
    });
    expect(state.units.filter((unit) => unit.owner === 2).every(
      (unit) => unit.statuses.some((status) => status.id === 'armor_down'),
    )).toBe(true);
  });
});
