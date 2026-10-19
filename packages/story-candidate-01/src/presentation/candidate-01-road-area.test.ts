import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { createBattleEngine, mapFromLevel, type GameMap } from '@empire/battle-engine';
import { CampaignBattleBridge, CampaignRuntime } from '@empire/campaign-engine';
import { createTestCatalog } from '@empire/test-content';
import { boardPiecesMarkup } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../index';
import { candidate01Level } from '../levels';
import { CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN, applyCandidate01BattleContext } from '../campaign';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { CANDIDATE_01_ROAD_MATERIAL as material, candidate01IsAreaRoad, candidate01RoadMaterialPieces } from './candidate-01-road-material';
import { candidate01RoadGraph, candidate01RoadCorridorGeometry } from './candidate-01-road-corridor';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const neighbours = [[0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1]];
const hash = (value: unknown) => createHash('sha256').update(JSON.stringify(value)).digest('hex');
const layers = (map: GameMap, levelId = 'c01-01', editing = false) =>
  candidate01MapSceneryLayers({ content, levelId, map, viewport: candidateReviewViewport(map), editing });
const roadPieces = (map: GameMap) => layers(map).ground.filter(piece => piece.markup.includes('data-runtime-raster="area-road"'));
const profile = candidate01EnvironmentScene('c01-01')?.roadCorridor;
if (!profile) throw new Error('reviewed c01-01 road profile is missing');
const fixtureProfile = { ...profile, protectedCells: [] };
const painted = (map: GameMap) => (x: number, y: number) => candidate01IsAreaRoad(candidateMaterial(content, map.tiles[y * map.width + x]));

/** Compare continuous geometry, independent of a rasterizer's directional AA. */
function geometrySegments(markup: string, rotate = false): string[] {
  const point = (p: number[]) => (rotate ? [160 - p[1], p[0]] : p).map(n => Number(n.toFixed(8))).join(',');
  const segments: string[] = [];
  const add = (points: number[][]) => {
    const forward = points.map(point).join(':');
    const backward = [...points].reverse().map(point).join(':');
    if (new Set(points.map(point)).size > 1) segments.push([forward, backward].sort()[0]);
  };
  for (const [, path] of markup.matchAll(/<path d="([^"]+)"/g)) {
    let current = [0, 0], start = current;
    for (const [, command, body] of path.matchAll(/([MLQZ])([^MLQZ]*)/g)) {
      const n = body.trim().split(/\s+/).filter(Boolean).map(Number);
      if (command === 'M') { current = n; start = n; }
      else if (command === 'L') { add([current, n]); current = n; }
      else if (command === 'Q') { add([current, n.slice(0, 2), n.slice(2)]); current = n.slice(2); }
      else { add([current, start]); current = start; }
    }
  }
  for (const [, x, y, r] of markup.matchAll(/<circle cx="([^"]+)" cy="([^"]+)" r="([^"]+)"/g)) segments.push(`circle:${point([Number(x), Number(y)])}:${r}`);
  for (const [, x, y, w, h] of markup.matchAll(/<rect x="([^"]+)" y="([^"]+)" width="([^"]+)" height="([^"]+)"/g)) {
    const left = Number(x), top = Number(y), right = left + Number(w), bottom = top + Number(h);
    segments.push(`rect:${[[left, top], [right, top], [right, bottom], [left, bottom]].map(point).sort().join(':')}`);
  }
  return segments.sort();
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

  it('derives every cardinal edge exactly once for all256 surrounding patterns and keeps diagonal-only cells separate', () => {
    for (let mask = 0; mask < 256; mask++) {
      const cells = new Set(['2,2', ...neighbours.flatMap(([dx, dy], bit) => mask & 1 << bit ? [`${2 + dx},${2 + dy}`] : [])]);
      const graph = candidate01RoadGraph(5, 5, (x, y) => cells.has(`${x},${y}`));
      const edges = graph.chains.flatMap(chain => chain.slice(1).map((p, i) => [p.join(','), chain[i].join(',')].sort().join(':')));
      expect(new Set(edges).size, `duplicate edge in mask ${mask}`).toBe(edges.length);
      expect(edges.length).toBe(graph.cells.reduce((n, p) => n + graph.neighbors(p).length, 0) / 2);
      for (const chain of graph.chains) for (let i = 1; i < chain.length; i++) {
        expect(Math.abs(chain[i][0] - chain[i - 1][0]) + Math.abs(chain[i][1] - chain[i - 1][1])).toBe(1);
      }
      expect(new Set(graph.cells.map(p => p.join(',')))).toEqual(cells);
    }
  });

  it('has exactly rotation-equivalent continuous lines, quadratic turns, caps and aprons for all256 patterns', () => {
    const geometries = Array.from({ length: 256 }, (_, mask) => {
      const cells = new Set(['2,2', ...neighbours.flatMap(([dx, dy], bit) => mask & 1 << bit ? [`${2 + dx},${2 + dy}`] : [])]);
      const graph = candidate01RoadGraph(5, 5, (x, y) => cells.has(`${x},${y}`));
      return candidate01RoadCorridorGeometry(graph, fixtureProfile, () => true).alpha;
    });
    for (let mask = 0; mask < 256; mask++) {
      expect(geometrySegments(geometries[mask], true), `rotation of mask ${mask}`)
        .toEqual(geometrySegments(geometries[((mask << 2) | (mask >> 6)) & 255]));
    }
  });

  it('derives the actual84 centers and99 ports without changing the authoritative map', () => {
    const map = mapFromLevel(content, candidate01Level('c01-01'));
    const before = hash(map);
    const graph = candidate01RoadGraph(map.width, map.height, painted(map));
    expect(graph.cells).toHaveLength(84);
    expect(graph.chains).toHaveLength(46);
    expect(graph.chains.reduce((n, chain) => n + chain.length - 1, 0)).toBe(99);
    expect(graph.occupied.has('11,8')).toBe(false);
    expect(graph.occupied.has('12,8')).toBe(false);
    const pieces = roadPieces(map);
    expect(pieces).toHaveLength(1);
    for (const y of [9, 10]) expect(pieces[0].markup).toContain(`M 16 ${y * 32 + 16} L 0 ${y * 32 + 16}`);
    for (const y of [6, 7]) expect(pieces[0].markup).toContain(`M 848 ${y * 32 + 16} L 864 ${y * 32 + 16}`);
    expect(pieces.every(piece => piece.foot === undefined)).toBe(true);
    expect(hash(map)).toBe(before);
    map.tiles[9 * map.width] = 'plain';
    expect(candidate01RoadGraph(map.width, map.height, painted(map)).cells).toHaveLength(83);
    expect(roadPieces(map)[0].markup).not.toBe(pieces[0].markup);
  });

  it('joins bridges at their threshold without including bridge cells in paint permission', () => {
    const map = mapFromLevel(content, candidate01Level('c01-01'));
    map.tiles.fill('plain');
    map.tiles[8 * map.width + 8] = 'road';
    map.tiles[8 * map.width + 9] = 'bridge';
    map.tiles[9 * map.width + 8] = 'village';
    map.tiles[7 * map.width + 8] = 'forest';
    map.tiles[8 * map.width + 7] = 'mountain';
    const before = hash(map);
    const graph = candidate01RoadGraph(map.width, map.height, painted(map));
    expect(graph.cells).toEqual([[8, 8], [8, 9]]);
    const markup = roadPieces(map)[0].markup;
    expect(markup).toContain('M 272 272 L 288 272');
    expect(markup).not.toContain('<rect x="288" y="256" width="32" height="32"/>');
    expect(candidate01IsAreaRoad(candidateMaterial(content, 'bridge'))).toBe(false);
    expect(hash(map)).toBe(before);
  });

  it('rounds all turns of standalone cycles and fills broad aprons without a center pinhole', () => {
    const graph = candidate01RoadGraph(4, 4, (x, y) => x >= 1 && x <= 2 && y >= 1 && y <= 2);
    expect(graph.chains).toHaveLength(1);
    expect(graph.chains[0][0]).toEqual(graph.chains[0].at(-1));
    const geometry = candidate01RoadCorridorGeometry(graph, fixtureProfile, () => true);
    expect(geometry.alpha.match(/ Q /g)).toHaveLength(4);
    expect(geometry.alpha).toContain('<rect x="48" y="48" width="32" height="32"/>');
    const singleton = candidate01RoadCorridorGeometry(candidate01RoadGraph(3, 3, (x, y) => x === 1 && y === 1), fixtureProfile, () => true);
    expect(singleton.alpha).toContain('<circle cx="48" cy="48" r="16"/>');
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
    const pieces = candidate01RoadMaterialPieces(2, 80, () => true, () => true, () => false, fixtureProfile);
    expect(pieces.map(({ x, y }) => [x, y])).toEqual([[0, 0], [0, 1024], [0, 2048]]);
    for (const piece of pieces) {
      expect(piece.markup.match(/href="[^\"]*\/road-earth@2x.png[^\"]*"/g)).toHaveLength(1);
      expect(piece.markup).toContain('width="1024" height="1024" preserveAspectRatio="none"');
      const id = /<mask id="([^\"]+)"/.exec(piece.markup)![1];
      expect(piece.markup).toContain(`mask="url(#${id})"`);
      const definitions = new Set([...piece.markup.matchAll(/id="([^\"]+)"/g)].map(match => match[1]));
      for (const reference of piece.markup.matchAll(/url\(#([^)]+)\)/g)) expect(definitions.has(reference[1])).toBe(true);
      expect(piece.markup).not.toContain('filter=');
    }
    expect(pieces[0].markup).toContain('transform="translate(0 0)"');
    expect(pieces[1].markup).toContain('transform="translate(0 -1024)"');
    expect(pieces[2].markup).toContain('width="64" height="512" viewBox="0 0 64 512"');
    expect(candidate01RoadMaterialPieces(2, 80, () => false, () => true, () => true, fixtureProfile)).toEqual([]);
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

  it('rejects unreviewed width/rounding/overhang and protected-cell collisions', () => {
    const graph = candidate01RoadGraph(3, 3, (x, y) => x === 1 && y === 1);
    for (const property of ['width', 'maxTangentCut', 'maxPlainOverhang'] as const) {
      expect(() => candidate01RoadCorridorGeometry(graph, { ...fixtureProfile, [property]: 100 }, () => true)).toThrow('reviewed 32/22/2');
    }
    expect(() => candidate01RoadCorridorGeometry(graph, { ...fixtureProfile, protectedCells: [[1, 1]] }, () => true)).toThrow('protected scene cell');
  });

  it('bounds every occupancy query and rejects invalid extents', () => {
    candidate01RoadMaterialPieces(1, 1, () => true, (x, y) => {
      expect([x, y]).toEqual([0, 0]); return true;
    }, () => true, fixtureProfile);
    for (const [width, height] of [[0, 1], [1, -1], [1.5, 2], [1, Infinity]]) {
      expect(() => candidate01RoadMaterialPieces(width, height, () => true, () => true, () => true, fixtureProfile)).toThrow('positive integers');
    }
  });
});
