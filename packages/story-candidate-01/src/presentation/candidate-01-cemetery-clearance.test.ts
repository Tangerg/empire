import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel, tileHash } from '@empire/battle-engine';
import { type BoardPiece } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01EnvironmentScene, CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup } from './candidate-01-map-scene';

const canopyCells = [[4, 8], [14, 10], [19, 4], [2, 6]] as const;
const propCells = [[3, 6], [10, 9], [14, 9]] as const;
const cells = [...canopyCells, ...propCells];
const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const level = candidate01Level('c01-11');
const map = mapFromLevel(catalog, level);
const context = { content: catalog, levelId: level.id, map, viewport: candidateReviewViewport(map) };
const baseline = { ...context, levelId: 'unregistered-cemetery-projection' };
const scene = candidate01EnvironmentScene(level.id)!;
const originalAt = (piece: BoardPiece, x: number, y: number) => piece.foot !== undefined
  && Math.floor(piece.x / 32) === x && Math.abs(piece.y - (y + 1.18) * 32) < 1e-6;
const local = (piece: BoardPiece) => cells.some(([x, y]) => originalAt(piece, x, y));
const authored = (piece: BoardPiece) => piece.foot !== undefined && scene.placements.some(p =>
  Math.abs(piece.x - p.x * 32) < 1e-6 && Math.abs(piece.y - (p.y + 1) * 32) < 1e-6);

describe('Cemetery seven-object decorative clearance', () => {
  it('owns exactly four forest canopies and three non-task cemetery decorations', () => {
    expect(scene.mapSize).toEqual([22, 16]);
    expect(scene.zones.map(z => z.bounds)).toEqual(cells.map(([x, y]) => [x, y, x, y]));
    expect(scene.placements).toHaveLength(7);
    expect(scene.roadCorridor).toBeUndefined();
    expect(scene.framePlacements).toBeUndefined();
    expect(scene.groundOutline).toBeUndefined();
    expect(scene.zones.every(z => z.authoredTerrainSurface === undefined)).toBe(true);
    scene.zones.forEach((z, i) => expect(z.authoredTerrainScenery).toEqual([i < 4 ? 'forest' : 'c01.graveyard']));
    for (const zone of scene.zones) {
      const [x, y] = zone.bounds;
      expect(zone.authoredTerrainScenery).toContain(map.tiles[y * map.width + x]);
      expect(level.scenario!.zones!.flatMap(z => z.cells)).not.toContainEqual({ x, y });
    }
  });

  it('replaces seven actual pieces while preserving every unrelated standing piece', () => {
    const before = candidate01MapSceneryLayers(baseline);
    const after = candidate01MapSceneryLayers(context);
    expect(before.underUnits.filter(local)).toHaveLength(7);
    expect(after.underUnits.filter(authored)).toHaveLength(7);
    expect(after.underUnits.filter(p => !authored(p))).toEqual(before.underUnits.filter(p => !local(p)));
    expect(after.underUnits).toHaveLength(before.underUnits.length);
    expect(after.ground).toEqual(before.ground);
    expect(after.overUnits).toEqual(before.overUnits);
    expect(candidate01SceneFrameMarkup(context)).toEqual(candidate01SceneFrameMarkup(baseline));
    expect(candidate01MapSceneryLayers({ ...context, editing: true }).ground).toEqual(before.ground);
  });

  it('keeps four forest contacts and flips while drawing the actual low fern cell', () => {
    const before = candidate01MapSceneryLayers(baseline).underUnits;
    const after = candidate01MapSceneryLayers(context).underUnits;
    const { cell, atlas } = CANDIDATE_01_ENVIRONMENT.cell('fern-bed');
    const crop = `x="${-(cell.index % atlas.columns) * atlas.cellWidth}" y="${-Math.floor(cell.index / atlas.columns) * atlas.cellHeight}"`;
    canopyCells.forEach(([x, y], i) => {
      const original = before.find(p => originalAt(p, x, y))!;
      const placement = scene.placements[i];
      expect(placement).toMatchObject({ id: 'fern-bed', scale: 0.48, layer: 'under-units', flip: tileHash(x, y, 1123) > 0.52 });
      const replacement = after.find(p => Math.abs(p.x - original.x) < 1e-6 && Math.abs(p.y - original.y) < 1e-6)!;
      expect(replacement).toBeDefined();
      expect(replacement.markup).toContain(atlas.raster.href);
      expect(replacement.markup).toContain(crop);
      expect(replacement.foot).toEqual(original.foot);
      expect(replacement.markup).not.toBe(original.markup);
    });
  });

  it('moves only three prop contacts north 13.76px without changing their source, scale, flip or shadow', () => {
    const before = candidate01MapSceneryLayers(baseline).underUnits;
    const after = candidate01MapSceneryLayers(context).underUnits;
    propCells.forEach(([x, y], i) => {
      expect(scene.placements[i + 4].id).toBe(['grave-cross-cluster', 'grave-dead-tree', 'grave-signboards'][i]);
      const original = before.find(p => originalAt(p, x, y))!;
      const replacement = after.find(p => Math.abs(p.x - original.x) < 1e-6 && p.y === (y + 0.75) * 32)!;
      expect(replacement).toBeDefined();
      expect(replacement.y - original.y).toBeCloseTo(-13.76, 10);
      expect(replacement.foot).toEqual(original.foot);
      expect(replacement.markup).toBe(original.markup.replace('candidate-environment-prop is-standing', 'candidate-environment-prop is-under-units is-standing'));
      expect(replacement.y).toBeLessThan(y * 32 + 31);
    });
  });

  it('retains all three visit triggers, eight owned terrain buildings and every initial actor cell', () => {
    const engine = createBattleEngine({ content: catalog });
    const state = engine.createState(level);
    const before = JSON.stringify({ level, state, map });
    candidate01MapSceneryLayers({ ...context, map: state.map });
    candidate01SceneFrameMarkup(context);
    expect(JSON.stringify({ level, state, map })).toBe(before);
    expect(state.structures).toEqual([]);
    expect(state.units.map(u => [u.x, u.y])).toEqual([[1,8],[2,7],[2,9],[3,6],[3,10],[4,8],[5,11],[1,13],[4,14],[7,14],[11,7],[9,5],[13,5],[10,9],[14,9],[7,2],[8,12],[18,6],[6,10],[19,3],[17,12]]);
    const buildings = state.map.tiles.flatMap((id, i) => id === 'c01.outpost' ? [[i % map.width, Math.floor(i / map.width), state.map.owners[i]]] : []);
    expect(buildings).toEqual([[1,1,1],[20,1,2],[5,4,0],[8,7,0],[1,8,1],[16,8,2],[1,13,0],[19,13,0]]);
    expect(level.scenario!.variables).toEqual({ names: 0 });
    expect(level.scenario!.zones).toEqual([{id:'name-a',cells:[{x:5,y:3}]},{id:'name-b',cells:[{x:14,y:7}]},{id:'name-c',cells:[{x:8,y:12}]}]);
    expect(level.scenario!.triggers).toEqual(['named_old_soldier','named_border_child','named_royal_deserter'].map((signal, i) => ({
      id: ['name-a','name-b','name-c'][i], timing:'afterAction', condition:{type:'unitInZone',zone:['name-a','name-b','name-c'][i],owner:1},
      effects:[{type:'addVariable',key:'names',amount:1},{type:'emitSignal',signal}],
    })));
    expect(level.rules?.turnLimit).toBe(16);
  });
});
