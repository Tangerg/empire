import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import manifest from '../../assets/final-fantasy-v1/manifest-final-fantasy-v1.json';
import document from '../../assets/final-fantasy-v1/manifest-hero-appearances.json';
import { candidate01Asset, candidate01TeamAsset } from './candidate-01-assets';
import { CANDIDATE_01_UNIT_ART } from './candidate-01-bindings';
import { candidate01UnitIcon, candidate01UnitPicture } from './candidate-01-runtime';
import { CANDIDATE_01_HERO_APPEARANCES, readCandidate01HeroAppearances, resolveCandidate01UnitArt,
  type Candidate01HeroAppearanceDocument } from './candidate-01-hero-appearances';

const root = '../../assets/final-fantasy-v1/';
function sheet(name: string) {
  return { png: `runtime-approved-v5/hero-appearance/${name}.png`,
    hdPng: `runtime-approved-v5/hero-appearance/${name}@2x.png`, pixelDensity: 2,
    hdWidth: 832, hdHeight: 128, frameWidth: 104, frameHeight: 64, frames: 4,
    frameOrder: ['idle', 'walk-a', 'attack', 'walk-b'], anchor: [52, 54] };
}
const hero = { ...sheet('test-hero'), id: 'test-hero', topicId: 'C01-UNIT-SWORDSMAN' };
const red = { ...sheet('test-hero-red'), appearanceId: hero.id, teamColor: '#a8493f' };
const fixture: Candidate01HeroAppearanceDocument = { schemaVersion: 1, appearances: [hero], teamVariants: [red] };
const files = Object.fromEntries([hero, red].flatMap(entry => [entry.png, entry.hdPng].map(path => [root + path, `/${path}`])));

function readHero(delta: Partial<typeof hero>) {
  return readCandidate01HeroAppearances({ ...fixture, appearances: [{ ...hero, ...delta }] }, files);
}

describe('explicit hero appearance document and resolution', () => {
  it('resolves hero-team, hero-base and generic-team in that order without changing canonical assets', () => {
    const before = JSON.stringify(candidate01Asset(hero.topicId));
    const catalog = readCandidate01HeroAppearances(fixture, files);
    const binding = { topicId: hero.topicId, appearanceId: hero.id };
    const heroBase = resolveCandidate01UnitArt(catalog, binding, '#ffffff');
    const heroRed = resolveCandidate01UnitArt(catalog, binding, '#A8493F');
    expect(heroBase.url).toBe(`/${hero.hdPng}`);
    expect(heroRed.url).toBe(`/${red.hdPng}`);
    expect(heroBase.footprint).toEqual(candidate01Asset(hero.topicId).footprint);
    expect(heroBase.category).toBe('combat-unit');
    expect(heroBase.assetId).toBe(candidate01Asset(hero.topicId).assetId);
    expect(heroBase.preserveAuthoredPalette).toBe(true);
    expect(heroRed.preserveAuthoredPalette).toBe(true);
    expect(resolveCandidate01UnitArt(catalog, { topicId: hero.topicId }, '#A8493F')).toBe(candidate01TeamAsset(hero.topicId, '#a8493f'));
    expect(candidate01TeamAsset(hero.topicId, '#a8493f').url).toContain('unit-swordsman-red@2x.png');
    expect(JSON.stringify(candidate01Asset(hero.topicId))).toBe(before);
    expect(manifest.assets.filter(asset => asset.topicId === hero.topicId)).toHaveLength(1);
  });

  it('keeps a named base when no named team variant exists, even when a generic red variant does', () => {
    const catalog = readCandidate01HeroAppearances({ ...fixture, teamVariants: [] }, files);
    expect(resolveCandidate01UnitArt(catalog, { topicId: hero.topicId, appearanceId: hero.id }, '#a8493f').url).toBe(`/${hero.hdPng}`);
  });

  it('permits independently registered wide canvases without altering the canonical footprint', () => {
    const catalog = readHero({ frameWidth: 176, frameHeight: 80, hdWidth: 1408, hdHeight: 160, anchor: [85.25, 67.5] });
    const actual = catalog.appearances.get(hero.id)!;
    expect([actual.width, actual.height, actual.frameWidth, actual.frameHeight]).toEqual([704, 80, 176, 80]);
    expect(actual.anchor).toEqual([85.25, 67.5]);
    expect(actual.footprint).toEqual([1, 1]);
  });

  it('rejects unknown or mismatched explicit appearances rather than falling back to a generic face', () => {
    const catalog = readCandidate01HeroAppearances(fixture, files);
    expect(() => resolveCandidate01UnitArt(catalog, { topicId: hero.topicId, appearanceId: 'unknown' }, '#a8493f')).toThrow('unknown bound appearance');
    expect(() => resolveCandidate01UnitArt(catalog, { topicId: hero.topicId, appearanceId: '' }, '#a8493f')).toThrow('unknown bound appearance');
    expect(() => resolveCandidate01UnitArt(catalog, { topicId: 'C01-UNIT-BANNER-GUARD', appearanceId: hero.id }, '#a8493f')).toThrow('topic mismatch');
  });

  it('rejects unknown topics, noncombat topics, duplicate ids and schema drift', () => {
    expect(() => readHero({ topicId: 'unknown' })).toThrow('unknown candidate-01 art topic');
    expect(() => readHero({ topicId: 'C01-CHAR-LEIA-01' })).toThrow('four-frame combat unit');
    expect(() => readHero({ topicId: 'C01-MISSION-REFUGEE-ADULT' })).toThrow('four-frame combat unit');
    expect(() => readHero({ id: '../hero' })).toThrow('invalid or duplicate appearance id');
    expect(() => readCandidate01HeroAppearances({ ...fixture, appearances: [hero, hero] }, files)).toThrow('duplicate appearance id');
    expect(() => readCandidate01HeroAppearances({ ...fixture, schemaVersion: 2 }, files)).toThrow('unsupported appearance schema');
    const withFootprint = { ...hero, footprint: [2, 2] };
    expect(() => readCandidate01HeroAppearances({ ...fixture, appearances: [withFootprint] }, files)).toThrow('unsupported or missing fields');
  });

  it.each([
    { frames: 3 }, { frameWidth: 0 }, { frameHeight: -1 }, { frameWidth: 104.5 },
    { frameHeight: Number.NaN }, { pixelDensity: 1 }, { hdWidth: 831 }, { hdHeight: 127 },
    { frameOrder: ['idle', 'walk-a', 'walk-b', 'attack'] },
    { frameOrder: ['idle', 'walk-a', 'attack', 'attack'] },
  ])('rejects incomplete or inconsistent frame geometry: %j', delta => {
    expect(() => readHero(delta)).toThrow('inconsistent frame/HD geometry or frame order');
  });

  it.each([[52], [52, Number.NaN], [-1, 54], [104, 54], [52, 64]])('rejects malformed or outside pivot %j', (...anchor) => {
    expect(() => readHero({ anchor })).toThrow();
  });

  it('requires two dedicated runtime files and refuses reused or escaped paths', () => {
    expect(() => readCandidate01HeroAppearances(fixture, {})).toThrow('missing from Vite graph');
    const onlyHd = { [root + hero.hdPng]: '/hero.png' };
    expect(() => readCandidate01HeroAppearances({ ...fixture, teamVariants: [] }, onlyHd)).toThrow('missing from Vite graph');
    expect(() => readHero({ png: 'runtime-approved-v5/combat-unit/unit-swordsman.png' })).toThrow('paired hero appearance runtime paths');
    expect(() => readHero({ png: 'runtime-approved-v5/hero-appearance/../unit.png' })).toThrow('paired hero appearance runtime paths');
    expect(() => readHero({ hdPng: hero.png })).toThrow('paired hero appearance runtime paths');
    expect(() => readCandidate01HeroAppearances({ ...fixture, appearances: [hero, { ...hero, id: 'different-hero' }] }, files)).toThrow('duplicate runtime path');
  });

  it('ties every authored team variant to a known appearance with unique case-insensitive team colour', () => {
    expect(() => readCandidate01HeroAppearances({ ...fixture, teamVariants: [{ ...red, appearanceId: 'unknown' }] }, files)).toThrow('unknown team-variant appearance');
    expect(() => readCandidate01HeroAppearances({ ...fixture, teamVariants: [{ ...red, teamColor: 'red' }] }, files)).toThrow('invalid or duplicate team variant');
    expect(() => readCandidate01HeroAppearances({ ...fixture, teamVariants: [red, { ...red, teamColor: '#A8493F' }] }, files)).toThrow('invalid or duplicate team variant');
    expect(() => readCandidate01HeroAppearances({ ...fixture, teamVariants: [{ ...red, hdHeight: 64 }] }, files)).toThrow('inconsistent frame/HD geometry');
  });
});

describe('unactivated production hero catalog', () => {
  it('contains no incomplete appearances and leaves every current unit and authored team source unchanged', () => {
    expect(document).toEqual({ schemaVersion: 1, appearances: [], teamVariants: [] });
    expect(CANDIDATE_01_HERO_APPEARANCES.appearances.size).toBe(0);
    for (const [type, binding] of Object.entries(CANDIDATE_01_UNIT_ART)) {
      if (!binding) throw new Error(`undefined art binding: ${type}`);
      expect(binding.appearanceId).toBeUndefined();
      for (const team of ['#a8493f', '#A8493F', '#4d5e76']) {
        const expected = candidate01TeamAsset(binding.topicId, team);
        expect(resolveCandidate01UnitArt(CANDIDATE_01_HERO_APPEARANCES, binding, team)).toBe(expected);
        const picture = candidate01UnitPicture(type, team)!;
        expect(picture.strip?.href).toBe(expected.url);
        expect(picture.strip?.frameWidth).toBe(expected.frameWidth);
        expect(picture.strip?.frameHeight).toBe(expected.frameHeight);
        expect(candidate01UnitIcon(type, team, 48)).toContain(expected.url);
      }
    }
    expect(candidate01UnitPicture('unknown-unit', '#a8493f')).toBeNull();
    expect(candidate01UnitIcon('unknown-unit', '#a8493f', 48)).toBeNull();
  });

  it('requires actual registered sheet dimensions and an RGBA PNG container when sources are activated', () => {
    const registered: Candidate01HeroAppearanceDocument = document;
    for (const entry of [...registered.appearances, ...registered.teamVariants]) {
      for (const [path, width, height] of [[entry.png, entry.frameWidth * 4, entry.frameHeight], [entry.hdPng, entry.hdWidth, entry.hdHeight]] as const) {
        const png = readFileSync(new URL(`${root}${path}`, import.meta.url));
        expect(png.subarray(0, 8)).toEqual(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]));
        expect([png.readUInt32BE(16), png.readUInt32BE(20), png[25]]).toEqual([width, height, 6]);
      }
    }
  });
});
