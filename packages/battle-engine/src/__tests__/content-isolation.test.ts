import { describe, expect, it } from 'vitest';
import { ContentPackInstaller, createContentCatalog } from '../content-pack';
import { createBattleEngine } from '../engine';
import { createState } from '../state';
import { mapFromLevel, normaliseLevel, terrainRows } from '../mapio';
import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '../resources';
import type { ContentCatalog, ContentPack } from '../content-pack';
import type { LevelData, TerrainDef, UnitDef, WeaponDef } from '../types';

/**
 * Content isolation.
 *
 * The catalog used to be a module-level singleton that every content pack
 * installed into. Two consequences were baked in and impossible to test:
 * the terrain legend was a single global namespace, so two themes could not
 * reuse the character `#`; and a balance override in one engine was visible to
 * every other engine and to the pack constants themselves.
 *
 * Catalogs are now per-composition, and packs are deep-copied on install.
 */

const terrain = (id: string, name: string, extra: Partial<TerrainDef> = {}): TerrainDef => ({
  id,
  name,
  cost: { walk: 1 },
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
  ...extra,
});

const weapon = (id: string, power: number): WeaponDef => ({
  id,
  name: id,
  power,
  damageType: 'kinetic',
  minRange: 1,
  maxRange: 1,
  moveAndAttack: true,
  lineOfSight: 'none',
  area: 'single',
  canCounter: true,
  cooldown: 0,
  resources: {},
  resourceRequirements: [],
  resourceCosts: [],
  bonuses: [],
  hitEffects: [],
  tags: [],
});

const unit = (id: string, weaponId: string): UnitDef => ({
  id,
  name: id,
  value: 100,
  recruitCosts: [],
  resources: {},
  maxHp: 100,
  defense: 0,
  movement: 3,
  movementClass: 'walk',
  armorClass: 'flesh',
  weapons: [weaponId],
  vision: 3,
  abilities: ['attack', 'wait'],
  defaultReaction: 'counter',
  tags: [],
  blurb: '',
});

/** Two self-contained themes that deliberately collide on every legend char. */
function themePack(prefix: string, power: number): ContentPack {
  return {
    id: `theme.${prefix}`,
    version: 1,
    movementProfiles: [
      { id: 'walk', name: 'walk', tags: [], maxClimb: null, maxDrop: null, uphillCostPerLevel: 0, ignoresCliffs: false },
    ],
    damageTypes: [{ id: 'kinetic', name: 'kinetic', tags: [] }],
    armorClasses: [{ id: 'flesh', name: 'flesh', tags: [] }],
    damageMatchups: [{ damageType: 'kinetic', armorClass: 'flesh', multiplier: 1 }],
    terrains: [
      terrain(`${prefix}.ground`, `${prefix} ground`),
      terrain(`${prefix}.keep`, `${prefix} keep`, { capturable: true, hq: true }),
    ],
    // Both themes claim '.' and 'C'. Under one global namespace the second
    // install threw "terrain character already registered".
    terrainCharacters: { '.': `${prefix}.ground`, C: `${prefix}.keep` },
    defaultTerrain: `${prefix}.ground`,
    weapons: [weapon(`${prefix}.blade`, power)],
    units: [unit(`${prefix}.trooper`, `${prefix}.blade`)],
  };
}

function catalogFor(pack: ContentPack): ContentCatalog {
  const catalog = createContentCatalog();
  new ContentPackInstaller(catalog).install(pack);
  return catalog;
}

const duel = (prefix: string): LevelData =>
  normaliseLevel({
    schema: 2,
    id: `${prefix}-duel`,
    name: prefix,
    width: 3,
    height: 1,
    terrain: ['C..'],
    owners: [{ x: 0, y: 0, owner: 1 }],
    units: [
      { x: 1, y: 0, unit: `${prefix}.trooper`, owner: 1 },
      { x: 2, y: 0, unit: `${prefix}.trooper`, owner: 2 },
    ],
    players: [
      {
        id: 1, name: 'P1', team: 1, color: '#3f7fd8', controller: 'human',
        resources: { [FUNDS_RESOURCE]: { current: 0, capacity: null }, [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 } },
      },
      {
        id: 2, name: 'P2', team: 2, color: '#d8483f', controller: 'ai',
        resources: { [FUNDS_RESOURCE]: { current: 0, capacity: null }, [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 } },
      },
    ],
    rules: {},
    victory: [{ type: 'routEnemies' }],
  });

describe('per-composition content catalogs', () => {
  it('lets two themes claim the same terrain legend characters', () => {
    const steel = catalogFor(themePack('steel', 30));
    const bronze = catalogFor(themePack('bronze', 70));

    expect(steel.terrainEncoding.terrain('.')).toBe('steel.ground');
    expect(bronze.terrainEncoding.terrain('.')).toBe('bronze.ground');
    expect(steel.terrainEncoding.terrain('C')).toBe('steel.keep');
    expect(bronze.terrainEncoding.terrain('C')).toBe('bronze.keep');
  });

  it('reads the same level rows as different terrain per catalog', () => {
    const steel = catalogFor(themePack('steel', 30));
    const bronze = catalogFor(themePack('bronze', 70));

    const steelMap = mapFromLevel(duel('steel'), steel);
    const bronzeMap = mapFromLevel(duel('bronze'), bronze);

    expect(steelMap.tiles[0]).toBe('steel.keep');
    expect(bronzeMap.tiles[0]).toBe('bronze.keep');
    // And each serialises back to the identical rows through its own encoding.
    expect(terrainRows(steelMap, steel)).toEqual(['C..']);
    expect(terrainRows(bronzeMap, bronze)).toEqual(['C..']);
  });

  it('runs two engines on different themes at the same time', () => {
    const steel = catalogFor(themePack('steel', 30));
    const bronze = catalogFor(themePack('bronze', 70));
    const steelEngine = createBattleEngine({ content: steel });
    const bronzeEngine = createBattleEngine({ content: bronze });

    const steelState = steelEngine.createState(duel('steel'));
    const bronzeState = bronzeEngine.createState(duel('bronze'));

    expect(steelEngine.forecast(steelState, steelState.units[0], steelState.units[1]).strike.base).toBe(30);
    expect(bronzeEngine.forecast(bronzeState, bronzeState.units[0], bronzeState.units[1]).strike.base).toBe(70);
  });

  it('keeps a balance override inside the catalog that made it', () => {
    const pack = themePack('steel', 30);
    const first = catalogFor(pack);
    const second = catalogFor(pack);

    first.weapons.override('steel.blade', { power: 999 });
    first.units.get('steel.trooper').tags.push('sandbox-only');

    expect(second.weapons.get('steel.blade').power).toBe(30);
    expect(second.units.get('steel.trooper').tags).not.toContain('sandbox-only');
    // The pack declaration itself must stay pristine for the next install.
    expect(pack.units![0].tags).not.toContain('sandbox-only');
  });

  it('still rejects a character collision inside one catalog', () => {
    const catalog = createContentCatalog();
    const installer = new ContentPackInstaller(catalog);
    installer.install(themePack('steel', 30));

    // Only the legend collides here, so that is the error that must surface.
    const rival: ContentPack = {
      id: 'theme.rival',
      version: 1,
      terrains: [terrain('rival.ground', 'rival ground')],
      terrainCharacters: { '.': 'rival.ground' },
    };
    expect(() => installer.install(rival)).toThrow(/terrain character already registered/);
  });

  it('refuses to build a state from a level the catalog cannot read', () => {
    const steel = catalogFor(themePack('steel', 30));
    expect(() => createState(duel('bronze'), steel)).toThrow(/unknown id "bronze.trooper"/);
  });
});
