import { describe, expect, it } from 'vitest';
import { candidate01Level } from '../levels';
import { mapFromLevel } from '@empire/battle-engine/level';
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
    const map = mapFromLevel(candidate01Level('c01-01'), TEST_CATALOG);
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
    const map = mapFromLevel(candidate01Level('c01-01'), TEST_CATALOG);
    const viewport = createSceneViewport(map.width, map.height, 32, candidate01SceneProfile('c01-01'));
    const frame = candidate01SceneFrameMarkup('c01-01', map, viewport);

    expect(viewport.sceneWidth).toBeGreaterThan(map.width * 32);
    expect(frame.backdrop).toContain('data-scene-viewport="authored-wide"');
    expect(frame.backdrop).toContain('c01-field-shadow');
    expect(frame.foreground).toContain('is-scene-frame');
  });

  it('does not leak authored Twin Hills dressing into other maps', () => {
    const map = mapFromLevel(candidate01Level('c01-02'), TEST_CATALOG);
    expect(candidate01MapSceneryMarkup('c01-02', map)).toEqual({
      ground: '',
      underUnits: '',
      overUnits: '',
    });
  });
});
