import meadow from '../../assets/final-fantasy-v1/world-materials-v1/manifest-meadow-stony-v2.json';
import scorched from '../../assets/final-fantasy-v1/world-materials-v1/manifest-wasteland-scorched-v2.json';
import scree from '../../assets/final-fantasy-v1/world-materials-v1/manifest-wasteland-scree-v2.json';
import { readCandidateWorldMaterial } from './candidate-01-world-material';

/** Original terrain tones own these three active roles; the other slots stay catalog-only. */
export const CANDIDATE_01_OPEN_GROUND_MATERIALS = [
  readCandidateWorldMaterial(meadow),
  readCandidateWorldMaterial(scorched),
  readCandidateWorldMaterial(scree),
];
for (const [material, atlas, tone] of [
  [CANDIDATE_01_OPEN_GROUND_MATERIALS[0], 'surface-meadow', 2],
  [CANDIDATE_01_OPEN_GROUND_MATERIALS[1], 'surface-wasteland', 1],
  [CANDIDATE_01_OPEN_GROUND_MATERIALS[2], 'surface-wasteland', 2],
] as const) {
  if (material.replaces.atlas !== atlas || material.replaces.tone !== tone
    || material.logicalSize.some(size => size !== 128)
    || material.rasterSize.some(size => size !== 512)) {
    throw new Error('candidate-01: open ground requires its original explicit tone and reviewed 128-unit world sample');
  }
}
