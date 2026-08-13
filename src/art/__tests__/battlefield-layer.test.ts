import { describe, expect, it } from 'vitest';
import { emptyLevel, mapFromLevel } from '../../core/mapio';
import { battlefieldFeatureMarkup, battlefieldRenderKey } from '../battlefield-layer';

describe('shared battlefield feature layer', () => {
  it('renders elevation, cliffs and directional cover through one adapter', () => {
    const map = mapFromLevel(emptyLevel(4, 4));
    map.elevation[0] = 2;
    map.cliffs.push({ from: { x: 0, y: 0 }, to: { x: 1, y: 0 } });
    map.directionalCover.push({ at: { x: 1, y: 1 }, sides: { north: 'half', east: 'full' } });

    const markup = battlefieldFeatureMarkup(map);
    expect(markup).toContain('>2</text>');
    expect(markup).toContain('#f0b24f');
    expect(markup).toContain('#4f9bc7');
    expect(markup).toContain('#d85c4c');
  });

  it('changes its cache key for every visual map feature', () => {
    const map = mapFromLevel(emptyLevel(4, 4));
    const initial = battlefieldRenderKey(map);
    map.owners[0] = 1;
    const ownership = battlefieldRenderKey(map);
    map.elevation[0] = 1;
    const elevation = battlefieldRenderKey(map);
    map.directionalCover.push({ at: { x: 0, y: 0 }, sides: { south: 'half' } });

    expect(new Set([initial, ownership, elevation, battlefieldRenderKey(map)]).size).toBe(4);
  });

  it('labels a continuous plateau once instead of covering every elevated cell', () => {
    const map = mapFromLevel(emptyLevel(3, 2));
    map.elevation = [2, 2, 0, 2, 2, 1];

    const markup = battlefieldFeatureMarkup(map);
    expect(markup.match(/class="elevation-badge"/g)).toHaveLength(2);
  });
});
