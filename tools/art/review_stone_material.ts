/** Actual runtime projection fixtures: unchanged connector alpha and continuous world UV. */
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { runtimeTileMarkup, type RuntimeCellAtlas } from '@empire/game-ui';
import { candidateWorldMaterialTileMarkup, type CandidateWorldMaterial } from '../../packages/story-candidate-01/src/presentation/candidate-01-world-material';

const output = resolve(process.env.STONE_REVIEW_OUTPUT ?? '/tmp/empire-stone-material');
mkdirSync(output, { recursive: true });
const root = resolve(import.meta.dirname, '../..');
const white = 'data:image/svg+xml;base64,' + Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128"><rect width="128" height="128" fill="white"/></svg>').toString('base64');
const material: CandidateWorldMaterial = { id: 'technical-white', source: '', sourceSha256: '',
  logicalSize: [128, 128], rasterSize: [512, 512], rasterPixelsPerWorldUnit: [8, 8],
  worldOrigin: [0, 0], fit: { inset: 0, bleed: 0 }, png: '', png2x: '', href: white };
const svg = (w: number, h: number, body: string) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
const baseline: string[] = [], actual: string[] = [];
for (const [family, filename] of ['stone-road-masks@2x.png', 'stone-road-edge-masks@2x.png'].entries()) {
  const atlas: RuntimeCellAtlas = { href: pathToFileURL(resolve(root,
    'packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1/runtime', filename)).href,
  cellWidth: 32, cellHeight: 32, columns: 16, rows: 4 };
  for (let cell = 0; cell < 64; cell++) {
    const x = (cell % 16) * 40 + 4, y = (family * 4 + Math.floor(cell / 16)) * 40 + 4;
    baseline.push(`<g transform="translate(${x} ${y})">${runtimeTileMarkup(atlas, cell, { bleed: 0.5 })}</g>`);
    actual.push(`<g transform="translate(${x} ${y})">${candidateWorldMaterialTileMarkup(material, x, y, { atlas, cell, id: filename })}</g>`);
  }
}
writeFileSync(resolve(output, 'legacy-alpha.svg'), svg(640, 320, baseline.join('')));
writeFileSync(resolve(output, 'world-alpha.svg'), svg(640, 320, actual.join('')));
if (process.env.STONE_UV_FIXTURE) {
  const texture = pathToFileURL(resolve(process.env.STONE_UV_FIXTURE)).href;
  const tiles = [];
  for (let y = 0; y < 256; y += 32) for (let x = 0; x < 256; x += 32) {
    tiles.push(`<g transform="translate(${x} ${y})">${candidateWorldMaterialTileMarkup({ ...material, id: 'uv-fixture', href: texture }, x, y)}</g>`);
  }
  writeFileSync(resolve(output, 'world-uv.svg'), svg(256, 256, tiles.join('')));
  writeFileSync(resolve(output, 'reference-uv.svg'), svg(256, 256,
    `<defs><pattern id="plane" patternUnits="userSpaceOnUse" width="128" height="128"><image href="${texture}" width="128" height="128"/></pattern></defs><rect width="256" height="256" fill="url(#plane)"/>`));
}
console.log(output);
