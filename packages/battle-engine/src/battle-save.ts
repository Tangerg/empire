import { DomainInvariantError, StoredDocumentError } from './domain/errors';
import { readCurrentDocument } from './save-schema';
import type { RuleReferenceRules } from './rule-references';
import type { RuleReferenceCheckRegistry } from './rule-references';
import type { ContentRegistry } from './registry';
import { rulesetDifferences, type BattleRulesetManifest } from './ruleset-manifest';
import type {
  DeploymentState,
  GameMap,
  GameState,
  RandomState,
  ScenarioState,
  TurnOrderState,
} from './types';

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
  schema: 1;
  battle: BattleSaveHeader;
  ruleset: BattleRulesetManifest;
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

/** The two owners consulted when a battle document is accepted. */
export interface BattleSaveEnvironment {
  readonly rules: BattleSaveRules;
  readonly rulesetManifest: BattleRulesetManifest;
}

export function createBattleSave(
  ruleset: BattleRulesetManifest,
  state: GameState,
  savedAt = new Date().toISOString(),
): BattleSave {
  if (!Number.isFinite(Date.parse(savedAt))) {
    throw new DomainInvariantError('cannot save battle with an invalid timestamp');
  }
  return {
    schema: BATTLE_SAVE_SCHEMA,
    battle: {
      levelId: state.levelId,
      levelName: state.levelName,
      turn: state.turn,
      phase: state.phase,
    },
    ruleset: structuredClone(ruleset),
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
    readonly environment: BattleSaveEnvironment,
  ) {}

  get rules(): BattleSaveRules {
    return this.environment.rules;
  }

  get state(): GameState {
    return this.save.state;
  }

  requireContent(family: keyof BattleSaveRules['content'], id: string, owner: string): void {
    const registry = this.rules.content[family] as ContentRegistry<{ id: string }>;
    if (!registry.has(id)) this.reject(`${owner} 引用了目录里没有的「${id}」`);
  }

  /** Every field of one aggregate, named in the refusal when it is wrong. */
  requireShape<T>(value: unknown, shape: Shape<T>, owner: string): void {
    if (!anObject(value)) this.reject(`${owner}缺失或损坏，存档内容不是一场战斗`);
    const fields = value as Record<string, unknown>;
    for (const [field, check] of Object.entries(shape) as [string, ShapeCheck][]) {
      if (!check(fields[field])) this.reject(`${owner}的「${field}」缺失或损坏，存档内容不是一场战斗`);
    }
  }

  /** A save is loaded or refused; there is no half-loaded battle to play. */
  reject(message: string): never {
    throw new StoredDocumentError(`战斗存档无法读取：${message}`);
  }
}

type SaveCheck = (inspection: SaveInspection) => void;

/* --------------------------------------------------------------- shape */

/** What one raw field has to be before anything is allowed to walk it. */
type ShapeCheck = (value: unknown) => boolean;

const anArray: ShapeCheck = (value) => Array.isArray(value);
const anObject: ShapeCheck = (value) =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const aNumber: ShapeCheck = (value) => typeof value === 'number' && Number.isFinite(value);
const aString: ShapeCheck = (value) => typeof value === 'string';
const orNull = (check: ShapeCheck): ShapeCheck => (value) => value === null || check(value);

/**
 * The shape of one aggregate a save is made of.
 *
 * `Record<keyof T, ShapeCheck>` is the whole point: the compiler refuses a
 * table that has not been taught a field the state grew. The condition this
 * replaces was hand-written and listed six of `GameState`'s twenty-three, so a
 * save with no `embarkedUnits` walked straight past it and died four checks
 * later with a `TypeError` — a defect's error raised for a document's problem,
 * which is exactly the distinction the error contract exists to keep.
 *
 * Depth stops where the per-field checks below take over: those know what a
 * unit or an overlay means, and this only knows that there is something there
 * to ask about.
 */
type Shape<T> = Record<keyof T, ShapeCheck>;

const STATE_SHAPE: Shape<GameState> = {
  levelId: aString,
  levelName: aString,
  map: anObject,
  units: anArray,
  structures: anArray,
  composites: anArray,
  embarkedUnits: anArray,
  markers: anArray,
  commanders: anArray,
  players: anArray,
  rules: anObject,
  turn: aNumber,
  currentPlayer: aNumber,
  phase: aString,
  winnerTeam: orNull(aNumber),
  endReason: aString,
  nextUnitId: aNumber,
  nextMarkerId: aNumber,
  deployment: orNull(anObject),
  scenario: anObject,
  turnOrder: anObject,
  actorTurns: aNumber,
  pendingCasts: anArray,
  random: anObject,
};

const MAP_SHAPE: Shape<GameMap> = {
  width: aNumber,
  height: aNumber,
  tiles: anArray,
  owners: anArray,
  captureProgress: anArray,
  elevation: anArray,
  cliffs: anArray,
  directionalCover: anArray,
};

const SCENARIO_SHAPE: Shape<ScenarioState> = {
  variables: anObject,
  zones: anObject,
  overlays: anArray,
  triggers: anArray,
  firedTriggerIds: anArray,
  triggerRuntime: anObject,
  eventCounts: anObject,
  zoneTags: anObject,
  engagementRules: anArray,
};

const TURN_ORDER_SHAPE: Shape<TurnOrderState> = {
  policy: aString,
  activeUnit: orNull(aNumber),
  data: anObject,
};

const RANDOM_SHAPE: Shape<RandomState> = { seed: aNumber, counters: anObject };

const DEPLOYMENT_SHAPE: Shape<DeploymentState> = {
  order: anArray,
  currentIndex: aNumber,
  assignments: anArray,
};

const HEADER_SHAPE: Shape<BattleSaveHeader> = {
  levelId: aString,
  levelName: aString,
  turn: aNumber,
  phase: aString,
};

const SAVE_SHAPE: Shape<BattleSave> = {
  schema: aNumber,
  battle: anObject,
  ruleset: anObject,
  savedAt: aString,
  state: anObject,
};

/** Enough shape to walk at all. Everything after this may assume the fields exist. */
const checkShape: SaveCheck = (inspection) => {
  inspection.requireShape(inspection.save, SAVE_SHAPE, '存档');
  const state = inspection.state as unknown;
  if (!anObject(state)) inspection.reject('存档内容不是一场战斗');
  inspection.requireShape(state, STATE_SHAPE, '战斗');
  const battle = state as GameState;
  inspection.requireShape(battle.map, MAP_SHAPE, '地图');
  inspection.requireShape(battle.scenario, SCENARIO_SHAPE, '剧本');
  inspection.requireShape(battle.turnOrder, TURN_ORDER_SHAPE, '行动顺序');
  inspection.requireShape(battle.random, RANDOM_SHAPE, '随机流');
  // Absent is a legal deployment: most battles never had one.
  if (battle.deployment) inspection.requireShape(battle.deployment, DEPLOYMENT_SHAPE, '部署');
};

const checkRuleset: SaveCheck = (inspection) => {
  if (!anObject(inspection.save.ruleset) ||
    !anObject(inspection.save.ruleset.plugins) ||
    !anObject(inspection.save.ruleset.contentPacks)) {
    inspection.reject('规则集版本信息缺失或损坏');
  }
  const invalidVersion = [
    ...Object.entries(inspection.save.ruleset.plugins),
    ...Object.entries(inspection.save.ruleset.contentPacks),
  ].find(([id, version]) => !id.trim() || !Number.isInteger(version) || version < 1);
  if (invalidVersion) inspection.reject(`规则集版本「${invalidVersion[0]}」损坏`);
  const differences = rulesetDifferences(
    inspection.environment.rulesetManifest,
    inspection.save.ruleset,
  );
  if (differences.length > 0) inspection.reject(differences.join('；'));
};

const checkMetadata: SaveCheck = (inspection) => {
  if (!aString(inspection.save.savedAt) || Number.isNaN(Date.parse(inspection.save.savedAt))) {
    inspection.reject('保存时间缺失或损坏');
  }
  inspection.requireShape(inspection.save.battle, HEADER_SHAPE, '摘要');
  const { battle, state } = inspection.save;
  if (!Number.isInteger(battle.turn) || battle.turn < 1 ||
    !['deployment', 'playing', 'over'].includes(battle.phase)) {
    inspection.reject('战斗摘要数值不合法');
  }
  if (battle.levelId !== state.levelId || battle.levelName !== state.levelName ||
    battle.turn !== state.turn || battle.phase !== state.phase) {
    inspection.reject('战斗摘要与战斗状态不一致');
  }
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
  checkRuleset,
  checkMetadata,
  checkMap,
  checkUnits,
  checkBattlefield,
  checkRuleReferences,
];

/**
 * Reads the one current schema, then runs the checks only a ruleset can make:
 * does this catalog hold the content the battle is played with, and does this
 * composition implement the rules it names.
 *
 * A save is the one document written by a *running* battle rather than authored
 * by hand, which is exactly why it needs this: a level is linted before play, a
 * save arrives from browser storage against an engine whose plugins may have
 * moved on. A version mismatch is refused instead of guessed or translated by
 * a speculative compatibility path.
 */
export class BattleSaveReader {
  /** Reads a header without accepting the battle; for a slot list. */
  header(raw: unknown): BattleSaveHeader {
    const save = readCurrentDocument<BattleSave>('battle save', BATTLE_SAVE_SCHEMA, raw);
    return this.requireHeader(save);
  }

  load(raw: unknown, environment: BattleSaveEnvironment): BattleSave {
    const save = readCurrentDocument<BattleSave>('battle save', BATTLE_SAVE_SCHEMA, raw);
    this.requireHeader(save);
    const inspection = new SaveInspection(save, environment);
    for (const check of SAVE_CHECKS) {
      try {
        check(inspection);
      } catch (error) {
        if (error instanceof StoredDocumentError) throw error;
        throw new StoredDocumentError(
          `战斗存档无法读取：校验「${check.name || 'anonymous'}」时遇到损坏数据`,
          { cause: error },
        );
      }
    }
    return save;
  }

  private requireHeader(save: BattleSave): BattleSaveHeader {
    if (!save.battle || typeof save.battle !== 'object' ||
      typeof save.battle.levelId !== 'string' || typeof save.battle.levelName !== 'string' ||
      !Number.isInteger(save.battle.turn) || save.battle.turn < 1 ||
      !['deployment', 'playing', 'over'].includes(save.battle.phase)) {
      throw new StoredDocumentError('battle save has no battle header');
    }
    return { ...save.battle };
  }
}
