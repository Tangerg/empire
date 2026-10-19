import type { RuntimeTileFit } from '@empire/game-ui';
import { fixedNumbers, type FixedNumbers } from './candidate-01-documents';

/** Shared source identity, sampling scale and raster density for world materials. */
export interface CandidateWorldMaterial {
  readonly id: string;
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
export const candidateWorldMaterialUrls = import.meta.glob<string>(
  '../../assets/final-fantasy-v1/world-materials-v1/runtime/*.png',
  { eager: true, import: 'default', query: '?url' },
);

interface WorldMaterialDocument {
  readonly schemaVersion: number;
  readonly id: string;
  readonly source: string;
  readonly sourceSha256: string;
  readonly logicalSize: readonly number[];
  readonly rasterSize: readonly number[];
  readonly worldOrigin: readonly number[];
  readonly fit: RuntimeTileFit;
  readonly png: string;
  readonly png2x: string;
}

export function readCandidateWorldMaterial<T extends WorldMaterialDocument>(document: T): T & CandidateWorldMaterial {
  const logicalSize = fixedNumbers(2, document.logicalSize, 'logicalSize', document.id);
  const rasterSize = fixedNumbers(2, document.rasterSize, 'rasterSize', document.id);
  const worldOrigin = fixedNumbers(2, document.worldOrigin, 'worldOrigin', document.id);
  if (document.schemaVersion !== 2 || [...logicalSize, ...rasterSize].some((value) => !Number.isInteger(value) || value <= 0)
    || worldOrigin.some((value) => value !== 0) || document.fit.inset !== 0 || document.fit.bleed !== 0) {
    throw new Error('candidate-01: world material requires positive integer size, world origin zero and an uncropped fit');
  }
  if (rasterSize[0] * logicalSize[1] !== rasterSize[1] * logicalSize[0]) {
    throw new Error('candidate-01: world raster and world aspect must agree');
  }
  const href = candidateWorldMaterialUrls[`${ROOT}${document.png2x}`];
  if (!href || !candidateWorldMaterialUrls[`${ROOT}${document.png}`]) throw new Error(`candidate-01: missing world material raster ${document.id}`);
  const rasterPixelsPerWorldUnit = fixedNumbers(2, rasterSize.map((pixels, axis) => pixels * 2 / logicalSize[axis]), 'rasterPixelsPerWorldUnit', document.id);
  return { ...document, logicalSize, rasterSize, rasterPixelsPerWorldUnit, worldOrigin, href };
}
