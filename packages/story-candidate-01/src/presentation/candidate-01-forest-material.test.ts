import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { idx, inBounds, mapFromLevel, type GameMap } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import type { BoardPiece } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../index';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import catalog from '../../../../tools/art/surface-overrides.json';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { CANDIDATE_01_FOREST_FLOOR, candidate01ForestBlendMarkup } from './candidate-01-forest-material';
import { candidate01PlayableShape } from './candidate-01-playable-shape';
import { candidate01BlobMask } from './candidate-01-square-mask';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');
const blendId = 'transition-meadow-forest';

function unwrapPlayableClip(pieces: readonly BoardPiece[], map: GameMap): BoardPiece[] {
  const shape = candidate01PlayableShape(content, map);
  if (!shape) return [...pieces];
  return pieces.map(piece => {
    const clip = /^<defs><clipPath id="([^"]+)"><path d="([^"]*)" transform="translate\(([^)]+)\)"\/><\/clipPath><\/defs><g clip-path="url\(#([^"]+)\)">([\s\S]*)<\/g>$/.exec(piece.markup);
    if (!clip) throw new Error('forest piece lacks its self-contained playable clip');
    expect(clip[2]).toBe(shape.path);
    expect(clip[3]).toBe(`${-piece.x} ${-piece.y}`);
    expect(clip[4]).toBe(clip[1]);
    return { ...piece, markup: clip[5] };
  });
}

describe('forest floor world sampling through the original Blob47 alpha', () => {
  it('keeps the native source and reviewed 128-unit world scale independent from raster density', () => {
    const material = CANDIDATE_01_FOREST_FLOOR;
    expect(material.replaces).toEqual({ atlas: 'surface-forest-floor', tone: 0 });
    expect(material.logicalSize).toEqual([128, 128]);
    expect(material.rasterSize).toEqual([512, 512]);
    expect(material.rasterPixelsPerWorldUnit).toEqual([8, 8]);
    expect(material.worldOrigin).toEqual([0, 0]);
    expect(material.fit).toEqual({ inset: 0, bleed: 0 });
    const source = readFileSync(resolve(pack, material.source));
    expect(createHash('sha256').update(source).digest('hex')).toBe(material.sourceSha256);
    expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
    for (const [density, path] of [[1, material.png], [2, material.png2x]] as const) {
      const image = readFileSync(resolve(pack, 'world-materials-v1', path));
      expect([image.readUInt32BE(16), image.readUInt32BE(20), image[25]]).toEqual([512 * density, 512 * density, 2]);
    }
    const markup = candidate01ForestBlendMarkup(blendId, 46, 96, 32);
    expect(markup).toContain('x="-96" y="-32" width="128" height="128"');
    expect(candidate01ForestBlendMarkup(blendId, 46, 224, 160)).toBe(markup);
    expect(candidate01ForestBlendMarkup(blendId, 46, 128, 0)).toBe(candidate01ForestBlendMarkup(blendId, 46, 0, 0));
  });

  it('preserves every valid blob index and all 256 input-neighbour normalizations without using old RGB', () => {
    const atlas = CANDIDATE_01_ENVIRONMENT.atlas(blendId);
    expect(atlas.componentCount).toBe(47);
    const visited = new Set<number>();
    for (let input = 0; input < 256; input++) {
      let normalized = input;
      for (const [diagonal, required] of [[2, 5], [8, 20], [32, 80], [128, 65]]) {
        if ((input & required) !== required) normalized &= ~diagonal;
      }
      const cell = CANDIDATE_01_ENVIRONMENT.blobIndex(blendId, input);
      expect(atlas.maskOrder?.[cell]).toBe(normalized);
      visited.add(cell);
      const markup = candidate01ForestBlendMarkup(blendId, cell, 32, 64);
      expect(markup).toContain(CANDIDATE_01_FOREST_FLOOR.href);
      expect(markup).toContain('forest-blend-masks@2x.png');
      expect(markup).toContain(`-${cell}-mask`);
      expect(markup).not.toContain(atlas.raster.href);
    }
    expect([...visited].sort((a, b) => a - b)).toEqual(Array.from({ length: 47 }, (_, index) => index));
    for (const untouched of ['transition-meadow-earth', 'transition-meadow-snow', 'transition-earth-stone', 'route-forest-trail']) {
      expect(candidate01ForestBlendMarkup(untouched, 0, 0, 0)).toBeNull();
    }
  });

  it('changes exactly 638 ground pieces across 11 actual maps and leaves rule/standing projections alone', () => {
    const counts = new Map([['c01-01', 126], ['c01-02', 26], ['c01-03', 9], ['c01-04', 17], ['c01-05', 11],
      ['c01-06', 12], ['c01-09', 11], ['c01-10', 13], ['c01-11', 24], ['c01-15', 364], ['c01-16', 25]]);
    let total = 0;
    for (const level of CANDIDATE_01_LEVELS) {
      const map = mapFromLevel(content, level);
      const before = JSON.stringify({ level, map });
      const context = { content, levelId: level.id, map, viewport: candidateReviewViewport(map) };
      const scene = candidate01MapSceneryLayers(context);
      const tiles = map.tiles.flatMap((id, index) => candidateMaterial(content, id).blend === blendId
        ? [{ x: index % map.width * 32, y: Math.floor(index / map.width) * 32 }] : []);
      expect(tiles, level.id).toHaveLength(counts.get(level.id) ?? 0);
      const pieces = scene.ground.filter(piece => piece.markup.includes(CANDIDATE_01_FOREST_FLOOR.href));
      expect(pieces.map(({ x, y }) => ({ x, y })), level.id).toEqual(tiles);
      const expected = tiles.map(({ x, y }) => {
        const mask = candidate01BlobMask(x / 32, y / 32, (nx, ny) => inBounds(map, nx, ny)
          && candidateMaterial(content, map.tiles[idx(map, nx, ny)]).blend === blendId);
        const cell = CANDIDATE_01_ENVIRONMENT.blobIndex(blendId, mask);
        const markup = candidate01ForestBlendMarkup(blendId, cell, x, y);
        if (markup === null) throw new Error('expected forest blend binding is missing');
        return { x, y, markup };
      });
      expect(unwrapPlayableClip(pieces, map), level.id).toEqual(expected);
      expect(scene.ground.map(piece => piece.markup).join(''), level.id).not.toContain(CANDIDATE_01_ENVIRONMENT.atlas(blendId).raster.href);
      for (const layer of [scene.underUnits, scene.overUnits]) expect(layer.some(piece => piece.markup.includes(CANDIDATE_01_FOREST_FLOOR.href))).toBe(false);
      const editor = candidate01MapSceneryLayers({ ...context, editing: true }).ground
        .filter(piece => piece.markup.includes(CANDIDATE_01_FOREST_FLOOR.href));
      expect(unwrapPlayableClip(editor, map), level.id).toEqual(expected);
      expect(candidate01MapSceneryLayers({ ...context, viewport: { ...context.viewport, sceneWidth: context.viewport.sceneWidth + 71 } }), level.id).toEqual(scene);
      expect(JSON.stringify({ level, map }), level.id).toBe(before);
      total += pieces.length;
    }
    expect(total).toBe(638);
  });

  it('registers the four original floor slots while keeping tones 1–3 catalog-only', () => {
    const entries = catalog.atlases['surface-forest-floor'].cells;
    expect(Object.keys(entries)).toEqual(['forest-floor-1', 'forest-floor-2', 'forest-floor-3', 'forest-floor-4']);
    for (const [slot, entry] of Object.entries(entries)) {
      expect(entry.source).toContain(`/surface-forest-floor/${slot}/master.png`);
      const source = readFileSync(resolve(import.meta.dirname, '../../../..', entry.source));
      expect(createHash('sha256').update(source).digest('hex')).toBe(entry.sha256);
      expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
    }
    expect(entries['forest-floor-1'].sha256).toBe(CANDIDATE_01_FOREST_FLOOR.sourceSha256);
    for (const level of CANDIDATE_01_LEVELS) {
      const map = mapFromLevel(content, level);
      expect(map.tiles.filter(id => candidateMaterial(content, id).surface === 'surface-forest-floor'), level.id).toEqual([]);
    }
  });
});
