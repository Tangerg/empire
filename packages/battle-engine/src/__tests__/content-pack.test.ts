import { describe, expect, it } from 'vitest';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { defineTerrain, defineUnit, defineWeapon } from '../content-builders';
import { ContentPackInstaller, createContentCatalog, type ContentPack } from '../content-pack';

describe('ContentPackInstaller', () => {
  it('composes shared rules and a theme into an isolated catalog', () => {
    const catalog = createContentCatalog();
    const installer = new ContentPackInstaller(catalog);

    expect(installer.install(COMMON_CONTENT_PACK, ANCIENT_EMPIRES_CONTENT_PACK)).toEqual([
      'empire.common',
      'empire.ancient-empires',
    ]);

    expect(catalog.units.get('knight').movementClass).toBe('mounted');
    expect(catalog.weapons.get('dragon_breath').area).toBe('line');
    expect(catalog.terrains.get('mountain').tags).toContain('high');
    expect(catalog.terrainEncoding.terrain('^')).toBe('mountain');
    expect(catalog.terrainEncoding.defaultTerrain).toBe('plain');
  });

  it('refuses every duplicate pack declaration instead of guessing from version equality', () => {
    const installer = new ContentPackInstaller(createContentCatalog());
    installer.install(COMMON_CONTENT_PACK);

    expect(() => installer.install(COMMON_CONTENT_PACK)).toThrow(/second declaration.*ambiguous/);
    expect(() => installer.install({ ...COMMON_CONTENT_PACK, version: 2 })).toThrow(/already installed/);
  });

  it('refuses a default terrain that cannot be serialized', () => {
    const pack: ContentPack = {
      id: 'test.unencoded-default',
      version: 1,
      terrains: [defineTerrain({ id: 'blank', name: 'Blank', cost: {} })],
      defaultTerrain: 'blank',
    };

    expect(() => new ContentPackInstaller(createContentCatalog()).install(pack))
      .toThrow(/default terrain "blank" has no character encoding/);
  });

  it('orders dependencies and rejects dependency cycles', () => {
    const installer = new ContentPackInstaller(createContentCatalog());
    expect(installer.install(ANCIENT_EMPIRES_CONTENT_PACK, COMMON_CONTENT_PACK)).toEqual([
      'empire.common',
      'empire.ancient-empires',
    ]);

    const cyclicCatalog = createContentCatalog();
    const cyclicInstaller = new ContentPackInstaller(cyclicCatalog);
    const left: ContentPack = { id: 'cycle.left', version: 1, dependencies: ['cycle.right'] };
    const right: ContentPack = { id: 'cycle.right', version: 1, dependencies: ['cycle.left'] };
    expect(() => cyclicInstaller.install(left, right)).toThrow(/cyclic content pack dependency/);
    expect(cyclicCatalog.packVersions.all()).toEqual([]);
  });

  it('rejects invalid cross references without partially mutating the catalog', () => {
    const catalog = createContentCatalog();
    const installer = new ContentPackInstaller(catalog);
    const invalid: ContentPack = {
      id: 'test.invalid',
      version: 1,
      movementProfiles: [{ id: 'walker', name: 'Walker', tags: ['ground'], maxClimb: 1, maxDrop: 1, uphillCostPerLevel: 1, ignoresCliffs: false }],
      damageTypes: [{ id: 'slash', name: 'Slash', tags: [] }],
      armorClasses: [{ id: 'light', name: 'Light', tags: [] }],
      damageMatchups: [{ damageType: 'slash', armorClass: 'light', multiplier: 1 }],
      weapons: [defineWeapon({ id: 'test_blade', name: 'Blade', power: 10, damageType: 'slash' })],
      units: [
        defineUnit(
          {
            id: 'broken',
            name: 'Broken',
            value: 100,
            recruitCosts: [],
            weapons: ['test_blade', 'missing_weapon'],
            movementClass: 'walker',
            armorClass: 'light',
          },
          new Map([
            [
              'test_blade',
              defineWeapon({ id: 'test_blade', name: 'Blade', power: 10, damageType: 'slash' }),
            ],
          ]),
        ),
      ],
    };

    expect(() => installer.install(invalid)).toThrow(/missing content id "missing_weapon"/);
    expect(catalog.movementProfiles.all()).toEqual([]);
    expect(catalog.weapons.all()).toEqual([]);
    expect(catalog.units.all()).toEqual([]);
    expect(catalog.packVersions.has(invalid.id)).toBe(false);
  });

  it('accepts story-neutral ids from unrelated genres through the same contract', () => {
    const stellarWeapon = defineWeapon({ id: 'stellar_pulse', name: 'Pulse', power: 30, damageType: 'energy' });
    const historicalWeapon = defineWeapon({ id: 'historical_spear', name: 'Spear', power: 28, damageType: 'formation_pierce' });
    const pack: ContentPack = {
      id: 'test.cross-theme',
      version: 1,
      movementProfiles: [{ id: 'test_ground', name: 'Ground', tags: ['ground'], maxClimb: 1, maxDrop: 1, uphillCostPerLevel: 1, ignoresCliffs: false }],
      damageTypes: [
        { id: 'energy', name: 'Energy', tags: ['stellar'] },
        { id: 'formation_pierce', name: 'Formation pierce', tags: ['historical'] },
      ],
      armorClasses: [{ id: 'light', name: 'Light', tags: [] }],
      damageMatchups: [
        { damageType: 'energy', armorClass: 'light', multiplier: 1.1 },
        { damageType: 'formation_pierce', armorClass: 'light', multiplier: 0.9 },
      ],
      statuses: [],
      weapons: [stellarWeapon, historicalWeapon],
      units: [
        defineUnit(
          {
            id: 'stellar_marine',
            name: 'Marine',
            value: 100,
            recruitCosts: [],
            weapons: [stellarWeapon.id],
            movementClass: 'test_ground',
            armorClass: 'light',
          },
          new Map([[stellarWeapon.id, stellarWeapon]]),
        ),
        defineUnit(
          {
            id: 'historical_guard',
            name: 'Guard',
            value: 100,
            recruitCosts: [],
            weapons: [historicalWeapon.id],
            movementClass: 'test_ground',
            armorClass: 'light',
          },
          new Map([[historicalWeapon.id, historicalWeapon]]),
        ),
      ],
      terrains: [
        defineTerrain({
          id: 'test_ground_tile',
          name: 'Ground',
          cost: { test_ground: 1 },
          produces: ['stellar_marine', 'historical_guard'],
        }),
      ],
      terrainCharacters: { '.': 'test_ground_tile' },
      defaultTerrain: 'test_ground_tile',
    };
    const catalog = createContentCatalog();

    new ContentPackInstaller(catalog).install(pack);

    expect(catalog.units.ids()).toEqual(['stellar_marine', 'historical_guard']);
    expect(catalog.terrains.get('test_ground_tile').produces).toHaveLength(2);
    expect(catalog.damageMatchups.effectiveness('energy', 'unknown_future_armor')).toBe(1);
  });
});

describe('what a catalog can and cannot judge on its own', () => {
  const base = (): ContentPack => ({
    id: 'test.base',
    version: 1,
    movementProfiles: [{ id: 'walker', name: 'Walker', tags: ['ground'], maxClimb: 1, maxDrop: 1, uphillCostPerLevel: 1, ignoresCliffs: false }],
    damageTypes: [{ id: 'slash', name: 'Slash', tags: [] }],
    armorClasses: [{ id: 'light', name: 'Light', tags: [] }],
    damageMatchups: [{ damageType: 'slash', armorClass: 'light', multiplier: 1 }],
  });

  const withRider = (rider: unknown): ContentPack => {
    const blade = defineWeapon({
      id: 'test_blade',
      name: 'Blade',
      power: 10,
      damageType: 'slash',
      hitEffects: [rider as never],
    });
    return {
      id: 'test.rider',
      version: 1,
      dependencies: ['test.base'],
      weapons: [blade],
      units: [defineUnit({
        id: 'test_soldier',
        name: 'Soldier',
        movementClass: 'walker',
        armorClass: 'light',
        value: 10,
        recruitCosts: [],
        weapons: ['test_blade'],
      }, new Map([['test_blade', blade]]))],
    };
  };

  it('accepts a weapon rider only a rule plugin defines', () => {
    // The installer used to treat every kind it did not recognise as a forced
    // move and validate `distance` on it, so a pack that used a rule pack's own
    // rider could not be installed at all. Whether the handler exists is the
    // ruleset's question, and the reference checks ask it.
    const catalog = createContentCatalog();
    const installer = new ContentPackInstaller(catalog);

    expect(() => installer.install(base(), withRider({ type: 'test.ignite', heat: 3 })))
      .not.toThrow();
    expect(catalog.weapons.get('test_blade').hitEffects[0].type).toBe('test.ignite');
  });

  it('still judges the payloads it does understand', () => {
    const shoves = new ContentPackInstaller(createContentCatalog());
    expect(() => shoves.install(base(), withRider({ type: 'forcedMove', mode: 'push', distance: -1 })))
      .toThrow(/invalid forced-movement effect/);

    const seals = new ContentPackInstaller(createContentCatalog());
    expect(() => seals.install(base(), withRider({ type: 'addStatus', status: 'nonesuch', duration: 1 })))
      .toThrow(/references missing content id "nonesuch"/);
  });
});
