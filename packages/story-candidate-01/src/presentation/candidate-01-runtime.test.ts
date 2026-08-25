import { describe, expect, it } from 'vitest';
import { terrainMarkup, unitPicture } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { CANDIDATE_01_ART } from './index';

/** The pack's own art, composed rather than registered into a global. */
const art = CANDIDATE_01_ART;
/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);

describe('candidate-01 runtime art bindings', () => {
  it('uses the authored four-frame sheet for covered game units', () => {
    const soldier = unitPicture(art, TEST_CATALOG.units.get('c01.swordsman'), '#3f7fd8');

    expect(soldier.body).toContain('data-runtime-raster="unit"');
    expect(soldier.strip!.href).toContain('unit-swordsman');
    expect(soldier.strip!.href).toContain('runtime-hd/combat-unit');
    expect(soldier.strip).toMatchObject({ frameCount: 4, frameWidth: 32, frameHeight: 48 });
    // The manifest's frame order is what names them: idle, walk-a, attack, walk-b.
    expect(soldier.strip!.clips.find((clip) => clip.id === 'attack')!.frames).toEqual([2]);
    expect(soldier.strip!.clips.find((clip) => clip.id === 'walk')!.frames).toEqual([1, 3]);
  });

  /**
   * A type this pack has no sheet for is drawn by something else, and stands still.
   *
   * This used to ask about `rogue`, one of the nine types the built-in levels are
   * fought with — and the pack ships a 刺客 sheet, so what the test recorded was
   * that nobody had bound it rather than that the fallback works. A type nobody has
   * ever drawn is the honest subject: no sheet means no strip, so there is nothing
   * to play, and the figure is generated from what the rules say about it.
   */
  it('keeps the programmatic fallback for uncovered units', () => {
    const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    catalog.units.define({ ...catalog.units.get('soldier'), id: 'probe.militia', name: '民兵' });
    const probe = unitPicture(art, catalog.units.get('probe.militia'), '#3f7fd8');

    expect(probe.body).not.toContain('data-runtime-raster');
    expect(probe.body.length).toBeGreaterThan(80);
    expect(probe.strip).toBeUndefined();
  });

  /**
   * This used to assert the opposite: that a road cell came back as one cell of
   * `terrain-border-2.png`, picked by the N/E/S/W mask.
   *
   * That was the second answer to "what is this cell's ground made of". It stamped
   * one four-variant tile per cell with no transitions and — for everything but the
   * roads — no connections either, and it ran on every level except chapter one.
   * The scene's material table owns the question now, so this painter has nothing
   * to say about the ground: empty, not `null`, because `null` would send the
   * generic painter in to draw a tile underneath the painted one.
   */
  it('says nothing about ground the scene paints', () => {
    for (const id of ['road', 'plain', 'water', 'forest', 'c01.street'] as const) {
      const markup = terrainMarkup(art, TEST_CATALOG.terrains.get(id), {
        x: 2,
        y: 3,
        linked: { n: true, e: true, s: false, w: false },
      });
      expect(markup, id).toBe('');
    }
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
