import baseDocument from '../../assets/final-fantasy-v1/world-materials-v1/manifest-stone-base-v2.json';
import pavingDocument from '../../assets/final-fantasy-v1/world-materials-v1/manifest-stone-paving-v2.json';
import connectorsDocument from '../../assets/final-fantasy-v1/world-materials-v1/manifest-stone-connectors-v1.json';
import { CANDIDATE_01_ENVIRONMENT } from './candidate-01-environment';
import { candidateWorldMaterialTileMarkup, candidateWorldMaterialUrls, readCandidateWorldMaterial } from './candidate-01-world-material';

/** The existing tone identity is retained; its sampling changes from one tile to a world plane. */
export const CANDIDATE_01_STONE_BASE = readCandidateWorldMaterial(baseDocument);
export const CANDIDATE_01_STONE_PAVING = readCandidateWorldMaterial(pavingDocument);
if (CANDIDATE_01_STONE_BASE.replaces.atlas !== 'surface-old-stone'
  || CANDIDATE_01_STONE_BASE.replaces.tone !== 0
  || CANDIDATE_01_STONE_BASE.logicalSize.some(size => size !== 128)
  || CANDIDATE_01_STONE_BASE.rasterSize.some(size => size !== 512)) {
  throw new Error('candidate-01: stone base requires the reviewed tone-zero 128-unit world sample');
}

if (CANDIDATE_01_STONE_PAVING.replaces.atlas !== 'surface-old-stone'
  || CANDIDATE_01_STONE_PAVING.replaces.tone !== 3
  || CANDIDATE_01_STONE_PAVING.logicalSize.some(size => size !== 128)
  || CANDIDATE_01_STONE_PAVING.rasterSize.some(size => size !== 512)
  || connectorsDocument.schemaVersion !== 1
  || connectorsDocument.atlases.map(atlas => atlas.id).join(',') !== 'route-stone-road,route-edge-stone-road') {
  throw new Error('candidate-01: stone paving requires the reviewed tone-three material and ordered legacy masks');
}
const root = '../../assets/final-fantasy-v1/world-materials-v1/';
const connectorMasks = new Map(connectorsDocument.atlases.map(spec => {
  const existing = CANDIDATE_01_ENVIRONMENT.atlas(spec.id);
  const href = candidateWorldMaterialUrls[`${root}${spec.png2x}`];
  if (!href || !candidateWorldMaterialUrls[`${root}${spec.png}`]) throw new Error(`candidate-01: missing stone mask ${spec.id}`);
  return [spec.id, { ...existing.raster, href }] as const;
}));

/** Only stone route RGB is replaced. The established atlas index and alpha own geometry. */
export function candidate01StoneConnectorMarkup(atlasId: string, cell: number, worldX: number, worldY: number): string | null {
  const atlas = connectorMasks.get(atlasId);
  return atlas ? candidateWorldMaterialTileMarkup(CANDIDATE_01_STONE_PAVING, worldX, worldY, { atlas, cell, id: atlasId }) : null;
}
