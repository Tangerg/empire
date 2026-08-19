import { describe, expect, it } from 'vitest';
import { terrainMarkup, unitSpriteMarkup } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { CANDIDATE_01_ART } from './index';

/** The pack's own art, composed rather than registered into a global. */
const art = CANDIDATE_01_ART;
/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);

describe('candidate-01 runtime art bindings', () => {
  it('uses the authored four-frame sheet for covered game units', () => {
    const soldier = unitSpriteMarkup(art, TEST_CATALOG.units.get('c01.swordsman'), '#3f7fd8');

    expect(soldier).toContain('data-runtime-raster="unit"');
    expect(soldier).toContain('unit-swordsman');
    expect(soldier).toContain('width="128" height="48"');
    expect(soldier).toContain('runtime-hd/combat-unit');
    expect(soldier).toContain('&quot;id&quot;:&quot;attack&quot;');
    expect(soldier).toContain('&quot;frames&quot;:[2]');
  });

  it('keeps the programmatic fallback for uncovered units', () => {
    const rogue = unitSpriteMarkup(art, TEST_CATALOG.units.get('rogue'), '#3f7fd8');

    expect(rogue).toContain('sprite-pixel');
    expect(rogue).not.toContain('data-runtime-raster="unit"');
  });

  it('selects connected terrain by the engine N/E/S/W mask', () => {
    const road = terrainMarkup(art, TEST_CATALOG.terrains.get('road'), {
      x: 2,
      y: 3,
      linked: { n: true, e: true, s: false, w: false },
    });

    expect(road).toContain('data-runtime-raster="atlas-cell"');
    expect(road).toContain('/terrain-border-2.png');
    expect(road).toContain('x="-96"');
  });

  it('uses the captured building state and a dynamic owner marker', () => {
    const castle = terrainMarkup(art, TEST_CATALOG.terrains.get('castle'), {
      x: 1,
      y: 1,
      ownerColor: '#d8483f',
      linked: { n: false, e: false, s: false, w: false },
    });

    expect(castle).toContain('data-candidate-art="structure"');
    expect(castle).toContain('/struct-lorne-keep.png');
    expect(castle).toContain('x="-128"');
    expect(castle).toContain('stroke="#d8483f"');
  });
});
