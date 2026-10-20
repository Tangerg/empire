import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { idx, mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { CANDIDATE_01_EDGE_DECORATIONS as decorations, candidate01EdgeDecorationPieces } from './candidate-01-edge-decoration';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');


/** Exercise every registered local support contract, independent of the remodeled map. */
function supportedFixture(scene = decorations.scenes[0]) {
  const [width, height] = scene.mapSize;
  const map = mapFromLevel(content, { ...candidate01Level('c01-02'), width, height, terrain: Array(height).fill('.'.repeat(width)), units: [], owners: [], elevation: Array(width * height).fill(0) });
  for (const { terrainSupport: { origin, rows } } of scene.placements) {
    rows.forEach((row, y) => row.forEach((terrain, x) => { map.tiles[idx(map, x + origin[0], y + origin[1])] = terrain; }));
  }
  return map;
}

describe('authored c01-02 ground-edge decoration', () => {
  it('resolves complete immutable sources and both native-density padded exports', () => {
    expect(decorations.meaningfulAlphaMin).toBe(2);
    expect(decorations.assets).toHaveLength(2);
    for (const asset of decorations.assets) {
      const source = readFileSync(resolve(pack, asset.source));
      expect(createHash('sha256').update(source).digest('hex')).toBe(asset.sourceSha256);
      expect([source.readUInt32BE(16), source.readUInt32BE(20)]).toEqual(asset.sourceSize);
      expect(asset.logicalAnchor).toEqual(asset.sourceAnchor.map((n) => (n + asset.sourcePadding) * asset.logicalPerSourcePixel));
      for (const [file, density] of [[asset.png, 1], [asset.png2x, 2]] as const) {
        const png = readFileSync(resolve(pack, 'edge-decoration-v1', file));
        expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual(asset.logicalSize.map((n) => Math.round(n * density)));
      }
      expect(asset.href).toContain(asset.png2x);
    }
  });

  it('draws all fourteen registered patches when their original local terrain support is present', () => {
    const map = supportedFixture();
    const before = JSON.stringify(map);
    const pieces = candidate01EdgeDecorationPieces('c01-02', map);
    expect(pieces).toHaveLength(14);
    expect(pieces.slice(0, 6).map(({ x, y }) => [x, y])).toEqual([
      [112, 120], [96, 168], [560, 120], [560, 168], [144, 296], [560, 248],
    ]);
    expect(pieces.slice(6).map(({ x, y }) => [x, y])).toEqual([
      [310, 64], [310, 160], [310, 256], [310, 368],
      [394, 64], [394, 160], [394, 256], [394, 368],
    ]);
    // Coordinates live outside the shared image. No 32px slices or per-cell reset.
    expect(pieces[0].markup).toBe(pieces[2].markup);
    expect(pieces[1].markup).toBe(pieces[3].markup);
    expect(pieces[6].markup).toBe(pieces[7].markup);
    expect(new Set(pieces.map((p) => p.markup)).size).toBe(6);
    for (const piece of pieces) {
      expect(piece.markup.match(/<image /g)).toHaveLength(1);
      expect(piece.markup).not.toMatch(/clipPath|mask=|opacity=|filter=|is-standing|rotate/);
      expect(piece.foot).toBeUndefined();
    }
    expect(JSON.stringify(map)).toBe(before);
  });

  it('stays below buildings and actors and keeps coordinates through camera changes', () => {
    const map = supportedFixture();
    const viewport = candidateReviewViewport(map);
    const context = { content, map, levelId: 'c01-02', viewport };
    const first = candidate01MapSceneryLayers(context);
    const second = candidate01MapSceneryLayers({ ...context, viewport: { ...viewport, originX: viewport.originX + 137, originY: viewport.originY + 53 } });
    const pieces = candidate01EdgeDecorationPieces('c01-02', map);
    expect(pieces).toHaveLength(14);
    expect(first.ground.slice(-pieces.length)).toEqual(pieces);
    expect(first.ground).toEqual(second.ground);
    expect([...first.underUnits, ...first.overUnits].some((p) => p.markup.includes('candidate-ground-edge'))).toBe(false);
  });

  it('fails closed locally when land, water, bridge or route support changes', () => {
    for (const scene of decorations.scenes) for (const replacement of ['water', 'bridge', 'road', 'forest'] as const) {
      for (const placement of scene.placements) {
        const map = supportedFixture(scene);
        expect(candidate01EdgeDecorationPieces(scene.levelId, map).some(p => p.x === placement.center[0] && p.y === placement.center[1])).toBe(true);
        const [x, y] = placement.terrainSupport.origin;
        map.tiles[idx(map, x, y)] = map.tiles[idx(map, x, y)] === replacement ? 'plain' : replacement;
        const before = JSON.stringify(map);
        expect(candidate01EdgeDecorationPieces(scene.levelId, map).some((p) => p.x === placement.center[0] && p.y === placement.center[1])).toBe(false);
        expect(JSON.stringify(map)).toBe(before);
      }
    }
    const map = mapFromLevel(content, candidate01Level('c01-02'));
    map.tiles[idx(map, 0, 0)] = 'forest';
    expect(candidate01EdgeDecorationPieces('c01-02', map)).toHaveLength(14);
  });

  it('does not claim coverage for another level or reuse a mismatched map extent', () => {
    const map = mapFromLevel(content, candidate01Level('c01-02'));
    expect(candidate01EdgeDecorationPieces('c01-01', map)).toEqual([]);
    expect(candidate01EdgeDecorationPieces('editor-map', map)).toEqual([]);
    expect(candidate01EdgeDecorationPieces('c01-02', { ...map, width: map.width - 1 })).toEqual([]);
    expect(candidate01EdgeDecorationPieces('c01-02', map)).toHaveLength(14);
    map.tiles.fill('forest');
    expect(candidate01EdgeDecorationPieces('c01-02', map)).toEqual([]);
  });
});
