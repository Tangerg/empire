import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel, TacticalGrids } from '@empire/battle-engine';
import { createSceneViewport, type BoardPiece } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup, candidate01SceneProfile } from './candidate-01-map-scene';

const cells = [
  [7, 3], [14, 5], [15, 6], [4, 7], [16, 7], [17, 7], [18, 7], [19, 7],
  [0, 8], [5, 8], [17, 8], [18, 8], [0, 9], [4, 9], [14, 9], [15, 10],
  [4, 11], [6, 12], [17, 12], [22, 12], [2, 13], [21, 13], [1, 14],
  [3, 15], [4, 15], [5, 15], [7, 15], [8, 15],
] as const;
const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const level = candidate01Level('c01-15');
const map = mapFromLevel(catalog, level);
const viewport = createSceneViewport(TacticalGrids.get('square4'), map.width, map.height, 32, candidate01SceneProfile());
const context = { content: catalog, levelId: level.id, map, viewport };
const baseline = { ...context, levelId: 'unregistered-silverwood-projection' };
const scene = candidate01EnvironmentScene(level.id)!;
const local = (piece: BoardPiece) => piece.foot !== undefined && cells.some(([x, y]) =>
  Math.floor(piece.x / 32) === x && Math.abs(piece.y - (y + 1.18) * 32) < .000001);

describe('Silverwood local decorative clearance', () => {
  it('authors only the measured 28 cells without optional road, outline or frame replacements', () => {
    expect(scene.mapSize).toEqual([24, 16]);
    expect(scene.zones.map(zone => zone.bounds)).toEqual(cells.map(([x, y]) => [x, y, x, y]));
    expect(scene.placements).toHaveLength(28);
    expect(scene.roadCorridor).toBeUndefined();
    expect(scene.framePlacements).toBeUndefined();
    expect(scene.groundOutline).toBeUndefined();
    expect(scene.zones.every(zone => zone.authoredTerrainSurface === undefined)).toBe(true);
    for (const zone of scene.zones) {
      const [x, y] = zone.bounds;
      expect(zone.authoredTerrainScenery).toContain(map.tiles[y * map.width + x]);
    }
    expect(scene.placements.filter(p => p.id === 'fern-bed' || p.id === 'stump-low')).toHaveLength(26);
    expect(scene.placements.every(p => p.layer === 'under-units')).toBe(true);
  });

  it('replaces precisely 28 decorations while preserving every other standing piece', () => {
    const before = candidate01MapSceneryLayers(baseline);
    const after = candidate01MapSceneryLayers(context);
    const original = before.underUnits.filter(local);
    const authored = (piece: BoardPiece) => piece.foot !== undefined && scene.placements.some(p =>
      Math.abs(piece.x - p.x * 32) < .000001 && Math.abs(piece.y - (p.y + 1) * 32) < .000001);
    expect(original).toHaveLength(28);
    expect(after.underUnits.filter(authored)).toHaveLength(28);
    expect(after.underUnits.filter(p => !authored(p))).toEqual(before.underUnits.filter(p => !local(p)));
    expect(after.underUnits).toHaveLength(before.underUnits.length);
  });

  it('retains exact ground, hill surfaces, outer frame and all over-unit pieces', () => {
    const before = candidate01MapSceneryLayers(baseline);
    const after = candidate01MapSceneryLayers(context);
    expect(after.ground).toEqual(before.ground);
    expect(after.overUnits).toEqual(before.overUnits);
    expect(candidate01SceneFrameMarkup(context)).toEqual(candidate01SceneFrameMarkup(baseline));
    expect(candidate01MapSceneryLayers({ ...context, editing: true }).ground).toEqual(before.ground);
  });

  it('moves only the two slope contacts back 13.76px and preserves their source, scale, flip and shadow', () => {
    const before = candidate01MapSceneryLayers(baseline).underUnits;
    const after = candidate01MapSceneryLayers(context).underUnits;
    for (const x of [17, 18]) {
      const original = before.find(p => local(p) && Math.floor(p.x / 32) === x && p.y === 8.18 * 32)!;
      const replacement = after.find(p => p.x === original.x && p.y === 248)!;
      expect(replacement).toBeDefined();
      expect(replacement.y - original.y).toBeCloseTo(-13.76, 10);
      expect(replacement.foot).toEqual(original.foot);
      expect(replacement.markup).toBe(original.markup.replace('candidate-environment-prop is-standing', 'candidate-environment-prop is-under-units is-standing'));
      expect(replacement.y).toBeLessThan(7 * 32 + 31);
    }
  });

  it('keeps Twin Hills continuous ground explicitly owned by its original landform zones', () => {
    const hills = candidate01EnvironmentScene('c01-01')!.zones.filter(zone => zone.authoredTerrainScenery);
    expect(hills.map(zone => zone.id)).toEqual(['north-hill', 'east-hill']);
    for (const zone of hills) expect(zone.authoredTerrainSurface).toEqual(zone.authoredTerrainScenery);
  });

  it('does not mutate terrain, deployment, structures, visibility, objectives or triggers', () => {
    const engine = createBattleEngine({ content: catalog });
    const state = engine.createState(level);
    const before = JSON.stringify({ level, state, map });
    candidate01MapSceneryLayers({ ...context, map: state.map });
    candidate01SceneFrameMarkup({ ...context, map: state.map });
    expect(JSON.stringify({ level, state, map })).toBe(before);
    expect(state.units).toHaveLength(22);
    expect(state.structures).toContainEqual(expect.objectContaining({ id: 'mother-root', x: 11, y: 7, hp: 500 }));
    expect(level.rules?.turnLimit).toBe(17);
  });
});
