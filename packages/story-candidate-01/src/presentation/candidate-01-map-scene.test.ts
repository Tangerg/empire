import { TacticalGrids, mapFromLevel } from '@empire/battle-engine';
const SQUARE = TacticalGrids.get('square4');
import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { candidate01Level } from '../levels';
import {
  candidate01MapSceneryMarkup,
  candidate01SceneFrameMarkup,
  candidate01SceneProfile,
} from './candidate-01-map-scene';
import { candidate01EnvironmentScene } from './candidate-01-environment';
import { createSceneViewport } from '@empire/game-ui';

import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);

describe('candidate-01 authored map scenery', () => {
  it('composes high-resolution surfaces, connected roads and authored regions for Twin Hills', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    expect(candidate01EnvironmentScene('c01-01')?.mapSize).toEqual([map.width, map.height]);
    const layers = candidate01MapSceneryMarkup('c01-01', map);
    const markup = `${layers.ground}${layers.underUnits}${layers.overUnits}`;

    expect(markup).toContain('pointer-events="none"');
    expect(markup).toContain('data-runtime-raster="atlas-cell"');
    expect(layers.ground).toContain('candidate-ground-route');
    expect(layers.ground).toContain('candidate-ground-route-edge');
    expect(layers.ground).toContain('surface-meadow');
    expect(layers.ground).toContain('data-depth="ground"');
    expect(layers.underUnits).toContain('data-depth="under-units"');
    expect(layers.overUnits).toContain('data-depth="over-units"');
    expect(markup).toContain('temperate-hill-cap');
    expect(markup).toContain('gray-camp-ground');
    expect(markup).toContain('village-square-foundation');
    expect(markup).toContain('frontier-farmhouse');
    expect(markup).toContain('C01-MISSION-BORDER-FARMER');
    expect(markup.match(/candidate-environment-prop/g)?.length ?? 0).toBeGreaterThan(20);
  });

  it('authors a non-playable forest frame outside the logical cells', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-01'));
    const viewport = createSceneViewport(SQUARE, map.width, map.height, 32, candidate01SceneProfile('c01-01'));
    const frame = candidate01SceneFrameMarkup('c01-01', map, viewport);

    expect(viewport.sceneWidth).toBeGreaterThan(map.width * 32);
    expect(frame.backdrop).toContain('data-scene-viewport="authored-wide"');
    expect(frame.backdrop).toContain('c01-field-shadow');
    expect(frame.foreground).toContain('is-scene-frame');
  });

  it('does not leak authored Twin Hills dressing into other maps', () => {
    const map = mapFromLevel(TEST_CATALOG, candidate01Level('c01-02'));
    expect(candidate01MapSceneryMarkup('c01-02', map)).toEqual({
      ground: '',
      underUnits: '',
      overUnits: '',
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
    const painted = candidate01SceneFrameMarkup('c01-01', map, viewport);
    const plain = candidate01SceneFrameMarkup('c01-09', map, viewport);

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
