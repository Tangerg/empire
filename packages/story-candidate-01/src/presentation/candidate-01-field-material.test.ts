import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { runtimeTileMarkup } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { candidate01Level } from '../levels';
import { CANDIDATE_01_FIELD_MATERIAL, candidate01FieldMaterialPieces } from './candidate-01-field-material';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { CANDIDATE_SURFACE_FIT, candidateMaterial } from './candidate-01-terrain-materials';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const material = CANDIDATE_01_FIELD_MATERIAL;
const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const rasterBox = (markup: string) => {
  const box = /<svg width="([\d.]+)" height="([\d.]+)" viewBox="0 0 ([\d.]+) ([\d.]+)"/.exec(markup);
  if (!box) throw new Error('field block has no explicit viewport');
  return box.slice(1).map(Number);
};

describe('approved broad world material', () => {
  it('resolves a checksummed immutable source and native 1x/2x output through one manifest', () => {
    const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');
    expect(createHash('sha256').update(readFileSync(resolve(pack, material.source))).digest('hex')).toBe(material.sourceSha256);
    expect(material.source).toBe('masters/approved-v5/grass-material/master.png');
    expect(material.logicalSize).toEqual([512, 512]);
    expect(material.worldOrigin).toEqual([0, 0]);
    expect(material.fit).toEqual({ inset: 0, bleed: 0 });
    expect(material.replaces).toEqual({ atlas: 'surface-meadow', tone: 0 });
    for (const [density, path] of [[1, material.png], [2, material.png2x]] as const) {
      const png = readFileSync(resolve(pack, 'world-materials-v1', path));
      expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual(material.logicalSize.map((n) => n * density));
    }
    expect(material.href).toContain(material.png2x);
  });

  it('keeps adjacent tactical cells in one world plane and repeats at the approved world period', () => {
    const pieces = candidate01FieldMaterialPieces(704, 448);
    expect(pieces.map(({ x, y }) => [x, y])).toEqual([[0, 0], [512, 0]]);
    expect(pieces.map((piece) => rasterBox(piece.markup))).toEqual([[512, 448, 512, 448], [192, 448, 192, 448]]);
    for (const piece of pieces) {
      expect(piece.markup).toContain('<image');
      expect(piece.markup).toContain('width="512" height="512" preserveAspectRatio="none"');
      expect(piece.markup).not.toContain('url(#');
    }
    const sample = (x: number, y: number) => {
      const piece = pieces.find((candidate) => {
        const [w, h] = rasterBox(candidate.markup);
        return x >= candidate.x && x < candidate.x + w && y >= candidate.y && y < candidate.y + h;
      });
      if (!piece) throw new Error('world sample outside field');
      return [x - piece.x, y - piece.y];
    };
    expect(sample(31.75, 60)).toEqual([31.75, 60]);
    expect(sample(32.25, 60)).toEqual([32.25, 60]);
    expect(sample(511.75, 60)).toEqual([511.75, 60]);
    expect(sample(512.25, 60)).toEqual([0.25, 60]);
    expect(sample(703.75, 447.75)).toEqual([191.75, 447.75]);
  });

  it('bounds texture size and shares repeated block markup on large battlefields', () => {
    const pieces = candidate01FieldMaterialPieces(81 * 32, 51 * 32);
    expect(pieces).toHaveLength(24);
    expect(new Set(pieces.map((piece) => piece.markup)).size).toBe(4);
    for (const piece of pieces) {
      const [w, h] = rasterBox(piece.markup);
      expect(w).toBeLessThanOrEqual(512);
      expect(h).toBeLessThanOrEqual(512);
      expect(piece.markup.match(/<image /g)).toHaveLength(1);
    }
  });

  it('does not let camera/inset changes reset world phase or mutate any shipped topology', () => {
    for (let chapter = 1; chapter <= 16; chapter++) {
      const levelId = `c01-${String(chapter).padStart(2, '0')}`;
      const map = mapFromLevel(content, candidate01Level(levelId));
      const before = JSON.stringify(map);
      const viewport = candidateReviewViewport(map);
      const context = { content, levelId, map, viewport };
      const first = candidate01MapSceneryLayers(context);
      const shifted = candidate01MapSceneryLayers({ ...context, viewport: { ...viewport, sceneWidth: viewport.sceneWidth + 127 } });
      expect(shifted.ground, levelId).toEqual(first.ground);
      expect(JSON.stringify(map), levelId).toBe(before);
      expect(map.tiles.length).toBe(map.width * map.height);
    }
  });

  it('preserves every non-base surface and the old atlas fit contract', () => {
    const map = mapFromLevel(content, candidate01Level('c01-02'));
    const layers = candidate01MapSceneryLayers({ content, map, levelId: 'c01-02', viewport: candidateReviewViewport(map) });
    for (let y = 0; y < map.height; y++) for (let x = 0; x < map.width; x++) {
      const terrain = candidateMaterial(content, map.tiles[y * map.width + x]);
      const atlas = terrain.surface ?? material.replaces.atlas;
      const tone = terrain.tone ?? material.replaces.tone;
      if (atlas === material.replaces.atlas && tone === material.replaces.tone) continue;
      const expected = runtimeTileMarkup(CANDIDATE_01_ENVIRONMENT.atlas(atlas).raster, tone, CANDIDATE_SURFACE_FIT);
      expect(layers.ground.some((piece) => piece.x === x * 32 && piece.y === y * 32 && piece.markup === expected)).toBe(true);
    }
    expect(CANDIDATE_SURFACE_FIT).toEqual({ inset: 3, bleed: 0.5 });
    expect(CANDIDATE_01_ENVIRONMENT.atlas('surface-meadow').raster).toMatchObject({ cellWidth: 32, cellHeight: 32, columns: 4, rows: 1 });
  });

  it('rejects invalid field extents instead of emitting an incomplete picture', () => {
    for (const [width, height] of [[0, 32], [-1, 32], [32, Infinity], [NaN, 32]]) {
      expect(() => candidate01FieldMaterialPieces(width, height)).toThrow('finite and positive');
    }
  });
});
