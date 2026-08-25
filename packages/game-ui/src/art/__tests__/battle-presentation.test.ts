import { TacticalGrids, mapFromLevel } from '@empire/battle-engine';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { candidate01Level, CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { GENERIC_PRESENTATION } from '../battle-presentation';
import { ArtDirection } from '../direction';
import { createSceneViewport } from '../scene-viewport';
import { boardPiecesMarkup } from '../board-surface';
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
    const scene = { content: TEST_CATALOG, levelId: 'c01-01', map, viewport };
    expect(presentation.sceneFrame(scene).backdrop).toContain('authored-wide');
    expect(boardPiecesMarkup(presentation.sceneLayers(scene).ground))
      .toContain('candidate-ground-route');
  });

  it('derives scene semantics from the catalog it is handed', () => {
    const presentation = CANDIDATE_01_ART.presentationFor('c01-01');
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(
      SQUARE,
      map.width,
      map.height,
      32,
      presentation.sceneProfile('c01-01'),
    );
    const withoutRoutes = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    for (const terrain of withoutRoutes.terrains.all()) {
      withoutRoutes.terrains.override(terrain.id, {
        tags: terrain.tags.filter((tag) => tag !== 'road' && tag !== 'building' && tag !== 'outpost'),
      });
    }

    const original = boardPiecesMarkup(presentation.sceneLayers({
      content: TEST_CATALOG, levelId: 'c01-01', map, viewport,
    }).ground);
    const changed = boardPiecesMarkup(presentation.sceneLayers({
      content: withoutRoutes, levelId: 'c01-01', map, viewport,
    }).ground);

    expect(original).toContain('candidate-ground-route');
    expect(changed).not.toContain('candidate-ground-route');
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
    const layers = presentation.sceneLayers({ content: TEST_CATALOG, levelId: 'sandbox-01', map, viewport });
    expect(layers).toMatchObject({ ground: [], overUnits: [] });
    // The fall of light spans the whole field, so it is one piece at the origin.
    expect(layers.underUnits).toHaveLength(1);
    expect(layers.underUnits[0]).toMatchObject({ x: 0, y: 0 });
    expect(layers.underUnits[0].markup).toContain('field-light');
    expect(layers.underUnits[0].markup).toContain(`width="${map.width * 32}"`);
    expect(presentation.effect('unknown')).toEqual({ body: '' });
  });

  /**
   * Composition refuses to guess. `presentationFor` takes the first entry that
   * `matches`, so the same art composed twice makes the second copy unreachable —
   * and only the provider list used to be checked for it.
   */
  it('refuses two entries under one name in either list', () => {
    const provider = { id: 'twice' };
    expect(() => new ArtDirection([provider, provider])).toThrow('duplicate art provider "twice"');
    expect(() => new ArtDirection([], [GENERIC_PRESENTATION, GENERIC_PRESENTATION]))
      .toThrow('duplicate battle presentation "generic"');
    expect(new ArtDirection([provider], [GENERIC_PRESENTATION]).presentations).toHaveLength(1);
  });
});
