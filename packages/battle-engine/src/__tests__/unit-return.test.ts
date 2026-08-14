import { describe, expect, it } from 'vitest';
import { createDefaultBattleRuleServices } from '../action-system';
import { changeMorale } from '../morale';
import { applyScenarioEffect } from '../scenario';
import { returnUnitToField } from '../unit-return';
import { UnitEntity } from '../domain/unit-entity';
import { TEST_CONTENT, makeLevel, testAddStatus, testState, u } from './fixtures';
import type { GameState } from '../types';

/**
 * Coming back onto the field, once.
 *
 * Reviving a corpse and recalling a withdrawal used to be two hand-written
 * blocks that had drifted apart; these are the invariants both of them are
 * now held to.
 */

const skirmish = (rules = {}): GameState => testState(makeLevel(['.....', '.....'], {
  units: [u(0, 0, 'soldier', 1), u(4, 0, 'soldier', 2)],
  rules,
  scenario: { zones: [{ id: 'rally', cells: [{ x: 2, y: 1 }, { x: 1, y: 1 }] }] },
}));

const rules = () => createDefaultBattleRuleServices({ content: TEST_CONTENT });

describe('a unit revived from a rout', () => {
  it('comes back with the will to stay', () => {
    const state = skirmish({ moraleEnabled: true });
    const engine = rules();
    const broken = state.units[0].id;
    changeMorale(engine, state, broken, -999, 'test', () => {});
    expect(state.markers[0]).toMatchObject({ kind: 'routed', fallenUnit: { morale: { current: 0 } } });

    applyScenarioEffect(engine, state, { type: 'reviveMarkers', selector: {} }, () => {});

    // At zero it would rout again on the next shock, which made resurrecting a
    // broken soldier a gift that evaporated.
    const back = state.units.find((unit) => unit.id === broken)!;
    expect(back.morale.current).toBe(back.morale.maximum);
    changeMorale(engine, state, broken, -20, 'test', () => {});
    expect(state.units.some((unit) => unit.id === broken)).toBe(true);
  });
});

describe('every return, whichever way it came', () => {
  it('leaves what was clinging to you behind', () => {
    const state = skirmish();
    const engine = rules();
    const victim = state.units[0];
    testAddStatus(victim, 'poisoned', 3);
    expect(victim.statuses).toHaveLength(1);

    applyScenarioEffect(engine, state, { type: 'withdrawUnits', selector: { owner: 1 } }, () => {});
    applyScenarioEffect(engine, state, {
      type: 'restoreWithdrawnUnits',
      selector: {},
      zone: 'rally',
    }, () => {});

    // A duration that stopped ticking while its owner was off the board would
    // come back stale; the withdrawal path used to carry it back anyway.
    const back = state.units.find((unit) => unit.id === victim.id)!;
    expect(back.statuses).toEqual([]);
    expect(back).toMatchObject({ x: 1, y: 1, done: true, capture: 0 });
  });

  it('arrives with its reaction unspent', () => {
    const state = skirmish();
    const unit = state.units[0];
    new UnitEntity(unit).consumeReaction(state.turn);
    applyScenarioEffect(rules(), state, { type: 'withdrawUnits', selector: { owner: 1 } }, () => {});
    applyScenarioEffect(rules(), state, { type: 'restoreWithdrawnUnits', selector: {}, zone: 'rally' }, () => {});

    const back = state.units.find((candidate) => candidate.id === unit.id)!;
    expect(new UnitEntity(back).canReact(state.turn)).toBe(true);
  });

  it('refuses a tile someone is standing on', () => {
    const state = skirmish();
    const engine = rules();
    applyScenarioEffect(engine, state, { type: 'withdrawUnits', selector: { owner: 1 } }, () => {});
    const marker = state.markers[0];
    state.units[0].x = marker.at.x;
    state.units[0].y = marker.at.y;

    expect(returnUnitToField(state, marker, { at: marker.at }, () => {})).toBeNull();
    expect(state.markers).toContain(marker);
  });

  it('refuses to bring back someone who is already here', () => {
    const state = skirmish();
    const engine = rules();
    applyScenarioEffect(engine, state, { type: 'withdrawUnits', selector: { owner: 1 } }, () => {});
    const marker = state.markers[0];

    expect(returnUnitToField(state, marker, { at: { x: 1, y: 1 } }, () => {})).toBeTruthy();
    expect(returnUnitToField(state, marker, { at: { x: 2, y: 1 } }, () => {})).toBeNull();
    expect(state.units.filter((unit) => unit.id === marker.fallenUnit!.id)).toHaveLength(1);
  });

  it('fills a rally zone in reading order and stops when it is full', () => {
    // Where a scenario may drop a unit is one search — passable ground, nobody
    // standing on it, always the same tile first — and it was written twice,
    // once for a teleport and once for a rescue. The zone here is declared out
    // of order on purpose: reading order, not declaration order, decides.
    const state = testState(makeLevel(['....', '....'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 1), u(2, 0, 'soldier', 1), u(3, 1, 'soldier', 2)],
      scenario: { zones: [{ id: 'rally', cells: [{ x: 2, y: 1 }, { x: 1, y: 1 }] }] },
    }));
    const engine = rules();
    applyScenarioEffect(engine, state, { type: 'withdrawUnits', selector: { owner: 1 } }, () => {});
    expect(state.markers).toHaveLength(3);

    applyScenarioEffect(engine, state, { type: 'restoreWithdrawnUnits', selector: {}, zone: 'rally' }, () => {});

    expect(state.units.filter((unit) => unit.owner === 1).map((unit) => ({ x: unit.x, y: unit.y })))
      .toEqual([{ x: 1, y: 1 }, { x: 2, y: 1 }]);
    // The third stays in the ground until there is room for it.
    expect(state.markers).toHaveLength(1);
  });

  it('reports the marker it consumed', () => {
    const state = skirmish();
    const engine = rules();
    applyScenarioEffect(engine, state, { type: 'withdrawUnits', selector: { owner: 1 } }, () => {});
    const marker = state.markers[0];
    const events: string[] = [];

    returnUnitToField(state, marker, { at: { x: 1, y: 1 } }, (event) => events.push(event.type));
    expect(events).toEqual(['markerRemoved', 'unitRevived']);
    expect(state.markers).not.toContain(marker);
  });
});
