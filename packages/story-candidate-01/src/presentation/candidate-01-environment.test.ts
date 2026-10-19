import { describe, expect, it } from 'vitest';
import { EnvironmentCatalog, type EnvironmentManifest } from '@empire/game-ui';
import {
  CANDIDATE_01_ENVIRONMENT,
  candidate01EnvironmentScene,
} from './candidate-01-environment';

describe('candidate-01 environment builder v1.1', () => {
  it('retains registered pivots and semantic footprints for approved forest cells', () => {
    const contracts = [
      { id: 'oak-ancient', foot: [640, 1150], scale: 1.375, footprint: [2, 2], obstruction: 2 },
      { id: 'mixed-forest-dense', foot: [640, 1145], scale: 1.4375, footprint: [2, 2], obstruction: 2 },
      { id: 'fern-bed', foot: [625, 955], scale: 0.65, footprint: [1, 1], obstruction: 0 },
      { id: 'boulder-large', foot: [640, 1048], scale: 0.6, footprint: [1, 1], obstruction: 1 },
      { id: 'oak-grove-dense', foot: [625, 1090], scale: 1.625, footprint: [2, 2], obstruction: 2 },
      { id: 'mixed-forest-edge', foot: [640, 1100], scale: 1.5, footprint: [2, 1], obstruction: 2 },
      { id: 'stump-hollow', foot: [625, 995], scale: 0.75, footprint: [1, 1], obstruction: 0 },
      { id: 'fallen-hollow-log', foot: [640, 875], scale: 0.7, footprint: [2, 1], obstruction: 1 },
      { id: 'bramble-dark', foot: [625, 990], scale: 0.8, footprint: [1, 1], obstruction: 0 },
      { id: 'bramble-berries', foot: [625, 960], scale: 0.85, footprint: [1, 1], obstruction: 0 },
      { id: 'leaf-pile', foot: [628, 680], scale: 0.4, footprint: [1, 1], obstruction: 0 },
      { id: 'boulder-ferns', foot: [625, 1015], scale: 0.65, footprint: [1, 1], obstruction: 1 },
    ];
    for (const { id, foot, scale, footprint, obstruction } of contracts) {
      const { cell, atlas } = CANDIDATE_01_ENVIRONMENT.cell(id);
      expect(cell.anchor?.[0]).toBeCloseTo(5 + foot[0] * 86 / 1254, 12);
      expect(cell.anchor?.[1]).toBeCloseTo(5 + foot[1] * 86 / 1254, 12);
      expect(cell.presentationScale).toBe(scale);
      expect(cell.footprint).toEqual(footprint);
      expect(cell.obstructionHeight).toBe(obstruction);
      expect(atlas.raster).toMatchObject({ cellWidth: 96, cellHeight: 96, columns: 4, rows: 4 });
      expect(atlas.raster.href).toContain('forest-temperate@2x.png');
    }
    expect(CANDIDATE_01_ENVIRONMENT.cell('mixed-forest-autumn').cell.anchor).toEqual([48, 91]);
    expect(CANDIDATE_01_ENVIRONMENT.cell('mixed-forest-autumn').cell.presentationScale).toBeUndefined();
  });

  it('rejects invalid presentation scales at the catalog boundary', () => {
    const manifest = (presentationScale: number): EnvironmentManifest => ({ atlases: [{
      id: 'probe', category: 'forest-scenery', png: 'probe.png', columns: 1, rows: 1,
      cellWidth: 96, cellHeight: 96, componentCount: 1,
      cells: [{ id: 'fern', anchor: [47.86, 70.49], presentationScale }],
    }] });
    expect(() => new EnvironmentCatalog(manifest(0.65), () => 'probe.png')).not.toThrow();
    for (const value of [0, -1, Number.NaN, Infinity]) {
      expect(() => new EnvironmentCatalog(manifest(value), () => 'probe.png')).toThrow('finite and positive');
    }
  });

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
