/** Actual Blob47 alpha projection, alongside a full-period world UV fixture. */
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { runtimeTileMarkup } from '@empire/game-ui';
import { CANDIDATE_01_ENVIRONMENT } from '../../packages/story-candidate-01/src/presentation/candidate-01-environment';
import { CANDIDATE_01_FOREST_FLOOR, candidate01ForestBlendMarkup } from '../../packages/story-candidate-01/src/presentation/candidate-01-forest-material';
import { writeWorldMaterialPlaneReview } from './review_world_material_plane';

const output = resolve(process.env.FOREST_REVIEW_OUTPUT ?? '/tmp/empire-forest-material');
const root = resolve(import.meta.dirname, '../..');
mkdirSync(output, { recursive: true });
writeWorldMaterialPlaneReview(CANDIDATE_01_FOREST_FLOOR, output);
const atlas = CANDIDATE_01_ENVIRONMENT.atlas('transition-meadow-forest');
const panels: Record<string, string[]> = { 'legacy-alpha': [], 'world-alpha': [] };
for (let cell = 0; cell < 47; cell++) {
  const x = (cell % 16) * 40 + 4, y = Math.floor(cell / 16) * 40 + 4;
  const draws = {
    'legacy-alpha': runtimeTileMarkup(atlas.raster, cell, { bleed: 0.5 }),
    'world-alpha': candidate01ForestBlendMarkup(atlas.id, cell, x, y),
  };
  for (const [name, markup] of Object.entries(draws)) {
    if (markup === null) throw new Error(`forest projection missing cell ${cell}`);
    panels[name].push(`<g transform="translate(${x} ${y})">${markup}</g>`);
  }
}
for (const [name, panel] of Object.entries(panels)) {
  const body = panel.join('').replace(/href="([^"]+)"/g, (_, href: string) =>
    `href="${pathToFileURL(resolve(root, href.split('?')[0].replace(/^\//, ''))).href}"`);
  writeFileSync(resolve(output, `${name}.svg`), `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="120" viewBox="0 0 640 120">${body}</svg>`);
}
