/**
 * Core domain types. Nothing here touches the DOM — the whole rules engine is
 * headless so it can run in tests, in a worker, or on a server later.
 *
 * Extension points are deliberate:
 *  - TerrainId / UnitTypeId / AbilityId are plain strings backed by registries,
 *    so new content is *data*, not new code paths.
 *  - `RuleSet` carries every tunable rule, so a level (or a future game mode)
 *    can change behaviour without forking the engine.
 *  - `Unit.meta` / `LevelData.extra` remain prototype escape hatches only;
 *    established mechanics have explicit, serialisable fields below.
 */

/**
 * Stable content id backed by the movement-profile registry. It intentionally
 * remains a string so a content pack can add naval, amphibious, hover, phase,
 * or another movement model without changing the engine's union types.
 */
export type MovementClass = string;
/** Open content ids. A theme may model steel, ballistics, energy, psionics, shields, or formations. */
export type DamageType = string;
export type ArmorClass = string;

/** 1..N are real players. 0 means "nobody owns this". */
export type PlayerId = number;
export interface Coord {
  x: number;
  y: number;
}

export type TerrainId = string;
export type UnitTypeId = string;
export type AbilityId = string;
export type StatusId = string;
export type WeaponId = string;
export type StructureTypeId = string;
export type StructureId = string;
export type OverlayTypeId = string;
export type OverlayId = string;
export type CommanderId = string;
export type TacticId = string;
export type ResourceId = string;
export type CareerId = string;
export type FormationId = string;
export type WeaponArea = string;
export type UnitRank = 0 | 1 | 2;
/**
 * Open content id backed by the reaction registry, like every other content id
 * here. A closed union would have meant a content pack could not add a stance
 * without editing the engine's types — which is exactly what the registry is
 * for.
 */
export type ReactionStance = string;
/**
 * Open for the same reason a reaction stance is: the tiling owns the set.
 *
 * A four-way board faces north/east/south/west, an eight-way board also faces
 * the corners, and a hex board has no north at all. `TacticalGrid.directions`
 * is the list; nothing else may assume its length or its members.
 */
export type Direction = string;
export type CoverLevel = 'none' | 'half' | 'full';

export type MoveCosts = Record<MovementClass, number | null>;

export interface ResourceAccountState {
  current: number;
  /** null means no upper bound. */
  capacity: number | null;
}

export type ResourceAccounts = Record<ResourceId, ResourceAccountState>;

export interface ResourceAmount {
  resource: ResourceId;
  amount: number;
}

/**
 * Open family of resource holders: who can own an account.
 *
 * It lives here beside every other extension map, and for the same reason —
 * so a plugin can declaration-merge a squad chest, a fleet's fuel or a city's
 * stores, and have every part of the engine that names a holder name it too.
 * The family used to be stated in six places, five of them closed lists.
 */
export interface ResourceSubjectKindMap {
  player: { kind: 'player'; player: PlayerState };
  unit: { kind: 'unit'; unit: Unit };
  weapon: { kind: 'weapon'; unit: Unit; weapon: WeaponId };
}

export type ResourceSubjectKind = Extract<keyof ResourceSubjectKindMap, string>;
export type ResourceSubject = ResourceSubjectKindMap[ResourceSubjectKind];

/**
 * A holder as a log line can carry it: ids, never object references.
 *
 * `slot` names the part of a holder that owns the account, when a holder has
 * parts — which magazine of which unit.
 */
export interface ResourceSubjectRef {
  kind: ResourceSubjectKind;
  id: string | number;
  slot?: string;
}

export interface ResourceTransaction extends ResourceAmount {
  subject: ResourceSubjectKind;
}

export interface MovementProfileDef {
  id: MovementClass;
  name: string;
  tags: string[];
  /** Maximum upward elevation delta per step; null ignores elevation. */
  maxClimb: number | null;
  /** Maximum safe downward elevation delta per step; null ignores elevation. */
  maxDrop: number | null;
  /** Extra movement cost for every positive elevation level entered. */
  uphillCostPerLevel: number;
  /** Flying/phase movement may cross an explicitly blocked cliff edge. */
  ignoresCliffs: boolean;
}

export interface DamageTypeDef {
  id: DamageType;
  name: string;
  tags: string[];
}

export interface ArmorClassDef {
  id: ArmorClass;
  name: string;
  tags: string[];
}

export interface DamageMatchupDef {
  damageType: DamageType;
  armorClass: ArmorClass;
  multiplier: number;
}

export interface TerrainDef {
  id: TerrainId;
  name: string;
  /** Cost to *enter* this tile, per movement class. null = impassable. */
  cost: MoveCosts;
  /** Fraction of incoming damage absorbed, 0..1. */
  defense: number;
  /** Vision bonus granted to the occupying unit. */
  vision: number;
  /** Participates in fog and direct-shot obstruction, resolved with height. */
  opaque: boolean;
  /** Omnidirectional protection when no stronger directional cover exists. */
  cover: CoverLevel;
  /** Height above the cell elevation that can obstruct a direct shot. */
  obstructionHeight: number;
  capturable: boolean;
  /** Resource grants collected by the owner at turn start. */
  ownerTurnGrants: ResourceAmount[];
  /** HP restored per turn to an owner's unit standing here. */
  heal: number;
  /** Unit types this tile can recruit. Empty = not a production site. */
  produces: UnitTypeId[];
  /** Losing this tile loses the game (the keep / 城堡). */
  hq: boolean;
  tags: string[];
}

export interface UnitDef {
  id: UnitTypeId;
  name: string;
  /** Story-neutral AI/threat valuation; payment is defined separately. */
  value: number;
  recruitCosts: ResourceAmount[];
  /** Accounts created for each deployed or recruited unit. */
  resources: ResourceAccounts;
  maxHp: number;
  /** Intrinsic damage reduction 0..1, stacks with terrain. */
  defense: number;
  movement: number;
  movementClass: MovementClass;
  armorClass: ArmorClass;
  /** Ordered weapon ids. The first entry is the default weapon. */
  weapons: WeaponId[];
  vision: number;
  abilities: AbilityId[];
  defaultReaction: ReactionStance;
  /** Optional battle-morale profile. Rules may disable morale globally. */
  morale?: {
    maximum: number;
    /** Fraction of incoming morale loss ignored, clamped to 0..0.9. */
    resilience: number;
  };
  /** Optional carrier capability; transported units retain full identity. */
  transport?: {
    capacity: number;
    allowedTags?: string[];
    forbiddenTags?: string[];
  };
  /** Formations this unit may adopt. Empty/omitted means none. */
  formations?: FormationId[];
  /**
   * Tiles of ground this type holds when `rules.zoneOfControl` is on.
   * Defaults to 1; 0 is for the units whose business is not standing in the way.
   */
  zoneOfControl?: number;
  tags: string[];
  /** Short flavour line shown in the inspector. */
  blurb: string;
}

/** Data-defined stance shared by historical formations and themed equivalents. */
export interface FormationDef {
  id: FormationId;
  name: string;
  attackMultiplier: number;
  defenseDelta: number;
  movementDelta: number;
  /** Minimum adjacent allied active units required to keep the formation. */
  minimumAdjacentAllies: number;
  tags: string[];
}

/**
 * A career is a progression node whose combat profile is supplied by a unit
 * definition. This keeps careers data-driven while a runtime unit retains its
 * identity, statuses, resources and battle history when changing careers.
 */
export interface CareerDef {
  id: CareerId;
  name: string;
  unitType: UnitTypeId;
  tier: number;
  /** Any one predecessor unlocks this branch; empty means a root career. */
  from: CareerId[];
  minimumRank: UnitRank;
  minimumMastery: number;
  costs: ResourceAmount[];
  masteryThreshold: number;
  masteryAbilities: AbilityId[];
  tags: string[];
}

export interface UnitCareerState {
  current: CareerId | null;
  unlocked: CareerId[];
  mastery: Record<CareerId, number>;
}

export interface TargetBonus {
  targetTag: string;
  multiplier: number;
  reason: string;
}

export interface WeaponDef {
  id: WeaponId;
  name: string;
  power: number;
  damageType: DamageType;
  minRange: number;
  maxRange: number;
  moveAndAttack: boolean;
  lineOfSight: 'none' | 'direct' | 'arc';
  /** Blast shape; open, resolved through the weapon-area registry. */
  area: WeaponArea;
  canCounter: boolean;
  cooldown: number;
  /** Per-weapon accounts created for every wielder, e.g. limited ammunition. */
  resources: ResourceAccounts;
  /** Checked for legality but not consumed. */
  resourceRequirements: ResourceTransaction[];
  /** Consumed only after a strike is legally committed. */
  resourceCosts: ResourceTransaction[];
  bonuses: TargetBonus[];
  hitEffects: WeaponHitEffect[];
  /**
   * Actor turns between committing this strike and resolving it. `0` resolves
   * immediately; anything higher makes the weapon a *cast*: the tile is locked
   * now and struck later, so a target may walk out of it.
   */
  castTurns: number;
  tags: string[];
}

/** Open family of deterministic effects applied after a weapon deals damage. */
export interface WeaponHitEffectKindMap {
  addStatus: { type: 'addStatus'; status: StatusId; duration: number };
  removeStatus: { type: 'removeStatus'; status: StatusId };
  forcedMove: {
    type: 'forcedMove';
    mode: 'push' | 'pull';
    distance: number;
    /** Optional impact damage when an obstruction stops movement early. */
    collisionDamage?: number;
  };
}

export type WeaponHitEffect = WeaponHitEffectKindMap[keyof WeaponHitEffectKindMap];

export interface UnitWeaponState {
  cooldownRemaining: number;
  resources: ResourceAccounts;
}

export interface StatusModifiers {
  attackMultiplier?: number;
  defenseDelta?: number;
  movementDelta?: number;
  cannotCapture?: boolean;
}

export interface PeriodicStatusEffect {
  timing: 'ownerTurnStart';
  /** Damage as a fraction of the target unit's maximum HP. */
  maxHpFraction: number;
  nonlethal: boolean;
}

export interface StatusDef {
  id: StatusId;
  name: string;
  stackMode: 'refresh' | 'extend' | 'stack';
  maxStacks: number;
  modifiers: StatusModifiers;
  periodic?: PeriodicStatusEffect;
  /** Ability or weapon tags disabled while this status is active. */
  blockedAbilityTags: string[];
  tags: string[];
}

export interface StatusInstance {
  id: StatusId;
  remaining: number;
  stacks: number;
  sourceUnitId?: number;
}

export interface StructureDef {
  id: StructureTypeId;
  name: string;
  maxHp: number;
  defense: number;
  blocksMovement: boolean;
  blocksVision: boolean;
  cover: CoverLevel;
  obstructionHeight: number;
  repairable: boolean;
  targetable: boolean;
  tags: string[];
}

export interface StructureState {
  id: StructureId;
  type: StructureTypeId;
  owner: PlayerId;
  x: number;
  y: number;
  hp: number;
  disabled: boolean;
  statuses: StatusInstance[];
}

export interface TerrainOverlayDef {
  id: OverlayTypeId;
  name: string;
  /** Added to a passable tile's entry cost; final cost is at least 1. */
  movementCostDelta: number;
  blockedMovementClasses: MovementClass[];
  defenseDelta: number;
  visionDelta: number;
  healDelta: number;
  /** Applied when an affected unit begins its owner's turn. */
  turnStartStatus?: { id: StatusId; duration: number };
  tags: string[];
}

export interface TerrainOverlayState {
  id: OverlayId;
  type: OverlayTypeId;
  cells: Coord[];
  /** null is permanent; otherwise measured in full rounds. */
  remainingRounds: number | null;
}

export interface Unit {
  id: number;
  key?: string;
  type: UnitTypeId;
  owner: PlayerId;
  x: number;
  y: number;
  hp: number;
  /** Already acted this turn. */
  done: boolean;
  /** Accumulated capture points on the tile the unit is standing on. */
  capture: number;
  /** Formal, serialisable tactical statuses. */
  statuses: StatusInstance[];
  /** Runtime state keyed by WeaponId; definitions remain in the registry. */
  weaponState: Record<WeaponId, UnitWeaponState>;
  /** Optional local formation leader; grants an aura only while in range. */
  commanderId: CommanderId | null;
  /** Per-battle growth hook; content decides thresholds and rewards. */
  rank: UnitRank;
  rankProgress: number;
  /** Capability-owned accounts such as momentum; absence means unavailable. */
  resources: ResourceAccounts;
  reaction: ReactionStance;
  /** Round number in which guard/support was consumed; -1 means unused. */
  reactionUsedRound: number;
  /** Cardinal orientation used by rear/side attacks and directional cover. */
  facing: Direction;
  morale: {
    current: number;
    maximum: number;
    resilience: number;
  };
  formation: FormationId | null;
  directive: UnitDirectiveState;
  /** Per-battle career graph state. Persistent campaigns may seed this. */
  career: UnitCareerState;
  /** Permanent-for-this-battle abilities earned by mastering careers. */
  learnedAbilities: AbilityId[];
  /** Room for future mechanics without touching the engine. */
  meta: Record<string, number | string | boolean>;
}

/**
 * Open content id backed by the directive registry, like every other content
 * id here. It shipped as a closed union of four, read by four scattered
 * branches, so a raid, a forage order or a convoy escort was a change to the
 * core rather than to a content pack.
 */
export type UnitDirectiveMode = string;

/** Story-neutral tactical intent consumed by AI but serialised with the unit. */
export interface UnitDirectiveState {
  mode: UnitDirectiveMode;
  zone?: string;
  waypoints: Coord[];
  cursor: number;
}

/* ------------------------------------------------------------------ rules */

export interface RuleSet {
  /** Ancient Empires captures a town the moment you step on it. */
  captureMode: 'instant' | 'progressive';
  /** Points needed in 'progressive' mode (a full-HP unit contributes all). */
  captureThreshold: number;
  /** Per-turn grants independent of any owned site. */
  baseResourceGrants: ResourceAmount[];
  /** Per-site resource override; missing id means use terrain grants. */
  siteResourceOverrides: Record<ResourceId, number>;
  healOnOwnedBuilding: boolean;
  /** Defender strikes back when the attacker is inside the defender's range. */
  counterAttack: boolean;
  fog: boolean;
  turnLimit: number | null;
  friendlyPassThrough: boolean;
  enemiesBlockMovement: boolean;
  /**
   * Units hold the ground around them: entering an enemy's zone ends the move,
   * and disengaging from one invites a parting shot. `UnitDef.zoneOfControl`
   * sets how far each type reaches.
   */
  zoneOfControl: boolean;
  maxUnitsPerPlayer: number | null;
  /** True lets a unit act on the turn it was bought; false is the classic rule. */
  recruitsActImmediately: boolean;
  /**
   * Registered turn-order policy id. 'side' gives Advance Wars / Ancient
   * Empires side turns; 'initiative' gives Tactics Ogre / FFT per-unit ordering.
   */
  turnOrder: string;
  /**
   * Registered tiling id: 'square4' for the classic orthogonal board, 'square8'
   * to let diagonals count as one step, 'hex' for six-neighbour cells. Storage
   * stays rectangular either way — only adjacency, distance and the picture move.
   */
  grid: string;
  highGroundThreshold: number;
  highGroundDamageMultiplier: number;
  sideAttackMultiplier: number;
  backAttackMultiplier: number;
  flankAttackMultiplier: number;
  halfCoverDefense: number;
  fullCoverDefense: number;
  /** Optional army-morale loop; disabled preserves classic attrition-only play. */
  moraleEnabled: boolean;
  /** Morale lost as a fraction of maximum HP damage dealt. */
  moraleDamageFactor: number;
  moraleAllyDefeatLoss: number;
  moraleDefeatShockRadius: number;
  moraleCommanderDefeatLoss: number;
}

export const DEFAULT_RULES: RuleSet = {
  captureMode: 'instant',
  captureThreshold: 100,
  baseResourceGrants: [],
  siteResourceOverrides: {},
  healOnOwnedBuilding: true,
  counterAttack: true,
  fog: false,
  turnLimit: null,
  friendlyPassThrough: true,
  enemiesBlockMovement: true,
  zoneOfControl: false,
  maxUnitsPerPlayer: null,
  recruitsActImmediately: false,
  turnOrder: 'side',
  grid: 'square4',
  highGroundThreshold: 1,
  highGroundDamageMultiplier: 1.1,
  sideAttackMultiplier: 1.1,
  backAttackMultiplier: 1.25,
  flankAttackMultiplier: 1.15,
  halfCoverDefense: 0.15,
  fullCoverDefense: 0.3,
  moraleEnabled: false,
  moraleDamageFactor: 0.6,
  moraleAllyDefeatLoss: 12,
  moraleDefeatShockRadius: 3,
  moraleCommanderDefeatLoss: 25,
};

/* ------------------------------------------------------------- objectives */

export interface ObjectiveMeta {
  /** Optional in a document; assigned before the battle starts. */
  id?: string;
  /** Presentation key/fallback label; story text remains outside the engine. */
  label?: string;
  hidden?: boolean;
  active?: boolean;
}

/**
 * Open objective algebra. Feature packages may add objective kinds through
 * declaration merging and register their behavior with ObjectiveHandlerRegistry.
 */
export interface ObjectiveKindMap {
  routEnemies: { type: 'routEnemies' };
  captureHQ: { type: 'captureHQ' };
  holdAllVillages: { type: 'holdAllVillages' };
  surviveTurns: { type: 'surviveTurns'; turns: number };
  eliminate: { type: 'eliminate'; selector: UnitSelector };
  destroy: { type: 'destroy'; structures: StructureId[] };
  neutralizeComposite: { type: 'neutralizeComposite'; composite: string; minimumNeutralized?: number };
  protect: { type: 'protect'; selector: UnitSelector; minimumAlive: number; untilTurn: number };
  escort: { type: 'escort'; selector: UnitSelector; zone: string; count: number };
  control: { type: 'control'; zone: string };
  score: { type: 'score'; variable: string; atLeast: number };
  interact: { type: 'interact'; variable: string; equals: ScenarioValue };
  all: { type: 'all'; objectives: Objective[] };
  any: { type: 'any'; objectives: Objective[] };
  sequence: { type: 'sequence'; objectives: Objective[] };
  optional: { type: 'optional'; objective: Objective };
  failOn: { type: 'failOn'; condition: ScenarioCondition; objective: Objective };
}

export type Objective = ObjectiveMeta & ObjectiveKindMap[keyof ObjectiveKindMap];

/**
 * An objective in play, whose id has been assigned.
 *
 * A document may leave `id` out and let the engine name the objective; a running
 * battle refers to one by id in its runtime states, its events and its saves, so
 * by then every objective has one. Two places used to assert past the optional
 * field instead of the type saying which side of that line it was on.
 */
export type RunningObjective = Objective & { id: string };

export type ObjectiveStatus = 'inactive' | 'active' | 'completed' | 'failed' | 'cancelled';
export type ObjectiveOutcome = 'pending' | 'success' | 'failure';

export interface ObjectiveRuntime {
  id: string;
  status: ObjectiveStatus;
  hidden: boolean;
}

export interface PlayerConfig {
  id: PlayerId;
  name: string;
  /** Team number — same team = allies. Usually 1:1 with player id. */
  team: number;
  color: string;
  controller: 'human' | 'ai';
  resources: ResourceAccounts;
  ai?: { aggression: number };
  /** Overrides `rules`-level victory conditions for this player. */
  objectives?: Objective[];
}

/* ------------------------------------------------------------------ level */

export interface LevelTileOwner {
  x: number;
  y: number;
  owner: PlayerId;
}

export interface LevelUnit {
  /** Stable scenario reference, unlike the generated numeric runtime id. */
  key?: string;
  x: number;
  y: number;
  unit: UnitTypeId;
  owner: PlayerId;
  hp?: number;
  commander?: CommanderId;
  rank?: UnitRank;
  rankProgress?: number;
  resources?: ResourceAccounts;
  reaction?: ReactionStance;
  facing?: Direction;
  career?: CareerId;
  unlockedCareers?: CareerId[];
  careerMastery?: Record<CareerId, number>;
  learnedAbilities?: AbilityId[];
  morale?: number;
  formation?: FormationId;
  directive?: Partial<UnitDirectiveState> & Pick<UnitDirectiveState, 'mode'>;
}

/** Explicitly impassable edge between two orthogonally adjacent cells. */
export interface LevelCliffEdge {
  from: Coord;
  to: Coord;
}

/** Directional protection on a cell, such as a wall or sandbag edge. */
export interface LevelDirectionalCover {
  at: Coord;
  sides: Partial<Record<Direction, Exclude<CoverLevel, 'none'>>>;
}

/** A player's units may be rearranged inside this scenario zone before turn 1. */
export interface LevelDeploymentZone {
  player: PlayerId;
  zone: string;
  /** Omitted means every unit owned by the player. */
  unitKeys?: string[];
}

export interface LevelDeployment {
  zones: LevelDeploymentZone[];
  /** Player order for confirming deployment; inferred from zones when omitted. */
  order?: PlayerId[];
}

export interface CommanderAura {
  attackMultiplier: number;
  defenseDelta: number;
  movementDelta: number;
}

export interface LevelCommander {
  id: CommanderId;
  unitKey: string;
  radius: number;
  aura?: Partial<CommanderAura>;
  turnGrants?: ResourceAmount[];
  tactics?: TacticId[];
}

export interface CommanderState {
  id: CommanderId;
  unitId: number;
  owner: PlayerId;
  radius: number;
  aura: CommanderAura;
  turnGrants: ResourceAmount[];
  tactics: TacticId[];
  usedTactics: TacticId[];
}

export type TacticEffect =
  | { type: 'addStatus'; status: StatusId; duration: number }
  | { type: 'removeStatus'; status: StatusId };

export interface TacticDef {
  id: TacticId;
  name: string;
  costs: ResourceAmount[];
  range: number;
  radius: number;
  target: 'self' | 'tile';
  effects: TacticEffect[];
  tags: string[];
}

export interface LevelStructure {
  id: StructureId;
  type: StructureTypeId;
  owner?: PlayerId;
  x: number;
  y: number;
  hp?: number;
  disabled?: boolean;
}

/** Several targetable structures acting as one large boss, ship, gate, or fortress. */
export interface LevelComposite {
  id: string;
  parts: StructureId[];
  /** Number of destroyed/disabled parts required to neutralize the whole. */
  minimumNeutralized?: number;
  tags?: string[];
}

export interface CompositeState {
  id: string;
  parts: StructureId[];
  minimumNeutralized: number;
  tags: string[];
}

export interface LevelZone {
  id: string;
  cells: Coord[];
  tags?: string[];
}

/** Runtime-local restrictions such as a bridge truce or protected hospital. */
export interface ZoneEngagementRule {
  id: string;
  zone: string;
  mode: 'no-attacks' | 'no-hostile-actions';
  players?: PlayerId[];
}

export type ScenarioValue = number | string | boolean;

export interface UnitSelector {
  ids?: number[];
  keys?: string[];
  owner?: PlayerId;
  zone?: string;
  anyTags?: string[];
}

export interface MarkerSelector {
  ids?: number[];
  kind?: string;
  owner?: PlayerId;
  zone?: string;
}

export interface ScenarioConditionKindMap {
  turnAtLeast: { type: 'turnAtLeast'; turn: number };
  turnCycle: { type: 'turnCycle'; every: number; offset?: number };
  /** Seeded coin flip; reproducible because the stream lives on the state. */
  chance: { type: 'chance'; percent: number; stream?: string };
  currentPlayer: { type: 'currentPlayer'; player: PlayerId };
  variable: { type: 'variable'; key: string; op: 'eq' | 'neq' | 'gte' | 'lte'; value: ScenarioValue };
  unitInZone: { type: 'unitInZone'; zone: string; owner?: PlayerId; anyTags?: string[] };
  unitCount: {
    type: 'unitCount';
    selector: UnitSelector;
    op: 'eq' | 'neq' | 'gte' | 'lte';
    value: number;
  };
  unitHealth: {
    type: 'unitHealth';
    selector: UnitSelector;
    aggregate: 'any' | 'all' | 'average';
    op: 'eq' | 'neq' | 'gte' | 'lte';
    /** Percentage of maximum HP, from 0 to 1. */
    value: number;
  };
  markerCount: {
    type: 'markerCount';
    selector: MarkerSelector;
    op: 'eq' | 'neq' | 'gte' | 'lte';
    value: number;
  };
  eventCount: {
    type: 'eventCount';
    event: Extract<GameEvent['type'], string>;
    op: 'eq' | 'neq' | 'gte' | 'lte';
    value: number;
  };
  structure: { type: 'structure'; id: StructureId; state: 'intact' | 'disabled' | 'destroyed' };
  composite: { type: 'composite'; id: string; state: 'intact' | 'damaged' | 'neutralized' };
  objective: { type: 'objective'; player: PlayerId; id: string; status: ObjectiveStatus };
  all: { type: 'all'; conditions: ScenarioCondition[] };
  any: { type: 'any'; conditions: ScenarioCondition[] };
  not: { type: 'not'; condition: ScenarioCondition };
}

export type ScenarioCondition = ScenarioConditionKindMap[keyof ScenarioConditionKindMap];

export interface ScenarioEffectKindMap {
  setVariable: { type: 'setVariable'; key: string; value: ScenarioValue };
  addVariable: { type: 'addVariable'; key: string; amount: number };
  addStatus: { type: 'addStatus'; selector: UnitSelector; status: StatusId; duration: number };
  removeStatus: { type: 'removeStatus'; selector: UnitSelector; status: StatusId };
  changeUnitOwner: { type: 'changeUnitOwner'; selector: UnitSelector; owner: PlayerId };
  spawnUnits: {
    type: 'spawnUnits';
    units: LevelUnit[];
    ready?: boolean;
    reason?: 'reinforcement' | 'summon';
  };
  withdrawUnits: { type: 'withdrawUnits'; selector: UnitSelector; leaveCorpse?: boolean };
  reviveMarkers: {
    type: 'reviveMarkers';
    selector: MarkerSelector;
    /** Restored owner; omitted keeps the fallen unit's owner. */
    owner?: PlayerId;
    hpPercent?: number;
  };
  removeMarkers: { type: 'removeMarkers'; selector: MarkerSelector };
  setPlayerTeam: { type: 'setPlayerTeam'; player: PlayerId; team: number };
  forceMove: {
    type: 'forceMove';
    selector: UnitSelector;
    mode: 'push' | 'pull';
    source: Coord;
    distance: number;
    collisionDamage?: number;
  };
  teleportUnits: { type: 'teleportUnits'; selector: UnitSelector; zone: string };
  addOverlay: { type: 'addOverlay'; id: OverlayId; overlay: OverlayTypeId; zone: string; rounds?: number | null };
  removeOverlay: { type: 'removeOverlay'; id: OverlayId };
  activateObjective: { type: 'activateObjective'; player: PlayerId; id: string };
  cancelObjective: { type: 'cancelObjective'; player: PlayerId; id: string };
  completeObjective: { type: 'completeObjective'; player: PlayerId; id: string };
  revealObjective: { type: 'revealObjective'; player: PlayerId; id: string };
  changeUnitResource: {
    type: 'changeUnitResource';
    selector: UnitSelector;
    resource: ResourceId;
    amount: number;
  };
  changeMorale: { type: 'changeMorale'; selector: UnitSelector; amount: number; reason?: string };
  surrenderUnits: { type: 'surrenderUnits'; selector: UnitSelector; to?: PlayerId };
  restoreWithdrawnUnits: {
    type: 'restoreWithdrawnUnits';
    selector: MarkerSelector;
    zone: string;
    owner?: PlayerId;
  };
  setUnitDirective: {
    type: 'setUnitDirective';
    selector: UnitSelector;
    directive: Partial<UnitDirectiveState> & Pick<UnitDirectiveState, 'mode'>;
  };
  addEngagementRule: { type: 'addEngagementRule'; rule: ZoneEngagementRule };
  removeEngagementRule: { type: 'removeEngagementRule'; id: string };
  replaceTerrain: { type: 'replaceTerrain'; zone: string; terrain: TerrainId };
  setElevation: { type: 'setElevation'; zone: string; value: number };
  addElevation: { type: 'addElevation'; zone: string; amount: number };
  setCliffs: { type: 'setCliffs'; edges: LevelCliffEdge[]; blocked: boolean };
  setDirectionalCover: {
    type: 'setDirectionalCover';
    covers: LevelDirectionalCover[];
  };
  damageStructure: { type: 'damageStructure'; id: StructureId; amount: number };
  repairStructure: { type: 'repairStructure'; id: StructureId; amount: number };
  moveComposite: { type: 'moveComposite'; id: string; dx: number; dy: number };
  emitSignal: { type: 'emitSignal'; signal: string };
}

export type ScenarioEffect = ScenarioEffectKindMap[keyof ScenarioEffectKindMap];

export interface ScenarioTrigger {
  id: string;
  timing: 'afterAction' | 'turnStart' | 'turnEnd';
  condition: ScenarioCondition;
  effects: ScenarioEffect[];
  /** Omitted triggers are one-shot. Repeating triggers fire at most once per timing occurrence. */
  repeat?: {
    everyRounds: number;
    startTurn?: number;
    endTurn?: number;
    maxFirings?: number;
  };
}

export interface LevelScenario {
  variables?: Record<string, ScenarioValue>;
  zones?: LevelZone[];
  overlays?: Array<{
    id: OverlayId;
    type: OverlayTypeId;
    zone: string;
    remainingRounds?: number | null;
  }>;
  triggers?: ScenarioTrigger[];
  engagementRules?: ZoneEngagementRule[];
}

export interface ScenarioState {
  variables: Record<string, ScenarioValue>;
  zones: Record<string, Coord[]>;
  overlays: TerrainOverlayState[];
  triggers: ScenarioTrigger[];
  firedTriggerIds: string[];
  triggerRuntime: Record<string, { count: number; lastOccurrence: string }>;
  eventCounts: Record<string, number>;
  zoneTags: Record<string, string[]>;
  engagementRules: ZoneEngagementRule[];
}

/** The on-disk / editor format. Terrain rows are legend characters. */
export interface LevelData {
  schema: 2;
  id: string;
  name: string;
  author?: string;
  description?: string;
  width: number;
  height: number;
  terrain: string[];
  /** Flat row-major elevation values; omitted means a completely level map. */
  elevation?: number[];
  cliffs?: LevelCliffEdge[];
  directionalCover?: LevelDirectionalCover[];
  owners: LevelTileOwner[];
  units: LevelUnit[];
  commanders?: LevelCommander[];
  structures?: LevelStructure[];
  composites?: LevelComposite[];
  players: PlayerConfig[];
  rules: Partial<RuleSet>;
  victory: Objective[];
  scenario?: LevelScenario;
  deployment?: LevelDeployment;
  /** Opaque campaign/content metadata; the battle core does not interpret it. */
  extra?: Record<string, unknown>;
}

/* ------------------------------------------------------------------ state */

export interface GameMap {
  width: number;
  height: number;
  /** Flat row-major array of terrain ids. */
  tiles: TerrainId[];
  /** Flat row-major array of tile owners (0 = neutral). */
  owners: PlayerId[];
  /** Flat row-major capture progress for the *current* contender. */
  captureProgress: number[];
  /** Flat row-major integer elevation layer. */
  elevation: number[];
  cliffs: LevelCliffEdge[];
  directionalCover: LevelDirectionalCover[];
}

export interface PlayerState {
  id: PlayerId;
  name: string;
  team: number;
  color: string;
  controller: 'human' | 'ai';
  resources: ResourceAccounts;
  alive: boolean;
  /** Did this player control a keep at level start? Gates the captureHQ goal. */
  startedWithHQ: boolean;
  objectives: RunningObjective[];
  objectiveStates: Record<string, ObjectiveRuntime>;
  ai: { aggression: number };
}

export interface BattlefieldMarker {
  id: number;
  kind: string;
  at: Coord;
  owner: PlayerId;
  /** Corpse markers own the complete battle-local unit snapshot needed to revive it. */
  fallenUnit?: Unit;
  meta: Record<string, number | string | boolean>;
}

export interface DeploymentAssignment {
  player: PlayerId;
  zone: string;
  unitIds: number[];
}

export interface DeploymentState {
  order: PlayerId[];
  currentIndex: number;
  assignments: DeploymentAssignment[];
}

/**
 * Turn-order state. `data` is opaque to the engine: the policy named by
 * `policy` is the only thing that interprets it, which keeps a save file from
 * being reinterpreted by a different ordering rule.
 */
/**
 * Serialisable random stream. Counter-based so streams stay independent: a new
 * consumer cannot shift the numbers an existing one already receives.
 */
export interface RandomState {
  seed: number;
  counters: Record<string, number>;
}

export interface TurnOrderState {
  policy: string;
  /** Unit currently entitled to act; null when a whole side may act. */
  activeUnit: number | null;
  data: Record<string, number>;
}

export type GamePhase = 'deployment' | 'playing' | 'over';

export interface GameState {
  levelId: string;
  levelName: string;
  map: GameMap;
  units: Unit[];
  structures: StructureState[];
  composites: CompositeState[];
  /** Off-board units currently carried by active transport units. */
  embarkedUnits: Array<{ carrier: number; unit: Unit }>;
  markers: BattlefieldMarker[];
  commanders: CommanderState[];
  players: PlayerState[];
  rules: RuleSet;
  /** 1-based round counter; increments when the turn wraps to the first player. */
  turn: number;
  currentPlayer: PlayerId;
  phase: GamePhase;
  winnerTeam: number | null;
  endReason: string;
  nextUnitId: number;
  nextMarkerId: number;
  deployment: DeploymentState | null;
  scenario: ScenarioState;
  turnOrder: TurnOrderState;
  /**
   * Monotonic count of actor turns handed out. Delays are measured in this
   * unit so a content pack means the same thing under either turn-order
   * family: "two turns from now" is two entitlements to act, whether those
   * belong to whole sides or to single units.
   */
  actorTurns: number;
  /** Committed strikes still charging. At most one per caster. */
  pendingCasts: PendingCast[];
  random: RandomState;
}

/**
 * A strike that has been committed but not yet resolved.
 *
 * The aim point and launch tile are frozen when the cast begins, so the shot
 * keeps the geometry it was fired with even if the battlefield moves under it.
 * A unit sustains at most one cast, which is why the caster is the identity.
 */
/** Why a committed cast failed to land. */
export type CastRefusal = 'casterLost' | 'weaponUnavailable' | 'targetProtected' | 'targetVacated';

export interface PendingCast {
  caster: number;
  owner: PlayerId;
  ability: AbilityId;
  weapon: WeaponId;
  /** Tile the strike is aimed at, locked when the cast began. */
  target: Coord;
  /** Tile it was launched from, so range and line of sight stay as cast. */
  origin: Coord;
  /** Actor-turn readings: when it was committed, and when it lands. */
  declaredAt: number;
  resolveAt: number;
}

/* ----------------------------------------------------------------- events */

/**
 * Open semantic event algebra. Rule plugins may declaration-merge new event
 * kinds and emit them from their typed handlers without falling back to an
 * unstructured message bus.
 */
export interface GameEventKindMap {
  turnStart: { type: 'turnStart'; player: PlayerId; turn: number; activeUnit?: number };
  roundStart: { type: 'roundStart'; turn: number };
  turnEnd: { type: 'turnEnd'; player: PlayerId };
  castBegan: {
    type: 'castBegan';
    unit: number;
    weapon: WeaponId;
    at: Coord;
    /** Actor turns still to wait when the cast was committed. */
    turns: number;
  };
  castResolved: { type: 'castResolved'; unit: number; weapon: WeaponId; at: Coord };
  castCancelled: {
    type: 'castCancelled';
    unit: number;
    weapon: WeaponId;
    at: Coord;
    reason: CastRefusal;
  };
  resourceChanged: {
    type: 'resourceChanged';
    resource: ResourceId;
    subject: ResourceSubjectRef;
    amount: number;
    current: number;
  };
  tacticUsed: { type: 'tacticUsed'; commander: CommanderId; tactic: TacticId; target: Coord };
  reactionChanged: { type: 'reactionChanged'; unit: number; stance: ReactionStance };
  facingChanged: { type: 'facingChanged'; unit: number; from: Direction; to: Direction };
  reactionTriggered: { type: 'reactionTriggered'; unit: number; stance: ReactionStance; protectedUnit?: number };
  rankProgressChanged: { type: 'rankProgressChanged'; unit: number; amount: number; current: number };
  rankChanged: { type: 'rankChanged'; unit: number; from: UnitRank; to: UnitRank };
  careerProgressChanged: { type: 'careerProgressChanged'; unit: number; career: CareerId; amount: number; current: number };
  careerMastered: { type: 'careerMastered'; unit: number; career: CareerId; abilities: AbilityId[] };
  careerChanged: { type: 'careerChanged'; unit: number; from: CareerId | null; to: CareerId; unitType: UnitTypeId };
  commanderDefeated: { type: 'commanderDefeated'; commander: CommanderId; unit: number };
  move: { type: 'move'; unit: number; path: Coord[] };
  attack: {
    type: 'attack';
    attacker: number;
    defender: number;
    protectedUnit?: number;
    weapon: WeaponId;
    damage: number;
    killed: boolean;
  };
  areaAttack: {
    type: 'areaAttack';
    attacker: number;
    defender: number;
    protectedUnit?: number;
    weapon: WeaponId;
    damage: number;
    killed: boolean;
  };
  counter: { type: 'counter'; attacker: number; defender: number; weapon: WeaponId; damage: number; killed: boolean };
  supportAttack: { type: 'supportAttack'; attacker: number; defender: number; weapon: WeaponId; damage: number; killed: boolean };
  /** A free blow at a unit disengaging from a zone of control, aimed at the tile it left. */
  partingShot: { type: 'partingShot'; attacker: number; defender: number; weapon: WeaponId; at: Coord; damage: number; killed: boolean };
  attackStructure: { type: 'attackStructure'; attacker: number; structure: StructureId; weapon: WeaponId; damage: number; destroyed: boolean };
  areaAttackStructure: { type: 'areaAttackStructure'; attacker: number; structure: StructureId; weapon: WeaponId; damage: number; destroyed: boolean };
  heal: { type: 'heal'; source: number; target: number; amount: number };
  regen: { type: 'regen'; unit: number; amount: number };
  capture: { type: 'capture'; at: Coord; player: PlayerId; progress: number; captured: boolean };
  recruit: { type: 'recruit'; unit: number; at: Coord };
  unitSpawned: { type: 'unitSpawned'; unit: number; at: Coord; reason: 'reinforcement' | 'summon' };
  unitWithdrawn: { type: 'unitWithdrawn'; unit: number; at: Coord };
  unitEmbarked: { type: 'unitEmbarked'; unit: number; carrier: number };
  unitDisembarked: { type: 'unitDisembarked'; unit: number; carrier: number; at: Coord };
  transportLost: { type: 'transportLost'; carrier: number; passengers: number[]; at: Coord };
  moraleChanged: { type: 'moraleChanged'; unit: number; amount: number; current: number; reason: string };
  unitRouted: { type: 'unitRouted'; unit: number; marker: number; at: Coord };
  unitSurrendered: { type: 'unitSurrendered'; unit: number; marker: number; at: Coord; to?: PlayerId };
  formationChanged: { type: 'formationChanged'; unit: number; from: FormationId | null; to: FormationId | null };
  directiveChanged: { type: 'directiveChanged'; unit: number; mode: UnitDirectiveMode };
  death: { type: 'death'; unit: number; at: Coord };
  markerAdded: { type: 'markerAdded'; marker: number; kind: string; at: Coord };
  markerRemoved: { type: 'markerRemoved'; marker: number; kind: string; at: Coord };
  unitRevived: { type: 'unitRevived'; unit: number; marker: number; at: Coord; hp: number };
  playerTeamChanged: { type: 'playerTeamChanged'; player: PlayerId; from: number; to: number };
  forcedMove: {
    type: 'forcedMove';
    unit: number;
    mode: 'push' | 'pull' | 'teleport';
    from: Coord;
    to: Coord;
    path: Coord[];
    collided: boolean;
  };
  collisionDamage: { type: 'collisionDamage'; unit: number; amount: number; hpAfter: number; killed: boolean };
  statusApplied: { type: 'statusApplied'; unit: number; status: StatusId; remaining: number; stacks: number };
  statusRemoved: { type: 'statusRemoved'; unit: number; status: StatusId };
  statusTick: { type: 'statusTick'; unit: number; status: StatusId; amount: number; hpAfter: number };
  structureDamaged: { type: 'structureDamaged'; structure: StructureId; amount: number; hpAfter: number };
  structureRepaired: { type: 'structureRepaired'; structure: StructureId; amount: number; hpAfter: number };
  structureDestroyed: { type: 'structureDestroyed'; structure: StructureId; at: Coord };
  structureMoved: { type: 'structureMoved'; structure: StructureId; from: Coord; to: Coord };
  unitOwnerChanged: { type: 'unitOwnerChanged'; unit: number; from: PlayerId; to: PlayerId };
  terrainChanged: { type: 'terrainChanged'; at: Coord; from: TerrainId; to: TerrainId };
  elevationChanged: { type: 'elevationChanged'; at: Coord; from: number; to: number };
  cliffChanged: { type: 'cliffChanged'; from: Coord; to: Coord; blocked: boolean };
  directionalCoverChanged: { type: 'directionalCoverChanged'; at: Coord; sides: Partial<Record<Direction, Exclude<CoverLevel, 'none'>>> };
  overlayAdded: { type: 'overlayAdded'; overlay: OverlayId; overlayType: OverlayTypeId; cells: Coord[] };
  overlayRemoved: { type: 'overlayRemoved'; overlay: OverlayId };
  objectiveChanged: { type: 'objectiveChanged'; player: PlayerId; objective: string; status: ObjectiveStatus; hidden: boolean };
  scenarioSignal: { type: 'scenarioSignal'; signal: string };
  defeat: { type: 'defeat'; player: PlayerId };
  gameOver: { type: 'gameOver'; team: number | null; reason: string };
  unitDeployed: { type: 'unitDeployed'; unit: number; from: Coord; to: Coord };
  deploymentConfirmed: { type: 'deploymentConfirmed'; player: PlayerId };
  battleStarted: { type: 'battleStarted'; player: PlayerId; turn: number };
}

export type GameEvent = GameEventKindMap[keyof GameEventKindMap];

/* ---------------------------------------------------------------- actions */

/**
 * An order: which ability, fired with which weapon, aimed where.
 *
 * It was written as a union of the three core ids plus an open member — which
 * constrained nothing, since the open member already admitted all three, and
 * read like a closed set to anyone deciding where a new ability belongs.
 * Whether a weapon or a target is required is the ability's answer, given in
 * `weaponChoices` and `selfTargeted`, and the dispatcher refuses an order that
 * disagrees with it.
 */
export type UnitCommand = { ability: string; weapon?: WeaponId; target?: Coord };

/**
 * Open action family. Content packages may declaration-merge new entries and
 * register a matching ActionHandler without editing the core dispatcher.
 */
export interface ActionKindMap {
  deployUnit: { kind: 'deployUnit'; unit: number; at: Coord };
  finishDeployment: { kind: 'finishDeployment' };
  command: { kind: 'command'; unit: number; path: Coord[]; command: UnitCommand };
  tactic: { kind: 'tactic'; commander: CommanderId; tactic: TacticId; target?: Coord };
  reaction: { kind: 'reaction'; unit: number; stance: ReactionStance };
  face: { kind: 'face'; unit: number; facing: Direction };
  changeCareer: { kind: 'changeCareer'; unit: number; career: CareerId };
  changeFormation: { kind: 'changeFormation'; unit: number; formation: FormationId | null };
  embark: { kind: 'embark'; unit: number; carrier: number };
  disembark: { kind: 'disembark'; carrier: number; unit: number; at: Coord };
  recruit: { kind: 'recruit'; at: Coord; unit: UnitTypeId };
  endTurn: { kind: 'endTurn' };
}

export type Action = ActionKindMap[keyof ActionKindMap];
