import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-field-material-v2.json';
import { escapeAttr, type BoardPiece } from '@empire/game-ui';
import { readCandidateWorldMaterial } from './candidate-01-world-material';

/** JSON owns the source, UV scale, fit and exact legacy base binding. */
export const CANDIDATE_01_FIELD_MATERIAL = readCandidateWorldMaterial(document);

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
