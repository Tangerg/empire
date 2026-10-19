import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-road-material-v2.json';
import { escapeAttr, runtimeAtlasCellMarkup, TILE, type BoardPiece } from '@empire/game-ui';
import { candidate01BlobMask } from './candidate-01-square-mask';
import { candidateConnectedMaterialMatcher, readCandidateOccupancyAtlas, readCandidateWorldMaterial } from './candidate-01-world-material';

export const CANDIDATE_01_ROAD_MATERIAL = readCandidateWorldMaterial(document);
const material = CANDIDATE_01_ROAD_MATERIAL;
const occupancy = material.occupancy;
if (occupancy.tileSize !== TILE || occupancy.columns !== 16 || occupancy.rows !== 16
  || occupancy.geometry !== 'external-inset-rounded-convex-concave-v1'
  || occupancy.inset !== 5 || occupancy.convexRadius !== 9 || occupancy.concaveRadius !== 5
  || occupancy.bitOrder.join(',') !== 'N,NE,E,SE,S,SW,W,NW'
  || material.logicalSize.some(size => size % TILE !== 0)) {
  throw new Error('candidate-01: road requires the reviewed rounded square-area mask profile');
}
const masks = readCandidateOccupancyAtlas(occupancy);

/** Paint ownership is narrower than route connectivity: a bridge joins but is not earth. */
export const candidate01IsAreaRoad = candidateConnectedMaterialMatcher(material.replaces);

/**
 * Occupancy changes alpha only. Each material-period piece samples the same
 * world-space RGB, with self-contained mask definitions for either backend.
 */
export function candidate01RoadMaterialPieces(
  width: number, height: number,
  painted: (x: number, y: number) => boolean,
  connected: (x: number, y: number) => boolean,
): BoardPiece[] {
  if (![width, height].every(value => Number.isInteger(value) && value > 0)) {
    throw new Error('candidate-01: road map dimensions must be positive integers');
  }
  const [periodX, periodY] = material.logicalSize;
  const cellsX = periodX / TILE;
  const cellsY = periodY / TILE;
  // Extend an existing edge port in the mask's virtual neighborhood, but never
  // create an extra painted cell outside the actual board. This keeps entries open.
  const joins = (x: number, y: number): boolean => connected(
    Math.max(0, Math.min(width - 1, x)), Math.max(0, Math.min(height - 1, y)),
  );
  const pieces: BoardPiece[] = [];
  for (let top = 0; top < height; top += cellsY) for (let left = 0; left < width; left += cellsX) {
    const cells: { x: number; y: number; mask: number }[] = [];
    for (let y = top; y < Math.min(top + cellsY, height); y++) for (let x = left; x < Math.min(left + cellsX, width); x++) {
      if (painted(x, y)) cells.push({ x: x - left, y: y - top, mask: candidate01BlobMask(x, y, joins) });
    }
    if (cells.length === 0) continue;
    const id = `c01-road-${cells.map(({ x, y, mask }) => `${x}_${y}_${mask}`).join('-')}`;
    const alpha = cells.map(({ x, y, mask }) => `<g transform="translate(${x * TILE} ${y * TILE})">${runtimeAtlasCellMarkup(masks, mask)}</g>`).join('');
    const w = Math.min(cellsX, width - left) * TILE;
    const h = Math.min(cellsY, height - top) * TILE;
    pieces.push({ x: left * TILE, y: top * TILE,
      markup: `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" overflow="hidden" data-runtime-raster="area-road">`
        + `<defs><mask id="${id}" maskUnits="userSpaceOnUse" x="0" y="0" width="${periodX}" height="${periodY}">${alpha}</mask></defs>`
        + `<image href="${escapeAttr(material.href)}" width="${periodX}" height="${periodY}" preserveAspectRatio="none" mask="url(#${id})"/></svg>` });
  }
  return pieces;
}
