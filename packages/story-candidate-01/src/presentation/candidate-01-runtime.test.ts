import { describe, expect, it } from 'vitest';
import { portraitMarkup, terrainMarkup, unitPicture } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { CANDIDATE_01_ART } from './index';
import { candidate01UnitIcon, candidate01CoverPropMarkup } from './candidate-01-runtime';

/** The pack's own art, composed rather than registered into a global. */
const art = CANDIDATE_01_ART;
/** Composed per suite, exactly like an application composition root. */
const TEST_CATALOG = createTestCatalog(CANDIDATE_01_CONTENT_PACK);

describe('candidate-01 runtime art bindings', () => {
  it('uses the authored four-frame sheet for covered game units', () => {
    const soldier = unitPicture(art, TEST_CATALOG.units.get('c01.swordsman'), '#3f7fd8');

    expect(soldier.body).toContain('data-runtime-raster="unit"');
    expect(soldier.strip!.href).toContain('unit-swordsman');
    expect(soldier.strip!.href).toContain('runtime-approved-v5/combat-unit/unit-swordsman@2x.png');
    expect(soldier.strip).toMatchObject({ frameCount: 4, frameWidth: 96, frameHeight: 64 });
    expect(soldier.strip!.x).toBeCloseTo(16 - 48.44444444444444, 12);
    expect(soldier.strip!.y).toBeCloseTo(31 - 53.27777777777778, 12);
    // The manifest's frame order is what names them: idle, walk-a, attack, walk-b.
    expect(soldier.strip!.clips.find((clip) => clip.id === 'attack')!.frames).toEqual([2]);
    expect(soldier.strip!.clips.find((clip) => clip.id === 'walk')!.frames).toEqual([1, 3]);
    expect(soldier.strip!.clips.find((clip) => clip.id === 'idle')!.frames).toEqual([0]);
  });

  it('fits icons and portraits to the approved frame instead of clipping them to 32×48', () => {
    const icon = candidate01UnitIcon('c01.swordsman', '#3f7fd8', 48);
    expect(icon).toContain('viewBox="-32.44444444444444 -22.27777777777778 96 64"');
    const portrait = portraitMarkup(art, TEST_CATALOG.units.get('c01.swordsman'), '#3f7fd8');
    expect(portrait).toContain('translate(32.44444444444444 46.27777777777778) scale(1)');
    expect(portrait).toContain('viewBox="0 0 96 64"');
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

  it('uses the captured building state and flies the owner\'s colours', () => {
    const keep = (ownerColor?: string) => terrainMarkup(art, TEST_CATALOG.terrains.get('castle'), {
      x: 1,
      y: 1,
      ...(ownerColor === undefined ? {} : { ownerColor }),
      linked: { n: false, e: false, s: false, w: false },
    });
    const castle = keep('#d8483f');

    expect(castle).toContain('data-candidate-art="structure"');
    expect(castle).toContain('runtime-approved-v5/structure/struct-lorne-keep@2x.png');
    expect(castle).toContain('x="-320"');
    expect(castle).toContain('width="480" height="160"');
    expect(castle).toContain('viewBox="0 0 160 160"');
    expect(castle).toContain('transform="translate(-64.60847880299252 -109.21945137157107)"');
    expect(keep()).toContain('x="0" y="0" width="480" height="160"');

    /*
     * A standard, not a ring under the roof.
     *
     * Ownership used to be a thin ellipse on the ground, which says "owned" only to
     * someone already looking at that tile. What the marker has to be is *the
     * owner's*, so this asks the question that matters: the same keep under two
     * holders is drawn two different ways, and neither is the unowned one.
     */
    expect(castle).toContain('candidate-owner-banner');
    expect(castle).toContain('#d8483f');
    expect(castle).not.toBe(keep('#3f7fd8'));
    expect(keep()).not.toContain('candidate-owner-banner');
  });
});


describe('registered cover proportions', () => {
  it('grounds half and full cover at the same tactical foot while retaining distinct visual heights', () => {
    const half = candidate01CoverPropMarkup('half');
    const full = candidate01CoverPropMarkup('full');
    expect(half).toContain('width="38.4" height="38.4"');
    expect(full).toContain('width="76.8" height="76.8"');
    const y = (markup: string) => Number(markup.match(/ y="([^"]+)"/)![1]);
    expect(y(half) + 58 * .6).toBeCloseTo(31);
    expect(y(full) + 49 * 1.2).toBeCloseTo(31);
    expect(half).toContain('bprop-cover-1.png');
    expect(full).toContain('bprop-cover-2.png');
  });
});
