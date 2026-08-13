import { describe, expect, it } from 'vitest';
import { cloneContentCatalog, GlobalContentCatalog } from '@empire/battle-engine';

const TEST_CATALOG = cloneContentCatalog(GlobalContentCatalog);
import { emptyLevel } from '@empire/battle-engine/mapio';
import { EditorDocument } from '../document';

describe('editor document aggregate', () => {
  it('owns terrain, ownership and flood-fill invariants', () => {
    const document = EditorDocument.fromLevel(TEST_CATALOG, emptyLevel(4, 4));
    document.setTerrain({ x: 1, y: 1 }, 'village');
    document.setOwner({ x: 1, y: 1 }, 1);
    expect(document.map.owners[5]).toBe(1);

    document.setTerrain({ x: 1, y: 1 }, 'plain');
    expect(document.map.owners[5]).toBe(0);

    document.floodFill({ x: 0, y: 0 }, 'road');
    expect(new Set(document.map.tiles)).toEqual(new Set(['road']));
  });

  it('keeps spatial features and units valid when resized', () => {
    const document = EditorDocument.fromLevel(TEST_CATALOG, emptyLevel(6, 6));
    document.placeUnit({ x: 5, y: 5 }, 'soldier', 1);
    document.toggleCliff({ x: 4, y: 5 }, { x: 5, y: 5 });
    document.setDirectionalCover({ x: 5, y: 5 }, 'north', 'full');

    expect(document.resize(4, 4)).toBe(true);
    expect(document.units).toEqual([]);
    expect(document.map.cliffs).toEqual([]);
    expect(document.map.directionalCover).toEqual([]);
    expect(document.map.tiles).toHaveLength(16);
  });

  it('restores behavior after history serialization and exports defensive copies', () => {
    const original = EditorDocument.fromLevel(TEST_CATALOG, emptyLevel(4, 4));
    const restored = EditorDocument.deserialize(TEST_CATALOG, original.serialize());
    restored.setTerrain({ x: 0, y: 0 }, 'forest');
    const level = restored.toLevel();
    level.units.push({ x: 0, y: 0, unit: 'soldier', owner: 1 });

    expect(restored).toBeInstanceOf(EditorDocument);
    expect(restored.map.tiles[0]).toBe('forest');
    expect(restored.units).toEqual([]);
  });

  it('rejects mutations that would create an invalid editor document', () => {
    const document = EditorDocument.fromLevel(TEST_CATALOG, emptyLevel(4, 4));

    expect(() => document.placeUnit({ x: 4, y: 0 }, 'soldier', 1)).toThrow(/outside/);
    expect(() => document.placeUnit({ x: 0, y: 0 }, 'soldier', 99)).toThrow(/unknown player/);
    expect(() => document.toggleCliff({ x: 0, y: 0 }, { x: 2, y: 0 })).toThrow(/adjacent/);
    expect(() => document.setElevation({ x: 0, y: 0 }, Number.NaN)).toThrow(/finite/);
  });
});
