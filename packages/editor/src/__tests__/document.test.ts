import { describe, expect, it } from 'vitest';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';

const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
import { emptyLevel } from '@empire/battle-engine/level';
import { EditorDocument } from '../document';
import type { LevelData } from '@empire/battle-engine';

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

describe('level aggregate round-trip', () => {
  /** A level that exercises every optional LevelData section. */
  const fullLevel = (): LevelData => ({
    schema: 2,
    id: 'round-trip',
    name: '完整关卡',
    author: '测试',
    description: '含触发器、建筑、指挥官与部署区',
    width: 4,
    height: 3,
    terrain: ['C-v.', '..b.', 'q..K'],
    elevation: [0, 1, 2, 0, 0, 0, 1, 0, 0, 0, 0, 2],
    cliffs: [{ from: { x: 0, y: 0 }, to: { x: 1, y: 0 } }],
    directionalCover: [{ at: { x: 2, y: 1 }, sides: { north: 'half' } }],
    owners: [
      { x: 0, y: 0, owner: 1 },
      { x: 2, y: 0, owner: 0 },
      { x: 2, y: 1, owner: 2 },
      { x: 0, y: 2, owner: 1 },
      { x: 3, y: 2, owner: 2 },
    ],
    units: [
      { key: 'hero', x: 1, y: 1, unit: 'soldier', owner: 1, rank: 1, facing: 'east' },
      { key: 'foe', x: 3, y: 1, unit: 'archer', owner: 2 },
    ],
    commanders: [{ id: 'cmd', unitKey: 'hero', radius: 2, aura: { attackMultiplier: 1.1 } }],
    structures: [{ id: 'gate', type: 'gate', x: 3, y: 0, owner: 0 }],
    composites: [{ id: 'wall', parts: ['gate'], minimumNeutralized: 1 }],
    players: [
      {
        id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human',
        resources: { funds: { current: 100, capacity: null } },
      },
      {
        id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'ai',
        resources: { funds: { current: 100, capacity: null } },
        ai: { aggression: 0.4 },
      },
    ],
    rules: { turnLimit: 12, turnOrder: 'initiative' },
    victory: [{ id: 'goal', type: 'routEnemies' }],
    scenario: {
      variables: { flag: false },
      zones: [{ id: 'north', cells: [{ x: 0, y: 0 }] }],
      triggers: [{
        id: 't1',
        timing: 'turnStart',
        condition: { type: 'turnAtLeast', turn: 2 },
        effects: [{ type: 'setVariable', key: 'flag', value: true }],
      }],
    },
    deployment: { zones: [{ player: 1, zone: 'north' }], order: [1] },
    extra: { chapter: 'c99' },
  });

  it('preserves every level section the editor does not yet edit', () => {
    const level = fullLevel();
    const exported = EditorDocument.fromLevel(TEST_CATALOG, level).toLevel();

    expect(exported.scenario).toEqual(level.scenario);
    expect(exported.deployment).toEqual(level.deployment);
    expect(exported.structures).toEqual(level.structures);
    expect(exported.composites).toEqual(level.composites);
    expect(exported.commanders).toEqual(level.commanders);
    expect(exported.extra).toEqual(level.extra);
  });

  it('round-trips a fully-featured level without losing any field', () => {
    const level = fullLevel();
    const exported = EditorDocument.fromLevel(TEST_CATALOG, level).toLevel();

    // Fails the moment LevelData grows a section the document does not carry.
    expect(Object.keys(exported).sort()).toEqual(Object.keys(level).sort());
    // The one intentional normalisation: the AI form always has a value.
    expect(exported).toEqual({
      ...level,
      players: level.players.map((player) => ({ ...player, ai: player.ai ?? { aggression: 0.5 } })),
    });
  });

  it('survives a save/undo cycle with the preserved sections intact', () => {
    const document = EditorDocument.fromLevel(TEST_CATALOG, fullLevel());
    const snapshot = document.serialize();

    // An undo snapshot must not drag the content catalog along with it.
    expect(snapshot).not.toContain('movementProfiles');
    expect(JSON.parse(snapshot).preserved.scenario.triggers).toHaveLength(1);

    const restored = EditorDocument.deserialize(TEST_CATALOG, snapshot);
    expect(restored.toLevel()).toEqual(document.toLevel());
  });

  it('keeps edits and preserved sections independent', () => {
    const document = EditorDocument.fromLevel(TEST_CATALOG, fullLevel());
    document.setTerrain({ x: 1, y: 0 }, 'forest');
    const exported = document.toLevel();

    expect(exported.terrain[0][1]).toBe('T');
    expect(exported.scenario?.triggers).toHaveLength(1);
  });
});
