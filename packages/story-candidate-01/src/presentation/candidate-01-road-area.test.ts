import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel, type GameMap } from '@empire/battle-engine';
import { CampaignBattleBridge, CampaignRuntime } from '@empire/campaign-engine';
import { createTestCatalog } from '@empire/test-content';
import { boardPiecesMarkup, type BoardPiece } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../index';
import { candidate01Level } from '../levels';
import { CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN, applyCandidate01BattleContext } from '../campaign';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { CANDIDATE_01_ROAD_MATERIAL as material, candidate01IsAreaRoad, candidate01RoadMaterialPieces } from './candidate-01-road-material';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const neighbours = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]];
const hash = (value: unknown) => createHash('sha256').update(JSON.stringify(value)).digest('hex');
const layers = (map: GameMap, levelId = 'c01-01', editing = false) =>
  candidate01MapSceneryLayers({ content, levelId, map, viewport: candidateReviewViewport(map), editing });
const roadPieces = (map: GameMap) => layers(map).ground.filter(piece => piece.markup.includes('data-runtime-raster="area-road"'));
function masks(pieces: readonly BoardPiece[]): Map<string, number> {
  return new Map(pieces.flatMap(piece => [...piece.markup.matchAll(/<g transform="translate\((\d+) (\d+)\)">([\s\S]*?)<\/g>/g)].map(([, x, y, markup]) => {
    const offset = /road-masks@2x\.png" x="(-?\d+)" y="(-?\d+)"/.exec(markup);
    if (!offset) throw new Error('area road cell has no exact mask-atlas sample');
    return [`${(piece.x + Number(x)) / 32},${(piece.y + Number(y)) / 32}`, Math.abs(Number(offset[1])) / 32 + Math.abs(Number(offset[2])) / 32 * 16] as const;
  })));
}

describe('continuous authored earth roads', () => {
  it('uses the immutable full earth source at one raster pixel per logical world unit, below native 2x world detail', () => {
    const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');
    expect(createHash('sha256').update(readFileSync(resolve(pack, material.source))).digest('hex'))
      .toBe('9afeebdb434aed8fe30459db2dbf457173c3b0d49fad2a966dbb8837718dbd53');
    expect(material.logicalSize).toEqual([1024, 1024]);
    expect(material.rasterPixelsPerWorldUnit).toEqual([1, 1]);
    expect(material.worldOrigin).toEqual([0, 0]);
    expect(material.fit).toEqual({ inset: 0, bleed: 0 });
    for (const [path, size] of [[material.png, 512], [material.png2x, 1024], [material.occupancy.png, 512], [material.occupancy.png2x, 1024]] as const) {
      const png = readFileSync(resolve(pack, 'world-materials-v1', path));
      expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([size, size]);
    }
  });

  it('projects all256 actual neighbor patterns, including diagonal-only changes', () => {
    for (let mask = 0; mask < 256; mask++) {
      const cells = new Set(['1,1', ...neighbours.flatMap(([dx, dy], bit) => mask & 1 << bit ? [`${1 + dx},${1 + dy}`] : [])]);
      const pieces = candidate01RoadMaterialPieces(3, 3, (x, y) => x === 1 && y === 1, (x, y) => cells.has(`${x},${y}`));
      expect(masks(pieces).get('1,1'), `mask ${mask}`).toBe(mask);
    }
  });

  it('paints exactly80 road and4 settlement cells and retains all exclusions and open boundary ports', () => {
    const map = mapFromLevel(content, candidate01Level('c01-01'));
    const before = hash(map);
    const pieces = roadPieces(map);
    expect(pieces).toHaveLength(1);
    const actual = masks(pieces);
    const expected = map.tiles.flatMap((id, index) => candidate01IsAreaRoad(candidateMaterial(content, id))
      ? [`${index % map.width},${Math.floor(index / map.width)}`] : []);
    expect([...actual.keys()]).toEqual(expected);
    expect(actual.size).toBe(84);
    expect(actual.has('11,8')).toBe(false);
    expect(actual.has('12,8')).toBe(false);
    for (const y of [9, 10]) expect(actual.get(`0,${y}`)! & 64).toBe(64);
    for (const y of [6, 7]) expect(actual.get(`26,${y}`)! & 4).toBe(4);
    expect(pieces.every(piece => piece.foot === undefined)).toBe(true);
    expect(hash(map)).toBe(before);
    map.tiles[9 * map.width] = 'plain';
    const changed = masks(roadPieces(map));
    expect(changed.size).toBe(83);
    expect(changed.has('0,9')).toBe(false);
    expect(changed.get('1,9')).not.toBe(actual.get('1,9'));
  });

  it('joins bridge and village thresholds without painting bridge water or unrelated terrain', () => {
    const map = mapFromLevel(content, candidate01Level('c01-01'));
    map.tiles.fill('plain');
    map.tiles[8 * map.width + 8] = 'road';
    map.tiles[8 * map.width + 9] = 'bridge';
    map.tiles[9 * map.width + 8] = 'village';
    map.tiles[7 * map.width + 8] = 'forest';
    map.tiles[8 * map.width + 7] = 'mountain';
    const before = hash(map);
    const actual = masks(roadPieces(map));
    expect([...actual.keys()]).toEqual(['8,8', '8,9']);
    expect(actual.get('8,8')).toBe(4 | 16);
    expect(candidate01IsAreaRoad(candidateMaterial(content, 'bridge'))).toBe(false);
    expect(hash(map)).toBe(before);
  });

  it('keeps the old connector path for every other scene and for editable c01-01 geometry', () => {
    for (const level of CANDIDATE_01_LEVELS) {
      const map = mapFromLevel(content, level);
      const play = layers(map, level.id);
      const edit = layers(map, level.id, true);
      expect(edit.ground.some(piece => piece.markup.includes('data-runtime-raster="area-road"'))).toBe(false);
      if (level.id === 'c01-01') {
        expect(boardPiecesMarkup(edit.ground)).toContain('route-dirt-road@2x.png');
        expect(boardPiecesMarkup(play.ground)).not.toContain('route-dirt-road@2x.png');
        expect(play.underUnits).toEqual(edit.underUnits);
        expect(play.overUnits).toEqual(edit.overUnits);
      } else expect(play, level.id).toEqual(edit);
    }
  });

  it('keeps world UV phase across cells and chunks, with each mask self-contained', () => {
    const pieces = candidate01RoadMaterialPieces(2, 80, () => true, () => true);
    expect(pieces.map(({ x, y }) => [x, y])).toEqual([[0, 0], [0, 1024], [0, 2048]]);
    for (const piece of pieces) {
      expect(piece.markup.match(/href="[^\"]*\/road-earth@2x.png[^\"]*"/g)).toHaveLength(1);
      expect(piece.markup).toContain('width="1024" height="1024" preserveAspectRatio="none"');
      const id = /<mask id="([^\"]+)"/.exec(piece.markup)![1];
      expect(piece.markup).toContain(`mask="url(#${id})"`);
      expect(piece.markup).not.toContain('filter=');
    }
    expect(pieces[0].markup).toBe(pieces[1].markup);
    expect(pieces[2].markup).toContain('width="64" height="512" viewBox="0 0 64 512"');
    expect(candidate01RoadMaterialPieces(2, 80, () => false, () => true)).toEqual([]);
  });

  it('does not mutate either campaign choice snapshot or actor/building contacts', () => {
    for (const choice of ['steady-advance', 'rush-north-hill']) {
      const runtime = new CampaignRuntime(CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN);
      runtime.advance(); runtime.advance(); runtime.choose(choice);
      const request = applyCandidate01BattleContext(runtime.beginBattle(new CampaignBattleBridge(candidate01Level, content)), runtime.state);
      const state = engine.createState(request.level);
      const before = hash({ request, state, campaign: runtime.state });
      const play = layers(state.map);
      const edit = layers(state.map, 'c01-01', true);
      expect(play.underUnits).toEqual(edit.underUnits);
      expect(play.overUnits).toEqual(edit.overUnits);
      expect(state.units).toHaveLength(19);
      expect(request.level.owners).toHaveLength(4);
      expect(hash({ request, state, campaign: runtime.state })).toBe(before);
    }
  });

  it('bounds every occupancy query and rejects invalid extents', () => {
    candidate01RoadMaterialPieces(1, 1, () => true, (x, y) => {
      expect([x, y]).toEqual([0, 0]); return true;
    });
    for (const [width, height] of [[0, 1], [1, -1], [1.5, 2], [1, Infinity]]) {
      expect(() => candidate01RoadMaterialPieces(width, height, () => true, () => true)).toThrow('positive integers');
    }
  });
});
