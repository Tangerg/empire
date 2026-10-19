import { candidate01BlobMask } from './candidate-01-square-mask';
import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-water-material-v2.json';
import { escapeAttr, runtimeAtlasCellMarkup, TILE, type BoardPiece, type RuntimeCellAtlas } from '@empire/game-ui';
import { candidateWorldMaterialUrls, readCandidateWorldMaterial } from './candidate-01-world-material';
import type { CandidateMaterial } from './candidate-01-terrain-materials';

export const CANDIDATE_01_WATER_MATERIAL = readCandidateWorldMaterial(document);
const material = CANDIDATE_01_WATER_MATERIAL;
const occupancy = material.occupancy;
if (occupancy.tileSize !== TILE || occupancy.columns !== 16 || occupancy.rows !== 16
  || occupancy.geometry !== 'full-shared-interiors-exposed-shore-inset-concave-notch-v1'
  || occupancy.bitOrder.join(',') !== 'N,NE,E,SE,S,SW,W,NW'
  || material.logicalSize.some((size) => size % TILE !== 0)) {
  throw new Error('candidate-01: water requires a complete eight-neighbour square-cell mask atlas');
}
const maskHref = candidateWorldMaterialUrls[`../../assets/final-fantasy-v1/world-materials-v1/${occupancy.png2x}`];
if (!maskHref) throw new Error('candidate-01: missing water occupancy masks');
const masks: RuntimeCellAtlas = {
  href: maskHref, cellWidth: TILE, cellHeight: TILE, columns: occupancy.columns, rows: occupancy.rows,
};

/** Water and bridge retain the same material identity; only their rendering changes. */
export const candidate01IsAreaWater = (value: CandidateMaterial): boolean =>
  value.connected?.atlas === material.replaces.atlas && value.connected.mask === material.replaces.mask;

/**
 * One self-contained masked texture per world period, rather than one repeated
 * material per tactical cell. Occupancy is read at absolute map coordinates,
 * including neighbours outside each block. Definitions travel with each picture
 * because the GPU texture baker does not see definitions in another BoardPiece.
 */
export function candidate01WaterMaterialPieces(
  width: number, height: number, occupied: (x: number, y: number) => boolean,
): BoardPiece[] {
  if (![width, height].every((value) => Number.isInteger(value) && value > 0)) {
    throw new Error('candidate-01: water map dimensions must be positive integers');
  }
  const [periodX, periodY] = material.logicalSize;
  const cellsX = periodX / TILE;
  const cellsY = periodY / TILE;
  const inside = (x: number, y: number): boolean => x >= 0 && y >= 0 && x < width && y < height && occupied(x, y);
  const pieces: BoardPiece[] = [];
  for (let top = 0; top < height; top += cellsY) for (let left = 0; left < width; left += cellsX) {
    const cells: { x: number; y: number; mask: number }[] = [];
    for (let y = top; y < Math.min(top + cellsY, height); y++) for (let x = left; x < Math.min(left + cellsX, width); x++) {
      if (!inside(x, y)) continue;
      const mask = candidate01BlobMask(x, y, inside);
      cells.push({ x: x - left, y: y - top, mask });
    }
    if (cells.length === 0) continue;
    // Content-derived ids make equal pictures cacheable, and unequal DOM masks distinct.
    const id = `c01-water-${cells.map(({ x, y, mask }) => `${x}_${y}_${mask}`).join('-')}`;
    const alpha = cells.map(({ x, y, mask }) => `<g transform="translate(${x * TILE} ${y * TILE})">${runtimeAtlasCellMarkup(masks, mask)}</g>`).join('');
    const w = Math.min(cellsX, width - left) * TILE;
    const h = Math.min(cellsY, height - top) * TILE;
    pieces.push({ x: left * TILE, y: top * TILE,
      markup: `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" overflow="hidden" data-runtime-raster="area-water">`
        + `<defs><mask id="${id}" maskUnits="userSpaceOnUse" x="0" y="0" width="${periodX}" height="${periodY}">${alpha}</mask></defs>`
        + `<image href="${escapeAttr(material.href)}" width="${periodX}" height="${periodY}" preserveAspectRatio="none" mask="url(#${id})"/></svg>` });
  }
  return pieces;
}
