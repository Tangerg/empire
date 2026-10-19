import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-field-material-v1.json';
import { escapeAttr, type BoardPiece, type RuntimeTileFit } from '@empire/game-ui';
import { fixedNumbers, type FixedNumbers } from './candidate-01-documents';

/** The approved broad field, including the legacy material it replaces. */
export interface CandidateFieldMaterial {
  readonly id: string;
  readonly replaces: { readonly atlas: string; readonly tone: number };
  readonly source: string;
  readonly sourceSha256: string;
  readonly logicalSize: FixedNumbers<2>;
  readonly worldOrigin: FixedNumbers<2>;
  readonly fit: RuntimeTileFit;
  readonly png: string;
  readonly png2x: string;
  readonly href: string;
}

const ROOT = '../../assets/final-fantasy-v1/world-materials-v1/';
const urls = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/world-materials-v1/runtime/*.png',
  { eager: true, import: 'default', query: '?url' },
);

function readMaterial(): CandidateFieldMaterial {
  const logicalSize = fixedNumbers(2, document.logicalSize, 'logicalSize', document.id);
  const worldOrigin = fixedNumbers(2, document.worldOrigin, 'worldOrigin', document.id);
  if (document.schemaVersion !== 1 || logicalSize.some((value) => !Number.isInteger(value) || value <= 0)
    || worldOrigin.some((value) => value !== 0) || document.fit.inset !== 0 || document.fit.bleed !== 0) {
    throw new Error('candidate-01: field material requires positive integer size, world origin zero and an uncropped fit');
  }
  const href = urls[`${ROOT}${document.png2x}`];
  if (!href || !urls[`${ROOT}${document.png}`]) throw new Error(`candidate-01: missing field material raster ${document.id}`);
  return { ...document, logicalSize, worldOrigin, href };
}

/** The JSON owns source, UV scale, fit and binding; both export and runtime read it. */
export const CANDIDATE_01_FIELD_MATERIAL = readMaterial();

/**
 * World-aligned blocks share one picture per edge size, never one per game tile.
 * Each picture is self-contained because the GPU backend bakes it in isolation.
 * Keeping a block at one material period bounds each HD texture to 1024² pixels;
 * a map-sized picture would instead grow with the battlefield. Cropped edge
 * blocks retain the full source dimensions, so they cannot stretch the UVs.
 */
export function candidate01FieldMaterialPieces(width: number, height: number): BoardPiece[] {
  if (![width, height].every((value) => Number.isFinite(value) && value > 0)) {
    throw new Error('candidate-01: field extent must be finite and positive');
  }
  const material = CANDIDATE_01_FIELD_MATERIAL;
  const [periodX, periodY] = material.logicalSize;
  const [originX, originY] = material.worldOrigin;
  const pieces: BoardPiece[] = [];
  for (let y = originY; y < height; y += periodY) {
    for (let x = originX; x < width; x += periodX) {
      const w = Math.min(periodX, width - x);
      const h = Math.min(periodY, height - y);
      pieces.push({ x, y, markup: `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"`
        + ' overflow="hidden" shape-rendering="crispEdges" data-runtime-raster="world-material">'
        + `<image href="${escapeAttr(material.href)}" width="${periodX}" height="${periodY}" preserveAspectRatio="none"/>`
        + '</svg>' });
    }
  }
  return pieces;
}
