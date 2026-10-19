/** The three actual semantic ground roles, each projected through the shared world renderer. */
import { resolve } from 'node:path';
import { CANDIDATE_01_OPEN_GROUND_MATERIALS } from '../../packages/story-candidate-01/src/presentation/candidate-01-open-ground-materials';
import { writeWorldMaterialPlaneReview } from './review_world_material_plane';

const output = process.env.OPEN_GROUND_REVIEW_OUTPUT ?? '/tmp/empire-open-ground-materials';
const period = Number(process.env.OPEN_GROUND_REVIEW_PERIOD ?? 128);
if (period !== 128 && period !== 256) throw new Error('open-ground review supports only the formal128 or diagnostic256 world period');
for (const source of CANDIDATE_01_OPEN_GROUND_MATERIALS) {
  const material = { ...source, logicalSize: [period, period] as const,
    rasterPixelsPerWorldUnit: [source.rasterSize[0] * 2 / period, source.rasterSize[1] * 2 / period] as const };
  writeWorldMaterialPlaneReview(material, resolve(output, material.id));
}
