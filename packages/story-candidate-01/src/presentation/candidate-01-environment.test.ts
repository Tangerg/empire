import { describe, expect, it } from 'vitest';
import { EnvironmentCatalog, type EnvironmentManifest } from '@empire/game-ui';
import {
  CANDIDATE_01_ENVIRONMENT,
  candidate01EnvironmentScene,
} from './candidate-01-environment';

describe('candidate-01 environment builder v1.1', () => {
  it('retains registered pivots and semantic footprints for approved forest cells', () => {
    const contracts = [
      { id: 'pine-low-shrub', foot: [610, 913], scale: 0.62, footprint: [1, 1], obstruction: 0 },
      { id: 'pine-fallen-log', foot: [635, 815], scale: 0.7, footprint: [2, 1], obstruction: 1 },
      { id: 'pine-stump', foot: [642, 879], scale: 0.68, footprint: [1, 1], obstruction: 0 },
      { id: 'pine-mossy-rock', foot: [630, 915], scale: 0.67, footprint: [1, 1], obstruction: 1 },
      { id: 'pine-mature-a', foot: [630, 1118], scale: 1.5, footprint: [1, 1], obstruction: 2 },
      { id: 'pine-mature-b', foot: [638, 1078], scale: 1.65, footprint: [1, 1], obstruction: 2 },
      { id: 'pine-dead', foot: [655, 1107], scale: 1.2, footprint: [1, 1], obstruction: 2 },
      { id: 'pine-mature-c', foot: [643, 1116], scale: 1.25, footprint: [1, 1], obstruction: 2 },
      { id: 'pine-underbrush-a', foot: [638, 1010], scale: 0.8, footprint: [1, 1], obstruction: 0 },
      { id: 'pine-underbrush-b', foot: [622, 914], scale: 0.65, footprint: [1, 1], obstruction: 0 },
      { id: 'pine-underbrush-c', foot: [610, 927], scale: 1, footprint: [1, 1], obstruction: 0 },
      { id: 'pine-underbrush-d', foot: [608, 884], scale: 0.75, footprint: [1, 1], obstruction: 0 },
      { id: 'pine-tall', foot: [640, 1165], scale: 1.2, footprint: [1, 1], obstruction: 2 },
      { id: 'pine-pair', foot: [650, 1050], scale: 1.4, footprint: [2, 1], obstruction: 2 },
      { id: 'pine-cluster', foot: [635, 1065], scale: 1.5, footprint: [2, 2], obstruction: 2 },
      { id: 'pine-sapling', foot: [644, 1085], scale: 0.7, footprint: [1, 1], obstruction: 1 },
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
      { id: 'mixed-forest-autumn', foot: [640, 1090], scale: 1.5, footprint: [2, 2], obstruction: 2 },
      { id: 'sapling-rock-cluster', foot: [650, 1060], scale: 0.9, footprint: [1, 1], obstruction: 1 },
      { id: 'stump-low', foot: [627, 880], scale: 0.8, footprint: [1, 1], obstruction: 0 },
      { id: 'forest-floor-cluster', foot: [628, 750], scale: 0.65, footprint: [1, 1], obstruction: 0 },
    ];
    for (const { id, foot, scale, footprint, obstruction } of contracts) {
      const { cell, atlas } = CANDIDATE_01_ENVIRONMENT.cell(id);
      expect(cell.anchor?.[0]).toBeCloseTo(5 + foot[0] * 86 / 1254, 12);
      expect(cell.anchor?.[1]).toBeCloseTo(5 + foot[1] * 86 / 1254, 12);
      expect(cell.presentationScale).toBe(scale);
      expect(cell.footprint).toEqual(footprint);
      expect(cell.obstructionHeight).toBe(obstruction);
      expect(atlas.raster).toMatchObject({ cellWidth: 96, cellHeight: 96, columns: 4, rows: 4 });
      expect(atlas.raster.href).toContain(`${id.startsWith('pine-') ? 'forest-existing' : 'forest-temperate'}@2x.png`);
    }
    expect(CANDIDATE_01_ENVIRONMENT.cell('grave-cross-cluster').cell.presentationScale).toBeUndefined();
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
