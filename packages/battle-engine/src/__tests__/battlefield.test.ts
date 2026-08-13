import { describe, expect, it } from 'vitest';
import { Battlefield } from '../domain/battlefield';
import { TEST_CONTENT, makeLevel, testState } from './fixtures';

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
});
