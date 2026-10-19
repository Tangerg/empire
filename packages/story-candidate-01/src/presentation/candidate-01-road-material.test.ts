import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { candidate01Level } from '../levels';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);

describe('approved dirt-road connectors', () => {
  it('resolves the approved source through the existing typed atlas and preserves its slot contract', () => {
    const root = resolve(import.meta.dirname, '../../../..');
    const binding = JSON.parse(readFileSync(resolve(root, 'tools/art/connector-overrides.json'), 'utf8'));
    expect(createHash('sha256').update(readFileSync(resolve(root, binding.source))).digest('hex')).toBe(binding.sha256);
    expect(binding.roadWidthLogical).toBe(22);
    for (const id of ['route-dirt-road', 'route-edge-dirt-road']) {
      const atlas = CANDIDATE_01_ENVIRONMENT.atlas(id);
      expect(atlas.raster).toMatchObject({ cellWidth: 32, cellHeight: 32, columns: 16, rows: 4 });
      expect(atlas.raster.href).toContain(`${id}@2x.png`);
      const png = readFileSync(resolve(root, 'packages/story-candidate-01/assets/final-fantasy-v1/environment-builder-v1/runtime/atlas', `${id}@2x.png`));
      expect(createHash('sha256').update(png).digest('hex')).toBe(binding.approved2xOutputs[`${id}@2x.png`]);
      for (let mask = 0; mask < 16; mask++) for (let variant = 0; variant < 4; variant++) {
        expect(CANDIDATE_01_ENVIRONMENT.connectedIndex(id, mask, variant)).toBe(mask * 4 + variant);
      }
    }
  });

  it('keeps all sixteen route connections, including building thresholds and bridge access, unchanged', () => {
    const map = mapFromLevel(content, candidate01Level('c01-02'));
    const centre = [8, 8];
    for (let mask = 0; mask < 16; mask++) {
      map.tiles.fill('plain');
      map.tiles[8 * map.width + 8] = 'road';
      for (const [bit, dx, dy, terrain] of [[1, 0, -1, 'road'], [2, 1, 0, 'bridge'], [4, 0, 1, 'village'], [8, -1, 0, 'barracks']] as const) {
        if (mask & bit) map.tiles[(centre[1] + dy) * map.width + centre[0] + dx] = terrain;
      }
      const before = JSON.stringify(map);
      const layers = candidate01MapSceneryLayers({ content, map, levelId: 'probe', viewport: candidateReviewViewport(map) });
      const road = layers.ground.find((piece) => piece.x === 256 && piece.y === 256 && piece.markup.includes('/route-dirt-road@2x.png'));
      if (!road) throw new Error('missing actual centre road piece');
      const offset = /x="(-?\d+)" y="(-?\d+)" width="512"/.exec(road.markup);
      if (!offset) throw new Error('road lost its fixed atlas viewport');
      const index = -Number(offset[1]) / 32 + -Number(offset[2]) / 32 * 16;
      expect(Math.floor(index / 4), `mask ${mask}`).toBe(mask);
      expect(JSON.stringify(map)).toBe(before);
    }
  });
});
