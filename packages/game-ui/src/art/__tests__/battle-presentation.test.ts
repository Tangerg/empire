import { TacticalGrids } from '@empire/battle-engine/tactical-grid';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { candidate01Level } from '@empire/story-candidate-01/levels';
import { mapFromLevel } from '@empire/battle-engine/level';
import { createSceneViewport } from '../scene-viewport';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';

import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);



describe('battle presentation', () => {
  it('resolves story art at the composition edge', () => {
    const presentation = CANDIDATE_01_ART.presentationFor('c01-01');
    const map = mapFromLevel(candidate01Level('c01-01'), TEST_CATALOG);
    const viewport = createSceneViewport(
      SQUARE,
      map.width,
      map.height,
      32,
      presentation.sceneProfile('c01-01'),
    );

    expect(presentation.id).toBe('candidate-01');
    expect(presentation.boardClass).toBe('candidate-map');
    expect(presentation.sceneFrame('c01-01', map, viewport).backdrop).toContain('authored-wide');
    expect(presentation.sceneLayers('c01-01', map).ground).toContain('candidate-ground-route');
  });

  it('keeps unknown campaigns on the story-neutral fallback', () => {
    const presentation = CANDIDATE_01_ART.presentationFor('sandbox-01');

    expect(presentation.id).toBe('generic');
    expect(presentation.sceneProfile('sandbox-01')).toEqual({});
    expect(presentation.sceneLayers('sandbox-01', mapFromLevel(candidate01Level('c01-01'), TEST_CATALOG))).toEqual({
      ground: '',
      underUnits: '',
      overUnits: '',
    });
    expect(presentation.effect('unknown', 16, 16)).toBe('');
  });
});
