import { describe, expect, it } from 'vitest';
import { runtimeTileMarkup, type RuntimeCellAtlas } from '@empire/game-ui';
import { candidateWorldMaterialTileMarkup, type CandidateWorldMaterial } from './candidate-01-world-material';

const material: CandidateWorldMaterial = {
  id: 'fixture-stone', source: 'fixture.png', sourceSha256: 'fixture',
  logicalSize: [128, 128], rasterSize: [512, 512], rasterPixelsPerWorldUnit: [8, 8],
  worldOrigin: [0, 0], fit: { inset: 0, bleed: 0 },
  png: 'fixture.png', png2x: 'fixture@2x.png', href: 'fixture@2x.png',
};
const atlas: RuntimeCellAtlas = { href: 'white-alpha-only.png', cellWidth: 32, cellHeight: 32, columns: 16, rows: 4 };

describe('world material cell projection', () => {
  it('samples the complete world period at absolute phase, including negative and repeated coordinates', () => {
    for (const [x, y, u, v] of [[0, 0, 0, 0], [32, 96, 32, 96], [128, 256, 0, 0], [-32, -64, 96, 64]]) {
      const markup = candidateWorldMaterialTileMarkup(material, x!, y!);
      expect(markup).toContain(`patternUnits="userSpaceOnUse" x="${-u!}" y="${-v!}" width="128" height="128" viewBox="0 0 128 128"`);
      expect(markup).toContain('viewBox="-1 -1 34 34"');
      expect(markup).toContain('href="fixture@2x.png" width="128" height="128"');
      expect(markup).not.toContain('<mask');
    }
    expect(candidateWorldMaterialTileMarkup(material, 0, 0)).toEqual(candidateWorldMaterialTileMarkup(material, 128, 256));
    expect(candidateWorldMaterialTileMarkup(material, 0, 0)).not.toEqual(candidateWorldMaterialTileMarkup(material, 32, 0));
  });

  it('carries the existing connector projection unmodified inside each self-contained alpha mask', () => {
    for (let cell = 0; cell < 64; cell++) {
      const markup = candidateWorldMaterialTileMarkup(material, 32, 64, { atlas, cell, id: 'stone-mask' });
      expect(markup).toContain(runtimeTileMarkup(atlas, cell, { bleed: 0.5 }));
      expect(markup).toContain('<mask');
      expect(markup).toContain('<pattern');
      expect(markup).toContain('white-alpha-only.png');
      expect(markup).not.toContain('route-stone-road');
      const ids = [...markup.matchAll(/id="([^"]+)"/g)].map(match => match[1]);
      for (const use of markup.matchAll(/url\(#([^)]*)\)/g)) expect(ids).toContain(use[1]);
    }
  });

  it('rejects invalid tile origins instead of emitting malformed UVs', () => {
    for (const n of [NaN, Infinity, -Infinity]) {
      expect(() => candidateWorldMaterialTileMarkup(material, n, 0)).toThrow('finite');
      expect(() => candidateWorldMaterialTileMarkup(material, 0, n)).toThrow('finite');
    }
  });
});
