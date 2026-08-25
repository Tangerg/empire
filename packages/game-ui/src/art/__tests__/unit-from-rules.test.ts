import { describe, expect, it } from 'vitest';
import { ContentPackInstaller, createContentCatalog, type UnitDef } from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { GENERIC_ART } from '../direction';
import { unitPicture } from '../units';
import { portraitMarkup } from '../portraits';

/** Composed per suite, exactly like an application composition root. */
function everyShippedUnit(): UnitDef[] {
  const content = createContentCatalog();
  new ContentPackInstaller(content).install(
    COMMON_CONTENT_PACK,
    ANCIENT_EMPIRES_CONTENT_PACK,
    CANDIDATE_01_CONTENT_PACK,
  );
  return [...content.units.all()];
}

const sprite = (unit: UnitDef) => unitPicture(GENERIC_ART, unit, '#3f7fd8').body;
const portrait = (unit: UnitDef) => portraitMarkup(GENERIC_ART, unit, '#3f7fd8');

/** A plain foot soldier, to vary one rule at a time. */
const plain = (over: Partial<UnitDef>): UnitDef => ({
  id: 'probe',
  name: '试验兵',
  value: 100,
  recruitCosts: [],
  resources: {},
  maxHp: 100,
  defense: 0.1,
  movement: 3,
  movementClass: 'foot',
  armorClass: 'light',
  weapons: ['probe-blade'],
  vision: 1,
  abilities: [],
  defaultReaction: 'counter',
  tags: [],
  blurb: '',
  ...over,
});

describe('a unit nobody drew is drawn from what the rules can see', () => {
  it('draws the same portrait deterministically', () => {
    const unit = plain({ id: 'stable-portrait' });
    expect(portrait(unit)).toBe(portrait(unit));
    expect(portraitMarkup(GENERIC_ART, unit, '#3f7fd8'))
      .not.toBe(portraitMarkup(GENERIC_ART, unit, '#d8483f'));
  });

  /**
   * The defect this replaces: `sprites[type] ?? sprites.soldier`.
   *
   * Thirty-one of the forty types this repository ships hit that line, so under
   * generic art the whole roster was one swordsman — including
   * `c01.supply-wagon`, a cart that carries units, drawn as an infantryman.
   */
  it('gives every shipped type a figure and a bust of its own', () => {
    const units = everyShippedUnit();
    for (const [what, draw] of [['sprite', sprite], ['portrait', portrait]] as const) {
      const seen = new Map<string, string>();
      const collisions: string[] = [];
      for (const unit of units) {
        const markup = draw(unit);
        const twin = seen.get(markup);
        if (twin) collisions.push(`${what}: ${twin} and ${unit.id} are drawn identically`);
        seen.set(markup, unit.id);
      }
      expect(collisions).toEqual([]);
      expect(seen.size).toBe(units.length);
    }
    expect(units.length).toBeGreaterThan(30);
  });

  /**
   * Two types alike in every rule this may read.
   *
   * A legion shield and a rune shield are exactly that, and so are a stone golem
   * and a cemetery colossus — drawing them identically is honest and useless,
   * because the player still has to tell them apart. Their names do it.
   */
  it('separates two types that differ only in name', () => {
    expect(sprite(plain({ id: 'alpha' }))).not.toBe(sprite(plain({ id: 'beta' })));
    expect(portrait(plain({ id: 'alpha' }))).not.toBe(portrait(plain({ id: 'beta' })));
  });

  it('reads build, pace and how many arms it carries', () => {
    const base = sprite(plain({ id: 'same' }));
    const heavy = sprite(plain({ id: 'same', maxHp: 220, defense: 0.4 }));
    const quick = sprite(plain({ id: 'same', movement: 9 }));
    const twoArmed = sprite(plain({ id: 'same', weapons: ['a', 'b'] }));
    const unarmed = sprite(plain({ id: 'same', weapons: [] }));

    expect(new Set([base, heavy, quick, twoArmed, unarmed]).size).toBe(5);
  });

  it('says what a unit is for, and that it carries other units', () => {
    const base = sprite(plain({ id: 'same' }));
    const medic = sprite(plain({ id: 'same', abilities: ['heal'] }));
    const taker = sprite(plain({ id: 'same', abilities: ['capture'] }));
    const scout = sprite(plain({ id: 'same', vision: 4 }));
    const cart = sprite(plain({ id: 'same', transport: { capacity: 2 } }));

    expect(new Set([base, medic, taker, scout, cart]).size).toBe(5);
    // The shipped cart is the one type that used to be drawn as a swordsman.
    const wagon = everyShippedUnit().find((unit) => (unit.transport?.capacity ?? 0) > 0)!;
    expect(sprite(wagon)).not.toBe(sprite({ ...wagon, transport: undefined }));
  });

  /**
   * A movement class and an armour class are open strings, so these are families
   * rather than lookups: the shape a family gets is arbitrary, and the grouping
   * is not. Two units that move the same way stand on the same footing whatever
   * the pack calls it.
   */
  it('groups by how a unit moves and how it is armoured, without naming either', () => {
    const foot = sprite(plain({ id: 'same', movementClass: 'foot' }));
    const hover = sprite(plain({ id: 'same', movementClass: 'hover' }));
    const alsoFoot = sprite(plain({ id: 'same', movementClass: 'foot', value: 400 }));
    expect(foot).not.toBe(hover);
    expect(alsoFoot).toBe(foot);

    expect(sprite(plain({ id: 'same', armorClass: 'light' })))
      .not.toBe(sprite(plain({ id: 'same', armorClass: 'chitin' })));
  });

  /** Standing in the way is not every unit's business, and it shows. */
  it('draws a bystander out of uniform', () => {
    expect(sprite(plain({ id: 'same', zoneOfControl: 0 })))
      .not.toBe(sprite(plain({ id: 'same', zoneOfControl: 1 })));
  });
});
