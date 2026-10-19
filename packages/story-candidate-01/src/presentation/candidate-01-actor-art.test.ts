import { describe, expect, it } from 'vitest';
import { CANDIDATE_01_LEVELS } from '../levels';
import { CANDIDATE_01_ART } from './index';
import { candidate01Asset } from './candidate-01-assets';
import { CANDIDATE_01_CHARACTER_ART, CANDIDATE_01_UNIT_ART } from './candidate-01-bindings';
import { CANDIDATE_01_ACTOR_ART, readCandidate01ActorArt, resolveCandidate01PresentationBinding,
  type Candidate01ActorArtBinding } from './candidate-01-actor-art';
import { resolveCandidate01UnitArt, type Candidate01HeroAppearanceCatalog } from './candidate-01-hero-appearances';
import { candidate01UnitIcon, candidate01UnitPicture } from './candidate-01-runtime';

const binding: Candidate01ActorArtBinding = { expectedUnitType: 'c01.swordsman',
  unitArt: { topicId: 'C01-UNIT-SWORDSMAN', appearanceId: 'test-torren-stage01' },
  characterTopicId: 'C01-CHAR-TORREN-01' };
const base = { ...candidate01Asset(binding.unitArt.topicId), url: '/test-torren-stage01.png' };
const red = { ...base, url: '/test-torren-stage01-red.png' };
const appearances: Candidate01HeroAppearanceCatalog = { appearances: new Map([['test-torren-stage01', base]]),
  teamVariants: new Map([['test-torren-stage01:#a8493f', red]]) };
const declarations = { 'campaign-torren': binding };

describe('explicit actor presentation bindings', () => {
  it('distinguishes two actors of the same rule type and preserves named team/base precedence', () => {
    const actors = readCandidate01ActorArt(appearances, declarations);
    const named = resolveCandidate01PresentationBinding(actors, 'c01.swordsman', { actorKey: 'campaign-torren' });
    const generic = resolveCandidate01PresentationBinding(actors, 'c01.swordsman', { actorKey: 'ridge-guard' });
    expect(named).toEqual({ unitArt: binding.unitArt, characterTopicId: binding.characterTopicId });
    expect(generic).toEqual({ unitArt: CANDIDATE_01_UNIT_ART['c01.swordsman'], characterTopicId: undefined });
    expect(resolveCandidate01UnitArt(appearances, named.unitArt!, '#A8493F')).toBe(red);
    expect(resolveCandidate01UnitArt(appearances, named.unitArt!, '#ffffff')).toBe(base);
    expect(resolveCandidate01UnitArt(appearances, generic.unitArt!, '#a8493f').url).toContain('unit-swordsman-red@2x');
  });

  it('requires the exact declared actor key and type, and leaves type-only calls generic', () => {
    const actors = readCandidate01ActorArt(appearances, declarations);
    for (const context of [undefined, {}, { actorKey: undefined }, { actorKey: '' }, { actorKey: 'torren' },
      { actorKey: 'CAMPAIGN-TORREN' }, { actorKey: 'campaign-torren-stage02' }, { actorKey: 'constructor' }]) {
      expect(resolveCandidate01PresentationBinding(actors, 'c01.swordsman', context).unitArt).toBe(CANDIDATE_01_UNIT_ART['c01.swordsman']);
    }
    expect(resolveCandidate01PresentationBinding(actors, 'unknown', { actorKey: 'unknown' }).unitArt).toBeUndefined();
    expect(() => resolveCandidate01PresentationBinding(actors, 'c01.archer', { actorKey: 'campaign-torren' }))
      .toThrow('expected c01.swordsman, received c01.archer');
  });

  it('uses the named frame when no actor card is declared, even for a type with its own card', () => {
    const hero = { ...base, topicId: 'C01-UNIT-BANNER-GUARD' };
    const catalog = { appearances: new Map([['test-hero', hero]]), teamVariants: new Map() };
    const actors = readCandidate01ActorArt(catalog, { 'test-actor': { expectedUnitType: 'c01.laiya',
      unitArt: { topicId: hero.topicId, appearanceId: 'test-hero' } } });
    expect(CANDIDATE_01_CHARACTER_ART['c01.laiya']).toBeDefined();
    expect(resolveCandidate01PresentationBinding(actors, 'c01.laiya', { actorKey: 'test-actor' }).characterTopicId).toBeUndefined();
  });

  it('rejects malformed declarations at startup, without needing to draw the actor', () => {
    const read = (delta: Partial<Candidate01ActorArtBinding>) => readCandidate01ActorArt(appearances,
      { 'campaign-torren': { ...binding, ...delta } });
    expect(() => read({ expectedUnitType: 'unknown' })).toThrow('unit type/topic mismatch');
    expect(() => read({ expectedUnitType: 'c01.archer' })).toThrow('unit type/topic mismatch');
    expect(() => read({ unitArt: { topicId: base.topicId } })).toThrow('explicit named appearance required');
    expect(() => read({ unitArt: { topicId: base.topicId, appearanceId: 'unknown' } })).toThrow('unknown bound appearance');
    expect(() => readCandidate01ActorArt({ ...appearances, appearances: new Map([['test-torren-stage01',
      { ...base, topicId: 'C01-UNIT-ARCHER' }]]) }, declarations)).toThrow('bound appearance topic mismatch');
    expect(() => read({ characterTopicId: 'unknown' })).toThrow('unknown candidate-01 art topic');
    expect(() => read({ characterTopicId: 'C01-UNIT-SWORDSMAN' })).toThrow('character-card topic');
    for (const key of ['', ' ', ' campaign-torren', 'campaign-torren ']) {
      expect(() => readCandidate01ActorArt(appearances, { [key]: binding })).toThrow('empty or padded actor key');
    }
  });
});

describe('reviewed exact-actor catalog', () => {
  it('preserves pictures/icons/portraits of all shipped actors and every type-only binding', () => {
    expect(CANDIDATE_01_ACTOR_ART.size).toBe(1);
    const portrait = (type: string, team: string, actorKey?: string) =>
      CANDIDATE_01_ART.resolve(provider => provider.portraitMarkup?.(type, team, { actorKey }));
    const actors = CANDIDATE_01_LEVELS.flatMap(level => level.units.map(actor => ({ actor,
      team: level.players.find(player => player.id === actor.owner)?.color ?? '#ffffff' })));
    expect(actors.filter(({ actor }) => actor.key === 'campaign-torren')).toHaveLength(16);
    expect(actors.filter(({ actor }) => actor.unit === 'c01.swordsman' && actor.key !== 'campaign-torren')).toHaveLength(29);
    for (const { actor, team } of actors) {
      const context = { actorKey: actor.key };
      if (actor.key === 'campaign-torren') {
        expect(actor.unit).toBe('c01.swordsman');
        expect(candidate01UnitPicture(actor.unit, team, context)?.strip?.href).toContain('hero-appearance/torren-stage01@2x.png');
        expect(candidate01UnitIcon(actor.unit, team, 32, context)).toContain('hero-appearance/torren-stage01@2x.png');
        expect(portrait(actor.unit, team, actor.key)).toContain('char-torren-01.png');
        expect(candidate01UnitPicture(actor.unit, team, context)).not.toEqual(candidate01UnitPicture(actor.unit, team));
        continue;
      }
      expect(candidate01UnitPicture(actor.unit, team, context)).toEqual(candidate01UnitPicture(actor.unit, team));
      expect(candidate01UnitIcon(actor.unit, team, 32, context)).toBe(candidate01UnitIcon(actor.unit, team, 32));
      expect(portrait(actor.unit, team, actor.key)).toBe(portrait(actor.unit, team));
    }
    for (const [type, unitArt] of Object.entries(CANDIDATE_01_UNIT_ART)) {
      expect(resolveCandidate01PresentationBinding(CANDIDATE_01_ACTOR_ART, type))
        .toEqual({ unitArt, characterTopicId: CANDIDATE_01_CHARACTER_ART[type] });
    }
  });
});
