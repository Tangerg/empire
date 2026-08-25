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
  const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile(levelId));
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
    expect(new Set(surfaces).size).toBe(4);
    // And every piece says where it goes, rather than carrying it inside.
    expect(layers.ground.every((piece) => !piece.markup.includes('translate') || piece.markup.includes('scale')))
      .toBe(true);
  });

  it('authors a non-playable forest frame outside the logical cells', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile('c01-01'));
    const frame = candidate01SceneFrameMarkup({ content: TEST_CATALOG, levelId: 'c01-01', map, viewport });

    expect(viewport.sceneWidth).toBeGreaterThan(map.width * 32);
    expect(frame.backdrop).toContain('data-scene-viewport="authored-wide"');
    expect(frame.backdrop).toContain('c01-field-shadow');
    expect(frame.foreground).toContain('is-scene-frame');
  });

  it('does not leak authored Twin Hills dressing into other maps', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-02'));
    expect(candidate01MapSceneryLayers(sceneContext('c01-02', map))).toEqual({
      ground: [],
      underUnits: [],
      overUnits: [],
    });
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
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile('c01-01'));
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
