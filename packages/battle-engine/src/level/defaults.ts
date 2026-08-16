import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '../resources';
import type { ContentCatalog } from '../content-pack';
import type { LevelData, Objective, PlayerConfig, RuleSet } from '../types';

/**
 * The ruleset a level plays under when it says nothing.
 *
 * Here rather than in `types.ts`, which is the module every content pack
 * declaration-merges its kind maps into and which now declares types only. This
 * was its one runtime value, and it belongs beside the other three answers to
 * "what does a level default to" — the empty level, the fallback victory, and
 * the patch that combines this with what the level actually said.
 */
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



/** Falls back to "rout them or take their keep" when a level states no goal. */
export const DEFAULT_VICTORY: Objective[] = [{ type: 'routEnemies' }, { type: 'captureHQ' }];

function defaultPlayer(
  id: number,
  name: string,
  color: string,
  controller: 'human' | 'ai',
): PlayerConfig {
  return {
    id,
    name,
    team: id,
    color,
    controller,
    resources: {
      [FUNDS_RESOURCE]: { current: 0, capacity: null },
      [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 },
    },
    ai: { aggression: 0.5 },
  };
}

/** The two sides a level has when it names none. */
export const defaultPlayers = (): PlayerConfig[] => [
  defaultPlayer(1, '蓝军', '#3f7fd8', 'human'),
  defaultPlayer(2, '红军', '#d8483f', 'ai'),
];

/**
 * A blank, valid level: two sides, the catalog's blank ground, the default
 * win conditions.
 *
 * The catalog is a parameter because the ground is its answer, not this
 * module's. `'.'` was hard-coded here, which quietly meant "every game's blank
 * terrain is whatever the ancient-empires pack registered under a full stop".
 */
export function emptyLevel(content: ContentCatalog, width = 20, height = 14): LevelData {
  const row = content.terrainEncoding.defaultCharacter.repeat(width);
  return {
    schema: 2,
    id: 'untitled',
    name: '未命名关卡',
    description: '',
    width,
    height,
    terrain: new Array(height).fill(row),
    elevation: new Array(width * height).fill(0),
    cliffs: [],
    directionalCover: [],
    owners: [],
    units: [],
    players: defaultPlayers(),
    rules: {},
    victory: [{ type: 'routEnemies' }, { type: 'captureHQ' }],
  };
}


/** The ruleset a level plays under: the engine defaults, patched by the level. */
export const resolveRules = (level: LevelData): RuleSet => ({ ...DEFAULT_RULES, ...(level.rules ?? {}) });
