import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-road-material-v2.json';
import { escapeAttr, TILE, type BoardPiece } from '@empire/game-ui';
import { candidateConnectedMaterialMatcher, readCandidateWorldMaterial } from './candidate-01-world-material';
import { candidate01RoadCorridorGeometry, candidate01RoadGraph, type CandidateRoadCorridorProfile } from './candidate-01-road-corridor';

export const CANDIDATE_01_ROAD_MATERIAL = readCandidateWorldMaterial(document);
const material = CANDIDATE_01_ROAD_MATERIAL;
if (material.logicalSize.some(size => size % TILE !== 0)) {
  throw new Error('candidate-01: road world material periods must align with cells');
}

/** Paint ownership is narrower than route connectivity: a bridge joins but is not earth. */
export const candidate01IsAreaRoad = candidateConnectedMaterialMatcher(material.replaces);

/** One continuous world sample, bounded by the scene's curved road corridor. */
export function candidate01RoadMaterialPieces(
  width: number, height: number,
  painted: (x: number, y: number) => boolean,
  connected: (x: number, y: number) => boolean,
  plain: (x: number, y: number) => boolean,
  profile: CandidateRoadCorridorProfile,
): BoardPiece[] {
  const graph = candidate01RoadGraph(width, height, painted, connected);
  if (graph.cells.length === 0) return [];
  const geometry = candidate01RoadCorridorGeometry(graph, profile, plain);
  const [periodX, periodY] = material.logicalSize;
  const cellsX = periodX / TILE, cellsY = periodY / TILE;
  const pieces: BoardPiece[] = [];
  for (let top = 0; top < height; top += cellsY) for (let left = 0; left < width; left += cellsX) {
    // Include the bounded shoulder if a road sits across this material seam.
    if (!graph.cells.some(([x, y]) => x >= left - 1 && x <= left + cellsX && y >= top - 1 && y <= top + cellsY)) continue;
    const id = `c01-road-${left}_${top}-${graph.cells.map(([x, y]) => `${x}_${y}`).join('-')}`;
    const w = Math.min(cellsX, width - left) * TILE, h = Math.min(cellsY, height - top) * TILE;
    pieces.push({ x: left * TILE, y: top * TILE,
      markup: `<svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" overflow="hidden" data-runtime-raster="area-road">`
        + `<defs><clipPath id="${id}-cells">${geometry.allowed}</clipPath><clipPath id="${id}-limit">${geometry.limits}</clipPath>`
        + `<mask id="${id}" maskUnits="userSpaceOnUse" x="0" y="0" width="${w}" height="${h}">`
        + `<g transform="translate(${-left * TILE} ${-top * TILE})"><g clip-path="url(#${id}-cells)"><g clip-path="url(#${id}-limit)">${geometry.alpha}</g></g></g></mask></defs>`
        + `<image href="${escapeAttr(material.href)}" width="${periodX}" height="${periodY}" preserveAspectRatio="none" mask="url(#${id})"/></svg>` });
  }
  return pieces;
}
