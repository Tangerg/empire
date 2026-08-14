import { describe, expect, it } from 'vitest';
import { Battlefield } from '../domain/battlefield';
import { MapLayers } from '../domain/map-layers';
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

/**
 * The writable side of the same layers.
 *
 * A scenario effect and the editor document each had their own copy of these
 * rules, and had already drifted: one matched a cliff edge through `edgeKey`,
 * the other by comparing both orientations by hand, and they disagreed about
 * whether a cover entry with no raised side should exist.
 */
describe('MapLayers', () => {
  const layers = () => new MapLayers(testState(makeLevel(['...', '...'])).map);

  it('reports the ground it replaced, and nothing when it replaced nothing', () => {
    const map = layers();
    expect(map.changeTerrain({ x: 1, y: 0 }, 'water')).toBe('plain');
    expect(map.changeTerrain({ x: 1, y: 0 }, 'water')).toBeNull();
    expect(map.terrainAt({ x: 1, y: 0 })).toBe('water');
  });

  it('reports a height change as the step it was', () => {
    const map = layers();
    expect(map.changeElevation({ x: 0, y: 0 }, 2.4)).toEqual({ from: 0, to: 2 });
    expect(map.changeElevation({ x: 0, y: 0 }, 2)).toBeNull();
    expect(map.raiseElevation({ x: 0, y: 0 }, 3)).toEqual({ from: 2, to: 5 });
    expect(map.raiseElevation({ x: 0, y: 0 }, 0)).toBeNull();
  });

  it('knows one edge by both of its ends', () => {
    const map = layers();
    const west = { x: 0, y: 0 };
    const east = { x: 1, y: 0 };
    expect(map.blockEdge(west, east, true)).toBe(true);
    expect(map.isBlockedEdge(east, west)).toBe(true);
    // Asking for the state it is already in changes nothing, and says so.
    expect(map.blockEdge(east, west, true)).toBe(false);
    expect(map.blockEdge(east, west, false)).toBe(true);
    expect(map.map.cliffs).toEqual([]);
  });

  it('refuses an edge between cells that do not share one', () => {
    const map = layers();
    expect(map.isEdge({ x: 0, y: 0 }, { x: 1, y: 1 })).toBe(false);
    expect(map.isEdge({ x: 0, y: 0 }, { x: 0, y: 9 })).toBe(false);
    expect(() => map.blockEdge({ x: 0, y: 0 }, { x: 1, y: 1 }, true)).toThrow(RangeError);
    expect(() => map.changeTerrain({ x: 9, y: 9 }, 'water')).toThrow(RangeError);
  });

  it('keeps a cover entry only while some side is raised', () => {
    const map = layers();
    const at = { x: 2, y: 1 };
    map.changeCoverSide(at, 'north', 'half');
    map.changeCoverSide(at, 'east', 'full');
    expect(map.coverAt(at)).toEqual({ north: 'half', east: 'full' });

    map.changeCover(at, { north: 'full' });
    expect(map.coverAt(at)).toEqual({ north: 'full' });

    map.changeCoverSide(at, 'north', null);
    expect(map.coverAt(at)).toEqual({});
    expect(map.map.directionalCover).toEqual([]);
  });
});
