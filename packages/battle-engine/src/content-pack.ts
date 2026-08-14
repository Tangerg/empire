import { DamageMatchupRegistry } from './data/damage';
import { TerrainEncodingRegistry } from './data/terrain-encoding';
import { ContentRegistry } from './registry';
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
  movementProfiles: ContentRegistry<MovementProfileDef>;
  damageTypes: ContentRegistry<DamageTypeDef>;
  armorClasses: ContentRegistry<ArmorClassDef>;
  damageMatchups: DamageMatchupRegistry;
  terrains: ContentRegistry<TerrainDef>;
  terrainEncoding: TerrainEncodingRegistry;
  weapons: ContentRegistry<WeaponDef>;
  units: ContentRegistry<UnitDef>;
  statuses: ContentRegistry<StatusDef>;
  structures: ContentRegistry<StructureDef>;
  terrainOverlays: ContentRegistry<TerrainOverlayDef>;
  tactics: ContentRegistry<TacticDef>;
  careers: ContentRegistry<CareerDef>;
  formations: ContentRegistry<FormationDef>;
}

/**
 * A family of definitions the catalog holds, keyed by the field it lives under
 * in both a pack and a catalog — they are deliberately the same name — and
 * valued by how it introduces itself in an error message.
 *
 * The twelve families used to be written out five times: two interfaces, a
 * factory, a cloner, and a pack-to-catalog mapping. Adding a thirteenth meant
 * finding all five, and the one that mattered was the cloner: a family missed
 * there is a registry silently *shared* between two engines, which is the one
 * isolation bug the architecture exists to prevent. Typing this table as an
 * exhaustive `Record` makes the compiler find the other places for you.
 */
const DEFINITION_FAMILIES: Record<DefinitionFamily, string> = {
  movementProfiles: 'movement profile',
  damageTypes: 'damage type',
  armorClasses: 'armor class',
  terrains: 'terrain',
  weapons: 'weapon',
  units: 'unit',
  statuses: 'status',
  structures: 'structure',
  terrainOverlays: 'terrain overlay',
  tactics: 'tactic',
  careers: 'career',
  formations: 'formation',
};

/** Every catalog field that is a plain registry of `{ id }` definitions. */
type DefinitionFamily = Exclude<keyof ContentCatalog, 'terrainEncoding' | 'damageMatchups'>;

const definitionFamilies = (): DefinitionFamily[] =>
  Object.keys(DEFINITION_FAMILIES) as DefinitionFamily[];

/** The two families that are not id-keyed tables keep their own constructors. */
function catalogOf(
  registries: Record<DefinitionFamily, ContentRegistry<{ id: string }>>,
  damageMatchups: DamageMatchupRegistry,
  terrainEncoding: TerrainEncodingRegistry,
): ContentCatalog {
  return { ...registries, damageMatchups, terrainEncoding } as unknown as ContentCatalog;
}

function eachFamily(
  build: (family: DefinitionFamily) => ContentRegistry<{ id: string }>,
): Record<DefinitionFamily, ContentRegistry<{ id: string }>> {
  return Object.fromEntries(definitionFamilies().map((family) => [family, build(family)])) as
    Record<DefinitionFamily, ContentRegistry<{ id: string }>>;
}

const familyRegistry = (catalog: ContentCatalog, family: DefinitionFamily) =>
  catalog[family] as ContentRegistry<{ id: string }>;

export function createContentCatalog(): ContentCatalog {
  return catalogOf(
    eachFamily((family) => new ContentRegistry(DEFINITION_FAMILIES[family])),
    new DamageMatchupRegistry(),
    new TerrainEncodingRegistry(),
  );
}

/**
 * Snapshot of definition registries for one engine/sandbox instance.
 *
 * Deep, not structural: a catalog owns its definitions, so one engine's balance
 * override cannot reach another engine built from the same packs.
 */
export function cloneContentCatalog(source: ContentCatalog): ContentCatalog {
  return catalogOf(
    eachFamily((family) => {
      const original = familyRegistry(source, family);
      const copy = original.clone();
      for (const definition of original.all()) copy.override(definition.id, structuredClone(definition));
      return copy;
    }),
    source.damageMatchups.clone(),
    source.terrainEncoding.clone(),
  );
}

/**
 * One installation under inspection: the packs about to land, the catalog they
 * land in, and the ids that exist once they have.
 *
 * Validation used to be five private methods on the installer, two of which
 * were hundred-line procedures covering a dozen unrelated rules apiece behind
 * local closures. The level linter had already been through this and came out
 * as an inspection plus a list of named checks; this is the same shape, for the
 * same reason: a rule nobody can name is a rule nobody can find.
 */
class ContentInstallation {
  constructor(
    readonly catalog: ContentCatalog,
    readonly packs: readonly ContentPack[],
  ) {}

  /** Definitions of one family the pending packs declare, in install order. */
  declared<K extends DefinitionFamily>(family: K): readonly DeclaredOf<K>[] {
    return this.packs.flatMap((pack) => (pack[family] ?? []) as readonly DeclaredOf<K>[]);
  }

  /** Every id of a family that will exist once this installation lands. */
  ids(family: DefinitionFamily): ReadonlySet<string> {
    return new Set([
      ...familyRegistry(this.catalog, family).ids(),
      ...this.declared(family).map((entry) => entry.id),
    ]);
  }

  /** Weapons this installation can resolve by id, catalog included. */
  weaponsById(): ReadonlyMap<string, WeaponDef> {
    return new Map([...this.catalog.weapons.all(), ...this.declared('weapons')]
      .map((weapon) => [weapon.id, weapon]));
  }

  requireId(family: DefinitionFamily, id: string, owner: string): void {
    if (!this.ids(family).has(id)) {
      throw new Error(`${owner} references missing content id "${id}"`);
    }
  }

  /** An installation is transactional, so the first broken rule refuses it. */
  reject(message: string): never {
    throw new Error(message);
  }
}

type DeclaredOf<K extends DefinitionFamily> = NonNullable<ContentPack[K]>[number];
type ContentCheck = (installation: ContentInstallation) => void;

/* ------------------------------------------------------------------ identity */

const checkUniqueDefinitions: ContentCheck = (installation) => {
  for (const family of definitionFamilies()) {
    const registry = familyRegistry(installation.catalog, family);
    const seen = new Set<string>();
    for (const entry of installation.declared(family)) {
      if (registry.has(entry.id) || seen.has(entry.id)) {
        installation.reject(`content ${family} id already registered: "${entry.id}"`);
      }
      seen.add(entry.id);
    }
  }
};

const checkTerrainEncoding: ContentCheck = (installation) => {
  const { catalog } = installation;
  const characters = new Set<string>();
  const terrains = new Set<string>();
  let defaultTerrain: string | undefined;
  for (const pack of installation.packs) {
    for (const [character, terrain] of Object.entries(pack.terrainCharacters ?? {})) {
      if ([...character].length !== 1) {
        installation.reject(`terrain character must contain one code point: "${character}"`);
      }
      if (catalog.terrainEncoding.hasCharacter(character) || characters.has(character)) {
        installation.reject(`terrain character already registered: "${character}"`);
      }
      if (catalog.terrainEncoding.hasTerrain(terrain) || terrains.has(terrain)) {
        installation.reject(`terrain already has a character: "${terrain}"`);
      }
      characters.add(character);
      terrains.add(terrain);
    }
    if (pack.defaultTerrain !== undefined) {
      if (defaultTerrain !== undefined && defaultTerrain !== pack.defaultTerrain) {
        installation.reject('multiple default terrains requested in one installation');
      }
      defaultTerrain = pack.defaultTerrain;
    }
  }
  const installed = catalog.terrainEncoding.currentDefaultTerrain();
  if (defaultTerrain !== undefined && installed !== null && installed !== defaultTerrain) {
    installation.reject(`default terrain already registered: "${installed}"`);
  }
};

const checkTerrainCharacterTargets: ContentCheck = (installation) => {
  for (const pack of installation.packs) {
    for (const terrain of Object.values(pack.terrainCharacters ?? {})) {
      installation.requireId('terrains', terrain, `content pack "${pack.id}" terrain encoding`);
    }
    if (pack.defaultTerrain !== undefined) {
      installation.requireId('terrains', pack.defaultTerrain, `content pack "${pack.id}"`);
    }
  }
};

/* ------------------------------------------------------------------- numbers */

function requireAccounts(installation: ContentInstallation, accounts: ResourceAccounts, owner: string): void {
  for (const [resource, account] of Object.entries(accounts)) {
    if (!resource.trim()) installation.reject(`${owner} has an empty resource id`);
    if (!Number.isFinite(account.current) || account.current < 0) {
      installation.reject(`${owner} resource "${resource}" current must be finite and non-negative`);
    }
    if (account.capacity !== null &&
      (!Number.isFinite(account.capacity) || account.capacity < account.current)) {
      installation.reject(`${owner} resource "${resource}" capacity must be null or at least current`);
    }
  }
}

function requireAmounts(
  installation: ContentInstallation,
  amounts: readonly ResourceAmount[],
  owner: string,
): void {
  for (const amount of amounts) {
    if (!amount.resource.trim()) installation.reject(`${owner} has an empty resource id`);
    if (!Number.isFinite(amount.amount) || amount.amount <= 0) {
      installation.reject(`${owner} resource "${amount.resource}" amount must be finite and positive`);
    }
  }
}

/* --------------------------------------------------------------------- units */

const checkUnits: ContentCheck = (installation) => {
  for (const unit of installation.declared('units')) {
    const owner = `unit "${unit.id}"`;
    if (!Number.isFinite(unit.value) || unit.value < 0) {
      installation.reject(`${owner} value must be finite and non-negative`);
    }
    requireAccounts(installation, unit.resources, owner);
    requireAmounts(installation, unit.recruitCosts, `${owner} recruit cost`);
    if (unit.morale && (!Number.isFinite(unit.morale.maximum) || unit.morale.maximum < 1 ||
      !Number.isFinite(unit.morale.resilience) || unit.morale.resilience < 0 || unit.morale.resilience > 0.9)) {
      installation.reject(`${owner} has an invalid morale profile`);
    }
    if (unit.transport && (!Number.isInteger(unit.transport.capacity) || unit.transport.capacity < 1)) {
      installation.reject(`${owner} transport capacity must be a positive integer`);
    }
    installation.requireId('movementProfiles', unit.movementClass, owner);
    installation.requireId('armorClasses', unit.armorClass, owner);
    if (unit.weapons.length === 0) installation.reject(`${owner} must define at least one weapon`);
    for (const weapon of unit.weapons) installation.requireId('weapons', weapon, owner);
    for (const formation of unit.formations ?? []) installation.requireId('formations', formation, owner);
  }
};

/* ------------------------------------------------------------------- weapons */

const checkWeapons: ContentCheck = (installation) => {
  for (const weapon of installation.declared('weapons')) {
    const owner = `weapon "${weapon.id}"`;
    requireAccounts(installation, weapon.resources, owner);
    requireAmounts(installation, weapon.resourceRequirements, `${owner} requirement`);
    requireAmounts(installation, weapon.resourceCosts, `${owner} cost`);
    installation.requireId('damageTypes', weapon.damageType, owner);
    for (const effect of weapon.hitEffects) {
      // Only the payloads a catalog can judge on its own. A rider a rule plugin
      // defines is checked by that plugin — this used to assume every kind it
      // did not recognise was a forced move, which refused any pack that used
      // one at all.
      if (effect.type === 'addStatus' || effect.type === 'removeStatus') {
        installation.requireId('statuses', effect.status, owner);
      } else if (effect.type === 'forcedMove' && (!Number.isInteger(effect.distance) ||
        effect.distance < 0 ||
        (effect.collisionDamage !== undefined &&
          (!Number.isFinite(effect.collisionDamage) || effect.collisionDamage < 0)))) {
        installation.reject(`${owner} has an invalid forced-movement effect`);
      }
    }
  }
};

/**
 * Every damage type a pack fields must be answerable against every armour class
 * it fields, or a strike between them has no number.
 */
const checkMatchupCoverage: ContentCheck = (installation) => {
  const matchups = [
    ...installation.catalog.damageMatchups.all(),
    ...installation.packs.flatMap((pack) => pack.damageMatchups ?? []),
  ];
  const covered = new Set(matchups.map((matchup) => `${matchup.damageType}\u0000${matchup.armorClass}`));
  const weaponsById = installation.weaponsById();
  for (const pack of installation.packs) {
    const units = pack.units ?? [];
    const damageTypes = new Set([
      ...(pack.weapons ?? []).map((weapon) => weapon.damageType),
      ...units.flatMap((unit) => unit.weapons.flatMap((id) => {
        const weapon = weaponsById.get(id);
        return weapon ? [weapon.damageType] : [];
      })),
    ]);
    const armorClasses = new Set(units.map((unit) => unit.armorClass));
    for (const damageType of damageTypes) {
      for (const armorClass of armorClasses) {
        if (!covered.has(`${damageType}\u0000${armorClass}`)) {
          installation.reject(`missing damage matchup: "${damageType}" -> "${armorClass}"`);
        }
      }
    }
  }
};

const checkDamageMatchups: ContentCheck = (installation) => {
  const seen = new Set<string>();
  for (const pack of installation.packs) {
    for (const matchup of pack.damageMatchups ?? []) {
      if (!Number.isFinite(matchup.multiplier) || matchup.multiplier <= 0) {
        installation.reject(`damage matchup "${matchup.damageType}" -> "${matchup.armorClass}" must be > 0`);
      }
      const key = `${matchup.damageType}\u0000${matchup.armorClass}`;
      if (installation.catalog.damageMatchups.has(matchup.damageType, matchup.armorClass) || seen.has(key)) {
        installation.reject(
          `damage matchup already registered: "${matchup.damageType}" -> "${matchup.armorClass}"`,
        );
      }
      seen.add(key);
    }
  }
};

/* ----------------------------------------------------- the rest of the world */

const checkTerrains: ContentCheck = (installation) => {
  for (const terrain of installation.declared('terrains')) {
    const owner = `terrain "${terrain.id}"`;
    requireAmounts(installation, terrain.ownerTurnGrants, `${owner} grant`);
    for (const unit of terrain.produces) installation.requireId('units', unit, owner);
  }
};

const checkCareers: ContentCheck = (installation) => {
  for (const career of installation.declared('careers')) {
    const owner = `career "${career.id}"`;
    requireAmounts(installation, career.costs, `${owner} cost`);
    if (!Number.isInteger(career.tier) || career.tier < 0) {
      installation.reject(`${owner} tier must be a non-negative integer`);
    }
    if (!Number.isFinite(career.minimumMastery) || career.minimumMastery < 0 ||
      !Number.isFinite(career.masteryThreshold) || career.masteryThreshold < 1) {
      installation.reject(`${owner} mastery values are invalid`);
    }
    installation.requireId('units', career.unitType, owner);
    for (const predecessor of career.from) installation.requireId('careers', predecessor, owner);
    for (const ability of career.masteryAbilities) {
      if (!ability.trim()) installation.reject(`${owner} has an empty mastery ability id`);
    }
  }
};

/** A career tree that loops would let a unit promote itself forever. */
const checkCareerTree: ContentCheck = (installation) => {
  const declared = new Map(installation.declared('careers').map((career) => [career.id, career]));
  const visiting = new Set<string>();
  const visited = new Set<string>();
  const visit = (id: string): void => {
    const career = declared.get(id);
    if (visited.has(id) || !career) return;
    if (visiting.has(id)) installation.reject(`cyclic career dependency involving "${id}"`);
    visiting.add(id);
    for (const predecessor of career.from) visit(predecessor);
    visiting.delete(id);
    visited.add(id);
  };
  for (const id of declared.keys()) visit(id);
};

const checkFormations: ContentCheck = (installation) => {
  for (const formation of installation.declared('formations')) {
    if (!Number.isFinite(formation.attackMultiplier) || formation.attackMultiplier <= 0 ||
      !Number.isFinite(formation.defenseDelta) || !Number.isInteger(formation.movementDelta) ||
      !Number.isInteger(formation.minimumAdjacentAllies) || formation.minimumAdjacentAllies < 0) {
      installation.reject(`formation "${formation.id}" has invalid modifiers`);
    }
  }
};

const checkTactics: ContentCheck = (installation) => {
  for (const tactic of installation.declared('tactics')) {
    const owner = `tactic "${tactic.id}"`;
    requireAmounts(installation, tactic.costs, `${owner} cost`);
    for (const effect of tactic.effects) installation.requireId('statuses', effect.status, owner);
  }
};

const checkTerrainOverlays: ContentCheck = (installation) => {
  for (const overlay of installation.declared('terrainOverlays')) {
    if (overlay.turnStartStatus) {
      installation.requireId('statuses', overlay.turnStartStatus.id, `terrain overlay "${overlay.id}"`);
    }
  }
};

/** Every rule an installation has to satisfy, each one named. */
const CONTENT_CHECKS: readonly ContentCheck[] = [
  checkUniqueDefinitions,
  checkTerrainEncoding,
  checkTerrainCharacterTargets,
  checkDamageMatchups,
  checkUnits,
  checkWeapons,
  checkTerrains,
  checkCareers,
  checkCareerTree,
  checkFormations,
  checkTactics,
  checkTerrainOverlays,
  checkMatchupCoverage,
];

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

    const ordered = this.orderPending(pending);
    const installation = new ContentInstallation(this.catalog, ordered);
    for (const check of CONTENT_CHECKS) check(installation);

    for (const pack of ordered) this.apply(pack);
    return ordered.map((pack) => pack.id);
  }

  isInstalled(id: string): boolean {
    return this.installed.has(id);
  }

  installedPacks(): ReadonlyMap<string, number> {
    return new Map(this.installed);
  }

  /** Pack identity and dependency order, which decides what the checks even see. */
  private orderPending(pending: readonly ContentPack[]): ContentPack[] {
    const ids = new Set<string>();
    for (const pack of pending) {
      if (!pack.id.trim()) throw new Error('content pack id cannot be empty');
      if (!Number.isInteger(pack.version) || pack.version < 1) {
        throw new Error(`content pack "${pack.id}" version must be a positive integer`);
      }
      if (ids.has(pack.id)) throw new Error(`duplicate content pack request: "${pack.id}"`);
      ids.add(pack.id);
    }
    return orderByDependencies(pending, {
      idOf: (pack) => pack.id,
      dependenciesOf: (pack) => pack.dependencies ?? [],
      isSatisfiedExternally: (id) => this.installed.has(id),
      missing: (pack, dependency) =>
        new Error(`content pack "${pack.id}" requires "${dependency}"`),
      cycle: (path) => new Error(`cyclic content pack dependency: ${path.join(' -> ')}`),
    });
  }

  private apply(pack: ContentPack): void {
    for (const family of definitionFamilies()) {
      const entries = pack[family] as readonly { id: string }[] | undefined;
      // Deep-copy on install: a pack is a *declaration*, a catalog *owns* its
      // definitions. Sharing the objects would let one catalog's balance
      // override leak into another catalog built from the same pack.
      if (entries) {
        familyRegistry(this.catalog, family).defineAll(entries.map((entry) => structuredClone(entry)));
      }
    }
    if (pack.terrainCharacters !== undefined || pack.defaultTerrain !== undefined) {
      this.catalog.terrainEncoding.register(pack.terrainCharacters ?? {}, pack.defaultTerrain);
    }
    if (pack.damageMatchups) this.catalog.damageMatchups.register(pack.damageMatchups);
    this.installed.set(pack.id, pack.version);
  }
}
