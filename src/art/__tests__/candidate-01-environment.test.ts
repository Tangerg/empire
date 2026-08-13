import { describe, expect, it } from 'vitest';
import {
  CANDIDATE_01_ENVIRONMENT_PACK,
  candidate01EnvironmentAtlas,
  candidate01EnvironmentCell,
  candidate01EnvironmentScene,
  environmentVariantIndex,
  resolveCandidate01TerrainVisual,
} from '../candidate-01-environment';

describe('candidate-01 environment builder v1.1', () => {
  it('exposes four variants per road mask and a separate route-edge layer', () => {
    const road = candidate01EnvironmentAtlas('route-dirt-road');
    const edge = candidate01EnvironmentAtlas('route-edge-dirt-road');

    expect(road.variantsPerMask).toBe(4);
    expect(road.componentCount).toBe(64);
    expect(edge.category).toBe('route-edge');
    expect(edge.componentCount).toBe(64);
    expect(road.url).toContain('route-dirt-road.png');
  });

  it('resolves semantic cells instead of numeric module names', () => {
    const camp = candidate01EnvironmentCell('gray-camp-ground');
    const ramp = candidate01EnvironmentCell('temperate-ramp-level-1');
    const wheat = candidate01EnvironmentCell('grain-drying-mat-linen');

    expect(camp.cell.footprint).toEqual([4, 3]);
    expect(ramp.cell.passable).toBe(true);
    expect(ramp.cell.heightDelta).toBe(1);
    expect(wheat.atlas.id).toBe('rural-life');
  });

  it('keeps Twin Hills on a small allowlist and leaves the candidate pack unpromoted', () => {
    const scene = candidate01EnvironmentScene('c01-01');

    expect(scene?.selection.allowAtlases.length).toBeLessThanOrEqual(10);
    expect(scene?.selection.denyAtlases).toContain('landmarks-large');
    expect(scene?.runtimeReady).toBe(false);
    expect(CANDIDATE_01_ENVIRONMENT_PACK.runtimeReady).toBe(false);
  });

  it('selects deterministic variants without changing the connection mask', () => {
    const a = environmentVariantIndex(10, 4, 7, 5, 31);
    const b = environmentVariantIndex(10, 4, 7, 5, 31);
    const c = environmentVariantIndex(10, 4, 8, 5, 31);

    expect(a).toBe(b);
    expect(Math.floor(a / 4)).toBe(10);
    expect(Math.floor(c / 4)).toBe(10);
  });

  it('resolves a matching road shoulder without changing the logical mask', () => {
    const visual = resolveCandidate01TerrainVisual('route-dirt-road', 6, 9, 5, 17);

    expect(Math.floor(visual.cellIndex / 4)).toBe(6);
    expect(visual.overlays).toHaveLength(1);
    expect(visual.overlays[0].atlas.id).toBe('route-edge-dirt-road');
    expect(visual.overlays[0].cellIndex).toBe(visual.cellIndex);
  });

  it('rejects invalid connection masks before selecting a tile', () => {
    expect(() => resolveCandidate01TerrainVisual('route-dirt-road', 16, 2, 3)).toThrow('invalid environment mask');
  });
});
