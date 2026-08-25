import { describe, expect, it } from 'vitest';
import type { TerrainDef } from '@empire/battle-engine';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { createTestCatalog } from '@empire/test-content';
import { GENERIC_ART } from '../direction';
import { terrainMarkup } from '../terrain';

/** Composed per suite, exactly like an application composition root. */
function everyShippedTerrain(): TerrainDef[] {
  // Common + Ancient Empires is what `createTestCatalog` installs; the story
  // pack is the extra this suite wants.
  const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
  return [...content.terrains.all()];
}

const CELL = { x: 3, y: 5, linked: { n: false, e: false, s: false, w: false } };
const draw = (terrain: TerrainDef) => terrainMarkup(GENERIC_ART, terrain, CELL);

/** A terrain with nothing remarkable about it, to vary one field at a time. */
const flat = (over: Partial<TerrainDef>): TerrainDef => ({
  id: 'probe',
  name: '试验地',
  cost: { foot: 1, mounted: 1, heavy: 1, flying: 1 },
  defense: 0,
  vision: 0,
  opaque: false,
  cover: 'none',
  obstructionHeight: 0,
  capturable: false,
  ownerTurnGrants: [],
  heal: 0,
  produces: [],
  hq: false,
  tags: [],
  ...over,
});

describe('a terrain nobody drew is drawn from what the rules can see', () => {
  /**
   * The defect this replaces: `painters[id] ?? painters.plain`.
   *
   * Eleven of the shipped terrains had no hand-drawn tile, so molten rock, a
   * graveyard, a river bank and a keep were all rendered as the same meadow —
   * and the map editor, which draws with the generic art, showed the campaign's
   * whole ground that way.
   */
  it('gives every shipped terrain a picture of its own', () => {
    const seen = new Map<string, string>();
    const collisions: string[] = [];
    for (const terrain of everyShippedTerrain()) {
      const markup = draw(terrain);
      const twin = seen.get(markup);
      if (twin) collisions.push(`${twin} and ${terrain.id} are drawn identically`);
      seen.set(markup, terrain.id);
    }

    expect(seen.size).toBeGreaterThan(20);
    expect(collisions).toEqual([]);
  });

  /**
   * Two terrains a pack invented on the same afternoon, alike in every rule.
   *
   * They must still be told apart, or a level made of both reads as one field —
   * which is the old defect in miniature.
   */
  it('separates two terrains that differ only in name', () => {
    expect(draw(flat({ id: 'ash' }))).not.toBe(draw(flat({ id: 'silt' })));
  });

  it('draws what may be crossed differently from what may not', () => {
    const open = draw(flat({ id: 'same' }));
    const sealed = draw(flat({ id: 'same', cost: { foot: null, mounted: null, heavy: null, flying: null } }));
    const specialised = draw(flat({ id: 'same', cost: { foot: null, mounted: null, heavy: null, flying: 1 } }));
    const slow = draw(flat({ id: 'same', cost: { foot: 3, mounted: 3, heavy: 3, flying: 1 } }));

    expect(new Set([open, sealed, specialised, slow]).size).toBe(4);
  });

  it('shows a holding, and whose it is', () => {
    const neutral = draw(flat({ id: 'post', capturable: true }));
    const held = terrainMarkup(GENERIC_ART, flat({ id: 'post', capturable: true }), { ...CELL, ownerColor: '#d8483f' });

    expect(neutral).not.toContain('#d8483f');
    expect(held).toContain('#d8483f');
    // And a plain field flies no flag at all.
    expect(draw(flat({ id: 'post' }))).not.toContain('#d8483f');
  });

  it('builds something on a tile that recruits or must be defended', () => {
    const bare = draw(flat({ id: 'yard' }));
    const site = draw(flat({ id: 'yard', produces: ['anything'] }));
    const keep = draw(flat({ id: 'yard', hq: true }));

    expect(site).not.toBe(bare);
    expect(keep).not.toBe(site);
  });

  /**
   * The engine's movement classes are open strings, so this painter may not look
   * for `foot`. A pack that ships nothing but `hover` and `burrow` gets the same
   * reading as one that ships the usual four.
   */
  it('reads the shape of the cost table rather than the names in it', () => {
    const usual = draw(flat({ id: 'x', cost: { foot: null, mounted: null, heavy: null, flying: 1 } }));
    const invented = draw(flat({ id: 'x', cost: { burrow: null, hover: null, phase: null, drift: 1 } }));

    expect(invented).toBe(usual);
  });
});
