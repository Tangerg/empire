import { describe, expect, it } from 'vitest';
import {
  ContentPackInstaller,
  createContentCatalog,
  type BattlefieldMarker,
  type StructureDef,
  type StructureState,
} from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';
import { GENERIC_ART } from '../direction';
import { markerFromRules, structureFromRules } from '../field-objects-from-rules';

/** Composed per suite, exactly like an application composition root. */
function shippedStructures(): StructureDef[] {
  const content = createContentCatalog();
  new ContentPackInstaller(content).install(
    COMMON_CONTENT_PACK,
    ANCIENT_EMPIRES_CONTENT_PACK,
    CANDIDATE_01_CONTENT_PACK,
  );
  return [...content.structures.all()];
}

const standing = (definition: StructureDef, over: Partial<StructureState> = {}): StructureState => ({
  id: 'probe',
  type: definition.id,
  owner: 1,
  x: 0,
  y: 0,
  hp: definition.maxHp,
  disabled: false,
  statuses: [],
  ...over,
});

const mark = (kind: string, over: Partial<BattlefieldMarker> = {}): BattlefieldMarker => ({
  id: 1,
  kind,
  at: { x: 0, y: 0 },
  owner: 1,
  ...over,
});

describe('nothing the rules track is invisible', () => {
  /**
   * The defect this replaces: the board asked the presentation, got `null`, and
   * drew nothing.
   *
   * Six structure types and five marker kinds ship here and every one was
   * invisible on a board with no painted scene. Worse in one shipped chapter:
   * `c01-15` places a 500 HP `c01.mother-root` and makes destroying it the
   * victory condition, and the campaign's own art has no topic for that type —
   * so the player was asked to break something that was not drawn.
   */
  it('draws every shipped structure type, under every art', () => {
    const drawn = new Map<string, string>();
    for (const definition of shippedStructures()) {
      const state = standing(definition);
      for (const art of [GENERIC_ART, CANDIDATE_01_ART]) {
        const presentation = art.presentation;
        const markup = presentation.structure(state, definition, '#d8483f')
          ?? structureFromRules(state, definition, '#d8483f');
        expect(markup.length, `${art === GENERIC_ART ? 'generic' : 'candidate-01'} / ${definition.id}`)
          .toBeGreaterThan(40);
      }
      drawn.set(structureFromRules(state, definition, '#d8483f'), definition.id);
    }

    // And two structure types are told apart, or a gate looks like a depot.
    expect(drawn.size).toBe(shippedStructures().length);
  });

  it('shows a structure being broken down, and one that is finished', () => {
    const [definition] = shippedStructures();
    const whole = structureFromRules(standing(definition), definition, '#d8483f');
    const hurt = structureFromRules(standing(definition, { hp: 1 }), definition, '#d8483f');
    const dead = structureFromRules(standing(definition, { hp: 1, disabled: true }), definition, '#d8483f');

    expect(new Set([whole, hurt, dead]).size).toBe(3);
  });

  /** A thing nothing may shoot has no condition to show, and shows none. */
  it('gives a condition bar only to what can be attacked', () => {
    const [definition] = shippedStructures();
    const targetable = structureFromRules(standing(definition), definition);
    const inert = structureFromRules(standing(definition), { ...definition, targetable: false });

    expect(targetable.length).toBeGreaterThan(inert.length);
  });

  it('draws a mark for every kind, and a body where a unit fell', () => {
    const kinds = ['corpse', 'routed', 'surrendered', 'withdrawn', 'interaction'];
    const drawn = new Set(kinds.map((kind) => markerFromRules(mark(kind), '#3f7fd8')));
    expect(drawn.size).toBe(kinds.length);
    for (const markup of drawn) expect(markup.length).toBeGreaterThan(40);

    // A marker carrying the unit that fell is a body, not a stake.
    const stake = markerFromRules(mark('routed'), '#3f7fd8');
    const body = markerFromRules(
      mark('routed', { fallenUnit: { id: 7 } as BattlefieldMarker['fallenUnit'] }),
      '#3f7fd8',
    );
    expect(body).not.toBe(stake);
  });

  /**
   * Whose it was is on the drawing, because that is what a marker is for.
   *
   * Asserted as "two sides differ" rather than "this hex appears": a body wears a
   * shaded version of the colour, and pinning the literal would have described
   * the shading rather than the fact.
   */
  it('wears the side that left it', () => {
    const fallen = { fallenUnit: { id: 7 } as BattlefieldMarker['fallenUnit'] };
    for (const over of [{}, fallen]) {
      expect(markerFromRules(mark('corpse', over), '#d8483f'))
        .not.toBe(markerFromRules(mark('corpse', over), '#3f7fd8'));
    }
    // And a side with no colour at all still leaves something behind.
    expect(markerFromRules(mark('corpse')).length).toBeGreaterThan(40);
  });
});
