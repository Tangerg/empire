import { describe, expect, it } from 'vitest';
import { Battlefield } from '../domain/battlefield';
import { TEST_CONTENT, makeLevel, testState, u } from './fixtures';

describe('Battlefield aggregate', () => {
  it('composes terrain, landform overlays and structures behind one cell model', () => {
    const state = testState(
      makeLevel(['T.'], {
        structures: [{ id: 'gate-a', type: 'gate', owner: 2, x: 0, y: 0 }],
        scenario: {
          zones: [
            { id: 'burning-woods', cells: [{ x: 0, y: 0 }] },
            { id: 'light-ground', cells: [{ x: 1, y: 0 }] },
          ],
          overlays: [
            { id: 'fire', type: 'fire_field', zone: 'burning-woods' },
            { id: 'gravity', type: 'low_gravity', zone: 'light-ground' },
          ],
        },
      }),
    );
    const battlefield = new Battlefield(state, TEST_CONTENT);
    const forest = battlefield.cellAt(0, 0);
    const plain = battlefield.cellAt(1, 0);

    expect(forest.terrain.id).toBe('forest');
    expect(forest.overlayStates.map((overlay) => overlay.id)).toEqual(['fire']);
    expect(forest.structure?.id).toBe('gate-a');
    expect(forest.movementCost('foot')).toBe(3);
    expect(forest.defense).toBeCloseTo(0.1);
    expect(forest.blocksMovement).toBe(true);
    expect(forest.blocksVision).toBe(true);

    expect(plain.movementCost('foot')).toBe(1);
    expect(plain.vision).toBe(1);
    expect(battlefield.contains({ x: 2, y: 0 })).toBe(false);
    expect(() => battlefield.cellAt(2, 0)).toThrow(RangeError);
  });

  it('answers where a unit may stand, both layers at once', () => {
    const state = testState(
      makeLevel(['~..'], {
        units: [u(2, 0, 'soldier', 1)],
        structures: [{ id: 'gate-a', type: 'gate', owner: 2, x: 1, y: 0 }],
      }),
    );
    const battlefield = new Battlefield(state, TEST_CONTENT);

    // Ground it cannot cross, and ground filled by something built on it: two
    // separate layers, and either one is enough to refuse the unit.
    expect(battlefield.cellAt(0, 0).movementCost('foot')).toBeNull();
    expect(battlefield.cellAt(0, 0).admits('foot')).toBe(false);
    expect(battlefield.cellAt(1, 0).movementCost('foot')).not.toBeNull();
    expect(battlefield.cellAt(1, 0).admits('foot')).toBe(false);
    expect(battlefield.cellAt(2, 0).admits('foot')).toBe(true);

    // Standing on it is a third question, and only placement asks it.
    expect(battlefield.cellAt(2, 0).canReceive('foot')).toBe(false);
    expect(battlefield.cellAt(2, 0).occupant?.id).toBe(state.units[0].id);
    // A step onto a filled tile is not an expensive step; it is no step.
    expect(battlefield.traversalCost({ x: 2, y: 0 }, { x: 1, y: 0 }, 'foot')).toBeNull();
  });
});
