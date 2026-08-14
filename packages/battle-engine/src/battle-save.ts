import { SchemaMigrator, type SchemaMigration } from './save-schema';
import type { RuleReferenceRules } from './rule-references';
import type { RuleReferenceCheckRegistry } from './rule-references';
import type { ContentRegistry } from './registry';
import type { GameState } from './types';

export const BATTLE_SAVE_SCHEMA = 1;

/**
 * What a save slot can say about a battle without loading it.
 *
 * A slot list is the reason this is a header and not just `state`: a menu should
 * be able to read "第 4 回合 · 银林关" off the file, and refusing an unusable
 * save should not require rehydrating it first.
 */
export interface BattleSaveHeader {
  readonly levelId: string;
  readonly levelName: string;
  readonly turn: number;
  readonly phase: GameState['phase'];
}

/** A battle interrupted mid-play, and everything needed to resume it. */
export interface BattleSave {
  schema: number;
  battle: BattleSaveHeader;
  savedAt: string;
  state: GameState;
}

/**
 * Ports a save has to consult to know whether this ruleset can run it.
 *
 * A saved battle names things twice over: content ids that must exist in the
 * catalog, and rule ids that must be registered as extension points. Both are
 * consumer-declared ports; `BattleRuleServices` satisfies them structurally.
 */
export interface BattleSaveRules extends RuleReferenceRules {
  readonly referenceChecks: RuleReferenceCheckRegistry;
}

export function createBattleSave(
  state: GameState,
  savedAt = new Date().toISOString(),
): BattleSave {
  return {
    schema: BATTLE_SAVE_SCHEMA,
    battle: {
      levelId: state.levelId,
      levelName: state.levelName,
      turn: state.turn,
      phase: state.phase,
    },
    savedAt,
    // A save is a document, not a view of a live battle: the next order must not
    // be able to edit what has already been written down.
    state: structuredClone(state),
  };
}

/* ------------------------------------------------------------------- checks */

/**
 * One save under inspection, and the two kinds of name it can get wrong.
 *
 * Same shape as the content installer's inspection and the level linter's, for
 * the same reason: a refusal has to say which rule refused, and a rule with no
 * name cannot.
 */
class SaveInspection {
  constructor(
    readonly save: BattleSave,
    readonly rules: BattleSaveRules,
  ) {}

  get state(): GameState {
    return this.save.state;
  }

  requireContent(family: keyof BattleSaveRules['content'], id: string, owner: string): void {
    const registry = this.rules.content[family] as ContentRegistry<{ id: string }>;
    if (!registry.has(id)) this.reject(`${owner} 引用了目录里没有的「${id}」`);
  }

  /** A save is loaded or refused; there is no half-loaded battle to play. */
  reject(message: string): never {
    throw new Error(`战斗存档无法读取：${message}`);
  }
}

type SaveCheck = (inspection: SaveInspection) => void;

/** Enough shape to walk at all. Everything after this may assume the fields exist. */
const checkShape: SaveCheck = (inspection) => {
  const state = inspection.state as Partial<GameState> | undefined;
  const missing = !state || typeof state !== 'object' ||
    !Array.isArray(state.units) || !Array.isArray(state.players) ||
    !Array.isArray(state.structures) || !Array.isArray(state.markers) ||
    !state.map || !Array.isArray(state.map.tiles) || !state.scenario;
  if (missing) inspection.reject('存档内容不是一场战斗');
};

const checkMap: SaveCheck = (inspection) => {
  const { map } = inspection.state;
  if (!Number.isInteger(map.width) || !Number.isInteger(map.height) || map.width < 1 || map.height < 1) {
    inspection.reject('地图尺寸不合法');
  }
  if (map.tiles.length !== map.width * map.height) inspection.reject('地图格数与尺寸不符');
  for (const terrain of new Set(map.tiles)) {
    inspection.requireContent('terrains', terrain, '地图');
  }
};

const checkUnits: SaveCheck = (inspection) => {
  const units = [
    ...inspection.state.units.map((unit) => ({ unit, by: `单位 ${unit.key ?? unit.id}` })),
    ...inspection.state.embarkedUnits.map((entry) =>
      ({ unit: entry.unit, by: `载具乘员 ${entry.unit.key ?? entry.unit.id}` })),
    ...inspection.state.markers.flatMap((marker) => marker.fallenUnit
      ? [{ unit: marker.fallenUnit, by: `离场单位 ${marker.fallenUnit.key ?? marker.id}` }]
      : []),
  ];
  for (const { unit, by } of units) {
    inspection.requireContent('units', unit.type, by);
    for (const weapon of Object.keys(unit.weaponState)) inspection.requireContent('weapons', weapon, by);
    for (const status of unit.statuses) inspection.requireContent('statuses', status.id, by);
    if (unit.career.current) inspection.requireContent('careers', unit.career.current, by);
    if (unit.formation) inspection.requireContent('formations', unit.formation, by);
  }
};

const checkBattlefield: SaveCheck = (inspection) => {
  for (const structure of inspection.state.structures) {
    inspection.requireContent('structures', structure.type, `结构 ${structure.id}`);
  }
  for (const overlay of inspection.state.scenario.overlays) {
    inspection.requireContent('terrainOverlays', overlay.type, `覆盖层 ${overlay.id}`);
  }
  for (const commander of inspection.state.commanders) {
    for (const tactic of commander.tactics) {
      inspection.requireContent('tactics', tactic, `指挥官 ${commander.id}`);
    }
  }
};

/** Every extension point a battle can name at runtime answers for itself. */
const checkRuleReferences: SaveCheck = (inspection) => {
  const issues = inspection.rules.referenceChecks.stateIssues(inspection.rules, inspection.state);
  if (issues.length > 0) inspection.reject(issues.join('；'));
};

const SAVE_CHECKS: readonly SaveCheck[] = [
  checkShape,
  checkMap,
  checkUnits,
  checkBattlefield,
  checkRuleReferences,
];

/**
 * Explicit, sequential schema migration, then the checks only a ruleset can
 * make: does this catalog hold the content the battle is played with, and does
 * this composition implement the rules it names.
 *
 * A save is the one document written by a *running* battle rather than authored
 * by hand, which is exactly why it needs this: a level is linted before play, a
 * save arrives from a browser's local storage months later, against an engine
 * whose plugins have moved on.
 */
export class BattleSaveMigrator {
  private readonly ladder = new SchemaMigrator<BattleSave>('battle save', BATTLE_SAVE_SCHEMA);

  register(fromSchema: number, migrate: SchemaMigration): this {
    this.ladder.register(fromSchema, migrate);
    return this;
  }

  /** Reads a header without accepting the battle; for a slot list. */
  header(raw: unknown): BattleSaveHeader {
    const save = this.ladder.load(raw);
    if (!save.battle || typeof save.battle.levelId !== 'string') {
      throw new Error('battle save has no battle header');
    }
    return { ...save.battle };
  }

  load(raw: unknown, rules: BattleSaveRules): BattleSave {
    const save = this.ladder.load(raw);
    this.header(save);
    const inspection = new SaveInspection(save, rules);
    for (const check of SAVE_CHECKS) check(inspection);
    return save;
  }

  clone(): BattleSaveMigrator {
    const copy = new BattleSaveMigrator();
    for (const [schema, migrate] of this.migrations()) copy.register(schema, migrate);
    return copy;
  }

  private migrations(): ReadonlyMap<number, SchemaMigration> {
    return this.ladder.registered();
  }
}

/** The default ladder: schema 1 is the first, so it has no migrations yet. */
export const DefaultBattleSaves = new BattleSaveMigrator();
