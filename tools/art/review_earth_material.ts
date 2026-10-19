import { CANDIDATE_01_EARTH_FLOOR, candidate01EarthBlendMarkup } from '../../packages/story-candidate-01/src/presentation/candidate-01-earth-material';
import { writeBlobMaterialReview } from './review_blob_material';

writeBlobMaterialReview(CANDIDATE_01_EARTH_FLOOR, 'transition-meadow-earth',
  (cell, x, y) => candidate01EarthBlendMarkup('transition-meadow-earth', cell, x, y),
  process.env.EARTH_REVIEW_OUTPUT ?? '/tmp/empire-earth-material');
