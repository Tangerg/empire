import { CANDIDATE_01_FOREST_FLOOR, candidate01ForestBlendMarkup } from '../../packages/story-candidate-01/src/presentation/candidate-01-forest-material';
import { writeBlobMaterialReview } from './review_blob_material';

writeBlobMaterialReview(CANDIDATE_01_FOREST_FLOOR, 'transition-meadow-forest',
  (cell, x, y) => candidate01ForestBlendMarkup('transition-meadow-forest', cell, x, y),
  process.env.FOREST_REVIEW_OUTPUT ?? '/tmp/empire-forest-material');
