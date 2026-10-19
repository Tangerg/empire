/** Actual graveyard source: tile-local projection versus one continuous world plane. */
import { CANDIDATE_01_GRAVEYARD_BASE } from '../../packages/story-candidate-01/src/presentation/candidate-01-graveyard-material';
import { writeWorldMaterialPlaneReview } from './review_world_material_plane';
writeWorldMaterialPlaneReview(CANDIDATE_01_GRAVEYARD_BASE, process.env.GRAVEYARD_REVIEW_OUTPUT ?? '/tmp/empire-graveyard-plane');
