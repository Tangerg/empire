import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-field-material-v2.json';
import { escapeAttr, type BoardPiece, type RuntimeTileFit } from '@empire/game-ui';
import { fixedNumbers, type FixedNumbers } from './candidate-01-documents';

/** The approved broad field, including the legacy material it replaces. */
export interface CandidateFieldMaterial {
  readonly id: string;
  readonly replaces: { readonly atlas: string; readonly tone: number };
  readonly source: string;
  readonly sourceSha256: string;
  readonly logicalSize: FixedNumbers<2>;
  /** Physical pixels of the lower-density export, independent of world sampling. */
  readonly rasterSize: FixedNumbers<2>;
  /** Actual HD-file pixel density in world units; enlarging UVs creates no detail. */
  readonly rasterPixelsPerWorldUnit: FixedNumbers<2>;
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
  const rasterSize = fixedNumbers(2, document.rasterSize, 'rasterSize', document.id);
  const worldOrigin = fixedNumbers(2, document.worldOrigin, 'worldOrigin', document.id);
  if (document.schemaVersion !== 2 || [...logicalSize, ...rasterSize].some((value) => !Number.isInteger(value) || value <= 0)
    || worldOrigin.some((value) => value !== 0) || document.fit.inset !== 0 || document.fit.bleed !== 0) {
    throw new Error('candidate-01: field material requires positive integer size, world origin zero and an uncropped fit');
  }
  if (rasterSize[0] * logicalSize[1] !== rasterSize[1] * logicalSize[0]) {
    throw new Error('candidate-01: field raster and world aspect must agree');
  }
  const href = urls[`${ROOT}${document.png2x}`];
  if (!href || !urls[`${ROOT}${document.png}`]) throw new Error(`candidate-01: missing field material raster ${document.id}`);
  const rasterPixelsPerWorldUnit = fixedNumbers(2, rasterSize.map((pixels, axis) => pixels * 2 / logicalSize[axis]), 'rasterPixelsPerWorldUnit', document.id);
  return { ...document, logicalSize, rasterSize, rasterPixelsPerWorldUnit, worldOrigin, href };
}

/** The JSON owns source, UV scale, fit and binding; both export and runtime read it. */
export const CANDIDATE_01_FIELD_MATERIAL = readMaterial();

/**
 * World-aligned blocks share one picture per edge size, never one per game tile.
 * Each picture is self-contained because the GPU backend bakes it in isolation.
 * Keeping a block at one material period bounds its size independently of the
 * battlefield. The source stays 1024² pixels; the texture backend may enlarge
 * it at higher display resolution, which does not create native source detail.
 * Cropped edge blocks retain the full source dimensions, so they cannot stretch the UVs.
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
