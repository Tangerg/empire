import { describe, expect, it } from 'vitest';
import { idx, mapFromLevel } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { candidate01MapSceneryLayers } from './candidate-01-map-scene';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';
import { candidate01RiverMaterialPieces, candidate01RiverPath, type CandidateRiverProfile } from './candidate-01-river-material';

const profile: CandidateRiverProfile = { width: 48, bankWidth: 58, knots: [[1, 0], [2, 20], [1, 40]] };
describe('authored river material', () => {
  it('binds the authored curve only in play and aligns its tangent knots to all three bridge centers', () => {
    const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    const level = candidate01Level('c01-02'), map = mapFromLevel(content, level);
    const river = candidate01EnvironmentScene(level.id)!.river!;
    const before = JSON.stringify({ map, level, river });
    const context = { content, levelId: level.id, map, viewport: candidateReviewViewport(map) };
    const play = candidate01MapSceneryLayers(context), edit = candidate01MapSceneryLayers({ ...context, editing: true });
    const water = candidate01RiverMaterialPieces(map.width, map.height,
      (x, y) => ['water', 'bridge'].includes(map.tiles[idx(map, x, y)]), river);
    expect(play.ground.filter(p => p.markup.includes('data-runtime-raster="area-water"')))
      .toEqual(water.filter(p => p.markup.includes('data-runtime-raster="area-water"')));
    expect(play.ground.filter(p => p.markup.includes('data-runtime-raster="river-bank"'))).toHaveLength(1);
    expect(edit.ground.some(p => p.markup.includes('data-runtime-raster="river-bank"'))).toBe(false);
    for (let y = 0; y < level.height; y++) {
      const columns = [...level.terrain[y]].flatMap((symbol, x) => symbol === '=' ? [x] : []);
      if (columns.length) expect(river.knots).toContainEqual([columns.reduce((a, b) => a + b + .5, 0) / columns.length, y + .5]);
    }
    expect(play.underUnits).toEqual(edit.underUnits);
    expect(play.overUnits).toEqual(edit.overUnits);
    expect(JSON.stringify({ map, level, river })).toBe(before);
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
      expect(markup).toContain('<rect x="0" y="32" width="32" height="32"/>');
      expect(markup).not.toContain('<rect x="0" y="0"');
      expect(markup).toContain('<g clip-path=');
    }
    expect(candidate01RiverMaterialPieces(3, 3, () => false, profile)).toEqual([]);
  });
  it('allows only the bounded bank rim onto explicitly permitted dry ground', () => {
    const pieces = candidate01RiverMaterialPieces(3, 3, (x, y) => x === 1 && y === 1, profile,
      (x, y) => y === 1 && x <= 1);
    const bank = pieces.find(p => p.markup.includes('data-runtime-raster="river-bank"'))!.markup;
    const water = pieces.find(p => p.markup.includes('data-runtime-raster="area-water"'))!.markup;
    expect(bank).toContain('<rect x="0" y="32" width="32" height="32"/>');
    expect(bank).not.toContain('<rect x="64" y="32" width="32" height="32"/>');
    expect(water).not.toContain('<rect x="0" y="32" width="32" height="32"/>');
    expect(bank).toContain('<rect x="27" y="27" width="42" height="42" rx="5"/>');
    expect(bank).toContain('-rim)"><path');
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
