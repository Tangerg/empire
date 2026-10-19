import { TacticalGrids, mapFromLevel } from '@empire/battle-engine';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { candidate01Level } from '../levels';
import {
  candidate01MapSceneryLayers,
  candidate01SceneFrameMarkup,
  candidate01PaintsCells,
  candidate01SceneProfile,
} from './candidate-01-map-scene';
import { CANDIDATE_01_ENVIRONMENT, candidate01EnvironmentScene } from './candidate-01-environment';
import { boardPiecesMarkup, createSceneViewport, type BoardPiece } from '@empire/game-ui';
import { candidate01FieldMaterialPieces } from './candidate-01-field-material';

import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const sceneContext = (levelId: string, map: ReturnType<typeof mapFromLevel>) => {
  const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile());
  return { content: TEST_CATALOG, levelId, map, viewport };
};

describe('candidate-01 authored map scenery', () => {
  it('applies each registered forest scale once while keeping its contact at the placement origin', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-02'));
    map.tiles.fill('forest');
    const context = sceneContext('c01-02', map);
    const field = boardPiecesMarkup(candidate01MapSceneryLayers(context).underUnits);
    const frame = candidate01SceneFrameMarkup(context).foreground;
    const motherRootMap = mapFromLevel(TEST_CATALOG, candidate01Level('c01-02'));
    motherRootMap.tiles.fill('c01.mother-root');
    const motherRoot = boardPiecesMarkup(candidate01MapSceneryLayers(sceneContext('c01-02', motherRootMap)).underUnits);
    const props = (markup: string) => markup.match(/<g class="candidate-environment-prop [\s\S]*?<\/svg>\s*<\/g>/g) ?? [];
    for (const [id, minPlacement, maxPlacement] of [
      ['oak-ancient', 0.74, 0.86], ['mixed-forest-dense', 0.74, 0.86],
      ['fern-bed', 0.58, 0.7], ['boulder-large', 1.05, 1.05],
      ['oak-grove-dense', 0.74, 0.86], ['mixed-forest-edge', 0.74, 0.86],
      ['stump-hollow', 0.58, 0.7], ['fallen-hollow-log', 0.9, 0.9],
      ['bramble-dark', 0.58, 0.7], ['bramble-berries', 0.58, 0.7],
      ['boulder-ferns', 0.8, 0.92],
      ['mixed-forest-autumn', 0.74, 0.86], ['sapling-rock-cluster', 0.58, 0.7],
      ['stump-low', 0.58, 0.7], ['forest-floor-cluster', 0.58, 0.7],
    ] as const) {
      const { cell, atlas } = CANDIDATE_01_ENVIRONMENT.cell(id);
      const crop = `x="${-(cell.index % 4) * 96}" y="${-Math.floor(cell.index / 4) * 96}"`;
      const source = id === 'boulder-ferns' ? motherRoot
        : id === 'boulder-large' || id === 'fallen-hollow-log' ? frame : field;
      const matches = props(source)
        .filter((markup) => markup.includes('forest-temperate@2x.png') && markup.includes(crop));
      expect(matches.length, id).toBeGreaterThan(0);
      for (const markup of matches) {
        const transform = /transform="translate\(([-\d.]+) ([-\d.]+)\) scale\(([-\d.]+)/.exec(markup);
        if (!transform || !cell.anchor || !cell.presentationScale) throw new Error(`incomplete forest presentation: ${id}`);
        const scale = Number(transform[3]);
        expect(Math.abs(scale)).toBeGreaterThanOrEqual(minPlacement * cell.presentationScale - 0.0005);
        expect(Math.abs(scale)).toBeLessThanOrEqual(maxPlacement * cell.presentationScale + 0.0005);
        // Translation and scale are rounded independently to 0.001 by the SVG adapter.
        const x = scale < 0 ? (atlas.cellWidth - cell.anchor[0]) * -scale : -cell.anchor[0] * scale;
        expect(Number(transform[1])).toBeCloseTo(x, 1);
        expect(Number(transform[2])).toBeCloseTo(-cell.anchor[1] * Math.abs(scale), 1);
      }
    }
  });

  it('composes high-resolution surfaces, connected roads and authored regions for Twin Hills', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    expect(candidate01EnvironmentScene('c01-01')?.mapSize).toEqual([map.width, map.height]);
    const layers = candidate01MapSceneryLayers(sceneContext('c01-01', map));
    const markup = boardPiecesMarkup([...layers.ground, ...layers.underUnits, ...layers.overUnits]);

    expect(markup).toContain('data-runtime-raster="atlas-cell"');
    expect(boardPiecesMarkup(layers.ground)).toContain('candidate-ground-route');
    expect(boardPiecesMarkup(layers.ground)).toContain('candidate-ground-route-edge');
    expect(boardPiecesMarkup(layers.ground)).toContain('surface-meadow');
    /*
     * Every authored placement reaches the depth it was declared at.
     *
     * This used to be four `expect(markup).toContain('temperate-hill-cap')` lines,
     * matching prop ids inside a `data-environment-cell-id` attribute whose only
     * reader in the repository was those lines. What they were reaching for is the
     * depth contract, so that is what is checked.
     */
    const scene = candidate01EnvironmentScene('c01-01')!;
    const declared = (layer: string) => scene.placements.filter((p) => p.layer === layer).length;
    const drawn = (pieces: readonly BoardPiece[], layer: string) =>
      pieces.filter((piece) => piece.markup.includes(`is-${layer}`)).length;

    expect(declared('foundation')).toBeGreaterThan(0);
    expect(drawn(layers.ground, 'foundation')).toBe(declared('foundation'));
    expect(drawn(layers.ground, 'ground-decal')).toBe(declared('ground-decal'));
    // Plus the two ambient villagers this module places itself.
    expect(drawn(layers.underUnits, 'under-units')).toBe(declared('under-units') + 2);
    // And none of them landed in a layer they were not declared for.
    expect(drawn(layers.ground, 'under-units')).toBe(0);
    expect(markup.match(/candidate-environment-prop/g)?.length ?? 0).toBeGreaterThan(20);

    // The broad base is shared in material-sized blocks; only the other two
    // surfaces retain cell-sized pictures, with their original fit and pixels.
    const field = candidate01FieldMaterialPieces(map.width * 32, map.height * 32);
    expect(layers.ground.slice(0, field.length)).toEqual(field);
    const surfaces = layers.ground
      .filter((piece) => piece.markup.includes('/surface-'))
      .map((piece) => piece.markup);
    expect(new Set(surfaces).size).toBe(2);
    // Board position stays external; area-water masks also place cells inside their shared texture block.
    expect(layers.ground.every((piece) => !piece.markup.includes('translate') || piece.markup.includes('scale') || piece.markup.includes('data-runtime-raster="area-water"')))
      .toBe(true);
  });

  it('authors a non-playable forest frame outside the logical cells', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile());
    const frame = candidate01SceneFrameMarkup({ content: TEST_CATALOG, levelId: 'c01-01', map, viewport });

    expect(viewport.sceneWidth).toBeGreaterThan(map.width * 32);
    expect(frame.backdrop).toContain('data-scene-viewport="authored-wide"');
    expect(frame.backdrop).toContain('c01-field-shadow');
    expect(frame.foreground).toContain('is-scene-frame');
  });

  it('does not leak authored Twin Hills dressing into other maps', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-02'));
    const layers = candidate01MapSceneryLayers(sceneContext('c01-02', map));
    const markup = boardPiecesMarkup([...layers.ground, ...layers.underUnits, ...layers.overUnits]);

    // Hand-placed props belong to the map somebody placed them on: chapter one's
    // authored layers reach no other chapter, and neither do its two farmers.
    expect(layers.overUnits).toEqual([]);
    expect(markup).not.toContain('is-ground-decal');
    expect(markup).not.toContain('MISSION-BORDER-FARMER');
  });

  /**
   * The point of this round.
   *
   * `candidate01MapSceneryLayers` used to return three empty arrays for every
   * level except `c01-01` and the experience lab, and `candidate01TerrainMarkup`
   * stamped one four-variant tile per cell for the rest — no transitions, no
   * connections. So fifteen chapters of sixteen, and every built-in level, were a
   * visible grid of stamps beside high-density painted buildings.
   *
   * Every level is composed from the material table now. What each terrain is made
   * of is a story decision the table states; what a *tag* can say is the fallback,
   * so a map this pack has never seen is painted rather than left bare.
   */
  it('paints every shipped chapter from the material table', () => {
    for (const levelId of ['c01-02', 'c01-09', 'c01-14', 'c01-16']) {
      const map = mapFromLevel(TEST_CATALOG, candidate01Level(levelId));
      const layers = candidate01MapSceneryLayers(sceneContext(levelId, map));
      const ground = boardPiecesMarkup(layers.ground);

      const field = candidate01FieldMaterialPieces(map.width * 32, map.height * 32);
      expect(layers.ground.slice(0, field.length), levelId).toEqual(field);
      expect(ground, levelId).toContain('world-materials-v1/runtime/meadow-base@2x.png');
      expect(ground, levelId).toContain('candidate-ground-route');
      expect(boardPiecesMarkup(layers.underUnits), levelId).toContain('candidate-environment-prop');
    }
  });

  /**
   * Which cells this kit's sheets fit, said rather than policed.
   *
   * Every sheet in it is a 32-unit square indexed by square neighbours. Handed a
   * hex board the scene drew the whole ground at `x * 32` — cells in the wrong
   * places, silently.
   *
   * It answers the question now instead of enforcing it, because the two callers
   * owe the reader different things: a battle is refused (`BoardView` throws) and a
   * picture of a map leaves the scene out and shows the tiles (`mapScenePieces`).
   * Enforcing it inside the scene made the level menu crash on a hex level saved
   * from the editor — the card could not be drawn at all.
   */
  it('says which cells its sheets fit, rather than policing it', () => {
    expect(candidate01PaintsCells(4)).toBe(true);
    expect(candidate01PaintsCells(6)).toBe(false);

    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-02'));
    const square = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile());
    const painted = candidate01MapSceneryLayers({
      content: TEST_CATALOG, levelId: 'c01-02', map, viewport: square,
    });
    const field = candidate01FieldMaterialPieces(map.width * 32, map.height * 32);
    expect(painted.ground.slice(0, field.length)).toEqual(field);
  });

  /**
   * A river is a river, a wood shows its edge, and a crossing has a deck.
   *
   * These three were unreachable before, not merely unused: the catalog was
   * filtered by the asset allowlist inside chapter one's scene document, so the
   * four water sheets, the three other routes and the crossings atlas were not in
   * the pack at all. `c01-02` is the first chapter with a river across it.
   */
  it('gives water, woodland and crossings the connected sheets they need', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-02'));
    const layers = candidate01MapSceneryLayers(sceneContext('c01-02', map));
    const ground = boardPiecesMarkup(layers.ground);
    const standing = boardPiecesMarkup(layers.underUnits);

    expect(map.tiles).toContain('water');
    expect(map.tiles).toContain('bridge');
    expect(ground).toContain('water-area@2x.png');
    expect(ground).toContain('transition-meadow-forest');
    expect(standing).toContain('crossings-fortifications');
    // A wood's edge carries the tall-prop shadow; its inside carries undergrowth.
    expect(standing).toContain('is-standing');
    expect(standing).toContain('forest-temperate');
  });
});

/**
 * This campaign's board art travels with its picture.
 *
 * Its shadows were in a stylesheet — first the shared one, then this pack's own —
 * and a stylesheet is not in the room when markup is rasterised into a texture,
 * which is how a GPU backend drew this campaign with no prop shadows at all.
 *
 * It used to be pasted into `backdrop`, which put it in the *DOM tree*: obeyed by
 * the DOM backend, invisible to every texture the other one bakes, because a
 * picture is baked from its own markup. It is the scene's declared `style` now, so
 * each backend can put it where its own pictures will see it.
 */
describe('the board carries its own look', () => {
  it('declares the pack style with every level, painted or not', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile());
    const painted = candidate01SceneFrameMarkup({ content: TEST_CATALOG, levelId: 'c01-01', map, viewport });
    const plain = candidate01SceneFrameMarkup({ content: TEST_CATALOG, levelId: 'c01-09', map, viewport });

    for (const [label, frame] of [['painted', painted], ['plain', plain]] as const) {
      expect(frame.style, label).toContain('<style>');
      // A prop's shadow and a sprite's rim light: the look, not a layer grade.
      expect(frame.style, label).toContain('.candidate-environment-prop');
      expect(frame.style, label).toContain('.candidate-map .unit .figure');
      // Nothing that has to reach through a layer, because a baked picture has no
      // layer above it and a container cannot be styled at all. Comments stripped:
      // this file explains at length which three layer rules used to be here.
      expect((frame.style ?? '').replace(/\/\*[\s\S]*?\*\//g, ''), label).not.toContain('.layer-');
      // And the style is not smuggled through the backdrop any more.
      expect(frame.backdrop, label).not.toContain('<style>');
    }
    // The painted scene still has its scenery, not only the style.
    expect(painted.backdrop).toContain('candidate-scene-backdrop');
  });

  /** What is left in the stylesheet has no picture to travel inside. */
  it('leaves only page-side rules in the pack stylesheet', () => {
    const css = readFileSync(
      join(import.meta.dirname, '..', 'styles', 'candidate-01.css'),
      'utf8',
    ).replace(/\/\*[\s\S]*?\*\//g, '');
    const selectors = [...css.matchAll(/([^{}]+)\{/g)].map(([, selector]) => selector.trim());

    expect(selectors.length).toBeGreaterThan(0);
    expect(selectors.filter((selector) => !/candidate-art-icon/.test(selector))).toEqual([]);
  });
});
