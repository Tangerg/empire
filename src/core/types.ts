/**
 * Core domain types. Nothing here touches the DOM — the whole rules engine is
 * headless so it can run in tests, in a worker, or on a server later.
 *
 * Extension points are deliberate:
 *  - TerrainId / UnitTypeId / AbilityId are plain strings backed by registries,
 *    so new content is *data*, not new code paths.
 *  - `RuleSet` carries every tunable rule, so a level (or a future game mode)
 *    can change behaviour without forking the engine.
 *  - `Unit.meta` / `LevelData.extra` are free-form bags for mechanics that do
 *    not exist yet (experience, statuses, scripted triggers...).
 */

export type MovementClass = 'foot' | 'mounted' | 'heavy' | 'flying';
export type DamageType = 'slash' | 'pierce' | 'blunt' | 'magic';
export type ArmorClass = 'unarmored' | 'light' | 'heavy' | 'flying';

/** 1..N are real players. 0 means "nobody owns this". */
export type PlayerId = number;
export const NEUTRAL: PlayerId = 0;

export interface Coord {
  x: number;
  y: number;
}

export type TerrainId = string;
export type UnitTypeId = string;
export type AbilityId = string;

export type MoveCosts = Record<MovementClass, number | null>;

export interface TerrainDef {
  id: TerrainId;
  name: string;
  /** Cost to *enter* this tile, per movement class. null = impassable. */
  cost: MoveCosts;
  /** Fraction of incoming damage absorbed, 0..1. */
  defense: number;
  /** Vision bonus granted to the occupying unit. */
  vision: number;
  /** Blocks line of sight through this tile (fog of war only). */
  opaque: boolean;
  capturable: boolean;
  /** Gold per turn for the owner. */
  income: number;
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
  cost: number;
  maxHp: number;
  attack: number;
  /** Intrinsic damage reduction 0..1, stacks with terrain. */
  defense: number;
  movement: number;
  movementClass: MovementClass;
  damageType: DamageType;
  armorClass: ArmorClass;
  minRange: number;
  maxRange: number;
  /** Siege-style units must stand still to fire. */
  attackAfterMove: boolean;
  vision: number;
  abilities: AbilityId[];
  tags: string[];
  /** Short flavour line shown in the inspector. */
  blurb: string;
}

export interface Unit {
  id: number;
  type: UnitTypeId;
  owner: PlayerId;
  x: number;
  y: number;
  hp: number;
  /** Already acted this turn. */
  done: boolean;
  /** Accumulated capture points on the tile the unit is standing on. */
  capture: number;
  /** Room for future mechanics without touching the engine. */
  meta: Record<string, number | string | boolean>;
}

/* ------------------------------------------------------------------ rules */

export interface RuleSet {
  /** Ancient Empires captures a town the moment you step on it. */
  captureMode: 'instant' | 'progressive';
  /** Points needed in 'progressive' mode (a full-HP unit contributes all). */
  captureThreshold: number;
  /** Gold every player gets each turn on top of building income. */
  baseIncome: number;
  /** Override every capturable tile's income (null = use terrain values). */
  incomeOverride: number | null;
  healOnOwnedBuilding: boolean;
  /** Defender strikes back when the attacker is inside the defender's range. */
  counterAttack: boolean;
  /** 0 = fully deterministic combat (forecast is exact). */
  damageVariance: number;
  fog: boolean;
  turnLimit: number | null;
  /** Units may walk through allies. */
  friendlyPassThrough: boolean;
  /** Enemy units block movement. */
  enemiesBlockMovement: boolean;
  maxUnitsPerPlayer: number | null;
  /** Units recruited this turn cannot act. */
  recruitsActImmediately: boolean;
}

export const DEFAULT_RULES: RuleSet = {
  captureMode: 'instant',
  captureThreshold: 100,
  baseIncome: 0,
  incomeOverride: null,
  healOnOwnedBuilding: true,
  counterAttack: true,
  damageVariance: 0,
  fog: false,
  turnLimit: null,
  friendlyPassThrough: true,
  enemiesBlockMovement: true,
  maxUnitsPerPlayer: null,
  recruitsActImmediately: false,
};

/* ------------------------------------------------------------- objectives */

export type Objective =
  | { type: 'routEnemies' }
  | { type: 'captureHQ' }
  | { type: 'holdAllVillages' }
  | { type: 'surviveTurns'; turns: number };

export interface PlayerConfig {
  id: PlayerId;
  name: string;
  /** Team number — same team = allies. Usually 1:1 with player id. */
  team: number;
  color: string;
  controller: 'human' | 'ai';
  funds: number;
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
  x: number;
  y: number;
  unit: UnitTypeId;
  owner: PlayerId;
  hp?: number;
}

/** The on-disk / editor format. Terrain rows are legend characters. */
export interface LevelData {
  schema: 1;
  id: string;
  name: string;
  author?: string;
  description?: string;
  width: number;
  height: number;
  terrain: string[];
  owners: LevelTileOwner[];
  units: LevelUnit[];
  players: PlayerConfig[];
  rules: Partial<RuleSet>;
  victory: Objective[];
  /** Reserved for future content (scripted triggers, dialogue, ...). */
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
}

export interface PlayerState {
  id: PlayerId;
  name: string;
  team: number;
  color: string;
  controller: 'human' | 'ai';
  funds: number;
  alive: boolean;
  objectives: Objective[];
  ai: { aggression: number };
}

export type GamePhase = 'playing' | 'over';

export interface GameState {
  levelId: string;
  levelName: string;
  map: GameMap;
  units: Unit[];
  players: PlayerState[];
  rules: RuleSet;
  /** 1-based round counter; increments when the turn wraps to the first player. */
  turn: number;
  currentPlayer: PlayerId;
  phase: GamePhase;
  winnerTeam: number | null;
  endReason: string;
  nextUnitId: number;
}

/* ----------------------------------------------------------------- events */

export type GameEvent =
  | { type: 'turnStart'; player: PlayerId; turn: number }
  | { type: 'turnEnd'; player: PlayerId }
  | { type: 'income'; player: PlayerId; amount: number }
  | { type: 'move'; unit: number; path: Coord[] }
  | { type: 'attack'; attacker: number; defender: number; damage: number; killed: boolean }
  | { type: 'counter'; attacker: number; defender: number; damage: number; killed: boolean }
  | { type: 'heal'; source: number; target: number; amount: number }
  | { type: 'regen'; unit: number; amount: number }
  | { type: 'capture'; at: Coord; player: PlayerId; progress: number; captured: boolean }
  | { type: 'recruit'; unit: number; at: Coord }
  | { type: 'death'; unit: number; at: Coord }
  | { type: 'defeat'; player: PlayerId }
  | { type: 'gameOver'; team: number | null; reason: string };

/* ---------------------------------------------------------------- actions */

export type UnitCommand =
  | { ability: 'wait' }
  | { ability: 'attack'; target: Coord }
  | { ability: 'capture' }
  | { ability: string; target?: Coord };

export type Action =
  | { kind: 'command'; unit: number; path: Coord[]; command: UnitCommand }
  | { kind: 'recruit'; at: Coord; unit: UnitTypeId }
  | { kind: 'endTurn' };
