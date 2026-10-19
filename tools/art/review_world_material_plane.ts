/** Write actual tile-projected and continuous-plane fixtures for the same material. */
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { candidateWorldMaterialTileMarkup, type CandidateWorldMaterial } from '../../packages/story-candidate-01/src/presentation/candidate-01-world-material';

export function writeWorldMaterialPlaneReview(source: CandidateWorldMaterial, directory: string): void {
  const output = resolve(directory);
  mkdirSync(output, { recursive: true });
  const material = { ...source, href: pathToFileURL(resolve(import.meta.dirname,
    '../../packages/story-candidate-01/assets/final-fantasy-v1/world-materials-v1', source.png2x)).href };
  const [period] = material.logicalSize;
  if (period !== material.logicalSize[1]) throw new Error('plane review requires a square material');
  const size = period * 2;
  const svg = (body: string) => `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">${body}</svg>`;
  const pieces = [];
  for (let y = 0; y < size; y += 32) for (let x = 0; x < size; x += 32) {
    pieces.push(`<g transform="translate(${x} ${y})">${candidateWorldMaterialTileMarkup(material, x, y)}</g>`);
  }
  writeFileSync(resolve(output, 'world-tiles.svg'), svg(pieces.join('')));
  writeFileSync(resolve(output, 'reference-plane.svg'), svg(`<defs><pattern id="plane" patternUnits="userSpaceOnUse" width="${period}" height="${period}"><image href="${material.href}" width="${period}" height="${period}"/></pattern></defs><rect width="${size}" height="${size}" fill="url(#plane)"/>`));
}
