import { describe, expect, it } from 'vitest';
import { candidateReviewViewport } from './candidate_review_viewport';

describe('candidate art review viewport', () => {
  it('uses the runtime tile unit and candidate board insets', () => {
    expect(candidateReviewViewport({ width: 12, height: 8 })).toMatchObject({
      originX: 74, originY: 62,
      fieldWidth: 384, fieldHeight: 256,
      sceneWidth: 532, sceneHeight: 392,
    });
  });

  it('builds each diagnostic extent independently', () => {
    const small = candidateReviewViewport({ width: 2, height: 3 });
    const large = candidateReviewViewport({ width: 12, height: 8 });
    expect(small.fieldWidth).toBe(64);
    expect(small.fieldHeight).toBe(96);
    expect(large.fieldWidth).toBe(384);
  });
});
