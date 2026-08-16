import { GENERIC_ART } from '../direction';
import { describe, expect, it } from 'vitest';
import { emptyLevel, mapFromLevel } from '@empire/battle-engine';
import { battlefieldFeatureMarkup, battlefieldRenderKey } from '../battlefield-layer';
import { squareLayout, type BoardLayout } from '../board-decorations';

import { createTestCatalog } from '@empire/test-content';

/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog();

describe('shared battlefield feature layer', () => {
  it('renders elevation, cliffs and directional cover through one adapter', () => {
    const map = mapFromLevel(TEST_CATALOG, emptyLevel(TEST_CATALOG, 4, 4));
    map.elevation[0] = 2;
    map.cliffs.push({ from: { x: 0, y: 0 }, to: { x: 1, y: 0 } });
    map.directionalCover.push({ at: { x: 1, y: 1 }, sides: { north: 'half', east: 'full' } });

    const markup = battlefieldFeatureMarkup({ art: GENERIC_ART, layout: squareLayout }, map);
    expect(markup).toContain('>2</text>');
    expect(markup).toContain('#f0b24f');
    expect(markup).toContain('#4f9bc7');
    expect(markup).toContain('#d85c4c');
  });

  it('changes its cache key for every visual map feature', () => {
    const map = mapFromLevel(TEST_CATALOG, emptyLevel(TEST_CATALOG, 4, 4));
    const initial = battlefieldRenderKey(map);
    map.owners[0] = 1;
    const ownership = battlefieldRenderKey(map);
    map.elevation[0] = 1;
    const elevation = battlefieldRenderKey(map);
    map.directionalCover.push({ at: { x: 0, y: 0 }, sides: { south: 'half' } });

    expect(new Set([initial, ownership, elevation, battlefieldRenderKey(map)]).size).toBe(4);
  });

  /**
   * The layer that stayed square.
   *
   * Terrain, units, grid lines and move range all moved to where the tiling puts
   * its cells; height badges, cliff marks and cover edges kept drawing at
   * `x * TILE`. A board whose cells are anywhere else got the right pictures in
   * the wrong places, and its cover was cached under a key that ignored it.
   */
  it('draws its furniture wherever the tiling puts the cells', () => {
    const map = mapFromLevel(TEST_CATALOG, emptyLevel(TEST_CATALOG, 4, 4));
    map.elevation[5] = 3;
    map.directionalCover.push({ at: { x: 1, y: 1 }, sides: { hexEast: 'full' } });
    // A tiling whose cells sit nowhere near `x * TILE`, and whose facings are
    // not the square board's four.
    const elsewhere: BoardLayout = {
      tileSize: 32,
      corners: 6,
      origin: (at) => ({ x: at.x * 40 + 500, y: at.y * 28 }),
      center: (at) => ({ x: at.x * 40 + 520, y: at.y * 28 + 14 }),
      outline: () => '',
      neighbour: (at) => ({ x: at.x * 40 + 600, y: at.y * 28 + 14 }),
    };

    const square = battlefieldFeatureMarkup({ art: GENERIC_ART, layout: squareLayout }, map);
    const hex = battlefieldFeatureMarkup({ art: GENERIC_ART, layout: elsewhere }, map);

    // The square layout has no `hexEast`, so its own neighbour lookup lands on
    // the cell itself and there is no edge to draw; the six-sided one draws it.
    expect(square).not.toContain('#d85c4c');
    expect(hex).toContain('#d85c4c');
    expect(hex).toContain('cx="565.92"');
    expect(battlefieldRenderKey(map)).toContain('hexEast=full');
  });

  it('labels a continuous plateau once instead of covering every elevated cell', () => {
    const map = mapFromLevel(TEST_CATALOG, emptyLevel(TEST_CATALOG, 3, 2));
    map.elevation = [2, 2, 0, 2, 2, 1];

    const markup = battlefieldFeatureMarkup({ art: GENERIC_ART, layout: squareLayout }, map);
    expect(markup.match(/class="elevation-badge"/g)).toHaveLength(2);
  });
});
