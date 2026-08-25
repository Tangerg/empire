import { describe, expect, it } from 'vitest';
import {
  CANDIDATE_01_ENVIRONMENT,
  candidate01EnvironmentScene,
} from './candidate-01-environment';

describe('candidate-01 environment builder v1.1', () => {
  it('exposes four variants per road mask and a separate route-edge layer', () => {
    const road = CANDIDATE_01_ENVIRONMENT.atlas('route-dirt-road');
    const edge = CANDIDATE_01_ENVIRONMENT.atlas('route-edge-dirt-road');

    expect(road.variantsPerMask).toBe(4);
    expect(road.componentCount).toBe(64);
    expect(edge.category).toBe('route-edge');
    expect(edge.componentCount).toBe(64);
    expect(road.url).toContain('route-dirt-road.png');
  });

  it('resolves semantic cells instead of numeric module names', () => {
    const camp = CANDIDATE_01_ENVIRONMENT.cell('gray-camp-ground');
    const ramp = CANDIDATE_01_ENVIRONMENT.cell('temperate-ramp-level-1');
    const wheat = CANDIDATE_01_ENVIRONMENT.cell('grain-drying-mat-linen');

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
  });
});
