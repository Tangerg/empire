import { TacticalGrids, mapFromLevel } from '@empire/battle-engine';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { candidate01Level, CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { createSceneViewport } from '../scene-viewport';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';

import { createTestCatalog } from '@empire/test-content';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);



describe('battle presentation', () => {
  it('resolves story art at the composition edge', () => {
    const presentation = CANDIDATE_01_ART.presentationFor('c01-01');
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
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
    expect(presentation.sceneLayers('c01-01', map, viewport).ground).toContain('candidate-ground-route');
  });

  it('keeps unknown campaigns on the story-neutral fallback', () => {
    const presentation = CANDIDATE_01_ART.presentationFor('sandbox-01');
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, presentation.sceneProfile('sandbox-01'));

    expect(presentation.id).toBe('generic');
    expect(presentation.sceneProfile('sandbox-01')).toEqual({});
    // No authored scenery — but not an unlit rectangle either: an unclaimed
    // level still gets a fall of light and ground that darkens at the edge,
    // which is the difference between a field and a spreadsheet.
    const layers = presentation.sceneLayers('sandbox-01', map, viewport);
    expect(layers).toMatchObject({ ground: '', overUnits: '' });
    expect(layers.underUnits).toContain('field-light');
    expect(layers.underUnits).toContain(`width="${map.width * 32}"`);
    expect(presentation.effect('unknown', 16, 16)).toBe('');
  });
});
