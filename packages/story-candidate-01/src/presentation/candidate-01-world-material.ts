import type { CandidateMaterial } from './candidate-01-terrain-materials';
import { escapeAttr, runtimeTileMarkup, TILE, type RuntimeCellAtlas, type RuntimeTileFit } from '@empire/game-ui';
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

/** Resolve an occupancy sheet once; material RGB and mask alpha have separate sources. */
export function readCandidateOccupancyAtlas(occupancy: {
  readonly png2x: string; readonly tileSize: number; readonly columns: number; readonly rows: number;
}): RuntimeCellAtlas {
  const href = candidateWorldMaterialUrls[`${ROOT}${occupancy.png2x}`];
  if (!href) throw new Error(`candidate-01: missing world occupancy atlas ${occupancy.png2x}`);
  return { href, cellWidth: occupancy.tileSize, cellHeight: occupancy.tileSize,
    columns: occupancy.columns, rows: occupancy.rows };
}

/** One binding decides which existing connected terrain material an area replaces. */
export function candidateConnectedMaterialMatcher(binding: { readonly atlas: string; readonly mask: string }): (value: CandidateMaterial) => boolean {
  return value => value.connected?.atlas === binding.atlas && value.connected.mask === binding.mask;
}

/** World UV stays continuous; only the optional legacy connector alpha is cell-local. */
export function candidateWorldMaterialTileMarkup(
  material: CandidateWorldMaterial,
  worldX: number,
  worldY: number,
  connector?: { readonly atlas: RuntimeCellAtlas; readonly cell: number; readonly id: string },
): string {
  if (![worldX, worldY].every(Number.isFinite)) throw new Error('candidate-01: world tile origin must be finite');
  const [width, height] = material.logicalSize;
  const u = ((worldX - material.worldOrigin[0]) % width + width) % width;
  const v = ((worldY - material.worldOrigin[1]) % height + height) % height;
  const id = `c01-world-${material.id}-${u}_${v}-${connector ? `${connector.id}-${connector.cell}` : 'surface'}`;
  const mask = connector ? `<mask id="${escapeAttr(id)}-mask" maskUnits="userSpaceOnUse" x="-1" y="-1" width="${TILE + 2}" height="${TILE + 2}">`
    + runtimeTileMarkup(connector.atlas, connector.cell, { bleed: 0.5 }) + '</mask>' : '';
  const paintOrigin = connector ? -1 : -0.5;
  const paintSize = connector ? TILE + 2 : TILE + 1;
  // The pattern supplies both sides of a period boundary, including the existing
  // half-pixel tile bleed. Stretching one clipped material sample would move UVs.
  // Integer outer bounds avoid antialiasing the legacy half-pixel edge twice.
  return `<svg x="-1" y="-1" width="${TILE + 2}" height="${TILE + 2}" viewBox="-1 -1 ${TILE + 2} ${TILE + 2}" overflow="hidden" data-runtime-raster="world-material-tile">`
    + `<defs><pattern id="${escapeAttr(id)}" patternUnits="userSpaceOnUse" x="${-u}" y="${-v}" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" preserveAspectRatio="none">`
    + `<image href="${escapeAttr(material.href)}" width="${width}" height="${height}" preserveAspectRatio="none"/></pattern>${mask}</defs>`
    + `<rect x="${paintOrigin}" y="${paintOrigin}" width="${paintSize}" height="${paintSize}" fill="url(#${escapeAttr(id)})"${connector ? ` mask="url(#${escapeAttr(id)}-mask)"` : ''}/></svg>`;
}
