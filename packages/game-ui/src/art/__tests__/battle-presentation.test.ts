import { createBattleEngine, TacticalGrids, mapFromLevel, type GameMap } from '@empire/battle-engine';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { candidate01Level, CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { GENERIC_PRESENTATION, paintsCells } from '../battle-presentation';
import { ArtDirection } from '../direction';
import { createSceneViewport } from '../scene-viewport';
import { boardPiecesMarkup } from '../board-surface';
import { battlefieldFeaturePieces, mapScenePieces } from '../battlefield-layer';
import { squareLayout } from '../board-decorations';
import { BoardView } from '../../ui/board';
import { svgBoardSurface } from '../svg-board-surface';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';

import { createTestCatalog } from '@empire/test-content';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const ENGINE = createBattleEngine({ content: TEST_CATALOG });



describe('battle presentation', () => {
  it('omits authored scene height badges before either renderer while retaining editor and other-level labels', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const before = JSON.stringify(map);
    const canvas = { art: CANDIDATE_01_ART, layout: squareLayout };
    const badges = (levelId?: string) => battlefieldFeaturePieces({ ...canvas, ...(levelId ? { levelId } : {}) }, map)
      .filter(piece => piece.markup.includes('elevation-badge'));
    // Both SVG and GPU consume these same BoardPieces; this is not a CSS-only hide.
    expect(badges('c01-01')).toEqual([]);
    expect(badges('c01-02').length).toBeGreaterThan(0);
    expect(badges().length).toBeGreaterThan(0);
    expect(JSON.stringify(map)).toBe(before);
  });

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
      .toContain('data-runtime-raster="area-road"');
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

    // Paint binding owns the same cells; catalog route tags own connections.
    const original = groundWith(TEST_CATALOG);
    const changed = groundWith(withoutRoutes);
    const geometry = (markup: string) => {
      const match = /data-runtime-raster="area-road">([\s\S]*?)<\/svg>/.exec(markup);
      if (!match) throw new Error('expected runtime area-road geometry');
      return match[1];
    };
    expect(original).toContain('data-runtime-raster="area-road"');
    expect(changed).toContain('data-runtime-raster="area-road"');
    expect(changed).not.toBe(original);
    expect([...geometry(original).matchAll(/<path /g)].length).toBeGreaterThan(0);
    expect([...geometry(changed).matchAll(/<path /g)]).toHaveLength(0);
    expect([...geometry(changed).matchAll(/<circle /g)]).toHaveLength(84);

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
    expect(ground).toContain('water-area@2x.png');
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
    // A broad world material covers the field without one surface stamp per
    // tactical cell. The un-authored chapter still gets its woodland frame.
    expect(boardPiecesMarkup(presentation.sceneLayers(scene).ground)).toContain('data-runtime-raster="world-material"');
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
   * A board this art has no sheets for is refused; a picture of it is not.
   *
   * The campaign's atlases are 32-unit squares indexed by square neighbours, and
   * there is no picture in them for a hexagon. Handed one, the scene drew the whole
   * ground at `x * 32`. Both halves of the answer come from one statement on the
   * port: the board throws, and a level card, the editor's canvas and a palette
   * swatch leave the scene out and show the tiles — because a battle drawn wrong is
   * worse than one refused, and a picture still owes the reader the map.
   */
  it('refuses a battle it cannot paint and still pictures the map', () => {
    const hex = TacticalGrids.get('hex');
    const level = candidate01Level('c01-02');
    const map = mapFromLevel(TEST_CATALOG, level);
    const canvas = { art: CANDIDATE_01_ART, content: TEST_CATALOG, grid: hex };

    expect(paintsCells(CANDIDATE_01_ART.presentation, 4)).toBe(true);
    expect(paintsCells(CANDIDATE_01_ART.presentation, hex.outline().length)).toBe(false);

    // The picture: no scene, and the per-cell painters still carry the map.
    expect(mapScenePieces(canvas, level.id, map)).toEqual({ ground: [], underUnits: [], overUnits: [] });
    expect(boardPiecesMarkup(mapScenePieces({ ...canvas, grid: SQUARE }, level.id, map).ground))
      .toContain('data-runtime-raster="world-material"');

    // The battle: refused, naming the art and the tiling.
    expect(() => new BoardView(
      { content: TEST_CATALOG, grid: hex, art: CANDIDATE_01_ART, renderer: svgBoardSurface },
      ENGINE.createState(level),
      { onTileClick: () => {}, onTileEnter: () => {}, onLeave: () => {}, onSecondary: () => {}, onScale: () => {} },
    )).toThrow(/candidate-01.*6 corners.*hex/s);
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
