import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../index';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import catalog from '../../../../tools/art/surface-overrides.json';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { CANDIDATE_01_FORGE_BASE } from './candidate-01-forge-material';
import { candidateWorldMaterialTileMarkup } from './candidate-01-world-material';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');

describe('forge world surface and original four-slot catalog', () => {
  it('uses the unchanged source-zero full canvas at 256 logical units and 512/1024 raster density', () => {
    const material = CANDIDATE_01_FORGE_BASE;
    expect(material.replaces).toEqual({ atlas: 'surface-forge-stone', tone: 0 });
    expect(material.logicalSize).toEqual([256, 256]);
    expect(material.rasterSize).toEqual([512, 512]);
    expect(material.rasterPixelsPerWorldUnit).toEqual([4, 4]);
    expect(material.worldOrigin).toEqual([0, 0]);
    expect(material.fit).toEqual({ inset: 0, bleed: 0 });
    const source = readFileSync(resolve(pack, material.source));
    expect(createHash('sha256').update(source).digest('hex')).toBe(material.sourceSha256);
    expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
    for (const [density, file] of [[1, material.png], [2, material.png2x]] as const) {
      const bytes = readFileSync(resolve(pack, 'world-materials-v1', file));
      expect([bytes.readUInt32BE(16), bytes.readUInt32BE(20), bytes[25]]).toEqual([512 * density, 512 * density, 2]);
    }
    const markup = candidateWorldMaterialTileMarkup(material, 224, 32);
    expect(markup).toContain('x="-224" y="-32" width="256" height="256"');
    expect(candidateWorldMaterialTileMarkup(material, 480, 288)).toBe(markup);
    expect(candidateWorldMaterialTileMarkup(material, 256, 0)).toBe(candidateWorldMaterialTileMarkup(material, 0, 0));
  });

  it('registers all four original source slots without inventing a terrain consumer for tones 1–3', () => {
    const entries = catalog.atlases['surface-forge-stone'].cells;
    expect(Object.keys(entries)).toEqual(['forge-stone-1', 'forge-stone-2', 'forge-stone-3', 'forge-stone-4']);
    for (const [slot, entry] of Object.entries(entries)) {
      expect(entry.source).toContain(`/surface-forge-stone/${slot}/master.png`);
      const source = readFileSync(resolve(import.meta.dirname, '../../../..', entry.source));
      expect(createHash('sha256').update(source).digest('hex')).toBe(entry.sha256);
      expect([source.readUInt32BE(16), source.readUInt32BE(20), source[25]]).toEqual([1254, 1254, 2]);
    }
    expect(entries['forge-stone-1'].sha256).toBe(CANDIDATE_01_FORGE_BASE.sourceSha256);
    const atlas = CANDIDATE_01_ENVIRONMENT.atlas('surface-forge-stone');
    expect(atlas.componentCount).toBe(4);
    for (const density of [1, 2]) {
      const png = readFileSync(resolve(pack, `environment-builder-v1/runtime/atlas/surface-forge-stone${density === 2 ? '@2x' : ''}.png`));
      expect([png.readUInt32BE(16), png.readUInt32BE(20), png[25]]).toEqual([128 * density, 32 * density, 2]);
    }
    const expected = new Map([['c01-14', 252]]);
    for (const level of CANDIDATE_01_LEVELS) {
      const before = JSON.stringify(level);
      const map = mapFromLevel(content, level);
      const beforeMap = JSON.stringify(map);
      const context = { content, map, levelId: level.id, viewport: candidateReviewViewport(map) };
      const forgeTiles = map.tiles.filter(id => candidateMaterial(content, id).surface === 'surface-forge-stone');
      expect(forgeTiles, level.id).toHaveLength(expected.get(level.id) ?? 0);
      for (const tile of forgeTiles) {
        const material = candidateMaterial(content, tile);
        expect(material.tone ?? 0).toBe(0);
        expect(material.connected).toBeUndefined();
        expect(material.blend).toBeUndefined();
      }
      const scene = candidate01MapSceneryLayers(context);
      const pieces = scene.ground.filter(piece => piece.markup.includes(CANDIDATE_01_FORGE_BASE.href));
      expect(pieces, level.id).toHaveLength(forgeTiles.length);
      expect(scene.ground.map(piece => piece.markup).join(''), level.id).not.toContain(atlas.raster.href);
      expect(candidate01MapSceneryLayers({ ...context, editing: true }).ground
        .filter(piece => piece.markup.includes(CANDIDATE_01_FORGE_BASE.href)), level.id).toEqual(pieces);
      expect(candidate01MapSceneryLayers({ ...context, viewport: { ...context.viewport, sceneWidth: context.viewport.sceneWidth + 123 } }).ground, level.id).toEqual(scene.ground);
      expect(JSON.stringify(level), level.id).toBe(before);
      expect(JSON.stringify(map), level.id).toBe(beforeMap);
    }
  });
});
