import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-earth-floor-v2.json';
import masksDocument from '../../assets/final-fantasy-v1/world-materials-v1/manifest-earth-blend-masks-v1.json';
import { candidateWorldMaterialTileMarkup, readCandidateWorldMaskAtlas, readCandidateWorldMaterial } from './candidate-01-world-material';

export const CANDIDATE_01_EARTH_FLOOR = readCandidateWorldMaterial(document);
if (CANDIDATE_01_EARTH_FLOOR.replaces.atlas !== 'surface-earth'
  || CANDIDATE_01_EARTH_FLOOR.replaces.tone !== 0
  || CANDIDATE_01_EARTH_FLOOR.logicalSize.some(size => size !== 256)
  || CANDIDATE_01_EARTH_FLOOR.rasterSize.some(size => size !== 512)
  || masksDocument.schemaVersion !== 1 || masksDocument.atlases.length !== 1
  || masksDocument.atlases[0].id !== 'transition-meadow-earth') {
  throw new Error('candidate-01: earth floor requires reviewed tone-zero sampling and the original earth blend masks');
}
const spec = masksDocument.atlases[0];
const mask = readCandidateWorldMaskAtlas(spec);

/** The existing Blob47 index owns coverage; world coordinates own the material sample. */
export function candidate01EarthBlendMarkup(atlasId: string, cell: number, worldX: number, worldY: number): string | null {
  return atlasId === spec.id
    ? candidateWorldMaterialTileMarkup(CANDIDATE_01_EARTH_FLOOR, worldX, worldY, { atlas: mask, cell, id: spec.id })
    : null;
}
