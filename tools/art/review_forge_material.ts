/** Actual forge source: tile-local projection versus one continuous world plane. */
import { CANDIDATE_01_FORGE_BASE } from '../../packages/story-candidate-01/src/presentation/candidate-01-forge-material';
import { writeWorldMaterialPlaneReview } from './review_world_material_plane';
writeWorldMaterialPlaneReview(CANDIDATE_01_FORGE_BASE, process.env.FORGE_REVIEW_OUTPUT ?? '/tmp/empire-forge-plane');
