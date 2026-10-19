import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-forest-floor-v2.json';
import masksDocument from '../../assets/final-fantasy-v1/world-materials-v1/manifest-forest-blend-masks-v1.json';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidateWorldMaterialTileMarkup, candidateWorldMaterialUrls, readCandidateWorldMaterial } from './candidate-01-world-material';

export const CANDIDATE_01_FOREST_FLOOR = readCandidateWorldMaterial(document);
if (CANDIDATE_01_FOREST_FLOOR.replaces.atlas !== 'surface-forest-floor'
  || CANDIDATE_01_FOREST_FLOOR.replaces.tone !== 0
  || CANDIDATE_01_FOREST_FLOOR.logicalSize.some(size => size !== 128)
  || CANDIDATE_01_FOREST_FLOOR.rasterSize.some(size => size !== 512)
  || masksDocument.schemaVersion !== 1 || masksDocument.atlases.length !== 1
  || masksDocument.atlases[0].id !== 'transition-meadow-forest') {
  throw new Error('candidate-01: forest floor requires reviewed tone-zero sampling and the original forest blend masks');
}
const spec = masksDocument.atlases[0];
const root = '../../assets/final-fantasy-v1/world-materials-v1/';
const href = candidateWorldMaterialUrls[`${root}${spec.png2x}`];
if (!href || !candidateWorldMaterialUrls[`${root}${spec.png}`]) throw new Error('candidate-01: missing forest blend alpha');
const mask = { ...CANDIDATE_01_ENVIRONMENT.atlas(spec.id).raster, href };

/** The existing Blob47 index owns coverage; world coordinates own the material sample. */
export function candidate01ForestBlendMarkup(atlasId: string, cell: number, worldX: number, worldY: number): string | null {
  return atlasId === spec.id
    ? candidateWorldMaterialTileMarkup(CANDIDATE_01_FOREST_FLOOR, worldX, worldY, { atlas: mask, cell, id: spec.id })
    : null;
}
