import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '../index';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidateMaterial } from './candidate-01-terrain-materials';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { CANDIDATE_01_STONE_BASE, CANDIDATE_01_STONE_PAVING, candidate01StoneConnectorMarkup } from './candidate-01-stone-material';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);

describe('approved stone world materials', () => {
  it('binds distinct approved source identities at 128 world units, retaining native density and whole-canvas fitting', () => {
    const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');
    for (const material of [CANDIDATE_01_STONE_BASE, CANDIDATE_01_STONE_PAVING]) {
      expect(createHash('sha256').update(readFileSync(resolve(pack, material.source))).digest('hex')).toBe(material.sourceSha256);
      expect(material.logicalSize).toEqual([128, 128]);
      expect(material.rasterSize).toEqual([512, 512]);
      expect(material.rasterPixelsPerWorldUnit).toEqual([8, 8]);
      expect(material.worldOrigin).toEqual([0, 0]);
      expect(material.fit).toEqual({ inset: 0, bleed: 0 });
      for (const [density, path] of [[1, material.png], [2, material.png2x]] as const) {
        const png = readFileSync(resolve(pack, 'world-materials-v1', path));
        expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual([512 * density, 512 * density]);
      }
    }
    expect(CANDIDATE_01_STONE_BASE.replaces).toEqual({ atlas: 'surface-old-stone', tone: 0 });
    expect(CANDIDATE_01_STONE_PAVING.replaces).toEqual({ atlas: 'surface-old-stone', tone: 3 });
    expect(CANDIDATE_01_STONE_PAVING.source).toContain('/old-stone-4/revision-2/');
    expect(CANDIDATE_01_ENVIRONMENT.atlas('surface-old-stone').componentCount).toBe(4);
  });

  it('retains all mask/variant indices and substitutes only stone route RGB', () => {
    for (const atlas of ['route-stone-road', 'route-edge-stone-road']) {
      for (let mask = 0; mask < 16; mask++) for (let variant = 0; variant < 4; variant++) {
        const cell = CANDIDATE_01_ENVIRONMENT.connectedIndex(atlas, mask, variant);
        expect(cell).toBe(mask * 4 + variant);
        const markup = candidate01StoneConnectorMarkup(atlas, cell, 64, 96)!;
        expect(markup).toContain(CANDIDATE_01_STONE_PAVING.href);
        expect(markup).toContain(atlas === 'route-stone-road' ? 'stone-road-masks@2x.png' : 'stone-road-edge-masks@2x.png');
        expect(markup).not.toContain(CANDIDATE_01_ENVIRONMENT.atlas(atlas).raster.href);
        expect(markup).toContain(`-${cell}-mask`);
      }
    }
    expect(candidate01StoneConnectorMarkup('route-dirt-road', 0, 0, 0)).toBeNull();
    expect(candidate01StoneConnectorMarkup('water-slow-river', 0, 0, 0)).toBeNull();
  });

  it('covers the actual 285 stone bases and 250 street connectors in c01-07 without changing terrain or actor data', () => {
    for (const level of CANDIDATE_01_LEVELS) {
      const before = JSON.stringify(level);
      const map = mapFromLevel(content, level);
      const beforeMap = JSON.stringify(map);
      const context = { content, map, levelId: level.id, viewport: candidateReviewViewport(map) };
      const ground = candidate01MapSceneryLayers(context).ground;
      const expectedBase = map.tiles.filter(id => {
        const material = candidateMaterial(content, id);
        return material.surface === 'surface-old-stone' && (material.tone ?? 0) === 0;
      }).length;
      const expectedRoutes = map.tiles.filter(id => candidateMaterial(content, id).connected?.atlas === 'route-stone-road').length;
      const bases = ground.filter(piece => piece.markup.includes(CANDIDATE_01_STONE_BASE.href));
      const routes = ground.filter(piece => piece.markup.includes('stone-road-masks@2x.png'));
      const edges = ground.filter(piece => piece.markup.includes('stone-road-edge-masks@2x.png'));
      expect(bases, level.id).toHaveLength(expectedBase);
      expect(routes, level.id).toHaveLength(expectedRoutes);
      expect(edges, level.id).toHaveLength(expectedRoutes);
      const joined = ground.map(piece => piece.markup).join('');
      for (const atlas of ['route-stone-road', 'route-edge-stone-road']) expect(joined, level.id).not.toContain(CANDIDATE_01_ENVIRONMENT.atlas(atlas).raster.href);
      if (level.id === 'c01-07') {
        expect(expectedBase).toBe(285);
        expect(expectedRoutes).toBe(250);
      }
      const shifted = candidate01MapSceneryLayers({ ...context, viewport: { ...context.viewport, sceneWidth: context.viewport.sceneWidth + 87 } });
      expect(shifted.ground, level.id).toEqual(ground);
      expect(JSON.stringify(map), level.id).toBe(beforeMap);
      expect(JSON.stringify(level), level.id).toBe(before);
    }
  });
});
