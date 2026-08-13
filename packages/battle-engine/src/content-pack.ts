import { MovementProfiles } from './data/movement';
import { ArmorClasses, DamageMatchups, DamageMatchupRegistry, DamageTypes } from './data/damage';
import { TerrainOverlays } from './data/overlays';
import { Structures } from './data/structures';
import { Tactics } from './data/tactics';
import { TerrainEncoding, TerrainEncodingRegistry } from './data/terrain-encoding';
import { Terrains } from './data/terrain';
import { UnitTypes } from './data/units';
import { Weapons } from './data/weapons';
import { Registry } from './registry';
import { Statuses } from './data/statuses';
import { Careers } from './data/careers';
import { Formations } from './data/formations';
import { orderByDependencies } from './dependency-order';
import type {
  CareerDef,
  FormationDef,
  MovementProfileDef,
  ArmorClassDef,
  DamageMatchupDef,
  DamageTypeDef,
  StatusDef,
  StructureDef,
  TacticDef,
  TerrainDef,
  TerrainId,
  TerrainOverlayDef,
  UnitDef,
  WeaponDef,
  ResourceAccounts,
  ResourceAmount,
} from './types';

/** Pure data package. It contains presentation and balance, never battle flow. */
export interface ContentPack {
  id: string;
  version: number;
  dependencies?: readonly string[];
  movementProfiles?: readonly MovementProfileDef[];
  damageTypes?: readonly DamageTypeDef[];
  armorClasses?: readonly ArmorClassDef[];
  damageMatchups?: readonly DamageMatchupDef[];
  terrains?: readonly TerrainDef[];
  terrainCharacters?: Readonly<Record<string, TerrainId>>;
  defaultTerrain?: TerrainId;
  weapons?: readonly WeaponDef[];
  units?: readonly UnitDef[];
  statuses?: readonly StatusDef[];
  structures?: readonly StructureDef[];
  terrainOverlays?: readonly TerrainOverlayDef[];
  tactics?: readonly TacticDef[];
  careers?: readonly CareerDef[];
  formations?: readonly FormationDef[];
}

export interface ContentCatalog {
  movementProfiles: Registry<MovementProfileDef>;
  damageTypes: Registry<DamageTypeDef>;
  armorClasses: Registry<ArmorClassDef>;
  damageMatchups: DamageMatchupRegistry;
  terrains: Registry<TerrainDef>;
  terrainEncoding: TerrainEncodingRegistry;
  weapons: Registry<WeaponDef>;
  units: Registry<UnitDef>;
  statuses: Registry<StatusDef>;
  structures: Registry<StructureDef>;
  terrainOverlays: Registry<TerrainOverlayDef>;
  tactics: Registry<TacticDef>;
  careers: Registry<CareerDef>;
  formations: Registry<FormationDef>;
}

export function createContentCatalog(): ContentCatalog {
  return {
    movementProfiles: new Registry('movement profile'),
    damageTypes: new Registry('damage type'),
    armorClasses: new Registry('armor class'),
    damageMatchups: new DamageMatchupRegistry(),
    terrains: new Registry('terrain'),
    terrainEncoding: new TerrainEncodingRegistry(),
    weapons: new Registry('weapon'),
    units: new Registry('unit'),
    statuses: new Registry('status'),
    structures: new Registry('structure'),
    terrainOverlays: new Registry('terrain overlay'),
    tactics: new Registry('tactic'),
    careers: new Registry('career'),
    formations: new Registry('formation'),
  };
}

export const GlobalContentCatalog: ContentCatalog = {
  movementProfiles: MovementProfiles,
  damageTypes: DamageTypes,
  armorClasses: ArmorClasses,
  damageMatchups: DamageMatchups,
  terrains: Terrains,
  terrainEncoding: TerrainEncoding,
  weapons: Weapons,
  units: UnitTypes,
  statuses: Statuses,
  structures: Structures,
  terrainOverlays: TerrainOverlays,
  tactics: Tactics,
  careers: Careers,
  formations: Formations,
};

function cloneDataRegistry<T extends { id: string }>(source: Registry<T>): Registry<T> {
  const copy = source.clone();
  for (const definition of source.all()) copy.override(definition.id, structuredClone(definition));
  return copy;
}

/** Snapshot of definition registries for one engine/sandbox instance. */
export function cloneContentCatalog(source: ContentCatalog): ContentCatalog {
  return {
    movementProfiles: cloneDataRegistry(source.movementProfiles),
    damageTypes: cloneDataRegistry(source.damageTypes),
    armorClasses: cloneDataRegistry(source.armorClasses),
    damageMatchups: source.damageMatchups.clone(),
    terrains: cloneDataRegistry(source.terrains),
    terrainEncoding: source.terrainEncoding.clone(),
    weapons: cloneDataRegistry(source.weapons),
    units: cloneDataRegistry(source.units),
    statuses: cloneDataRegistry(source.statuses),
    structures: cloneDataRegistry(source.structures),
    terrainOverlays: cloneDataRegistry(source.terrainOverlays),
    tactics: cloneDataRegistry(source.tactics),
    careers: cloneDataRegistry(source.careers),
    formations: cloneDataRegistry(source.formations),
  };
}

type RegistryField = Exclude<keyof ContentCatalog, 'terrainEncoding' | 'damageMatchups'>;

const PACK_FIELDS: ReadonlyArray<{
  pack: keyof ContentPack;
  catalog: RegistryField;
}> = [
  { pack: 'movementProfiles', catalog: 'movementProfiles' },
  { pack: 'damageTypes', catalog: 'damageTypes' },
  { pack: 'armorClasses', catalog: 'armorClasses' },
  { pack: 'terrains', catalog: 'terrains' },
  { pack: 'weapons', catalog: 'weapons' },
  { pack: 'units', catalog: 'units' },
  { pack: 'statuses', catalog: 'statuses' },
  { pack: 'structures', catalog: 'structures' },
  { pack: 'terrainOverlays', catalog: 'terrainOverlays' },
  { pack: 'tactics', catalog: 'tactics' },
  { pack: 'careers', catalog: 'careers' },
  { pack: 'formations', catalog: 'formations' },
];

function idsIn<T extends { id: string }>(registry: Registry<T>, incoming: readonly T[]): Set<string> {
  return new Set([...registry.ids(), ...incoming.map((entry) => entry.id)]);
}

/**
 * Installs packs atomically after validating ids, dependencies and references.
 * Reinstalling the same id/version is intentionally idempotent for HMR/tests.
 */
export class ContentPackInstaller {
  private readonly installed = new Map<string, number>();

  constructor(readonly catalog: ContentCatalog) {}

  install(...requested: readonly ContentPack[]): string[] {
    const pending = requested.filter((pack) => {
      const version = this.installed.get(pack.id);
      if (version === undefined) return true;
      if (version !== pack.version) {
        throw new Error(`content pack "${pack.id}" already installed at version ${version}, requested ${pack.version}`);
      }
      return false;
    });
    if (pending.length === 0) return [];

    const pendingIds = new Set<string>();
    for (const pack of pending) {
      if (!pack.id.trim()) throw new Error('content pack id cannot be empty');
      if (!Number.isInteger(pack.version) || pack.version < 1) {
        throw new Error(`content pack "${pack.id}" version must be a positive integer`);
      }
      if (pendingIds.has(pack.id)) throw new Error(`duplicate content pack request: "${pack.id}"`);
      pendingIds.add(pack.id);
    }
    const ordered = orderByDependencies(pending, {
      idOf: (pack) => pack.id,
      dependenciesOf: (pack) => pack.dependencies ?? [],
      isSatisfiedExternally: (id) => this.installed.has(id),
      missing: (pack, dependency) =>
        new Error(`content pack "${pack.id}" requires "${dependency}"`),
      cycle: (path) => new Error(`cyclic content pack dependency: ${path.join(' -> ')}`),
    });

    this.validateUniqueDefinitions(ordered);
    this.validateDamageMatchups(ordered);
    this.validateTerrainEncoding(ordered);
    this.validateResourceShapes(ordered);
    this.validateReferences(ordered);

    for (const pack of ordered) {
      for (const field of PACK_FIELDS) {
        const entries = pack[field.pack] as readonly { id: string }[] | undefined;
        if (entries) (this.catalog[field.catalog] as Registry<{ id: string }>).defineAll(entries);
      }
      if (pack.terrainCharacters !== undefined || pack.defaultTerrain !== undefined) {
        this.catalog.terrainEncoding.register(pack.terrainCharacters ?? {}, pack.defaultTerrain);
      }
      if (pack.damageMatchups) this.catalog.damageMatchups.register(pack.damageMatchups);
      this.installed.set(pack.id, pack.version);
    }
    return ordered.map((pack) => pack.id);
  }

  isInstalled(id: string): boolean {
    return this.installed.has(id);
  }

  installedPacks(): ReadonlyMap<string, number> {
    return new Map(this.installed);
  }

  private validateUniqueDefinitions(packs: readonly ContentPack[]): void {
    for (const field of PACK_FIELDS) {
      const registry = this.catalog[field.catalog] as Registry<{ id: string }>;
      const seen = new Set<string>();
      for (const pack of packs) {
        const entries = pack[field.pack] as readonly { id: string }[] | undefined;
        for (const entry of entries ?? []) {
          if (registry.has(entry.id) || seen.has(entry.id)) {
            throw new Error(`content ${String(field.pack)} id already registered: "${entry.id}"`);
          }
          seen.add(entry.id);
        }
      }
    }
  }

  private validateTerrainEncoding(packs: readonly ContentPack[]): void {
    const characters = new Set<string>();
    const terrains = new Set<string>();
    let defaultTerrain: string | undefined;
    for (const pack of packs) {
      for (const [character, terrain] of Object.entries(pack.terrainCharacters ?? {})) {
        if ([...character].length !== 1) throw new Error(`terrain character must contain one code point: "${character}"`);
        if (this.catalog.terrainEncoding.hasCharacter(character) || characters.has(character)) {
          throw new Error(`terrain character already registered: "${character}"`);
        }
        if (this.catalog.terrainEncoding.hasTerrain(terrain) || terrains.has(terrain)) {
          throw new Error(`terrain already has a character: "${terrain}"`);
        }
        characters.add(character);
        terrains.add(terrain);
      }
      if (pack.defaultTerrain !== undefined) {
        if (defaultTerrain !== undefined && defaultTerrain !== pack.defaultTerrain) {
          throw new Error('multiple default terrains requested in one installation');
        }
        defaultTerrain = pack.defaultTerrain;
      }
    }
    const installedDefault = this.catalog.terrainEncoding.currentDefaultTerrain();
    if (defaultTerrain !== undefined && installedDefault !== null && installedDefault !== defaultTerrain) {
      throw new Error(`default terrain already registered: "${installedDefault}"`);
    }
  }

  private validateDamageMatchups(packs: readonly ContentPack[]): void {
    const seen = new Set<string>();
    for (const pack of packs) {
      for (const matchup of pack.damageMatchups ?? []) {
        if (!Number.isFinite(matchup.multiplier) || matchup.multiplier <= 0) {
          throw new Error(
            `damage matchup "${matchup.damageType}" -> "${matchup.armorClass}" must be > 0`,
          );
        }
        const key = `${matchup.damageType}\u0000${matchup.armorClass}`;
        if (this.catalog.damageMatchups.has(matchup.damageType, matchup.armorClass) || seen.has(key)) {
          throw new Error(
            `damage matchup already registered: "${matchup.damageType}" -> "${matchup.armorClass}"`,
          );
        }
        seen.add(key);
      }
    }
  }

  private validateResourceShapes(packs: readonly ContentPack[]): void {
    const validateAccounts = (accounts: ResourceAccounts, owner: string): void => {
      for (const [resource, account] of Object.entries(accounts)) {
        if (!resource.trim()) throw new Error(`${owner} has an empty resource id`);
        if (!Number.isFinite(account.current) || account.current < 0) {
          throw new Error(`${owner} resource "${resource}" current must be finite and non-negative`);
        }
        if (account.capacity !== null &&
          (!Number.isFinite(account.capacity) || account.capacity < account.current)) {
          throw new Error(`${owner} resource "${resource}" capacity must be null or at least current`);
        }
      }
    };
    const validateAmounts = (amounts: readonly ResourceAmount[], owner: string): void => {
      for (const amount of amounts) {
        if (!amount.resource.trim()) throw new Error(`${owner} has an empty resource id`);
        if (!Number.isFinite(amount.amount) || amount.amount <= 0) {
          throw new Error(`${owner} resource "${amount.resource}" amount must be finite and positive`);
        }
      }
    };

    for (const pack of packs) {
      for (const unit of pack.units ?? []) {
        if (!Number.isFinite(unit.value) || unit.value < 0) {
          throw new Error(`unit "${unit.id}" value must be finite and non-negative`);
        }
        validateAccounts(unit.resources, `unit "${unit.id}"`);
        validateAmounts(unit.recruitCosts, `unit "${unit.id}" recruit cost`);
        if (unit.morale && (!Number.isFinite(unit.morale.maximum) || unit.morale.maximum < 1 ||
          !Number.isFinite(unit.morale.resilience) || unit.morale.resilience < 0 || unit.morale.resilience > 0.9)) {
          throw new Error(`unit "${unit.id}" has an invalid morale profile`);
        }
        if (unit.transport && (!Number.isInteger(unit.transport.capacity) || unit.transport.capacity < 1)) {
          throw new Error(`unit "${unit.id}" transport capacity must be a positive integer`);
        }
      }
      for (const weapon of pack.weapons ?? []) {
        validateAccounts(weapon.resources, `weapon "${weapon.id}"`);
        validateAmounts(weapon.resourceRequirements, `weapon "${weapon.id}" requirement`);
        validateAmounts(weapon.resourceCosts, `weapon "${weapon.id}" cost`);
      }
      for (const terrain of pack.terrains ?? []) {
        validateAmounts(terrain.ownerTurnGrants, `terrain "${terrain.id}" grant`);
      }
      for (const tactic of pack.tactics ?? []) {
        validateAmounts(tactic.costs, `tactic "${tactic.id}" cost`);
      }
      for (const career of pack.careers ?? []) {
        validateAmounts(career.costs, `career "${career.id}" cost`);
        if (!Number.isInteger(career.tier) || career.tier < 0) {
          throw new Error(`career "${career.id}" tier must be a non-negative integer`);
        }
        if (!Number.isFinite(career.minimumMastery) || career.minimumMastery < 0 ||
          !Number.isFinite(career.masteryThreshold) || career.masteryThreshold < 1) {
          throw new Error(`career "${career.id}" mastery values are invalid`);
        }
      }
      for (const formation of pack.formations ?? []) {
        if (!Number.isFinite(formation.attackMultiplier) || formation.attackMultiplier <= 0 ||
          !Number.isFinite(formation.defenseDelta) || !Number.isInteger(formation.movementDelta) ||
          !Number.isInteger(formation.minimumAdjacentAllies) || formation.minimumAdjacentAllies < 0) {
          throw new Error(`formation "${formation.id}" has invalid modifiers`);
        }
      }
    }
  }

  private validateReferences(packs: readonly ContentPack[]): void {
    const incoming = <K extends keyof ContentPack>(key: K) =>
      packs.flatMap((pack) => (pack[key] as readonly { id: string }[] | undefined) ?? []);
    const movements = idsIn(this.catalog.movementProfiles, incoming('movementProfiles'));
    const damageTypes = idsIn(this.catalog.damageTypes, incoming('damageTypes'));
    const armorClasses = idsIn(this.catalog.armorClasses, incoming('armorClasses'));
    const terrains = idsIn(this.catalog.terrains, incoming('terrains'));
    const weapons = idsIn(this.catalog.weapons, incoming('weapons'));
    const units = idsIn(this.catalog.units, incoming('units'));
    const statuses = idsIn(this.catalog.statuses, incoming('statuses'));
    const careers = idsIn(this.catalog.careers, incoming('careers'));
    const formations = idsIn(this.catalog.formations, incoming('formations'));

    const requireId = (available: ReadonlySet<string>, id: string, owner: string): void => {
      if (!available.has(id)) throw new Error(`${owner} references missing content id "${id}"`);
    };

    for (const unit of incoming('units') as UnitDef[]) {
      requireId(movements, unit.movementClass, `unit "${unit.id}"`);
      requireId(armorClasses, unit.armorClass, `unit "${unit.id}"`);
      if (unit.weapons.length === 0) throw new Error(`unit "${unit.id}" must define at least one weapon`);
      for (const weapon of unit.weapons) requireId(weapons, weapon, `unit "${unit.id}"`);
      for (const formation of unit.formations ?? []) requireId(formations, formation, `unit "${unit.id}"`);
    }
    for (const terrain of incoming('terrains') as TerrainDef[]) {
      for (const unit of terrain.produces) requireId(units, unit, `terrain "${terrain.id}"`);
    }
    for (const career of incoming('careers') as CareerDef[]) {
      requireId(units, career.unitType, `career "${career.id}"`);
      for (const predecessor of career.from) requireId(careers, predecessor, `career "${career.id}"`);
      for (const ability of career.masteryAbilities) {
        if (!ability.trim()) throw new Error(`career "${career.id}" has an empty mastery ability id`);
      }
    }
    const incomingCareers = new Map((incoming('careers') as CareerDef[]).map((career) => [career.id, career]));
    const visitingCareers = new Set<string>();
    const visitedCareers = new Set<string>();
    const visitCareer = (id: string): void => {
      if (visitedCareers.has(id) || !incomingCareers.has(id)) return;
      if (visitingCareers.has(id)) throw new Error(`cyclic career dependency involving "${id}"`);
      visitingCareers.add(id);
      for (const predecessor of incomingCareers.get(id)!.from) visitCareer(predecessor);
      visitingCareers.delete(id);
      visitedCareers.add(id);
    };
    for (const id of incomingCareers.keys()) visitCareer(id);
    for (const weapon of incoming('weapons') as WeaponDef[]) {
      requireId(damageTypes, weapon.damageType, `weapon "${weapon.id}"`);
      for (const effect of weapon.hitEffects) {
        if (effect.type === 'addStatus' || effect.type === 'removeStatus') {
          requireId(statuses, effect.status, `weapon "${weapon.id}"`);
        } else if (!Number.isInteger(effect.distance) || effect.distance < 0 ||
          (effect.collisionDamage !== undefined && (!Number.isFinite(effect.collisionDamage) || effect.collisionDamage < 0))) {
          throw new Error(`weapon "${weapon.id}" has an invalid forced-movement effect`);
        }
      }
    }
    const matchups = [
      ...this.catalog.damageMatchups.all(),
      ...packs.flatMap((pack) => pack.damageMatchups ?? []),
    ];
    const hasMatchup = (damageType: string, armorClass: string): boolean =>
      matchups.some(
        (matchup) => matchup.damageType === damageType && matchup.armorClass === armorClass,
      );
    for (const pack of packs) {
      const packUnits = pack.units ?? [];
      const weaponById = new Map([
        ...this.catalog.weapons.all(),
        ...packs.flatMap((candidate) => candidate.weapons ?? []),
      ].map((weapon) => [weapon.id, weapon]));
      const usedDamageTypes = new Set([
        ...(pack.weapons ?? []).map((weapon) => weapon.damageType),
        ...packUnits.flatMap((unit) => unit.weapons.map((id) => weaponById.get(id)?.damageType).filter((id): id is string => id !== undefined)),
      ]);
      const usedArmorClasses = new Set(packUnits.map((unit) => unit.armorClass));
      for (const damageType of usedDamageTypes) {
        for (const armorClass of usedArmorClasses) {
          if (!hasMatchup(damageType, armorClass)) {
            throw new Error(`missing damage matchup: "${damageType}" -> "${armorClass}"`);
          }
        }
      }
    }
    for (const overlay of incoming('terrainOverlays') as TerrainOverlayDef[]) {
      if (overlay.turnStartStatus) {
        requireId(statuses, overlay.turnStartStatus.id, `terrain overlay "${overlay.id}"`);
      }
    }
    for (const tactic of incoming('tactics') as TacticDef[]) {
      for (const effect of tactic.effects) requireId(statuses, effect.status, `tactic "${tactic.id}"`);
    }
    for (const pack of packs) {
      for (const terrain of Object.values(pack.terrainCharacters ?? {})) {
        requireId(terrains, terrain, `content pack "${pack.id}" terrain encoding`);
      }
      if (pack.defaultTerrain !== undefined) requireId(terrains, pack.defaultTerrain, `content pack "${pack.id}"`);
    }
  }
}

export const GlobalContentPacks = new ContentPackInstaller(GlobalContentCatalog);

export function installContentPacks(...packs: readonly ContentPack[]): string[] {
  return GlobalContentPacks.install(...packs);
}
