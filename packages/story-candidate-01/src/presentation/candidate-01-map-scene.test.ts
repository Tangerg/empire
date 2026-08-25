import { TacticalGrids, mapFromLevel } from '@empire/battle-engine';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { candidate01Level } from '../levels';
import {
  candidate01MapSceneryLayers,
  candidate01SceneFrameMarkup,
  candidate01SceneProfile,
} from './candidate-01-map-scene';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { boardPiecesMarkup, createSceneViewport, type BoardPiece } from '@empire/game-ui';

import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const sceneContext = (levelId: string, map: ReturnType<typeof mapFromLevel>) => {
  const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile());
  return { content: TEST_CATALOG, levelId, map, viewport };
};

describe('candidate-01 authored map scenery', () => {
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

    /*
     * The point of the change: a picture's identity is no longer its place.
     *
     * The ground layer used to be one string of 20,339 nodes at 81×51 with every
     * cell's coordinates baked into its own markup. It is one surface picture per
     * cell now, and there are four of them on the whole map.
     */
    expect(layers.ground.length).toBeGreaterThan(map.width * map.height);
    const surfaces = layers.ground
      .slice(0, map.width * map.height)
      .map((piece) => piece.markup);
    /*
     * One picture per material on the map, not one per cell and not four.
     *
     * Twin Hills is made of three: the meadow, the hillsides drawn in the meadow
     * sheet's stony cell, and the scree its mountains stand on. It used to be four
     * — one per variant of the single surface — and every arrangement of those
     * variants across the field, by cell or by block, read as a pattern laid over
     * the map. A sheet's cells are materials; the material table picks one each.
     */
    expect(new Set(surfaces).size).toBe(3);
    // And every piece says where it goes, rather than carrying it inside.
    expect(layers.ground.every((piece) => !piece.markup.includes('translate') || piece.markup.includes('scale')))
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

      // A surface under every cell, and no cell drawn twice by the surface pass.
      expect(layers.ground.length, levelId).toBeGreaterThanOrEqual(map.width * map.height);
      expect(ground, levelId).toContain('surface-meadow');
      expect(ground, levelId).toContain('candidate-ground-route');
      expect(boardPiecesMarkup(layers.underUnits), levelId).toContain('candidate-environment-prop');
    }
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
    expect(ground).toContain('water-slow-river');
    expect(ground).toContain('transition-meadow-forest');
    expect(standing).toContain('crossings-fortifications');
    // A wood's edge carries the tall-prop shadow; its inside carries undergrowth.
    expect(standing).toContain('is-standing');
    expect(standing).toContain('forest-temperate');
  });
});

/**
 * This campaign's board art travels inside its picture.
 *
 * Its shadows and colour grades were in a stylesheet — first the shared one, then
 * this pack's own — and a stylesheet is not in the room when markup is rasterised
 * into a texture, which is how a GPU backend would have drawn the whole campaign
 * with no shadows and no grade at all. Every level carries it, painted scene or not:
 * an atlas tile and a unit figure wear these shadows even where no scenery was
 * authored.
 */
describe('the board carries its own look', () => {
  it('ships the pack style with every level, painted or not', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile());
    const painted = candidate01SceneFrameMarkup({ content: TEST_CATALOG, levelId: 'c01-01', map, viewport });
    const plain = candidate01SceneFrameMarkup({ content: TEST_CATALOG, levelId: 'c01-09', map, viewport });

    for (const [label, frame] of [['painted', painted], ['plain', plain]] as const) {
      expect(frame.backdrop, label).toContain('<style>');
      // The two most expensive rules, and the ones a hand-written list forgot.
      expect(frame.backdrop, label).toContain('.candidate-map .layer-terrain');
      expect(frame.backdrop, label).toContain('.candidate-map .layer-ground');
      expect(frame.backdrop, label).toContain('.candidate-environment-prop');
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
