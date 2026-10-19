/** The three actual semantic ground roles, each projected through the shared world renderer. */
import { resolve } from 'node:path';
import { CANDIDATE_01_OPEN_GROUND_MATERIALS } from '../../packages/story-candidate-01/src/presentation/candidate-01-open-ground-materials';
import { writeWorldMaterialPlaneReview } from './review_world_material_plane';

const output = process.env.OPEN_GROUND_REVIEW_OUTPUT ?? '/tmp/empire-open-ground-materials';
for (const material of CANDIDATE_01_OPEN_GROUND_MATERIALS) {
  writeWorldMaterialPlaneReview(material, resolve(output, material.id));
}
