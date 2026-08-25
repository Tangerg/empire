import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '../resources';
import type { ContentCatalog } from '../content-pack';
import type { LevelData, Objective, PlayerConfig, RuleSet, TerrainDef, LevelTileOwner } from '../types';

/**
 * The ruleset a level plays under when it says nothing.
 *
 * Here rather than in `types.ts`, which is the module every content pack
 * declaration-merges its kind maps into and which now declares types only. This
 * was its one runtime value, and it belongs beside the other three answers to
 * "what does a level default to" — the empty level, the fallback victory, and
 * the patch that combines this with what the level actually said.
 */
const BASE_RULES: Omit<RuleSet, 'baseResourceGrants' | 'siteResourceOverrides'> = {
  captureMode: 'instant',
  captureThreshold: 100,
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

/** The default geometry is useful to editors without exposing mutable rule state. */
export const DEFAULT_GRID: RuleSet['grid'] = BASE_RULES.grid;
/** The default actor policy, named once for authoring tools and state creation. */
export const DEFAULT_TURN_ORDER: RuleSet['turnOrder'] = BASE_RULES.turnOrder;

/** A fresh rules aggregate: no battle can mutate another battle's defaults. */
export const defaultRules = (): RuleSet => ({
  ...BASE_RULES,
  baseResourceGrants: [],
  siteResourceOverrides: {},
});

/** A fresh fallback objective tree for each level and player. */
export const defaultVictory = (): Objective[] => [
  { type: 'routEnemies' },
  { type: 'captureHQ' },
];

/**
 * What a side starts as: no money spent, a full command allowance, a middling AI.
 *
 * Exported because the editor was writing it out again when you add a player —
 * the same two accounts and the same aggression, in a package that cannot see this
 * one's defaults change. Its name and colour are the caller's: those are the only
 * two things a new side actually differs by.
 */
export function defaultPlayer(
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
 * What a level is called before anyone names it.
 *
 * Written twice: here, and again in `schema.ts` as the fallback for a document
 * that carries no name — so a blank level and a nameless loaded one agreed only
 * by coincidence.
 */
export const UNNAMED_LEVEL = Object.freeze({ id: 'untitled', name: '未命名关卡' });

/**
 * The terrain this ruleset builds units from, or `null` if it has none.
 *
 * Asked of the catalog rather than named here, for the same reason the blank
 * ground is: which terrain is a barracks is the pack's answer. The first one in
 * declaration order, because a pack lists its main one first and a blank document
 * is not the place to weigh them.
 */
function productionTerrain(content: ContentCatalog): TerrainDef | null {
  return content.terrains.all().find((terrain) => terrain.produces.length > 0) ?? null;
}

/**
 * A blank, playable level: two sides, each with somewhere to build, the catalog's
 * blank ground and the default win conditions.
 *
 * This said "blank, valid level" and shipped an invalid one. Both sides had
 * nothing to move and nothing to build, which the engine's own lint calls
 * `开局即败` — so the editor opened on a red card with two errors against a
 * document nobody had touched yet, and the default victory condition (rout the
 * enemy) had no enemy to rout.
 *
 * The catalog answers both questions: the ground is
 * `terrainEncoding.defaultCharacter` (`'.'` was hard-coded here, which quietly
 * meant "every game's blank terrain is whatever the ancient-empires pack
 * registered under a full stop"), and the barracks is the first terrain that
 * produces anything. A ruleset with no such terrain gets the empty field it used
 * to get, and the lint says so — which is the honest answer for a pack where
 * nothing can be built.
 */
export function emptyLevel(content: ContentCatalog, width = 20, height = 14): LevelData {
  const blank = content.terrainEncoding.defaultCharacter;
  const barracks = productionTerrain(content);
  const character = barracks ? content.terrainEncoding.character(barracks.id) : null;
  const terrain = new Array(height).fill(blank.repeat(width));
  const players = defaultPlayers();
  // Opposite ends of the middle row, so a new document reads as two sides facing
  // each other rather than as a corner case.
  const homes = [
    { player: players[0].id, x: 1, y: Math.floor(height / 2) },
    { player: players[1].id, x: width - 2, y: Math.floor(height / 2) },
  ];
  const owners: LevelTileOwner[] = [];
  if (character) {
    for (const home of homes) {
      const row = terrain[home.y] as string;
      terrain[home.y] = row.slice(0, home.x) + character + row.slice(home.x + 1);
      owners.push({ x: home.x, y: home.y, owner: home.player });
    }
  }
  return {
    schema: 2,
    id: UNNAMED_LEVEL.id,
    name: UNNAMED_LEVEL.name,
    description: '',
    width,
    height,
    terrain,
    elevation: new Array(width * height).fill(0),
    cliffs: [],
    directionalCover: [],
    owners,
    units: [],
    players,
    rules: {},
    victory: defaultVictory(),
  };
}


/** The ruleset a level plays under: the engine defaults, patched by the level. */
export function resolveRules(level: LevelData): RuleSet {
  const defaults = defaultRules();
  const overrides = level.rules;
  return {
    ...defaults,
    ...overrides,
    baseResourceGrants: (overrides?.baseResourceGrants ?? defaults.baseResourceGrants)
      .map((grant) => ({ ...grant })),
    siteResourceOverrides: {
      ...defaults.siteResourceOverrides,
      ...overrides?.siteResourceOverrides,
    },
  };
}
