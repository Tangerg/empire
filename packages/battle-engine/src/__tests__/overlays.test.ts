import { describe, expect, it } from 'vitest';
import { moveCostOf } from '../movement';
import type { GameEvent } from '../types';
import { makeLevel, testApply, testForecast, testMoveField, testScenarioEffect, testState, u } from './fixtures';

describe('terrain overlays', () => {
  it('changes movement without replacing the base terrain', () => {
    const state = testState(
      makeLevel(['...'], {
        units: [u(0, 0, 'soldier', 1), u(2, 0, 'knight', 2)],
        scenario: {
          zones: [{ id: 'waterline', cells: [{ x: 1, y: 0 }] }],
          overlays: [{ id: 'tide', type: 'flooded', zone: 'waterline' }],
        },
      }),
    );
    expect(moveCostOf(testMoveField(state, state.units[0]), state.map, { x: 1, y: 0 })).toBe(2);
    expect(testMoveField(state, state.units[1]).tiles.has(1)).toBe(false);
    expect(state.map.tiles[1]).toBe('plain');
  });

  it('feeds the same effective defense into forecast and resolution', () => {
    const normal = testState(
      makeLevel(['.T'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const burning = testState(
      makeLevel(['.T'], {
        units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
        scenario: {
          zones: [{ id: 'breach', cells: [{ x: 1, y: 0 }] }],
          overlays: [{ id: 'fire', type: 'fire_field', zone: 'breach' }],
        },
      }),
    );
    expect(testForecast(burning, burning.units[0], burning.units[1]).strike.damage).toBeGreaterThan(
      testForecast(normal, normal.units[0], normal.units[1]).strike.damage,
    );
  });

  it('adds and removes a named overlay through generic scenario effects', () => {
    const state = testState(
      makeLevel(['...'], {
        units: [u(0, 0, 'soldier', 1), u(2, 0, 'soldier', 2)],
        scenario: { zones: [{ id: 'chamber', cells: [{ x: 1, y: 0 }] }] },
      }),
    );
    const events: GameEvent[] = [];
    testScenarioEffect(
      state,
      { type: 'addOverlay', id: 'breach', overlay: 'vacuum', zone: 'chamber', rounds: 2 },
      (event) => events.push(event),
    );
    expect(state.scenario.overlays[0]).toMatchObject({ id: 'breach', remainingRounds: 2 });
    expect(events[0]).toMatchObject({ type: 'overlayAdded', overlay: 'breach' });

    testScenarioEffect(state, { type: 'removeOverlay', id: 'breach' }, (event) => events.push(event));
    expect(state.scenario.overlays).toHaveLength(0);
    expect(events.at(-1)).toMatchObject({ type: 'overlayRemoved', overlay: 'breach' });
  });

  it('resolves environmental status effects in the owner-turn lifecycle', () => {
    const state = testState(
      makeLevel(['..'], {
        units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
        scenario: {
          zones: [{ id: 'hazard', cells: [{ x: 0, y: 0 }] }],
          overlays: [{ id: 'fire', type: 'fire_field', zone: 'hazard' }],
        },
      }),
    );
    testApply(state, { kind: 'endTurn' });
    const events = testApply(state, { kind: 'endTurn' });
    expect(events.some((event) => event.type === 'statusApplied' && event.unit === 1)).toBe(true);
    expect(events.some((event) => event.type === 'statusTick' && event.unit === 1)).toBe(true);
    expect(state.units[0].hp).toBeLessThan(100);
  });
});
