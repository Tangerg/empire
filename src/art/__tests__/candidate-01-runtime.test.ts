import { describe, expect, it } from 'vitest';
import { terrainMarkup } from '../terrain';
import { unitSpriteMarkup } from '../units';

describe('candidate-01 runtime art bindings', () => {
  it('uses the authored four-frame sheet for covered game units', () => {
    const soldier = unitSpriteMarkup('soldier', '#3f7fd8');

    expect(soldier).toContain('data-runtime-raster="unit"');
    expect(soldier).toContain('gray-banner-soldier-walk');
    expect(soldier).toContain('width="128" height="48"');
  });

  it('keeps the programmatic fallback for uncovered units', () => {
    const rogue = unitSpriteMarkup('rogue', '#3f7fd8');

    expect(rogue).toContain('sprite-pixel');
    expect(rogue).not.toContain('data-runtime-raster="unit"');
  });

  it('selects connected terrain by the engine N/E/S/W mask', () => {
    const road = terrainMarkup('road', {
      x: 2,
      y: 3,
      linked: { n: true, e: true, s: false, w: false },
    });

    expect(road).toContain('data-runtime-raster="atlas-cell"');
    expect(road).toContain('/road.png');
    expect(road).toContain('x="-96"');
  });

  it('uses the captured building state and a dynamic owner marker', () => {
    const castle = terrainMarkup('castle', {
      x: 1,
      y: 1,
      ownerColor: '#d8483f',
      linked: { n: false, e: false, s: false, w: false },
    });

    expect(castle).toContain('data-runtime-raster="structure"');
    expect(castle).toContain('/castle-states.png');
    expect(castle).toContain('y="-128"');
    expect(castle).toContain('stroke="#d8483f"');
  });
});
