import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { idx, mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { GROUND_TAGS, taggedGround, type BoardPiece } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { candidate01RiverMaterialPieces, candidate01RiverPath, type CandidateRiverProfile } from './candidate-01-river-material';
import { candidate01PlayableShape } from './candidate-01-playable-shape';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { CANDIDATE_01_ROAD_MATERIAL, candidate01IsAreaRoad } from './candidate-01-road-material';
import { CANDIDATE_01_WATER_MATERIAL } from './candidate-01-water-material';

const profile: CandidateRiverProfile = { width: 48, bankWidth: 58, knots: [[1, 0], [2, 20], [1, 40]] };
const sourceHashes = () => [CANDIDATE_01_ROAD_MATERIAL, CANDIDATE_01_WATER_MATERIAL].map(material =>
  createHash('sha256').update(readFileSync(resolve(import.meta.dirname, '../../assets/final-fantasy-v1', material.source))).digest('hex'));

function unwrapPlayableClip(pieces: readonly BoardPiece[], shape: NonNullable<ReturnType<typeof candidate01PlayableShape>>): BoardPiece[] {
  return pieces.map(piece => {
    const clip = /^<defs><clipPath id="([^"]+)"><path d="([^"]*)" transform="translate\(([^)]+)\)"\/><\/clipPath><\/defs><g clip-path="url\(#([^"]+)\)">([\s\S]*)<\/g>$/.exec(piece.markup);
    if (!clip) throw new Error('river piece lacks its self-contained playable clip');
    expect(clip[2]).toBe(shape.path);
    expect(clip[3]).toBe(`${-piece.x} ${-piece.y}`);
    expect(clip[4]).toBe(clip[1]);
    return { ...piece, markup: clip[5] };
  });
}

describe('authored river material', () => {
  it('binds the authored curve only in play and aligns its tangent knots to all three bridge centers', () => {
    const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    const level = candidate01Level('c01-02'), map = mapFromLevel(content, level);
    const river = candidate01EnvironmentScene(level.id)!.river!;
    const before = JSON.stringify({ map, level, river });
    const sourcesBefore = sourceHashes();
    const context = { content, levelId: level.id, map, viewport: candidateReviewViewport(map) };
    const play = candidate01MapSceneryLayers(context), edit = candidate01MapSceneryLayers({ ...context, editing: true });
    const occupied = (x: number, y: number) => ['water', 'bridge'].includes(map.tiles[idx(map, x, y)]);
    const expected = candidate01RiverMaterialPieces(map.width, map.height, occupied, river, (x, y) => {
      const id = map.tiles[idx(map, x, y)];
      return occupied(x, y) || id === 'plain'
        || candidate01IsAreaRoad(candidateMaterial(content, id)) && !taggedGround(content.terrains.get(id), GROUND_TAGS.settlement);
    });
    const shape = candidate01PlayableShape(content, map);
    if (!shape) throw new Error('Three Bridges must have a terrain-owned irregular boundary');
    for (const kind of ['area-water', 'river-bank']) {
      const actual = play.ground.filter(p => p.markup.includes(`data-runtime-raster="${kind}"`));
      expect(unwrapPlayableClip(actual, shape)).toEqual(expected.filter(p => p.markup.includes(`data-runtime-raster="${kind}"`)));
      expect(actual.map(p => [p.x, p.y])).toEqual(kind === 'area-water' ? [[512, 0], [512, 512]] : [[0, 0], [1024, 0]]);
    }
    expect(edit.ground.some(p => p.markup.includes('data-runtime-raster="river-bank"'))).toBe(false);
    let crossings = 0;
    for (let y = 0; y < level.height; y++) {
      const columns = [...level.terrain[y]].flatMap((symbol, x) => symbol === '=' ? [x] : []);
      if (!columns.length) continue;
      const x = columns.reduce((a, b) => a + b + .5, 0) / columns.length;
      const knot = river.knots.findIndex(point => point[0] === x && point[1] === y + .5);
      expect(knot, `bridge at row ${y}`).toBeGreaterThanOrEqual(0);
      expect(river.widths?.[knot]).toBe(river.width);
      crossings++;
    }
    expect(crossings).toBe(3);
    expect(river.widths).toHaveLength(river.knots.length);
    expect(river.widths).toContain(24);
    expect(play.underUnits).toEqual(edit.underUnits);
    expect(play.overUnits).toEqual(edit.overUnits);
    expect(JSON.stringify({ map, level, river })).toBe(before);
    expect(sourceHashes()).toEqual(sourcesBefore);
  });
  it('uses level crossing tangents and preserves native world material periods', () => {
    expect(candidate01RiverPath({ ...profile, knots: [[1, 0], [2, 3]] })).toBe('M 32 0 C 32 32 64 64 64 96');
    const pieces = candidate01RiverMaterialPieces(3, 40, () => true, profile);
    const water = pieces.filter(p => p.markup.includes('data-runtime-raster="area-water"'));
    expect(water.map(p => [p.x, p.y])).toEqual([[0, 0], [0, 512], [0, 1024]]);
    for (const piece of water) {
      expect(piece.markup).toContain('width="512" height="512" mask=');
      expect(piece.markup).toContain(`transform="translate(0 ${-piece.y})"`);
      expect(piece.markup.match(/water-area@2x.png/g)).toHaveLength(1);
      const definitions = new Set([...piece.markup.matchAll(/id="([^"]+)"/g)].map(m => m[1]));
      for (const reference of piece.markup.matchAll(/url\(#([^)]+)\)/g)) expect(definitions.has(reference[1])).toBe(true);
    }
  });
  it('clips both material strokes to authoritative occupied cells and bounds all queries', () => {
    const pieces = candidate01RiverMaterialPieces(1, 2, (x, y) => {
      expect(x).toBe(0); expect(y).toBeGreaterThanOrEqual(0); expect(y).toBeLessThan(2); return y === 1;
    }, profile);
    expect(pieces).toHaveLength(2);
    for (const { markup } of pieces) {
      expect(markup).toContain('M0 32h32v32h-32Z');
      expect(markup).not.toContain('M0 0h32v32h-32Z');
      expect(markup).toContain('<g clip-path=');
    }
    expect(candidate01RiverMaterialPieces(3, 3, () => false, profile)).toEqual([]);
  });
  it('allows only the bounded bank rim onto explicitly permitted dry ground', () => {
    const pieces = candidate01RiverMaterialPieces(3, 3, (x, y) => x === 1 && y === 1, profile,
      (x, y) => y === 1 && x <= 1);
    const bank = pieces.find(p => p.markup.includes('data-runtime-raster="river-bank"'))!.markup;
    const water = pieces.find(p => p.markup.includes('data-runtime-raster="area-water"'))!.markup;
    expect(bank).toContain('M0 32h32v32h-32Z');
    expect(bank).not.toContain('M64 32h32v32h-32Z');
    expect(water).not.toContain('M0 32h32v32h-32Z');
    expect(bank).toContain('<rect x="27" y="27" width="42" height="42" rx="5"/>');
    expect(bank).toContain('-rim)"><path');
  });
  it('pinches a ribbon without changing its centerline, terrain permission, material sample or inputs', () => {
    const uniform: CandidateRiverProfile = { width: 48, bankWidth: 58, knots: [[2, 0], [2, 3], [2, 6]] };
    const pinched: CandidateRiverProfile = { ...uniform, widths: [48, 24, 48] };
    const before = JSON.stringify({ uniform, pinched, road: CANDIDATE_01_ROAD_MATERIAL, water: CANDIDATE_01_WATER_MATERIAL });
    const baseline = candidate01RiverMaterialPieces(4, 7, (x) => x === 1 || x === 2, uniform);
    const actual = candidate01RiverMaterialPieces(4, 7, (x) => x === 1 || x === 2, pinched);
    expect(candidate01RiverPath(pinched)).toBe(candidate01RiverPath(uniform));
    expect(actual.map(({ x, y }) => [x, y])).toEqual(baseline.map(({ x, y }) => [x, y]));
    const paths = [
      'M 35 0 C 35 32 47 64 47 96 C 47 128 35 160 35 192 L 93 192 C 93 160 81 128 81 96 C 81 64 93 32 93 0 Z',
      'M 40 0 C 40 32 52 64 52 96 C 52 128 40 160 40 192 L 88 192 C 88 160 76 128 76 96 C 76 64 88 32 88 0 Z',
    ];
    expect(actual).toHaveLength(paths.length);
    for (const [index, piece] of actual.entries()) {
      expect(piece.markup).toContain(`d="${paths[index]}" fill="white"`);
      expect(piece.markup).not.toContain('stroke-width=');
      expect(baseline[index].markup).toContain(`stroke-width="${index === 0 ? 58 : 48}"`);
      for (const clip of ['terrain', 'rim']) {
        const definition = new RegExp(`<clipPath id="[^"]+-${clip}">([\\s\\S]*?)<\\/clipPath>`);
        expect(definition.exec(piece.markup)?.[1]).toBe(definition.exec(baseline[index].markup)?.[1]);
      }
      const image = (markup: string) => /<image [^>]+>/.exec(markup)?.[0].replace(/ mask="[^"]+"/, '');
      expect(image(piece.markup)).toBe(image(baseline[index].markup));
      const ids = new Set([...piece.markup.matchAll(/id="([^"]+)"/g)].map(match => match[1]));
      for (const reference of piece.markup.matchAll(/url\(#([^)]+)\)/g)) expect(ids.has(reference[1])).toBe(true);
      const maskId = (markup: string) => /<mask id="([^"]+)"/.exec(markup)?.[1];
      expect(maskId(piece.markup)).not.toBe(maskId(baseline[index].markup));
    }
    expect(JSON.stringify({ uniform, pinched, road: CANDIDATE_01_ROAD_MATERIAL, water: CANDIDATE_01_WATER_MATERIAL })).toBe(before);
  });
  it('accepts both local width bounds and rejects missing, nonfinite or out-of-range entries', () => {
    expect(() => candidate01RiverPath({ ...profile, widths: [16, 24, 48] })).not.toThrow();
    const sparse = [48, 24, 48];
    delete sparse[1];
    for (const widths of [[], [48], [48, 24], [48, 24, 48, 48], [48, 15, 48], [48, 49, 48],
      [48, NaN, 48], [48, Infinity, 48], [48, -Infinity, 48], sparse]) {
      const invalid = { ...profile, widths };
      expect(() => candidate01RiverPath(invalid)).toThrow('bounded widths');
      expect(() => candidate01RiverMaterialPieces(3, 3, () => true, invalid)).toThrow('bounded widths');
    }
  });
  it('rejects invalid extents and non-downstream profiles rather than silently drawing them', () => {
    for (const [width, height] of [[0, 1], [1, -1], [1.5, 2], [1, Infinity]]) {
      expect(() => candidate01RiverMaterialPieces(width, height, () => true, profile)).toThrow('positive integers');
    }
    for (const invalid of [{ width: 0 }, { width: Infinity }, { bankWidth: 48 }, { bankWidth: 65 }, { width: 1 },
      { knots: [[1, 0]] }, { knots: [[1, 1], [2, 1]] }, { knots: [[1, 0], [NaN, 1]] }]) {
      expect(() => candidate01RiverPath({ ...profile, ...invalid } as CandidateRiverProfile)).toThrow('downstream knots');
    }
  });
});
