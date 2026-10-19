import document from '../../assets/final-fantasy-v1/world-materials-v1/manifest-forge-base-v2.json';
import { readCandidateWorldMaterial } from './candidate-01-world-material';

/** Only the already-consumed tone zero moves from tile stamps to a world plane. */
export const CANDIDATE_01_FORGE_BASE = readCandidateWorldMaterial(document);
if (CANDIDATE_01_FORGE_BASE.replaces.atlas !== 'surface-forge-stone'
  || CANDIDATE_01_FORGE_BASE.replaces.tone !== 0
  || CANDIDATE_01_FORGE_BASE.logicalSize.some(size => size !== 256)
  || CANDIDATE_01_FORGE_BASE.rasterSize.some(size => size !== 512)) {
  throw new Error('candidate-01: forge base requires the reviewed tone-zero 256-unit world sample');
}
