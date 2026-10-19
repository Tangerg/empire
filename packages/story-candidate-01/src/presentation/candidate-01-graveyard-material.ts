import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-graveyard-base-v2.json';
import { readCandidateWorldMaterial } from './candidate-01-world-material';

/** Only the already-consumed tone zero moves from tile stamps to a world plane. */
export const CANDIDATE_01_GRAVEYARD_BASE = readCandidateWorldMaterial(document);
if (CANDIDATE_01_GRAVEYARD_BASE.replaces.atlas !== 'surface-graveyard'
  || CANDIDATE_01_GRAVEYARD_BASE.replaces.tone !== 0
  || CANDIDATE_01_GRAVEYARD_BASE.logicalSize.some(size => size !== 256)
  || CANDIDATE_01_GRAVEYARD_BASE.rasterSize.some(size => size !== 512)) {
  throw new Error('candidate-01: graveyard base requires the reviewed tone-zero 256-unit world sample');
}
