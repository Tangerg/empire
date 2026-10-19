import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel, tileHash } from '@empire/battle-engine';
import type { BoardPiece } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01EnvironmentScene, CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup } from './candidate-01-map-scene';

const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const clearances = [
  { levelId: 'c01-03', cell: [2, 5], terrain: 'forest', id: 'fern-bed', foot: [80.18315, 197.76], originalId: 'mixed-forest-autumn' },
  { levelId: 'c01-10', cell: [17, 7], terrain: 'forest', id: 'fern-bed', foot: [561.13346, 261.76], originalId: 'oak-ancient' },
  { levelId: 'c01-10', cell: [19, 6], terrain: 'hill', id: 'temperate-low-ledge', foot: [623.7925, 216], originalId: 'temperate-low-ledge' },
] as const;
const nearContact = (piece: BoardPiece, x: number, y: number) => piece.foot !== undefined
  && Math.abs(piece.x - x) < 1e-6 && Math.abs(piece.y - y) < 1e-6;

describe('Three local decorative clearances around named Roderick', () => {
  it.each(clearances)('replaces only the intended $levelId $terrain at $cell with its exact source/contact', spec => {
    const level = candidate01Level(spec.levelId), map = mapFromLevel(catalog, level);
    const context = { content: catalog, levelId: level.id, map, viewport: candidateReviewViewport(map) };
    const scene = candidate01EnvironmentScene(level.id)!;
    const [x, y] = spec.cell;
    const originalX = (x + .5) * 32 + (tileHash(x, y, 1122) - .5) * (spec.terrain === 'forest' ? 9 : 5);
    const before = candidate01MapSceneryLayers({ ...context, levelId: 'unregistered-local-clearance' });
    const after = candidate01MapSceneryLayers(context);
    const original = before.underUnits.find(p => nearContact(p, originalX, (y + 1.18) * 32))!;
    const actual = after.underUnits.filter(p => nearContact(p, spec.foot[0], spec.foot[1]));
    expect(original).toBeDefined();
    expect(actual).toHaveLength(1);
    const replacement = actual[0];
    const zoneIndex = scene.zones.findIndex(z => z.bounds[0] === x && z.bounds[1] === y);
    expect(scene.zones[zoneIndex].bounds).toEqual([x, y, x, y]);
    expect(scene.zones[zoneIndex].authoredTerrainScenery).toEqual([spec.terrain]);
    const placement = scene.placements[zoneIndex];
    expect(placement).toMatchObject({ id: spec.id, layer: 'under-units', flip: tileHash(x, y, 1123) > .52 });
    expect(map.tiles[y * map.width + x]).toBe(spec.terrain);
    const { cell, atlas } = CANDIDATE_01_ENVIRONMENT.cell(spec.id);
    expect(replacement.markup).toContain(atlas.raster.href);
    expect(replacement.markup).toContain(`x="${-(cell.index % atlas.columns) * atlas.cellWidth}" y="${-Math.floor(cell.index / atlas.columns) * atlas.cellHeight}"`);
    expect(replacement.foot).toEqual(original.foot);
    if (spec.terrain === 'forest') {
      expect(placement.scale).toBe(.48);
      expect(replacement.x).toBe(original.x);
      expect(replacement.y).toBe(original.y);
      const old = CANDIDATE_01_ENVIRONMENT.cell(spec.originalId);
      expect(original.markup).toContain(`x="${-(old.cell.index % old.atlas.columns) * old.atlas.cellWidth}" y="0"`);
    } else {
      expect(placement.scale).toBe(.46 + tileHash(x, y, 1121) * .12);
      expect(placement.standingShadow).toBe(true);
      expect(replacement.x).toBe(original.x);
      expect(replacement.y - original.y).toBeCloseTo(-13.76, 10);
      expect(replacement.markup).toBe(original.markup.replace('candidate-environment-prop is-standing', 'candidate-environment-prop is-under-units is-standing'));
      expect(replacement.x).toBeGreaterThan(x * 32);
      expect(replacement.x).toBeLessThan((x + 1) * 32);
      expect(replacement.y).toBeGreaterThan(y * 32);
      expect(replacement.y).toBeLessThan((y + 1) * 32);
      expect(map.elevation[y * map.width + x]).toBe(1);
      expect(catalog.terrains.get('hill')).toMatchObject({ defense: .3, vision: 1, opaque: false, cover: 'none', obstructionHeight: 0 });
    }
  });

  it.each(['c01-03', 'c01-10'])('retains all other actual scene pieces, ground and frame in %s', levelId => {
    const level = candidate01Level(levelId), map = mapFromLevel(catalog, level);
    const context = { content: catalog, levelId, map, viewport: candidateReviewViewport(map) };
    const baseline = { ...context, levelId: 'unregistered-local-clearance' };
    const scene = candidate01EnvironmentScene(levelId)!;
    const specs = clearances.filter(s => s.levelId === levelId);
    expect(scene.mapSize).toEqual([map.width, map.height]);
    expect(scene.zones).toHaveLength(specs.length);
    expect(scene.placements).toHaveLength(specs.length);
    expect(scene.roadCorridor).toBeUndefined();
    expect(scene.framePlacements).toBeUndefined();
    expect(scene.groundOutline).toBeUndefined();
    expect(scene.zones.every(z => z.authoredTerrainSurface === undefined)).toBe(true);
    const before = candidate01MapSceneryLayers(baseline), after = candidate01MapSceneryLayers(context);
    const prior = (p: BoardPiece) => specs.some(({cell:[x,y]}) => p.foot !== undefined && Math.floor(p.x / 32) === x && Math.abs(p.y - (y+1.18)*32) < 1e-6);
    const authored = (p: BoardPiece) => specs.some(s => nearContact(p, s.foot[0], s.foot[1]));
    expect(before.underUnits.filter(prior)).toHaveLength(specs.length);
    expect(after.underUnits.filter(authored)).toHaveLength(specs.length);
    expect(after.underUnits.filter(p => !authored(p))).toEqual(before.underUnits.filter(p => !prior(p)));
    expect(after.underUnits).toHaveLength(before.underUnits.length);
    expect(after.ground).toEqual(before.ground);
    expect(after.overUnits).toEqual(before.overUnits);
    expect(candidate01SceneFrameMarkup(context)).toEqual(candidate01SceneFrameMarkup(baseline));
    expect(candidate01MapSceneryLayers({...context, editing:true})).toEqual(after);
  });

  it.each(['c01-03', 'c01-10'])('retains rules, structures, triggers, actor standing and actual visibility in %s', levelId => {
    const level = candidate01Level(levelId), engine = createBattleEngine({content:catalog}), state = engine.createState(level);
    const roderick = state.units.find(u => u.key === 'roderick')!;
    expect([roderick.x,roderick.y,roderick.owner]).toEqual(levelId==='c01-03'?[3,5,1]:[18,6,2]);
    const visible = () => state.players.map(p => engine.space.visibleUnits(state,p.id).map(u=>u.id));
    const before=JSON.stringify({level,state,visible:visible()});
    candidate01MapSceneryLayers({content:catalog,levelId,map:state.map,viewport:candidateReviewViewport(state.map)});
    expect(JSON.stringify({level,state,visible:visible()})).toBe(before);
    expect(visible().every(ids=>ids.includes(roderick.id))).toBe(true);
    for(const {cell:[x,y],terrain} of clearances.filter(s=>s.levelId===levelId)) {
      expect(state.units.some(u=>u.x===x&&u.y===y)).toBe(false);
      expect(state.structures.some(s=>s.x===x&&s.y===y)).toBe(false);
      expect(level.scenario!.zones!.flatMap(z=>z.cells)).not.toContainEqual({x,y});
      expect(state.map.owners[y*state.map.width+x]).toBe(0);
      if(terrain==='forest') expect(catalog.terrains.get(terrain)).toMatchObject({opaque:true,cover:'half',obstructionHeight:2,defense:.2});
    }
  });
});
