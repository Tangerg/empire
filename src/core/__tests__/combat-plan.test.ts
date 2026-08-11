import { describe, expect, it } from 'vitest';
import { applyAction } from '../actions';
import { forecastCombatPlan, weaponAreaCells } from '../combat-plan';
import { weaponDef } from '../data/weapons';
import { createState } from '../state';
import { makeLevel, u } from './fixtures';

describe('combat plans and area weapons', () => {
  it('expands cross, square-ring, and line templates deterministically', () => {
    const state = createState(
      makeLevel(['...', '...', '...'], { units: [u(0, 0, 'mage', 1), u(2, 2, 'soldier', 2)] }),
    );
    const base = weaponDef('mage_overcharge');
    const center = { x: 1, y: 1 };
    expect(weaponAreaCells(state, { x: 0, y: 1 }, center, { ...base, area: 'cross1' })).toHaveLength(5);
    expect(weaponAreaCells(state, { x: 0, y: 1 }, center, { ...base, area: 'ring1' })).toHaveLength(9);
    expect(weaponAreaCells(state, { x: 0, y: 1 }, { x: 2, y: 1 }, { ...base, area: 'line' })).toEqual([
      { x: 1, y: 1 },
      { x: 2, y: 1 },
    ]);
  });

  it('forecasts and commits every hostile unit and structure from one immutable plan', () => {
    const state = createState(
      makeLevel(['...', '...', '...'], {
        units: [
          u(0, 1, 'mage', 1),
          u(1, 1, 'soldier', 2),
          u(1, 0, 'archer', 2),
          u(2, 1, 'soldier', 1),
        ],
        structures: [{ id: 'node', type: 'command_node', owner: 2, x: 1, y: 2 }],
      }),
    );
    const attacker = state.units[0];
    const plan = forecastCombatPlan(
      state,
      attacker,
      { x: 1, y: 1 },
      { x: 0, y: 1 },
      'mage_overcharge',
    );

    expect(plan.area).toBe('cross1');
    expect(plan.unitHits.map((hit) => ({ target: hit.target, primary: hit.primary }))).toEqual([
      { target: state.units[1].id, primary: true },
      { target: state.units[2].id, primary: false },
    ]);
    expect(plan.structureHits).toHaveLength(1);
    expect(plan.structureHits[0]).toMatchObject({ target: 'node', primary: false });

    const predictedHp = new Map(plan.unitHits.map((hit) => [hit.target, hit.hpAfter]));
    const events = applyAction(state, {
      kind: 'command',
      unit: attacker.id,
      path: [{ x: 0, y: 1 }],
      command: { ability: 'attack', weapon: 'mage_overcharge', target: { x: 1, y: 1 } },
    });

    for (const [id, hp] of predictedHp) {
      expect(state.units.find((unit) => unit.id === id)?.hp ?? 0).toBe(hp);
    }
    expect(state.units.find((unit) => unit.owner === 1 && unit.id !== attacker.id)?.hp).toBe(100);
    expect(state.structures[0].hp).toBe(plan.structureHits[0].forecast.hpAfter);
    expect(events.map((event) => event.type)).toEqual(expect.arrayContaining([
      'attack',
      'areaAttack',
      'areaAttackStructure',
    ]));
    expect(attacker.weaponState.mage_overcharge.cooldownRemaining).toBe(2);
  });

  it('lets a line weapon hit intervening enemies once without friendly fire', () => {
    const state = createState(
      makeLevel(['...'], {
        units: [u(0, 0, 'dragon', 1), u(1, 0, 'soldier', 2), u(2, 0, 'soldier', 2)],
      }),
    );
    const plan = forecastCombatPlan(state, state.units[0], { x: 2, y: 0 }, { x: 0, y: 0 }, 'dragon_breath');
    expect(plan.unitHits.map((hit) => hit.target).sort()).toEqual([state.units[1].id, state.units[2].id]);
  });

  it('skips a later area recipient that already routed from an earlier morale shock', () => {
    const state = createState(
      makeLevel(['...', '...', '...'], {
        units: [u(0, 1, 'mage', 1), u(1, 1, 'soldier', 2, 1), u(1, 0, 'soldier', 2)],
        rules: { moraleEnabled: true },
      }),
    );
    const routedId = state.units[2].id;
    state.units[2].morale.current = 1;

    const events = applyAction(state, {
      kind: 'command',
      unit: state.units[0].id,
      path: [{ x: 0, y: 1 }],
      command: { ability: 'attack', weapon: 'mage_overcharge', target: { x: 1, y: 1 } },
    });

    expect(state.units.some((unit) => unit.id === routedId)).toBe(false);
    expect(events).toContainEqual(expect.objectContaining({ type: 'unitRouted', unit: routedId }));
  });
});
