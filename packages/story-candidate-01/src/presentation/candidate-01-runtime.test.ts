import { describe, expect, it } from 'vitest';
import { portraitMarkup, terrainMarkup, unitPicture } from '@empire/game-ui';
import { createTestCatalog } from '@empire/test-content';
import { CANDIDATE_01_CONTENT_PACK } from '../index';
import { CANDIDATE_01_ART } from './index';
import { CANDIDATE_01_PORTRAITS } from './candidate-01-story';
import { CANDIDATE_01_CHARACTER_ART } from './candidate-01-bindings';
import { candidate01Asset } from './candidate-01-assets';
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

  it('keeps Laiya named card and four-pose appearance separate from the generic banner guard', () => {
    const named = unitPicture(art, TEST_CATALOG.units.get('c01.laiya'), '#a8493f');
    const generic = unitPicture(art, TEST_CATALOG.units.get('c01.banner-guard'), '#a8493f');
    expect(named.strip?.href).toContain('hero-appearance/laiya-stage01@2x.png');
    expect(generic.strip?.href).toContain('combat-unit/unit-banner-guard@2x.png');
    expect(named.strip?.frameCount).toBe(4);
    expect(named.strip?.clips.find(clip => clip.id === 'walk')?.frames).toEqual([1, 3]);
    expect(CANDIDATE_01_CHARACTER_ART['c01.laiya']).toBe('C01-CHAR-LEIA-01');
    const card = candidate01Asset('C01-CHAR-LEIA-01');
    expect(card.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect([card.width, card.height]).toEqual([192, 224]);
    expect(CANDIDATE_01_PORTRAITS.laiya).toBe(card.url);
    expect(portraitMarkup(art, TEST_CATALOG.units.get('c01.laiya'), '#a8493f')).toContain(card.url);
    expect(portraitMarkup(art, TEST_CATALOG.units.get('c01.laiya'), '#a8493f')).not.toContain('unit-banner-guard');
  });

  it('binds Roderick to the reviewed mounted named appearance and transparent-source card', () => {
    const rule = TEST_CATALOG.units.get('c01.roderick');
    const named = unitPicture(art, rule, '#4d86b8');
    const generic = candidate01Asset('C01-UNIT-HEAVY-KNIGHT');
    expect(named.strip?.href).toContain('hero-appearance/roderick-stage01@2x.png');
    expect(named.strip).toMatchObject({ frameCount: 4, frameWidth: 128, frameHeight: 80 });
    expect(named.strip?.clips.find(clip => clip.id === 'walk')?.frames).toEqual([1, 3]);
    expect(generic.url).toContain('combat-unit/unit-heavy-knight@2x.png');
    expect(CANDIDATE_01_CHARACTER_ART['c01.roderick']).toBe('C01-CHAR-RODERICK-01');
    const card = candidate01Asset('C01-CHAR-RODERICK-01');
    expect(card.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect([card.width, card.height]).toEqual([192, 224]);
    expect(CANDIDATE_01_PORTRAITS.roderick).toBe(card.url);
    expect(portraitMarkup(art, rule, '#4d86b8')).toContain(card.url);
  });

  it('binds Mirelle to the reviewed lantern-bearing named appearance and transparent-source card', () => {
    const rule = TEST_CATALOG.units.get('c01.mirelle');
    const named = unitPicture(art, rule, '#4d86b8');
    const generic = candidate01Asset('C01-UNIT-GRAVEKEEPER');
    expect(named.strip?.href).toContain('hero-appearance/mirelle-stage01@2x.png');
    expect(named.strip).toMatchObject({ frameCount: 4, frameWidth: 104, frameHeight: 64 });
    expect(named.strip?.clips.find(clip => clip.id === 'walk')?.frames).toEqual([1, 3]);
    expect(generic.url).toContain('combat-unit/unit-gravekeeper@2x.png');
    expect(CANDIDATE_01_CHARACTER_ART['c01.mirelle']).toBe('C01-CHAR-MIREL-01');
    const card = candidate01Asset('C01-CHAR-MIREL-01');
    expect(card.qualityTier).toBe('approved-chunky-v5-export-verified');
    expect([card.width, card.height]).toEqual([192, 224]);
    expect(CANDIDATE_01_PORTRAITS.mirelle).toBe(card.url);
    expect(portraitMarkup(art, rule, '#4d86b8')).toContain(card.url);
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
    const record = candidate01Asset('C01-STRUCT-LORNE-KEEP');
    const scale = record.presentationScale!;
    expect(scale).toBe(0.65);
    expect(castle).toContain(`transform="translate(${16 - record.anchor![0] * scale} ${31 - record.anchor![1] * scale}) scale(${scale})"`);
    expect(record.footprint).toEqual([2, 2]);
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


describe('authored team palette selection', () => {
  it('selects the red authored sequence and its own pivot without changing unit rules', () => {
    const def = TEST_CATALOG.units.get('c01.swordsman');
    const red = unitPicture(art, def, '#a8493f');
    const blue = unitPicture(art, def, '#4d86b8');
    const other = unitPicture(art, def, '#ffffff');
    expect(red.strip!.href).toContain('unit-swordsman-red@2x.png');
    expect(blue.strip!.href).toContain('unit-swordsman@2x.png');
    expect(other.strip!.href).toBe(blue.strip!.href);
    expect(red.strip!.x).not.toBe(blue.strip!.x);
    expect(red.strip!.clips).toEqual(blue.strip!.clips);
    expect(red.strip!.tint).toBeUndefined();
    expect(blue.strip!.tint).toBeUndefined();
    expect(red.body).toContain('#a8493f');
    expect(blue.body).toContain('#4d86b8');
    expect(unitPicture(art, def, '#A8493F').strip!.href).toBe(red.strip!.href);
  });
});


describe('native status icon consumers', () => {
  it('keeps status meanings and32px contracts through the actual HUD provider', () => {
    const bindings = { poisoned: '01', silenced: '02', 'c01.oath-controlled': '04', shaken: '05', inspired: '12', armor_down: '08' };
    for (const [status, slot] of Object.entries(bindings)) {
      const markup = art.resolve(provider => provider.statusIcon?.(status));
      expect(markup, status).toContain(`runtime/status/status-${slot}.png`);
      const asset = candidate01Asset(`C01-STATUS-${slot}`);
      expect(asset.width).toBe(32);
      expect(asset.height).toBe(32);
      expect(asset.anchor).toEqual([16, 16]);
    }
    expect(art.resolve(provider => provider.statusIcon?.('not-a-status'))).toBeNull();
  });
});
