import { sharesEdge } from '../grid';
import { type ContentCatalog } from '../content-pack';
import type {
  Coord,
  GameMap,
  LevelData,
  Objective,
  PlayerConfig,
  ScenarioCondition,
  ScenarioEffect,
  ScenarioTrigger,
  TerrainDef,
  UnitSelector,
} from '../types';
import { LevelDeclarations, objectivesOf } from './declarations';
import { scheduleOf } from '../domain/scenario-trigger';
import { declaredChildObjectives, isCompositeObjective } from '../objective-model';
import { LevelIssueLog, type LevelIssue } from './issues';
import { mapFromLevel } from './map';

export type { LevelIssue };

/**
 * One level under inspection: the document, the catalog it is written against,
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
    readonly level: LevelData,
    readonly content: ContentCatalog,
    private readonly log: LevelIssueLog,
  ) {
    this.map = this.buildMap();
    this.declarations = new LevelDeclarations(level, log);
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

  terrainAt(tile: number): TerrainDef {
    return this.content.terrains.get(this.map!.tiles[tile]);
  }

  private buildMap(): GameMap | null {
    try {
      return mapFromLevel(this.level, this.content);
    } catch (error) {
      this.log.error((error as Error).message);
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
    if (!inspection.inBounds(cover.at)) inspection.error(`方向掩体越界：${cover.at.x},${cover.at.y}`);
    if (Object.keys(cover.sides).length === 0) {
      inspection.warn(`方向掩体 ${cover.at.x},${cover.at.y} 没有任何受保护边`);
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
  if (unit.facing !== undefined && !['north', 'east', 'south', 'west'].includes(unit.facing)) {
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
  if (unit.x < 0 || unit.y < 0 || unit.x >= map.width || unit.y >= map.height) {
    inspection.error(`单位越界：${unit.x},${unit.y}`);
    return;
  }
  const tile = unit.y * map.width + unit.x;
  if (occupied.has(tile)) inspection.error(`格子 ${unit.x},${unit.y} 上有多个单位`);
  occupied.add(tile);
  const terrain = inspection.terrainAt(tile);
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
    if (map && (structure.x < 0 || structure.y < 0 || structure.x >= map.width || structure.y >= map.height)) {
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
      if (map && (cell.x < 0 || cell.y < 0 || cell.x >= map.width || cell.y >= map.height)) {
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

/* ---------------------------------------------------------------- objectives */

const checkObjectives: LevelCheck = (inspection) => {
  for (const player of inspection.level.players) {
    for (const objective of objectivesOf(inspection.level, player)) {
      checkObjective(inspection, objective);
    }
  }
};

function checkObjective(inspection: LevelInspection, objective: Objective): void {
  const { declarations } = inspection;
  const named = objective.id ?? objective.type;
  if (objective.type === 'destroy') {
    for (const id of objective.structures) {
      if (!declarations.structures.has(id)) inspection.error(`目标 ${named} 引用了未知结构 ${id}`);
    }
  }
  if (objective.type === 'neutralizeComposite') {
    if (!declarations.composites.has(objective.composite)) {
      inspection.error(`目标 ${named} 引用了未知复合目标 ${objective.composite}`);
    }
    if (objective.minimumNeutralized !== undefined &&
      (!Number.isInteger(objective.minimumNeutralized) || objective.minimumNeutralized < 1)) {
      inspection.error(`复合目标 ${named} 的瘫痪数量必须 >= 1`);
    }
  }
  if ((objective.type === 'escort' || objective.type === 'control') && !declarations.zones.has(objective.zone)) {
    inspection.error(`目标 ${named} 引用了未知区域 ${objective.zone}`);
  }
  if (objective.type === 'protect' && (objective.minimumAlive < 1 || objective.untilTurn < 1)) {
    inspection.error(`保护目标 ${objective.id ?? ''} 的人数和截止回合必须 >= 1`);
  }
  if (objective.type === 'escort' && objective.count < 1) {
    inspection.error(`护送目标 ${objective.id ?? ''} 的抵达人数必须 >= 1`);
  }
  if (isCompositeObjective(objective) && declaredChildObjectives(objective).length === 0) {
    inspection.error(`组合目标 ${named} 不能为空`);
  }
  for (const child of declaredChildObjectives(objective)) checkObjective(inspection, child);
}

/* ------------------------------------------------------------------ triggers */

const checkTriggers: LevelCheck = (inspection) => {
  for (const trigger of inspection.level.scenario?.triggers ?? []) {
    checkCondition(inspection, trigger.condition);
    for (const effect of trigger.effects) checkEffect(inspection, effect);
    checkRepeat(inspection, trigger);
  }
};

function checkRepeat(inspection: LevelInspection, trigger: ScenarioTrigger): void {
  for (const fault of scheduleOf(trigger)?.faults ?? []) {
    inspection.error(`触发器 ${trigger.id} 的${fault}`);
  }
}

function checkCondition(inspection: LevelInspection, condition: ScenarioCondition): void {
  const { declarations } = inspection;
  const zone = (id: string) => {
    if (!declarations.zones.has(id)) inspection.error(`触发条件引用了未知区域 ${id}`);
  };
  if (condition.type === 'unitInZone') zone(condition.zone);
  else if (condition.type === 'unitCount' || condition.type === 'unitHealth' || condition.type === 'markerCount') {
    if (condition.selector.zone) zone(condition.selector.zone);
  } else if (condition.type === 'structure' && !declarations.structures.has(condition.id)) {
    inspection.error(`触发条件引用了未知结构 ${condition.id}`);
  } else if (condition.type === 'composite' && !declarations.composites.has(condition.id)) {
    inspection.error(`触发条件引用了未知复合目标 ${condition.id}`);
  } else if (condition.type === 'currentPlayer' && !declarations.players.has(condition.player)) {
    inspection.error(`触发条件引用了未知玩家 ${condition.player}`);
  } else if (condition.type === 'turnCycle' && (!Number.isInteger(condition.every) || condition.every < 1)) {
    inspection.error('循环回合条件的间隔必须是正整数');
  } else if (condition.type === 'objective') {
    if (!declarations.players.has(condition.player)) {
      inspection.error(`触发条件引用了未知玩家 ${condition.player}`);
    }
    if (!declarations.objectivesOfPlayer(condition.player).has(condition.id)) {
      inspection.error(`触发条件引用了未知目标 ${condition.player}:${condition.id}`);
    }
  } else if (condition.type === 'all' || condition.type === 'any') {
    for (const child of condition.conditions) checkCondition(inspection, child);
  } else if (condition.type === 'not') checkCondition(inspection, condition.condition);
}

/** The units an effect aims at, for the effects that aim at units. */
function unitSelectorOf(effect: ScenarioEffect): UnitSelector | null {
  switch (effect.type) {
    case 'addStatus':
    case 'removeStatus':
    case 'changeUnitOwner':
    case 'changeUnitResource':
    case 'withdrawUnits':
    case 'forceMove':
    case 'teleportUnits':
    case 'changeMorale':
    case 'surrenderUnits':
    case 'setUnitDirective':
      return effect.selector;
    default:
      return null;
  }
}

/** The objective an effect steers, for the effects that steer one. */
function steeredObjective(effect: ScenarioEffect): { player: number; id: string } | null {
  switch (effect.type) {
    case 'activateObjective':
    case 'cancelObjective':
    case 'completeObjective':
    case 'revealObjective':
      return { player: effect.player, id: effect.id };
    default:
      return null;
  }
}

function checkEffect(inspection: LevelInspection, effect: ScenarioEffect): void {
  const { content, declarations } = inspection;
  const zone = (id: string, subject = '场景效果') => {
    if (!declarations.zones.has(id)) inspection.error(`${subject}引用了未知区域 ${id}`);
  };

  if ((effect.type === 'addStatus' || effect.type === 'removeStatus') && !content.statuses.has(effect.status)) {
    inspection.error(`场景效果引用了未知状态 ${effect.status}`);
  }
  const aimedAt = unitSelectorOf(effect);
  if (aimedAt?.zone) zone(aimedAt.zone);
  if ((effect.type === 'reviveMarkers' || effect.type === 'removeMarkers') && effect.selector.zone) {
    zone(effect.selector.zone, '场景标记效果');
  }
  if (effect.type === 'restoreWithdrawnUnits') {
    zone(effect.zone, '恢复撤退单位');
    if (effect.selector.zone) zone(effect.selector.zone, '恢复撤退单位的选择器');
  }
  if (effect.type === 'setUnitDirective') {
    if (effect.directive.zone) zone(effect.directive.zone, '战术指令');
    for (const waypoint of effect.directive.waypoints ?? []) {
      if (!inspection.inBounds(waypoint)) {
        inspection.error(`战术指令的巡逻点越界：${waypoint.x},${waypoint.y}`);
      }
    }
  }
  if (effect.type === 'addEngagementRule') {
    if (!effect.rule.id.trim()) inspection.error('交战规则缺少 id');
    zone(effect.rule.zone, '交战规则');
    for (const owner of effect.rule.players ?? []) {
      if (!declarations.players.has(owner)) inspection.error(`交战规则引用了未知玩家 ${owner}`);
    }
  }
  if (effect.type === 'spawnUnits') {
    for (const unit of effect.units) {
      if (!content.units.has(unit.unit)) inspection.error(`增援引用了未知兵种 ${unit.unit}`);
      if (!declarations.players.has(unit.owner)) inspection.error(`增援引用了未知玩家 ${unit.owner}`);
      if (!inspection.inBounds(unit)) inspection.error(`增援位置越界：${unit.x},${unit.y}`);
    }
  }
  if (effect.type === 'setPlayerTeam' && !declarations.players.has(effect.player)) {
    inspection.error(`阵营变更引用了未知玩家 ${effect.player}`);
  }
  if (effect.type === 'forceMove') {
    if (!inspection.inBounds(effect.source)) {
      inspection.error(`强制位移来源越界：${effect.source.x},${effect.source.y}`);
    }
    if (!Number.isInteger(effect.distance) || effect.distance < 0) {
      inspection.error('强制位移距离必须是非负整数');
    }
  }
  if (effect.type === 'teleportUnits') {
    zone(effect.zone, '传送效果');
    if (effect.selector.zone) zone(effect.selector.zone, '传送选择器');
  }
  if (effect.type === 'addOverlay') {
    if (!content.terrainOverlays.has(effect.overlay)) {
      inspection.error(`场景效果引用了未知覆盖 ${effect.overlay}`);
    }
    zone(effect.zone);
  }
  if (effect.type === 'replaceTerrain' || effect.type === 'setElevation' || effect.type === 'addElevation') {
    zone(effect.zone);
  }
  if (effect.type === 'replaceTerrain' && !content.terrains.has(effect.terrain)) {
    inspection.error(`场景效果引用了未知地形 ${effect.terrain}`);
  }
  if ((effect.type === 'setElevation' && !Number.isInteger(effect.value)) ||
    (effect.type === 'addElevation' && !Number.isInteger(effect.amount))) {
    inspection.error('动态海拔必须使用整数');
  }
  if (effect.type === 'setCliffs') {
    for (const edge of effect.edges) {
      if (!inspection.inBounds(edge.from) || !inspection.inBounds(edge.to) ||
        !sharesEdge(edge.from, edge.to)) {
        inspection.error(`动态悬崖边无效：${edge.from.x},${edge.from.y} -> ${edge.to.x},${edge.to.y}`);
      }
    }
  }
  if (effect.type === 'setDirectionalCover') {
    for (const cover of effect.covers) {
      if (!inspection.inBounds(cover.at)) {
        inspection.error(`动态方向掩体越界：${cover.at.x},${cover.at.y}`);
      }
    }
  }
  if ((effect.type === 'damageStructure' || effect.type === 'repairStructure') &&
    !declarations.structures.has(effect.id)) {
    inspection.error(`场景效果引用了未知结构 ${effect.id}`);
  }
  if (effect.type === 'moveComposite' && !declarations.composites.has(effect.id)) {
    inspection.error(`场景效果引用了未知复合目标 ${effect.id}`);
  }
  const steered = steeredObjective(effect);
  if (steered && !declarations.objectivesOfPlayer(steered.player).has(steered.id)) {
    inspection.error(`场景效果引用了未知目标 ${steered.player}:${steered.id}`);
  }
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
      (owner, tile) => owner === player.id && inspection.terrainAt(tile).produces.length > 0,
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

/** Structural + playability lint. The editor surfaces this live. */
export function validateLevel(level: LevelData, content: ContentCatalog): LevelIssue[] {
  const log = new LevelIssueLog();
  const inspection = new LevelInspection(level, content, log);
  for (const check of LEVEL_CHECKS) check(inspection);
  return log.issues;
}
