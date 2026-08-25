import { sharesEdge, inBounds, idx} from './grid';
import { type ContentCatalog } from './content-pack';
import type { RuleReferenceCheckRegistry, RuleReferenceRules } from './rule-references';
import type { PayloadReferences } from './payload-references';
import type { TacticalGrid } from './tactical-grid';
import type {
  Coord,
  GameMap,
  LevelData,
  Objective,
  PlayerConfig,
  ScenarioCondition,
  TerrainDef,
} from './types';
import { LevelDeclarations, objectivesOf } from './level/declarations';
import { resolveRules } from './level/defaults';
import { scheduleOf } from './domain/scenario-trigger';
import { declaredChildObjectives, isCompositeObjective } from './objective-model';
import { LevelIssueLog, type LevelIssue } from './level/issues';
import { mapFromLevel } from './level/map';
import { StoredDocumentError } from './domain/errors';

export type { LevelIssue };

/**
 * Port declared by the linter.
 *
 * A level is only playable *under a ruleset*: the catalog defines the content
 * ids it names, the tiling defines the facings it may use, and the handler
 * registries are the only things that can say what one of its payloads points
 * at. `BattleRuleServices` satisfies this structurally.
 */
export interface LevelValidationRules extends RuleReferenceRules {
  readonly referenceChecks: RuleReferenceCheckRegistry;
}

/**
 * One level under inspection: the document, the ruleset it is written against,
 * the runtime map it produces, and the names it declares.
 *
 * Every check reads this and writes findings to it, which is what replaced
 * thirty checks sharing one function's local variables.
 */
export class LevelInspection {
  readonly declarations: LevelDeclarations;
  /** Null when the terrain itself is unreadable; placement checks then abstain. */
  readonly map: GameMap | null;

  constructor(
    readonly rules: LevelValidationRules,
    readonly level: LevelData,
    private readonly log: LevelIssueLog,
  ) {
    this.map = this.buildMap();
    this.declarations = new LevelDeclarations(level, log);
  }

  get content(): ContentCatalog {
    return this.rules.content;
  }

  /**
   * The tiling this level plays on, or null when it names one the ruleset does
   * not implement — which the ruleset's own reference checks report.
   */
  get grid(): TacticalGrid | null {
    return this.rules.grids.tryGet(resolveRules(this.level).grid) ?? null;
  }

  /**
   * Whether the board this level plays on has a facing by that name.
   *
   * Abstains when the level names a tiling the ruleset does not implement: one
   * finding about the board is enough, and it is already reported.
   */
  admitsFacing(direction: string): boolean {
    const grid = this.grid;
    return !grid || grid.directions.some((facing) => facing.id === direction);
  }

  error(message: string): void {
    this.log.error(message);
  }

  warn(message: string): void {
    this.log.warn(message);
  }

  /** Against the *declared* size, so a broken terrain block still lints. */
  inBounds(cell: Coord): boolean {
    return Number.isInteger(cell.x) && Number.isInteger(cell.y) &&
      cell.x >= 0 && cell.y >= 0 && cell.x < this.level.width && cell.y < this.level.height;
  }

  /**
   * The ground on one tile of a map a caller has already read.
   *
   * Takes the map rather than reaching for `this.map`: that field is null when
   * the terrain block itself is unreadable, and every caller has to abstain in
   * that case anyway. Passing it is the difference between the type saying so
   * and a `!` claiming the caller remembered.
   */
  terrainOn(map: GameMap, tile: number): TerrainDef {
    return this.content.terrains.get(map.tiles[tile]);
  }

  private buildMap(): GameMap | null {
    try {
      return mapFromLevel(this.content, this.level);
    } catch (error) {
      if (!(error instanceof StoredDocumentError)) throw error;
      this.log.error(error.message);
      return null;
    }
  }
}

type LevelCheck = (inspection: LevelInspection) => void;

/* ------------------------------------------------------------------ identity */

const checkIdentity: LevelCheck = (inspection) => {
  const { level } = inspection;
  if (!level.id?.trim()) inspection.error('关卡缺少 id');
  if (!level.name?.trim()) inspection.warn('关卡缺少名称');
};

/* ------------------------------------------------------------------ geometry */

const checkCliffs: LevelCheck = (inspection) => {
  const seen = new Set<string>();
  for (const cliff of inspection.level.cliffs ?? []) {
    const edge = `${cliff.from.x},${cliff.from.y} -> ${cliff.to.x},${cliff.to.y}`;
    if (!inspection.inBounds(cliff.from) || !inspection.inBounds(cliff.to)) {
      inspection.error(`悬崖边越界：${edge}`);
      continue;
    }
    if (!sharesEdge(cliff.from, cliff.to)) {
      inspection.error(`悬崖边必须连接相邻格：${edge}`);
    }
    const key = [`${cliff.from.x},${cliff.from.y}`, `${cliff.to.x},${cliff.to.y}`].sort().join('|');
    if (seen.has(key)) inspection.warn(`悬崖边重复：${key}`);
    seen.add(key);
  }
};

const checkDirectionalCover: LevelCheck = (inspection) => {
  for (const cover of inspection.level.directionalCover ?? []) {
    const at = `${cover.at.x},${cover.at.y}`;
    if (!inspection.inBounds(cover.at)) inspection.error(`方向掩体越界：${at}`);
    if (Object.keys(cover.sides).length === 0) {
      inspection.warn(`方向掩体 ${at} 没有任何受保护边`);
    }
    // A side the board has no name for protects against nothing: an attack can
    // never arrive from a facing the tiling does not admit.
    for (const side of Object.keys(cover.sides)) {
      if (!inspection.admitsFacing(side)) inspection.error(`方向掩体 ${at} 的受保护边无效：${side}`);
    }
  }
};

/* ------------------------------------------------------------------- players */

function checkResourceAccounts(
  inspection: LevelInspection,
  accounts: PlayerConfig['resources'],
  owner: string,
): void {
  for (const [id, account] of Object.entries(accounts)) {
    if (!id.trim()) inspection.error(`${owner} 的资源 id 不能为空`);
    if (!Number.isFinite(account.current) || account.current < 0) {
      inspection.error(`${owner} 的资源 "${id}" 当前值必须 >= 0`);
    }
    if (account.capacity !== null && (!Number.isFinite(account.capacity) || account.capacity < 0)) {
      inspection.error(`${owner} 的资源 "${id}" 上限必须 >= 0 或 null`);
    }
    if (account.capacity !== null && account.current > account.capacity) {
      inspection.error(`${owner} 的资源 "${id}" 当前值不能超过上限`);
    }
  }
}

const checkPlayers: LevelCheck = (inspection) => {
  const { level } = inspection;
  for (const player of level.players) {
    checkResourceAccounts(inspection, player.resources, `玩家 ${player.id}`);
  }
  if (level.players.length < 2) inspection.error('至少需要两名玩家');
  if (!level.players.some((player) => player.controller === 'human')) inspection.warn('没有任何人类玩家');
};

/* --------------------------------------------------------------------- units */

const checkUnits: LevelCheck = (inspection) => {
  const occupied = new Set<number>();
  for (const unit of inspection.level.units) {
    checkUnitContent(inspection, unit);
    checkUnitCareer(inspection, unit);
    checkUnitPlacement(inspection, unit, occupied);
  }
};

type UnitPlacement = LevelData['units'][number];

function checkUnitContent(inspection: LevelInspection, unit: UnitPlacement): void {
  const { content, declarations } = inspection;
  if (!content.units.has(unit.unit)) inspection.error(`未知兵种 "${unit.unit}"`);
  if (!declarations.players.has(unit.owner)) {
    inspection.error(`单位 ${unit.unit}@${unit.x},${unit.y} 的归属玩家 ${unit.owner} 不存在`);
  }
  if (unit.rank !== undefined && ![0, 1, 2].includes(unit.rank)) {
    inspection.error(`单位 ${unit.unit} 的军衔必须是 0、1 或 2`);
  }
  if (unit.rankProgress !== undefined && (!Number.isFinite(unit.rankProgress) || unit.rankProgress < 0)) {
    inspection.error(`单位 ${unit.unit} 的军衔经验必须 >= 0`);
  }
  checkResourceAccounts(inspection, unit.resources ?? {}, `单位 ${unit.unit}`);
  // Facings belong to the tiling, not to this list: four names hardcoded here
  // refused every direction a hex or eight-way board has.
  if (unit.facing !== undefined && !inspection.admitsFacing(unit.facing)) {
    inspection.error(`单位 ${unit.unit} 的朝向无效：${unit.facing}`);
  }
  if (unit.morale !== undefined && (!Number.isFinite(unit.morale) || unit.morale < 0)) {
    inspection.error(`单位 ${unit.unit} 的士气必须 >= 0`);
  }
  if (unit.formation !== undefined) {
    if (!content.formations.has(unit.formation)) {
      inspection.error(`单位 ${unit.unit} 使用未知阵形 "${unit.formation}"`);
    } else if (!content.units.tryGet(unit.unit)?.formations?.includes(unit.formation)) {
      inspection.error(`单位 ${unit.unit} 不能使用阵形 "${unit.formation}"`);
    }
  }
  for (const waypoint of unit.directive?.waypoints ?? []) {
    if (!inspection.inBounds(waypoint)) {
      inspection.error(`单位 ${unit.unit} 的巡逻点越界：${waypoint.x},${waypoint.y}`);
    }
  }
}

function checkUnitCareer(inspection: LevelInspection, unit: UnitPlacement): void {
  const { content } = inspection;
  if (unit.career !== undefined) {
    const career = content.careers.tryGet(unit.career);
    if (!career) inspection.error(`单位 ${unit.unit} 使用未知职业 "${unit.career}"`);
    else if (career.unitType !== unit.unit) {
      inspection.error(`职业 ${unit.career} 与兵种 ${unit.unit} 不匹配`);
    }
  }
  for (const career of unit.unlockedCareers ?? []) {
    if (!content.careers.has(career)) inspection.error(`单位 ${unit.unit} 解锁了未知职业 "${career}"`);
  }
  for (const [career, mastery] of Object.entries(unit.careerMastery ?? {})) {
    if (!content.careers.has(career)) inspection.error(`单位 ${unit.unit} 记录了未知职业熟练度 "${career}"`);
    if (!Number.isFinite(mastery) || mastery < 0) inspection.error(`单位 ${unit.unit} 的职业熟练度必须 >= 0`);
  }
}

function checkUnitPlacement(
  inspection: LevelInspection,
  unit: UnitPlacement,
  occupied: Set<number>,
): void {
  const map = inspection.map;
  if (!map) return;
  if (!inBounds(map, unit.x, unit.y)) {
    inspection.error(`单位越界：${unit.x},${unit.y}`);
    return;
  }
  const tile = idx(map, unit.x, unit.y);
  if (occupied.has(tile)) inspection.error(`格子 ${unit.x},${unit.y} 上有多个单位`);
  occupied.add(tile);
  const terrain = inspection.terrainOn(map, tile);
  const definition = inspection.content.units.tryGet(unit.unit);
  if (definition && terrain.cost[definition.movementClass] == null) {
    inspection.error(`${definition.name} 无法站在 ${terrain.name} 上（${unit.x},${unit.y}）`);
  }
}

/* ---------------------------------------------------------------- commanders */

const checkCommanders: LevelCheck = (inspection) => {
  const { level, content, declarations } = inspection;
  for (const commander of level.commanders ?? []) {
    if (!declarations.unitKeys.has(commander.unitKey)) {
      inspection.error(`指挥官 ${commander.id} 引用了未知单位 key ${commander.unitKey}`);
    }
    if (!Number.isFinite(commander.radius) || commander.radius < 0) {
      inspection.error(`指挥官 ${commander.id} 的范围必须 >= 0`);
    }
    for (const tactic of commander.tactics ?? []) {
      if (!content.tactics.has(tactic)) {
        inspection.error(`指挥官 ${commander.id} 使用未知战术 "${tactic}"`);
      }
    }
  }
  for (const unit of level.units) {
    if (unit.commander && !declarations.commanders.has(unit.commander)) {
      inspection.error(`单位 ${unit.key ?? `${unit.x},${unit.y}`} 链接到未知指挥官 ${unit.commander}`);
    }
  }
};

/* ------------------------------------------------------- structures & composites */

const checkStructures: LevelCheck = (inspection) => {
  const { level, content, declarations, map } = inspection;
  for (const structure of level.structures ?? []) {
    if (!content.structures.has(structure.type)) inspection.error(`未知结构 "${structure.type}"`);
    if (map && !inBounds(map, structure.x, structure.y)) {
      inspection.error(`结构越界：${structure.id}@${structure.x},${structure.y}`);
    }
    if (structure.owner !== undefined && structure.owner !== 0 && !declarations.players.has(structure.owner)) {
      inspection.error(`结构 ${structure.id} 的归属玩家 ${structure.owner} 不存在`);
    }
  }
};

const checkComposites: LevelCheck = (inspection) => {
  const { declarations } = inspection;
  for (const composite of inspection.level.composites ?? []) {
    if (composite.parts.length === 0) inspection.error(`复合目标 ${composite.id} 没有部件`);
    for (const part of composite.parts) {
      if (!declarations.structures.has(part)) {
        inspection.error(`复合目标 ${composite.id} 引用了未知结构 ${part}`);
      }
    }
    const threshold = composite.minimumNeutralized ?? composite.parts.length;
    if (!Number.isInteger(threshold) || threshold < 1 || threshold > composite.parts.length) {
      inspection.error(`复合目标 ${composite.id} 的瘫痪阈值无效`);
    }
  }
};

/* ------------------------------------------------------------ zones & engagement */

const checkZones: LevelCheck = (inspection) => {
  const { level, declarations, map } = inspection;
  for (const zone of level.scenario?.zones ?? []) {
    for (const cell of zone.cells) {
      if (map && !inBounds(map, cell.x, cell.y)) {
        inspection.error(`区域 ${zone.id} 包含越界格：${cell.x},${cell.y}`);
      }
    }
  }
  for (const unit of level.units) {
    if (unit.directive?.zone && !declarations.zones.has(unit.directive.zone)) {
      inspection.error(`单位 ${unit.key ?? unit.unit} 的战术指令引用了未知区域 ${unit.directive.zone}`);
    }
  }
};

const checkEngagementRules: LevelCheck = (inspection) => {
  const { declarations } = inspection;
  for (const rule of inspection.level.scenario?.engagementRules ?? []) {
    if (!declarations.zones.has(rule.zone)) {
      inspection.error(`交战规则 ${rule.id} 引用了未知区域 ${rule.zone}`);
    }
    for (const owner of rule.players ?? []) {
      if (!declarations.players.has(owner)) {
        inspection.error(`交战规则 ${rule.id} 引用了未知玩家 ${owner}`);
      }
    }
  }
};

/* ---------------------------------------------------------------- deployment */

const checkDeployment: LevelCheck = (inspection) => {
  const { level, declarations } = inspection;
  const deployment = level.deployment;
  if (!deployment) return;

  const order = deployment.order ?? deployment.zones.map((entry) => entry.player);
  if (order.length === 0) inspection.error('部署阶段至少需要一名玩家');
  for (const owner of order) {
    if (!declarations.players.has(owner)) inspection.error(`部署顺序引用了未知玩家 ${owner}`);
  }

  const assigned = new Set<string>();
  for (const assignment of deployment.zones) {
    if (!declarations.players.has(assignment.player)) {
      inspection.error(`部署区域引用了未知玩家 ${assignment.player}`);
    }
    if (!declarations.zones.has(assignment.zone)) {
      inspection.error(`部署区域引用了未知区域 ${assignment.zone}`);
    }
    for (const key of assignment.unitKeys ?? []) {
      const unit = level.units.find((candidate) => candidate.key === key);
      if (!unit) inspection.error(`部署区域引用了未知单位 key ${key}`);
      else if (unit.owner !== assignment.player) {
        inspection.error(`部署单位 ${key} 不属于玩家 ${assignment.player}`);
      }
      if (assigned.has(key)) inspection.error(`部署单位 ${key} 被分配到多个区域`);
      assigned.add(key);
    }
  }

  // A player may have one catch-all zone; two would leave "everyone else"
  // ambiguous, and the deployment phase would place units arbitrarily.
  for (const owner of new Set(deployment.zones.map((entry) => entry.player))) {
    if (deployment.zones.filter((entry) => entry.player === owner && !entry.unitKeys).length > 1) {
      inspection.error(`玩家 ${owner} 有多个未指定单位的部署区域`);
    }
  }
};

/* ------------------------------------------------------------------ overlays */

const checkOverlays: LevelCheck = (inspection) => {
  const { content, declarations } = inspection;
  for (const overlay of inspection.level.scenario?.overlays ?? []) {
    if (!content.terrainOverlays.has(overlay.type)) inspection.error(`未知地形覆盖 "${overlay.type}"`);
    if (!declarations.zones.has(overlay.zone)) {
      inspection.error(`地形覆盖 ${overlay.id} 引用了未知区域 ${overlay.zone}`);
    }
    if (overlay.remainingRounds !== undefined && overlay.remainingRounds !== null && overlay.remainingRounds < 1) {
      inspection.error(`地形覆盖 ${overlay.id} 的持续回合必须 >= 1`);
    }
  }
};

/* ---------------------------------------------------------------- references */

/**
 * Resolves what a payload said it points at against this document.
 *
 * The linter knows nothing about which kinds of effect, condition or objective
 * exist — it asks the handler that runs one what it points at, and only decides
 * whether those names are declared here. That is what replaced two hundred lines
 * of `effect.type === '…'` in this module, and it is why a rule pack's own kind
 * is linted as thoroughly as a built-in one.
 *
 * Standing orders are the one namespace deliberately left alone: whether a
 * ruleset *implements* a rule id is `RuleReferenceCheck`'s question, and it is
 * asked of the same declaration.
 */
function checkReferences(inspection: LevelInspection, by: string, cited: PayloadReferences): void {
  const { content, declarations } = inspection;
  const unknown = (subject: string, name: string | number): void =>
    inspection.error(`${by} 引用了未知${subject} ${name}`);

  for (const id of cited.zones) if (!declarations.zones.has(id)) unknown('区域', id);
  for (const id of cited.players) if (!declarations.players.has(id)) unknown('玩家', id);
  for (const id of cited.structures) if (!declarations.structures.has(id)) unknown('结构', id);
  for (const id of cited.composites) if (!declarations.composites.has(id)) unknown('复合目标', id);
  for (const aim of cited.objectives) {
    if (!declarations.objectivesOfPlayer(aim.player).has(aim.id)) unknown('目标', `${aim.player}:${aim.id}`);
  }
  for (const id of cited.statuses) if (!content.statuses.has(id)) unknown('状态', id);
  for (const id of cited.terrains) if (!content.terrains.has(id)) unknown('地形', id);
  for (const id of cited.overlays) if (!content.terrainOverlays.has(id)) unknown('地形覆盖', id);
  for (const id of cited.unitTypes) if (!content.units.has(id)) unknown('兵种', id);
  for (const at of cited.cells) {
    if (!inspection.inBounds(at)) inspection.error(`${by} 的位置越界：${at.x},${at.y}`);
  }
  for (const edge of cited.edges) {
    if (!inspection.inBounds(edge.from) || !inspection.inBounds(edge.to) || !sharesEdge(edge.from, edge.to)) {
      inspection.error(`${by} 的边无效：${edge.from.x},${edge.from.y} -> ${edge.to.x},${edge.to.y}`);
    }
  }
  for (const fault of cited.faults) inspection.error(`${by}：${fault}`);
  for (const condition of cited.conditions) checkCondition(inspection, by, condition);
}

/* ---------------------------------------------------------------- objectives */

const checkObjectives: LevelCheck = (inspection) => {
  for (const player of inspection.level.players) {
    for (const objective of objectivesOf(inspection.level, player)) {
      checkObjective(inspection, objective);
    }
  }
};

function checkObjective(inspection: LevelInspection, objective: Objective): void {
  const named = `目标 ${objective.id ?? objective.type}`;
  checkReferences(inspection, named, inspection.rules.objectives.references(objective));
  if (isCompositeObjective(objective) && declaredChildObjectives(objective).length === 0) {
    inspection.error(`组合目标 ${objective.id ?? objective.type} 不能为空`);
  }
  for (const child of declaredChildObjectives(objective)) checkObjective(inspection, child);
}

/* ------------------------------------------------------------------ triggers */

const checkTriggers: LevelCheck = (inspection) => {
  for (const trigger of inspection.level.scenario?.triggers ?? []) {
    const by = `触发器 ${trigger.id}`;
    checkCondition(inspection, by, trigger.condition);
    for (const effect of trigger.effects) {
      checkReferences(inspection, `${by} 的效果 ${effect.type}`, inspection.rules.scenarioEffects.references(effect));
    }
    for (const fault of scheduleOf(trigger)?.faults ?? []) inspection.error(`${by} 的${fault}`);
  }
};

function checkCondition(inspection: LevelInspection, by: string, condition: ScenarioCondition): void {
  const { scenarioConditions } = inspection.rules;
  checkReferences(inspection, `${by} 的条件 ${condition.type}`, scenarioConditions.references(condition));
  // A kind nobody registered has no knowable children, and the missing kind is
  // what the ruleset's own reference checks report.
  if (!scenarioConditions.has(condition.type)) return;
  for (const child of scenarioConditions.children(condition)) checkCondition(inspection, by, child);
}

/* --------------------------------------------------------------- playability */

const checkTileOwners: LevelCheck = (inspection) => {
  for (const owned of inspection.level.owners ?? []) {
    if (owned.owner !== 0 && !inspection.declarations.players.has(owned.owner)) {
      inspection.error(`建筑归属玩家 ${owned.owner} 不存在`);
    }
  }
};

/** A side with nothing to move and nothing to build has already lost. */
const checkOpeningPosition: LevelCheck = (inspection) => {
  const { level, map } = inspection;
  if (!map) return;
  for (const player of level.players) {
    const hasUnits = level.units.some((unit) => unit.owner === player.id);
    const hasProduction = map.owners.some(
      (owner, tile) => owner === player.id && inspection.terrainOn(map, tile).produces.length > 0,
    );
    if (!hasUnits && !hasProduction) {
      inspection.error(`玩家 ${player.id}（${player.name}）既没有单位也没有生产建筑，开局即败`);
    }
  }
};

const checkVictoryConditions: LevelCheck = (inspection) => {
  const { level } = inspection;
  const victory = level.victory ?? [];
  if (victory.length === 0 && !level.players.some((player) => player.objectives?.length)) {
    inspection.warn('未设置胜利条件，将回退为「歼灭敌军 / 攻占城堡」');
  }
};

/**
 * Structural + playability lint, in the order an author reads a level file.
 *
 * This was one four-hundred-line function whose statement order was load-bearing:
 * each section left a `Set` of ids behind for the sections after it, so no check
 * could be named, moved, or read on its own. Names are now gathered once by
 * `LevelDeclarations`, and each check is a named question about the document.
 */
const LEVEL_CHECKS: readonly LevelCheck[] = [
  checkIdentity,
  checkCliffs,
  checkDirectionalCover,
  checkPlayers,
  checkUnits,
  checkCommanders,
  checkStructures,
  checkComposites,
  checkZones,
  checkEngagementRules,
  checkDeployment,
  checkOverlays,
  checkObjectives,
  checkTriggers,
  checkTileOwners,
  checkOpeningPosition,
  checkVictoryConditions,
];

/**
 * Everything wrong with one level under one ruleset. The editor surfaces this
 * live, and the engine refuses to build a state from a level with any error.
 *
 * Two questions, one answer: does the document hold together against its
 * catalog, and does this ruleset implement every rule the document names. Both
 * callers used to combine the two halves by hand, which is two places to forget
 * the second one.
 */
export function validateLevel(rules: LevelValidationRules, level: LevelData): LevelIssue[] {
  const log = new LevelIssueLog();
  const inspection = new LevelInspection(rules, level, log);
  for (const check of LEVEL_CHECKS) check(inspection);
  for (const missing of rules.referenceChecks.levelIssues(rules, level)) log.error(missing);
  return log.issues;
}
