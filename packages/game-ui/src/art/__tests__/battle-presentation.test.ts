import { TacticalGrids, mapFromLevel, type GameMap } from '@empire/battle-engine';
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
    const presentation = CANDIDATE_01_ART.presentation;
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

  /**
   * What the catalog decides, and what the pack decides.
   *
   * A road's ground being a road is the pack's own statement — it knows what its
   * terrains are made of, and 焦土农田 is bare earth for a reason no tag records.
   * What a road *connects to* is the catalog's: the mask is built from the `road`,
   * `building` and `outpost` tags, so a ruleset that stops calling a village a
   * place you can walk into gets roads that stop at its gate.
   *
   * And a terrain the pack has never heard of still gets painted, from the one
   * thing the rules do say about it.
   */
  it('derives scene semantics from the catalog it is handed', () => {
    const presentation = CANDIDATE_01_ART.presentation;
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

    const groundWith = (content: typeof TEST_CATALOG) =>
      boardPiecesMarkup(presentation.sceneLayers({ content, levelId: 'c01-01', map, viewport }).ground);

    // Same roads, knitted differently: with nothing to link to, every road cell
    // falls to the sheet's unconnected mask.
    const original = groundWith(TEST_CATALOG);
    const changed = groundWith(withoutRoutes);
    expect(original).toContain('candidate-ground-route');
    expect(changed).toContain('candidate-ground-route');
    expect(changed).not.toBe(original);

    const roadCells = (markup: string) => new Set(
      [...markup.matchAll(/route-dirt-road@2x\.png" x="(-?\d+)"/g)].map(([, x]) => x),
    );
    // Sixteen masks × four variants; with nothing to link to, only the four
    // variants of the unconnected mask are left.
    expect(roadCells(original).size).toBeGreaterThan(4);
    expect(roadCells(changed).size).toBe(4);
  });

  /** A terrain the pack never named is painted from what the rules say about it. */
  it('paints an unknown terrain from its tags', () => {
    const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    catalog.terrains.define({ ...catalog.terrains.get('water'), id: 'probe.canal', name: '运河', tags: ['water'] });
    const map: GameMap = {
      width: 2,
      height: 1,
      tiles: ['probe.canal', 'probe.canal'],
      owners: [0, 0],
      captureProgress: [0, 0],
      elevation: [0, 0],
      cliffs: [],
      directionalCover: [],
    };
    const presentation = CANDIDATE_01_ART.presentation;
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, presentation.sceneProfile('probe'));

    const ground = boardPiecesMarkup(
      presentation.sceneLayers({ content: catalog, levelId: 'probe', map, viewport }).ground,
    );
    expect(ground).toContain('water-slow-river');
  });

  it('paints a level the campaign never authored a scene for', () => {
    /*
     * This used to assert the opposite: `presentationFor('sandbox-01')` fell
     * through to the generic look, because the pack's `matches` predicate only
     * claimed `c01-*`. That predicate is why fifteen of sixteen chapters and every
     * built-in level were drawn as flat stamped tiles — the composition the root
     * chose was declined by the pack it chose.
     *
     * An art direction carries one scene now. Whatever level the root hands it,
     * the material table paints, from what the rules say about each terrain.
     */
    const presentation = CANDIDATE_01_ART.presentation;
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-09'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, presentation.sceneProfile('c01-09'));
    const scene = { content: TEST_CATALOG, levelId: 'c01-09', map, viewport };

    expect(presentation.id).toBe('candidate-01');
    // Every cell gets a surface, and the chapter with no authored scene gets its
    // woodland frame like every other.
    expect(presentation.sceneLayers(scene).ground.length).toBeGreaterThanOrEqual(map.width * map.height);
    expect(presentation.sceneFrame(scene).foreground).toContain('is-scene-frame');
  });

  it('lights an unpainted field when the art composes no scene', () => {
    const presentation = new ArtDirection().presentation;
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, presentation.sceneProfile('sandbox-01'));

    expect(presentation.id).toBe('generic');
    expect(presentation.sceneProfile('sandbox-01')).toEqual({});
    // No authored scenery — but not an unlit rectangle either: an unpainted
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
   * Composition refuses to guess. `resolve` takes the first provider with an
   * answer, so the same provider composed twice makes the second copy unreachable.
   */
  it('refuses two providers under one name', () => {
    const provider = { id: 'twice' };
    expect(() => new ArtDirection([provider, provider])).toThrow('duplicate art provider "twice"');
    expect(new ArtDirection([provider]).presentation).toBe(GENERIC_PRESENTATION);
  });
});
