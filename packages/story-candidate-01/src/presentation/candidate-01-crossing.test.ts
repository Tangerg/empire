import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { candidate01Level } from '../levels';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { CANDIDATE_01_HORIZONTAL_CROSSING as crossing, CANDIDATE_01_VERTICAL_CROSSING as vertical, candidate01HorizontalCrossingMarkup } from './candidate-01-crossing';
import { candidate01Asset } from './candidate-01-assets';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const pack = resolve(import.meta.dirname, '../../assets/final-fantasy-v1');

describe('registered horizontal crossing', () => {
  it('keeps the complete approved canvas, source contact and reviewed scale', () => {
    expect(createHash('sha256').update(readFileSync(resolve(pack, crossing.source))).digest('hex')).toBe(crossing.sourceSha256);
    expect(crossing.sourceSize).toEqual([1729, 910]);
    expect(crossing.sourceFoot).toEqual([864, 418]);
    expect(crossing.logicalPerSourcePixel).toBe(0.08098336948662328);
    expect(crossing.logicalSize[0]).toBeCloseTo(140.02024584237165, 8);
    expect(crossing.logicalSize[1]).toBeCloseTo(73.69486623282718, 8);
    for (const [key, size] of [['png', [140, 74]], ['png2x', [280, 147]]] as const) {
      const png = readFileSync(resolve(pack, 'registered-crossings-v1', crossing[key]));
      expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual(size);
      expect(createHash('sha256').update(png).digest('hex')).toBe(crossing.approvedExports[key]);
    }
    const markup = candidate01HorizontalCrossingMarkup();
    expect(markup).toContain(`translate(${-crossing.anchor[0]} ${-crossing.anchor[1]})`);
    expect(markup).toContain(`width="${crossing.logicalSize[0]}" height="${crossing.logicalSize[1]}"`);
    expect(markup).not.toContain('rotate');
    for (let axis = 0; axis < 2; axis++) {
      expect(crossing.sourceFoot[axis] * crossing.logicalSize[axis] / crossing.sourceSize[axis] - crossing.anchor[axis]).toBeCloseTo(0, 10);
    }
  });

  it('preserves immutable legacy atlases and excludes bridge cells from registered fortress overlays', () => {
    const catalog = JSON.parse(readFileSync(resolve(pack, '../../../../tools/art/environment-overrides.json'), 'utf8'));
    expect(Object.keys(catalog.atlases['crossings-fortifications'].cells).sort()).toEqual(['stone-gatehouse', 'stone-wall-corner', 'stone-wall-straight']);
    // Exact unchanged-cell pixel checks are also enforced by export_environment_overrides --check.
    for (const [name, hash] of [
      ['crossings-fortifications.png', '43e02953833b91f400684b8ce30db4a855283148ceff2235311591799de1e3ab'],
      ['crossings-fortifications@2x.png', '3d7bdd4b141721c668256b9d378b6ff833cd979e2f317c3078b472ee8cf27fd1'],
    ]) {
      const bytes = readFileSync(resolve(pack, 'masters/approved-v5/environment/baselines', name));
      expect(createHash('sha256').update(bytes).digest('hex')).toBe(hash);
    }
  });

  it('draws each c01-02 bridge run once at the original midpoint without changing the map', () => {
    const map = mapFromLevel(content, candidate01Level('c01-02'));
    const before = JSON.stringify(map);
    const layers = candidate01MapSceneryLayers({ content, map, levelId: 'c01-02', viewport: candidateReviewViewport(map) });
    const decks = layers.underUnits.filter((piece) => piece.markup.includes(crossing.href));
    expect(decks.map(({ x, y }) => [x, y])).toEqual([[352, 112], [352, 208], [352, 304]]);
    expect(new Set(decks.map(({ markup }) => markup)).size).toBe(1);
    expect(JSON.stringify(map)).toBe(before);
    const interactive = candidate01Asset('C01-STRUCT-THREE-BRIDGE-SPAN');
    expect(interactive.frames).toBe(3);
    expect(interactive.url).not.toBe(crossing.href);
  });

  it('preserves run detection and fixed authored scale for different horizontal and vertical spans', () => {
    const map = mapFromLevel(content, candidate01Level('c01-02'));
    for (const horizontal of [true, false]) for (const length of [1, 2, 3, 4]) {
      map.tiles.fill('plain');
      const x = 8, y = 6;
      for (let i = 0; i < length; i++) map.tiles[(y + (horizontal ? 0 : i)) * map.width + x + (horizontal ? i : 0)] = 'bridge';
      map.tiles[(y - (horizontal ? 0 : 1)) * map.width + x - (horizontal ? 1 : 0)] = 'road';
      map.tiles[(y + (horizontal ? 0 : length)) * map.width + x + (horizontal ? length : 0)] = 'road';
      const before = JSON.stringify(map);
      const layers = candidate01MapSceneryLayers({ content, map, levelId: 'probe', viewport: candidateReviewViewport(map) });
      const decks = layers.underUnits.filter((piece) => piece.markup.includes('crossings-fortifications') || piece.markup.includes(crossing.href));
      expect(decks).toHaveLength(1);
      expect([decks[0].x, decks[0].y]).toEqual(horizontal ? [(x + length / 2) * 32, (y + .5) * 32] : [(x + .5) * 32, (y + length / 2) * 32]);
      if (horizontal) expect(decks[0].markup).toBe(candidate01HorizontalCrossingMarkup());
      else {
        expect(decks[0].markup).toContain('crossings-fortifications@2x.png');
        expect(decks[0].markup).not.toContain(crossing.href);
      }
      expect(JSON.stringify(map)).toBe(before);
    }
  });
});

describe('registered southern stone source', () => {
  it('retains its full canvas, independent densities and deck pivot without a standing foot', () => {
    expect(createHash('sha256').update(readFileSync(resolve(pack, vertical.source))).digest('hex')).toBe(vertical.sourceSha256);
    expect(vertical.sourceSha256).toBe('49d5a96e9a051aecb6196d8cb5f9312beb60e31cf6eafcb0f2bc483f543c0e03');
    expect(vertical.sourceSize).toEqual([1254, 1254]);
    expect(vertical.sourceFoot).toEqual([627, 594]);
    expect(vertical.logicalPerSourcePixel).toBe(96 / 784);
    expect(vertical.logicalSize).toEqual([1254 * 96 / 784, 1254 * 96 / 784]);
    for (const [key, size] of [['png', [154, 154]], ['png2x', [307, 307]]] as const) {
      const png = readFileSync(resolve(pack, 'registered-crossings-v1', vertical[key]));
      expect([png.readUInt32BE(16), png.readUInt32BE(20)]).toEqual(size);
      expect(createHash('sha256').update(png).digest('hex')).toBe(vertical.approvedExports[key]);
    }
    for (let axis = 0; axis < 2; axis++) {
      expect(vertical.sourceFoot[axis] * vertical.logicalSize[axis] / vertical.sourceSize[axis] - vertical.anchor[axis]).toBeCloseTo(0, 10);
    }
    expect((202 - vertical.sourceFoot[1]) * vertical.logicalPerSourcePixel).toBe(-48);
    expect((986 - vertical.sourceFoot[1]) * vertical.logicalPerSourcePixel).toBe(48);
  });
});
