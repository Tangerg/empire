import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { inBounds, idx, mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../index';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import catalog from '../../../../tools/art/surface-overrides.json';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { candidate01IsAreaWater } from './candidate-01-water-material';
import { candidate01BlobMask } from './candidate-01-square-mask';
import { readCandidateWorldMaskAtlas } from './candidate-01-world-material';
import { CANDIDATE_01_EARTH_FLOOR, candidate01EarthBlendMarkup } from './candidate-01-earth-material';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');
const blendId = 'transition-meadow-earth';

describe('earth material for terrain interiors and generated shores', () => {
  it('retains the exact opaque source identity and reviewed independent world/raster scales', () => {
    const material = CANDIDATE_01_EARTH_FLOOR;
    expect(material.replaces).toEqual({ atlas: 'surface-earth', tone: 0 });
    expect(material.logicalSize).toEqual([256, 256]);
    expect(material.rasterSize).toEqual([512, 512]);
    expect(material.rasterPixelsPerWorldUnit).toEqual([4, 4]);
    expect(material.worldOrigin).toEqual([0, 0]);
    expect(material.fit).toEqual({ inset: 0, bleed: 0 });
    const source = readFileSync(resolve(pack, material.source));
    expect(createHash('sha256').update(source).digest('hex')).toBe(material.sourceSha256);
    expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
    for (const [density, path] of [[1, material.png], [2, material.png2x]] as const) {
      const image = readFileSync(resolve(pack, 'world-materials-v1', path));
      expect([image.readUInt32BE(16), image.readUInt32BE(20), image[25]]).toEqual([512 * density, 512 * density, 2]);
    }
    const markup = candidate01EarthBlendMarkup(blendId, 46, 224, 32);
    expect(markup).toContain('x="-224" y="-32" width="256" height="256"');
    expect(candidate01EarthBlendMarkup(blendId, 46, 480, 288)).toBe(markup);
    expect(candidate01EarthBlendMarkup(blendId, 46, 256, 0)).toBe(candidate01EarthBlendMarkup(blendId, 46, 0, 0));
  });

  it('keeps all 47 mask identities and original 256-neighbour normalization with no old RGB', () => {
    const atlas = CANDIDATE_01_ENVIRONMENT.atlas(blendId);
    const seen = new Set<number>();
    for (let input = 0; input < 256; input++) {
      let normalized = input;
      for (const [diagonal, required] of [[2, 5], [8, 20], [32, 80], [128, 65]]) {
        if ((input & required) !== required) normalized &= ~diagonal;
      }
      const cell = CANDIDATE_01_ENVIRONMENT.blobIndex(blendId, input);
      expect(atlas.maskOrder?.[cell]).toBe(normalized);
      const markup = candidate01EarthBlendMarkup(blendId, cell, 64, 96);
      expect(markup).toContain(CANDIDATE_01_EARTH_FLOOR.href);
      expect(markup).toContain('earth-blend-masks@2x.png');
      expect(markup).toContain(`-${cell}-mask`);
      expect(markup).not.toContain(atlas.raster.href);
      seen.add(cell);
    }
    expect([...seen].sort((a, b) => a - b)).toEqual(Array.from({ length: 47 }, (_, index) => index));
    for (const other of ['transition-meadow-forest', 'transition-meadow-snow', 'transition-earth-stone', 'route-dirt-road']) {
      expect(candidate01EarthBlendMarkup(other, 0, 0, 0)).toBeNull();
    }
    expect(() => readCandidateWorldMaskAtlas({ id: blendId, png: 'runtime/missing.png', png2x: 'runtime/earth-blend-masks@2x.png' })).toThrow('missing material mask');
  });

  it('covers 169 explicit interiors and 134 generated banks using their different original neighbour predicates', () => {
    const counts = new Map([['c01-02', [0, 28]], ['c01-05', [55, 0]], ['c01-06', [0, 28]], ['c01-12', [114, 38]], ['c01-14', [0, 40]]]);
    let total = 0;
    for (const level of CANDIDATE_01_LEVELS) {
      const map = mapFromLevel(content, level);
      const before = JSON.stringify({ level, map });
      const materialAt = (x: number, y: number) => inBounds(map, x, y) ? candidateMaterial(content, map.tiles[idx(map, x, y)]) : null;
      const isWater = (x: number, y: number) => {
        const material = materialAt(x, y);
        return material !== null && candidate01IsAreaWater(material);
      };
      const isShore = (x: number, y: number) => !isWater(x, y)
        && [[0, -1], [1, 0], [0, 1], [-1, 0]].some(([dx, dy]) => isWater(x + dx, y + dy));
      const context = { content, levelId: level.id, map, viewport: candidateReviewViewport(map) };
      const scene = candidate01MapSceneryLayers(context);
      const expected = [];
      let interiors = 0, banks = 0;
      for (let y = 0; y < map.height; y++) for (let x = 0; x < map.width; x++) {
        const material = materialAt(x, y);
        const interior = material?.blend === blendId;
        const bank = material?.blend === undefined && isShore(x, y);
        if (!interior && !bank) continue;
        expect(isWater(x, y)).toBe(false);
        const mask = candidate01BlobMask(x, y, interior ? (nx, ny) => materialAt(nx, ny)?.blend === blendId : isShore);
        const cell = CANDIDATE_01_ENVIRONMENT.blobIndex(blendId, mask);
        expected.push({ x: x * 32, y: y * 32, markup: candidate01EarthBlendMarkup(blendId, cell, x * 32, y * 32) });
        if (interior) interiors++; else banks++;
      }
      expect([interiors, banks], level.id).toEqual(counts.get(level.id) ?? [0, 0]);
      const actual = scene.ground.filter(piece => piece.markup.includes(CANDIDATE_01_EARTH_FLOOR.href));
      expect(actual, level.id).toEqual(expected);
      const water = scene.ground.flatMap((piece, index) => piece.markup.includes('data-runtime-raster="area-water"') ? [index] : []);
      for (const piece of actual) if (water.length) expect(scene.ground.indexOf(piece)).toBeLessThan(Math.min(...water));
      expect(scene.ground.map(piece => piece.markup).join('')).not.toContain(CANDIDATE_01_ENVIRONMENT.atlas(blendId).raster.href);
      for (const layer of [scene.underUnits, scene.overUnits]) expect(layer.some(piece => piece.markup.includes(CANDIDATE_01_EARTH_FLOOR.href))).toBe(false);
      expect(candidate01MapSceneryLayers({ ...context, editing: true }).ground.filter(piece => piece.markup.includes(CANDIDATE_01_EARTH_FLOOR.href)), level.id).toEqual(actual);
      expect(candidate01MapSceneryLayers({ ...context, viewport: { ...context.viewport, sceneWidth: context.viewport.sceneWidth + 71 } }), level.id).toEqual(scene);
      expect(JSON.stringify({ level, map })).toBe(before);
      total += actual.length;
    }
    expect(total).toBe(303);
  });

  it('keeps the other three native earth slots catalog-only without reclassifying terrain', () => {
    const entries = catalog.atlases['surface-earth'].cells;
    expect(Object.keys(entries)).toEqual(['earth-1', 'earth-2', 'earth-3', 'earth-4']);
    for (const [slot, entry] of Object.entries(entries)) {
      expect(entry.source).toContain(`/surface-earth/${slot}/master.png`);
      const source = readFileSync(resolve(import.meta.dirname, '../../../..', entry.source));
      expect(createHash('sha256').update(source).digest('hex')).toBe(entry.sha256);
      expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
    }
    expect(entries['earth-1'].sha256).toBe(CANDIDATE_01_EARTH_FLOOR.sourceSha256);
    for (const level of CANDIDATE_01_LEVELS) expect(mapFromLevel(content, level).tiles.filter(id => candidateMaterial(content, id).surface === 'surface-earth'), level.id).toEqual([]);
  });
});
