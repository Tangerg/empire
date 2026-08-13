import { TerrainEncoding } from './data/terrain-encoding';
import { idx } from './grid';
import type {
  GameMap,
  LevelData,
  Objective,
  PlayerConfig,
  ScenarioCondition,
  ScenarioEffect,
  TerrainId,
} from './types';
import { DEFAULT_RULES } from './types';
import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from './resources';
import { type ContentCatalog } from './content-pack';

/**
 * Level files store terrain as rows of single characters so a map diff is
 * readable in git and hand-editable in a pinch. The editor reads and writes
 * exactly this format.
 */
export const terrainCharacter = (terrain: TerrainId): string | undefined => TerrainEncoding.character(terrain);
export const terrainForCharacter = (character: string): TerrainId | undefined => TerrainEncoding.terrain(character);

export class LevelFormatError extends Error {}

function serializedTerrainCharacter(terrain: TerrainId): string {
  const exact = terrainCharacter(terrain);
  if (exact !== undefined) return exact;
  const fallback = terrainCharacter(TerrainEncoding.defaultTerrain);
  if (fallback !== undefined) return fallback;
  throw new LevelFormatError(`terrain "${terrain}" has no serialized character`);
}

/* --------------------------------------------------------------- deserialize */

export function mapFromLevel(level: LevelData, content: ContentCatalog): GameMap {
  const { width, height } = level;
  if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1) {
    throw new LevelFormatError(`bad map size ${width}x${height}`);
  }
  if (level.terrain.length !== height) {
    throw new LevelFormatError(`terrain has ${level.terrain.length} rows, expected ${height}`);
  }

  const tiles: TerrainId[] = new Array(width * height).fill(content.terrainEncoding.defaultTerrain);
  level.terrain.forEach((row, y) => {
    if (row.length !== width) {
      throw new LevelFormatError(`terrain row ${y} has ${row.length} chars, expected ${width}`);
    }
    for (let x = 0; x < width; x++) {
      const ch = row[x];
      const id = content.terrainEncoding.terrain(ch);
      if (!id) throw new LevelFormatError(`unknown terrain char "${ch}" at ${x},${y}`);
      tiles[y * width + x] = id;
    }
  });

  const map: GameMap = {
    width,
    height,
    tiles,
    owners: new Array(width * height).fill(0),
    captureProgress: new Array(width * height).fill(0),
    elevation: level.elevation?.slice() ?? new Array(width * height).fill(0),
    cliffs: (level.cliffs ?? []).map((edge) => ({ from: { ...edge.from }, to: { ...edge.to } })),
    directionalCover: (level.directionalCover ?? []).map((cover) => ({
      at: { ...cover.at },
      sides: { ...cover.sides },
    })),
  };

  if (map.elevation.length !== width * height) {
    throw new LevelFormatError(`elevation has ${map.elevation.length} cells, expected ${width * height}`);
  }
  if (map.elevation.some((value) => !Number.isInteger(value))) {
    throw new LevelFormatError('elevation values must be integers');
  }
  for (const cliff of map.cliffs) {
    const valid = [cliff.from, cliff.to].every((cell) =>
      Number.isInteger(cell.x) && Number.isInteger(cell.y) &&
      cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height);
    if (!valid || Math.abs(cliff.from.x - cliff.to.x) + Math.abs(cliff.from.y - cliff.to.y) !== 1) {
      throw new LevelFormatError(`invalid cliff edge ${cliff.from.x},${cliff.from.y} -> ${cliff.to.x},${cliff.to.y}`);
    }
  }
  for (const cover of map.directionalCover) {
    if (cover.at.x < 0 || cover.at.y < 0 || cover.at.x >= width || cover.at.y >= height) {
      throw new LevelFormatError(`directional cover out of bounds at ${cover.at.x},${cover.at.y}`);
    }
    for (const [side, level] of Object.entries(cover.sides)) {
      if (!['north', 'east', 'south', 'west'].includes(side) || !['half', 'full'].includes(level!)) {
        throw new LevelFormatError(`invalid directional cover ${side}:${String(level)}`);
      }
    }
  }

  for (const o of level.owners ?? []) {
    if (o.x < 0 || o.y < 0 || o.x >= width || o.y >= height) {
      throw new LevelFormatError(`owner entry out of bounds at ${o.x},${o.y}`);
    }
    const i = idx(map, o.x, o.y);
    if (!content.terrains.get(map.tiles[i]).capturable) {
      throw new LevelFormatError(`tile ${o.x},${o.y} (${map.tiles[i]}) cannot be owned`);
    }
    map.owners[i] = o.owner;
  }

  return map;
}

/* ----------------------------------------------------------------- serialize */

export function terrainRows(map: GameMap): string[] {
  const rows: string[] = [];
  for (let y = 0; y < map.height; y++) {
    let row = '';
    for (let x = 0; x < map.width; x++) {
      row += serializedTerrainCharacter(map.tiles[y * map.width + x]);
    }
    rows.push(row);
  }
  return rows;
}

/* ------------------------------------------------------------------ validate */

export interface LevelIssue {
  severity: 'error' | 'warning';
  message: string;
}

function validateResourceAccounts(
  accounts: PlayerConfig['resources'],
  owner: string,
  error: (message: string) => void,
): void {
  for (const [id, account] of Object.entries(accounts)) {
    if (!id.trim()) error(`${owner} 的资源 id 不能为空`);
    if (!Number.isFinite(account.current) || account.current < 0) {
      error(`${owner} 的资源 "${id}" 当前值必须 >= 0`);
    }
    if (account.capacity !== null && (!Number.isFinite(account.capacity) || account.capacity < 0)) {
      error(`${owner} 的资源 "${id}" 上限必须 >= 0 或 null`);
    }
    if (account.capacity !== null && account.current > account.capacity) {
      error(`${owner} 的资源 "${id}" 当前值不能超过上限`);
    }
  }
}

/** Structural + playability lint. The editor surfaces this live. */
export function validateLevel(
  level: LevelData,
  content: ContentCatalog,
): LevelIssue[] {
  const issues: LevelIssue[] = [];
  const err = (message: string) => issues.push({ severity: 'error', message });
  const warn = (message: string) => issues.push({ severity: 'warning', message });

  let map: GameMap | null = null;
  try {
    map = mapFromLevel(level, content);
  } catch (e) {
    err((e as Error).message);
  }

  if (!level.id?.trim()) err('关卡缺少 id');
  if (!level.name?.trim()) warn('关卡缺少名称');

  const inLevelBounds = (c: { x: number; y: number }) =>
    Number.isInteger(c.x) && Number.isInteger(c.y) && c.x >= 0 && c.y >= 0 && c.x < level.width && c.y < level.height;
  const cliffKeys = new Set<string>();
  for (const cliff of level.cliffs ?? []) {
    if (!inLevelBounds(cliff.from) || !inLevelBounds(cliff.to)) {
      err(`悬崖边越界：${cliff.from.x},${cliff.from.y} -> ${cliff.to.x},${cliff.to.y}`);
      continue;
    }
    if (Math.abs(cliff.from.x - cliff.to.x) + Math.abs(cliff.from.y - cliff.to.y) !== 1) {
      err(`悬崖边必须连接相邻格：${cliff.from.x},${cliff.from.y} -> ${cliff.to.x},${cliff.to.y}`);
    }
    const ends = [`${cliff.from.x},${cliff.from.y}`, `${cliff.to.x},${cliff.to.y}`].sort();
    const key = ends.join('|');
    if (cliffKeys.has(key)) warn(`悬崖边重复：${key}`);
    cliffKeys.add(key);
  }
  for (const cover of level.directionalCover ?? []) {
    if (!inLevelBounds(cover.at)) err(`方向掩体越界：${cover.at.x},${cover.at.y}`);
    if (Object.keys(cover.sides).length === 0) warn(`方向掩体 ${cover.at.x},${cover.at.y} 没有任何受保护边`);
  }

  const ids = new Set<number>();
  for (const p of level.players) {
    if (p.id < 1) err(`玩家 id 必须 >= 1（发现 ${p.id}）`);
    if (ids.has(p.id)) err(`玩家 id 重复：${p.id}`);
    ids.add(p.id);
    validateResourceAccounts(p.resources, `玩家 ${p.id}`, err);
  }
  if (level.players.length < 2) err('至少需要两名玩家');
  if (!level.players.some((p) => p.controller === 'human')) warn('没有任何人类玩家');

  const seen = new Set<number>();
  const unitKeys = new Set<string>();
  for (const u of level.units) {
    if (u.key) {
      if (unitKeys.has(u.key)) err(`单位 key 重复：${u.key}`);
      unitKeys.add(u.key);
    }
    if (!content.units.has(u.unit)) err(`未知兵种 "${u.unit}"`);
    if (!ids.has(u.owner)) err(`单位 ${u.unit}@${u.x},${u.y} 的归属玩家 ${u.owner} 不存在`);
    if (u.rank !== undefined && ![0, 1, 2].includes(u.rank)) err(`单位 ${u.unit} 的军衔必须是 0、1 或 2`);
    if (u.rankProgress !== undefined && (!Number.isFinite(u.rankProgress) || u.rankProgress < 0)) {
      err(`单位 ${u.unit} 的军衔经验必须 >= 0`);
    }
    validateResourceAccounts(u.resources ?? {}, `单位 ${u.unit}`, err);
    if (u.facing !== undefined && !['north', 'east', 'south', 'west'].includes(u.facing)) {
      err(`单位 ${u.unit} 的朝向无效：${u.facing}`);
    }
    if (u.morale !== undefined && (!Number.isFinite(u.morale) || u.morale < 0)) {
      err(`单位 ${u.unit} 的士气必须 >= 0`);
    }
    if (u.formation !== undefined) {
      if (!content.formations.has(u.formation)) err(`单位 ${u.unit} 使用未知阵形 "${u.formation}"`);
      else if (!content.units.tryGet(u.unit)?.formations?.includes(u.formation)) {
        err(`单位 ${u.unit} 不能使用阵形 "${u.formation}"`);
      }
    }
    for (const waypoint of u.directive?.waypoints ?? []) {
      if (!inLevelBounds(waypoint)) err(`单位 ${u.unit} 的巡逻点越界：${waypoint.x},${waypoint.y}`);
    }
    if (u.career !== undefined) {
      const career = content.careers.tryGet(u.career);
      if (!career) err(`单位 ${u.unit} 使用未知职业 "${u.career}"`);
      else if (career.unitType !== u.unit) err(`职业 ${u.career} 与兵种 ${u.unit} 不匹配`);
    }
    for (const career of u.unlockedCareers ?? []) {
      if (!content.careers.has(career)) err(`单位 ${u.unit} 解锁了未知职业 "${career}"`);
    }
    for (const [career, mastery] of Object.entries(u.careerMastery ?? {})) {
      if (!content.careers.has(career)) err(`单位 ${u.unit} 记录了未知职业熟练度 "${career}"`);
      if (!Number.isFinite(mastery) || mastery < 0) err(`单位 ${u.unit} 的职业熟练度必须 >= 0`);
    }
    if (map) {
      if (u.x < 0 || u.y < 0 || u.x >= map.width || u.y >= map.height) {
        err(`单位越界：${u.x},${u.y}`);
        continue;
      }
      const key = u.y * map.width + u.x;
      if (seen.has(key)) err(`格子 ${u.x},${u.y} 上有多个单位`);
      seen.add(key);
      const t = content.terrains.get(map.tiles[key]);
      const def = content.units.tryGet(u.unit);
      if (def && t.cost[def.movementClass] == null) {
        err(`${def.name} 无法站在 ${t.name} 上（${u.x},${u.y}）`);
      }
    }
  }

  const commanderIds = new Set<string>();
  for (const commander of level.commanders ?? []) {
    if (!commander.id.trim()) err('指挥官缺少 id');
    if (commanderIds.has(commander.id)) err(`指挥官 id 重复：${commander.id}`);
    commanderIds.add(commander.id);
    if (!unitKeys.has(commander.unitKey)) {
      err(`指挥官 ${commander.id} 引用了未知单位 key ${commander.unitKey}`);
    }
    if (!Number.isFinite(commander.radius) || commander.radius < 0) {
      err(`指挥官 ${commander.id} 的范围必须 >= 0`);
    }
    for (const tactic of commander.tactics ?? []) {
      if (!content.tactics.has(tactic)) err(`指挥官 ${commander.id} 使用未知战术 "${tactic}"`);
    }
  }
  for (const unit of level.units) {
    if (unit.commander && !commanderIds.has(unit.commander)) {
      err(`单位 ${unit.key ?? `${unit.x},${unit.y}`} 链接到未知指挥官 ${unit.commander}`);
    }
  }
  const structureIds = new Set<string>();
  for (const structure of level.structures ?? []) {
    if (!structure.id.trim()) err('结构缺少 id');
    if (structureIds.has(structure.id)) err(`结构 id 重复：${structure.id}`);
    structureIds.add(structure.id);
    if (!content.structures.has(structure.type)) err(`未知结构 "${structure.type}"`);
    if (map && (structure.x < 0 || structure.y < 0 || structure.x >= map.width || structure.y >= map.height)) {
      err(`结构越界：${structure.id}@${structure.x},${structure.y}`);
    }
    if (structure.owner !== undefined && structure.owner !== 0 && !ids.has(structure.owner)) {
      err(`结构 ${structure.id} 的归属玩家 ${structure.owner} 不存在`);
    }
  }

  const compositeIds = new Set<string>();
  for (const composite of level.composites ?? []) {
    if (!composite.id.trim()) err('复合目标缺少 id');
    if (compositeIds.has(composite.id)) err(`复合目标 id 重复：${composite.id}`);
    compositeIds.add(composite.id);
    if (composite.parts.length === 0) err(`复合目标 ${composite.id} 没有部件`);
    for (const part of composite.parts) if (!structureIds.has(part)) {
      err(`复合目标 ${composite.id} 引用了未知结构 ${part}`);
    }
    const threshold = composite.minimumNeutralized ?? composite.parts.length;
    if (!Number.isInteger(threshold) || threshold < 1 || threshold > composite.parts.length) {
      err(`复合目标 ${composite.id} 的瘫痪阈值无效`);
    }
  }

  const zoneIds = new Set<string>();
  for (const zone of level.scenario?.zones ?? []) {
    if (!zone.id.trim()) err('区域缺少 id');
    if (zoneIds.has(zone.id)) err(`区域 id 重复：${zone.id}`);
    zoneIds.add(zone.id);
    for (const cell of zone.cells) {
      if (map && (cell.x < 0 || cell.y < 0 || cell.x >= map.width || cell.y >= map.height)) {
        err(`区域 ${zone.id} 包含越界格：${cell.x},${cell.y}`);
      }
    }
  }
  for (const unit of level.units) {
    if (unit.directive?.zone && !zoneIds.has(unit.directive.zone)) {
      err(`单位 ${unit.key ?? unit.unit} 的战术指令引用了未知区域 ${unit.directive.zone}`);
    }
  }
  const engagementRuleIds = new Set<string>();
  for (const rule of level.scenario?.engagementRules ?? []) {
    if (!rule.id.trim()) err('交战规则缺少 id');
    if (engagementRuleIds.has(rule.id)) err(`交战规则 id 重复：${rule.id}`);
    engagementRuleIds.add(rule.id);
    if (!zoneIds.has(rule.zone)) err(`交战规则 ${rule.id} 引用了未知区域 ${rule.zone}`);
    for (const owner of rule.players ?? []) if (!ids.has(owner)) err(`交战规则 ${rule.id} 引用了未知玩家 ${owner}`);
  }

  if (level.deployment) {
    const order = level.deployment.order ?? level.deployment.zones.map((entry) => entry.player);
    if (order.length === 0) err('部署阶段至少需要一名玩家');
    for (const owner of order) if (!ids.has(owner)) err(`部署顺序引用了未知玩家 ${owner}`);
    const assignedKeys = new Set<string>();
    for (const assignment of level.deployment.zones) {
      if (!ids.has(assignment.player)) err(`部署区域引用了未知玩家 ${assignment.player}`);
      if (!zoneIds.has(assignment.zone)) err(`部署区域引用了未知区域 ${assignment.zone}`);
      for (const key of assignment.unitKeys ?? []) {
        const unit = level.units.find((candidate) => candidate.key === key);
        if (!unit) err(`部署区域引用了未知单位 key ${key}`);
        else if (unit.owner !== assignment.player) err(`部署单位 ${key} 不属于玩家 ${assignment.player}`);
        if (assignedKeys.has(key)) err(`部署单位 ${key} 被分配到多个区域`);
        assignedKeys.add(key);
      }
    }
    for (const owner of new Set(level.deployment.zones.map((entry) => entry.player))) {
      if (level.deployment.zones.filter((entry) => entry.player === owner && !entry.unitKeys).length > 1) {
        err(`玩家 ${owner} 有多个未指定单位的部署区域`);
      }
    }
  }

  const overlayIds = new Set<string>();
  for (const overlay of level.scenario?.overlays ?? []) {
    if (!overlay.id.trim()) err('地形覆盖缺少 id');
    if (overlayIds.has(overlay.id)) err(`地形覆盖 id 重复：${overlay.id}`);
    overlayIds.add(overlay.id);
    if (!content.terrainOverlays.has(overlay.type)) err(`未知地形覆盖 "${overlay.type}"`);
    if (!zoneIds.has(overlay.zone)) err(`地形覆盖 ${overlay.id} 引用了未知区域 ${overlay.zone}`);
    if (overlay.remainingRounds !== undefined && overlay.remainingRounds !== null && overlay.remainingRounds < 1) {
      err(`地形覆盖 ${overlay.id} 的持续回合必须 >= 1`);
    }
  }

  const objectiveIdsByPlayer = new Map<number, Set<string>>();
  const validateObjectives = (objectives: Objective[], owner: number) => {
    const objectiveIds = new Set<string>();
    objectiveIdsByPlayer.set(owner, objectiveIds);
    const visit = (objective: Objective) => {
      if (objective.id) {
        if (objectiveIds.has(objective.id)) err(`玩家 ${owner} 的目标 id 重复：${objective.id}`);
        objectiveIds.add(objective.id);
      }
      if (objective.type === 'destroy') {
        for (const id of objective.structures) {
          if (!structureIds.has(id)) err(`目标 ${objective.id ?? objective.type} 引用了未知结构 ${id}`);
        }
      }
      if (objective.type === 'neutralizeComposite') {
        if (!compositeIds.has(objective.composite)) {
          err(`目标 ${objective.id ?? objective.type} 引用了未知复合目标 ${objective.composite}`);
        }
        if (objective.minimumNeutralized !== undefined &&
          (!Number.isInteger(objective.minimumNeutralized) || objective.minimumNeutralized < 1)) {
          err(`复合目标 ${objective.id ?? objective.type} 的瘫痪数量必须 >= 1`);
        }
      }
      if ((objective.type === 'escort' || objective.type === 'control') && !zoneIds.has(objective.zone)) {
        err(`目标 ${objective.id ?? objective.type} 引用了未知区域 ${objective.zone}`);
      }
      if (objective.type === 'protect' && (objective.minimumAlive < 1 || objective.untilTurn < 1)) {
        err(`保护目标 ${objective.id ?? ''} 的人数和截止回合必须 >= 1`);
      }
      if (objective.type === 'escort' && objective.count < 1) {
        err(`护送目标 ${objective.id ?? ''} 的抵达人数必须 >= 1`);
      }
      if (objective.type === 'all' || objective.type === 'any' || objective.type === 'sequence') {
        if (objective.objectives.length === 0) err(`组合目标 ${objective.id ?? objective.type} 不能为空`);
        objective.objectives.forEach(visit);
      } else if (objective.type === 'optional' || objective.type === 'failOn') {
        visit(objective.objective);
      }
    };
    objectives.forEach(visit);
  };
  for (const owner of level.players) {
    validateObjectives(owner.objectives?.length ? owner.objectives : (level.victory ?? []), owner.id);
  }

  const validateCondition = (condition: ScenarioCondition): void => {
    if (condition.type === 'unitInZone' && !zoneIds.has(condition.zone)) {
      err(`触发条件引用了未知区域 ${condition.zone}`);
    } else if ((condition.type === 'unitCount' || condition.type === 'unitHealth') &&
      condition.selector.zone && !zoneIds.has(condition.selector.zone)) {
      err(`触发条件引用了未知区域 ${condition.selector.zone}`);
    } else if (condition.type === 'markerCount' && condition.selector.zone && !zoneIds.has(condition.selector.zone)) {
      err(`触发条件引用了未知区域 ${condition.selector.zone}`);
    } else if (condition.type === 'structure' && !structureIds.has(condition.id)) {
      err(`触发条件引用了未知结构 ${condition.id}`);
    } else if (condition.type === 'composite' && !compositeIds.has(condition.id)) {
      err(`触发条件引用了未知复合目标 ${condition.id}`);
    } else if (condition.type === 'currentPlayer' && !ids.has(condition.player)) {
      err(`触发条件引用了未知玩家 ${condition.player}`);
    } else if (condition.type === 'turnCycle' && (!Number.isInteger(condition.every) || condition.every < 1)) {
      err('循环回合条件的间隔必须是正整数');
    } else if (condition.type === 'objective') {
      if (!ids.has(condition.player)) err(`触发条件引用了未知玩家 ${condition.player}`);
      if (!objectiveIdsByPlayer.get(condition.player)?.has(condition.id)) {
        err(`触发条件引用了未知目标 ${condition.player}:${condition.id}`);
      }
    } else if (condition.type === 'all' || condition.type === 'any') {
      condition.conditions.forEach(validateCondition);
    } else if (condition.type === 'not') {
      validateCondition(condition.condition);
    }
  };

  const validateEffect = (effect: ScenarioEffect): void => {
    if ((effect.type === 'addStatus' || effect.type === 'removeStatus') && !content.statuses.has(effect.status)) {
      err(`场景效果引用了未知状态 ${effect.status}`);
    }
    const unitSelector =
      effect.type === 'addStatus' || effect.type === 'removeStatus' ||
      effect.type === 'changeUnitOwner' || effect.type === 'changeUnitResource' ||
      effect.type === 'withdrawUnits' || effect.type === 'forceMove' ||
      effect.type === 'teleportUnits' || effect.type === 'changeMorale' ||
      effect.type === 'surrenderUnits' || effect.type === 'setUnitDirective'
        ? effect.selector : null;
    if (unitSelector?.zone && !zoneIds.has(unitSelector.zone)) {
      err(`场景效果引用了未知区域 ${unitSelector.zone}`);
    }
    if ((effect.type === 'reviveMarkers' || effect.type === 'removeMarkers') &&
      effect.selector.zone && !zoneIds.has(effect.selector.zone)) {
      err(`场景标记效果引用了未知区域 ${effect.selector.zone}`);
    }
    if (effect.type === 'restoreWithdrawnUnits') {
      if (!zoneIds.has(effect.zone)) err(`恢复撤退单位引用了未知区域 ${effect.zone}`);
      if (effect.selector.zone && !zoneIds.has(effect.selector.zone)) err(`恢复撤退单位的选择器引用了未知区域 ${effect.selector.zone}`);
    }
    if (effect.type === 'setUnitDirective') {
      if (effect.directive.zone && !zoneIds.has(effect.directive.zone)) err(`战术指令引用了未知区域 ${effect.directive.zone}`);
      for (const waypoint of effect.directive.waypoints ?? []) if (!inLevelBounds(waypoint)) {
        err(`战术指令的巡逻点越界：${waypoint.x},${waypoint.y}`);
      }
    }
    if (effect.type === 'addEngagementRule') {
      if (!effect.rule.id.trim()) err('交战规则缺少 id');
      if (!zoneIds.has(effect.rule.zone)) err(`交战规则引用了未知区域 ${effect.rule.zone}`);
      for (const owner of effect.rule.players ?? []) if (!ids.has(owner)) {
        err(`交战规则引用了未知玩家 ${owner}`);
      }
    }
    if (effect.type === 'spawnUnits') {
      for (const unit of effect.units) {
        if (!content.units.has(unit.unit)) err(`增援引用了未知兵种 ${unit.unit}`);
        if (!ids.has(unit.owner)) err(`增援引用了未知玩家 ${unit.owner}`);
        if (!inLevelBounds(unit)) err(`增援位置越界：${unit.x},${unit.y}`);
      }
    }
    if (effect.type === 'setPlayerTeam' && !ids.has(effect.player)) {
      err(`阵营变更引用了未知玩家 ${effect.player}`);
    }
    if (effect.type === 'forceMove') {
      if (!inLevelBounds(effect.source)) err(`强制位移来源越界：${effect.source.x},${effect.source.y}`);
      if (!Number.isInteger(effect.distance) || effect.distance < 0) err('强制位移距离必须是非负整数');
    }
    if (effect.type === 'teleportUnits') {
      if (!zoneIds.has(effect.zone)) err(`传送效果引用了未知区域 ${effect.zone}`);
      if (effect.selector.zone && !zoneIds.has(effect.selector.zone)) err(`传送选择器引用了未知区域 ${effect.selector.zone}`);
    }
    if (effect.type === 'addOverlay') {
      if (!content.terrainOverlays.has(effect.overlay)) err(`场景效果引用了未知覆盖 ${effect.overlay}`);
      if (!zoneIds.has(effect.zone)) err(`场景效果引用了未知区域 ${effect.zone}`);
    }
    if ((effect.type === 'replaceTerrain' || effect.type === 'setElevation' || effect.type === 'addElevation') && !zoneIds.has(effect.zone)) {
      err(`场景效果引用了未知区域 ${effect.zone}`);
    }
    if (effect.type === 'replaceTerrain' && !content.terrains.has(effect.terrain)) {
      err(`场景效果引用了未知地形 ${effect.terrain}`);
    }
    if ((effect.type === 'setElevation' && !Number.isInteger(effect.value)) ||
      (effect.type === 'addElevation' && !Number.isInteger(effect.amount))) {
      err('动态海拔必须使用整数');
    }
    if (effect.type === 'setCliffs') {
      for (const edge of effect.edges) {
        if (!inLevelBounds(edge.from) || !inLevelBounds(edge.to) ||
          Math.abs(edge.from.x - edge.to.x) + Math.abs(edge.from.y - edge.to.y) !== 1) {
          err(`动态悬崖边无效：${edge.from.x},${edge.from.y} -> ${edge.to.x},${edge.to.y}`);
        }
      }
    }
    if (effect.type === 'setDirectionalCover') {
      for (const cover of effect.covers) if (!inLevelBounds(cover.at)) {
        err(`动态方向掩体越界：${cover.at.x},${cover.at.y}`);
      }
    }
    if (
      (effect.type === 'damageStructure' || effect.type === 'repairStructure') &&
      !structureIds.has(effect.id)
    ) {
      err(`场景效果引用了未知结构 ${effect.id}`);
    }
    if (effect.type === 'moveComposite' && !compositeIds.has(effect.id)) {
      err(`场景效果引用了未知复合目标 ${effect.id}`);
    }
    if (
      effect.type === 'activateObjective' ||
      effect.type === 'cancelObjective' ||
      effect.type === 'completeObjective' ||
      effect.type === 'revealObjective'
    ) {
      if (!objectiveIdsByPlayer.get(effect.player)?.has(effect.id)) {
        err(`场景效果引用了未知目标 ${effect.player}:${effect.id}`);
      }
    }
  };

  const triggerIds = new Set<string>();
  for (const trigger of level.scenario?.triggers ?? []) {
    if (!trigger.id.trim()) err('触发器缺少 id');
    if (triggerIds.has(trigger.id)) err(`触发器 id 重复：${trigger.id}`);
    triggerIds.add(trigger.id);
    validateCondition(trigger.condition);
    trigger.effects.forEach(validateEffect);
    if (trigger.repeat) {
      if (!Number.isInteger(trigger.repeat.everyRounds) || trigger.repeat.everyRounds < 1) {
        err(`触发器 ${trigger.id} 的循环间隔必须是正整数`);
      }
      if (trigger.repeat.startTurn !== undefined && trigger.repeat.startTurn < 1) {
        err(`触发器 ${trigger.id} 的起始回合必须 >= 1`);
      }
      if (trigger.repeat.endTurn !== undefined && trigger.repeat.startTurn !== undefined &&
        trigger.repeat.endTurn < trigger.repeat.startTurn) {
        err(`触发器 ${trigger.id} 的结束回合早于起始回合`);
      }
      if (trigger.repeat.maxFirings !== undefined &&
        (!Number.isInteger(trigger.repeat.maxFirings) || trigger.repeat.maxFirings < 1)) {
        err(`触发器 ${trigger.id} 的最大触发次数必须是正整数`);
      }
    }
  }

  for (const o of level.owners ?? []) {
    if (o.owner !== 0 && !ids.has(o.owner)) err(`建筑归属玩家 ${o.owner} 不存在`);
  }

  if (map) {
    for (const p of level.players) {
      const hasUnits = level.units.some((u) => u.owner === p.id);
      const hasProduction = map.owners.some(
        (owner, i) => owner === p.id && content.terrains.get(map!.tiles[i]).produces.length > 0,
      );
      if (!hasUnits && !hasProduction) {
        err(`玩家 ${p.id}（${p.name}）既没有单位也没有生产建筑，开局即败`);
      }
    }
  }

  const victory = level.victory ?? [];
  if (victory.length === 0 && !level.players.some((p) => p.objectives?.length)) {
    warn('未设置胜利条件，将回退为「歼灭敌军 / 攻占城堡」');
  }

  return issues;
}

/* ------------------------------------------------------------------ factory */

export function emptyLevel(width = 20, height = 14): LevelData {
  const row = '.'.repeat(width);
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
    players: [
      defaultPlayer(1, '蓝军', '#3f7fd8', 'human'),
      defaultPlayer(2, '红军', '#d8483f', 'ai'),
    ],
    rules: {},
    victory: [{ type: 'routEnemies' }, { type: 'captureHQ' }],
  };
}

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

export const DEFAULT_VICTORY: Objective[] = [{ type: 'routEnemies' }, { type: 'captureHQ' }];

/** Normalise a loaded blob into a fully-populated LevelData. */
export function normaliseLevel(raw: unknown): LevelData {
  if (typeof raw !== 'object' || raw === null) throw new LevelFormatError('关卡数据不是对象');
  const o = raw as Partial<LevelData>;
  if (o.schema !== 2) throw new LevelFormatError(`不支持的 schema：${String(o.schema)}；当前需要 schema 2`);
  if (!Array.isArray(o.terrain)) throw new LevelFormatError('缺少 terrain');
  const height = o.height ?? o.terrain.length;
  const width = o.width ?? (o.terrain[0]?.length ?? 0);
  const players = o.players ?? emptyLevel().players;
  for (const candidate of players) {
    if (!candidate.resources || typeof candidate.resources !== 'object') {
      throw new LevelFormatError(
        `玩家 ${candidate.id ?? '?'} 缺少 resources；旧资金/指挥点字段已停止支持`,
      );
    }
  }
  return {
    schema: 2,
    id: o.id ?? 'untitled',
    name: o.name ?? '未命名关卡',
    author: o.author,
    description: o.description ?? '',
    width,
    height,
    terrain: o.terrain,
    elevation: Array.isArray(o.elevation) ? o.elevation.map(Number) : new Array(width * height).fill(0),
    cliffs: Array.isArray(o.cliffs) ? o.cliffs : [],
    directionalCover: Array.isArray(o.directionalCover) ? o.directionalCover : [],
    owners: o.owners ?? [],
    units: o.units ?? [],
    commanders: o.commanders ?? [],
    structures: o.structures ?? [],
    composites: o.composites ?? [],
    players,
    rules: o.rules ?? {},
    victory: o.victory ?? DEFAULT_VICTORY,
    scenario: o.scenario,
    deployment: o.deployment,
    extra: o.extra,
  };
}

export const resolveRules = (level: LevelData) => ({ ...DEFAULT_RULES, ...(level.rules ?? {}) });
