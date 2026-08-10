import { Terrains } from './data/terrain';
import { UnitTypes } from './data/units';
import { idx } from './grid';
import type { GameMap, LevelData, Objective, PlayerConfig, TerrainId } from './types';
import { DEFAULT_RULES } from './types';

/**
 * Level files store terrain as rows of single characters so a map diff is
 * readable in git and hand-editable in a pinch. The editor reads and writes
 * exactly this format.
 */
export const TERRAIN_CHARS: Record<string, TerrainId> = {
  '.': 'plain',
  '-': 'road',
  '=': 'bridge',
  T: 'forest',
  h: 'hill',
  '^': 'mountain',
  '~': 'water',
  '#': 'wall',
  v: 'village',
  b: 'barracks',
  C: 'castle',
};

export const CHAR_OF_TERRAIN: Record<TerrainId, string> = Object.fromEntries(
  Object.entries(TERRAIN_CHARS).map(([c, t]) => [t, c]),
);

export const DEFAULT_TERRAIN: TerrainId = 'plain';

export class LevelFormatError extends Error {}

/* --------------------------------------------------------------- deserialize */

export function mapFromLevel(level: LevelData): GameMap {
  const { width, height } = level;
  if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1) {
    throw new LevelFormatError(`bad map size ${width}x${height}`);
  }
  if (level.terrain.length !== height) {
    throw new LevelFormatError(`terrain has ${level.terrain.length} rows, expected ${height}`);
  }

  const tiles: TerrainId[] = new Array(width * height).fill(DEFAULT_TERRAIN);
  level.terrain.forEach((row, y) => {
    if (row.length !== width) {
      throw new LevelFormatError(`terrain row ${y} has ${row.length} chars, expected ${width}`);
    }
    for (let x = 0; x < width; x++) {
      const ch = row[x];
      const id = TERRAIN_CHARS[ch];
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
  };

  for (const o of level.owners ?? []) {
    if (o.x < 0 || o.y < 0 || o.x >= width || o.y >= height) {
      throw new LevelFormatError(`owner entry out of bounds at ${o.x},${o.y}`);
    }
    const i = idx(map, o.x, o.y);
    if (!Terrains.get(map.tiles[i]).capturable) {
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
      row += CHAR_OF_TERRAIN[map.tiles[y * map.width + x]] ?? '.';
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

/** Structural + playability lint. The editor surfaces this live. */
export function validateLevel(level: LevelData): LevelIssue[] {
  const issues: LevelIssue[] = [];
  const err = (message: string) => issues.push({ severity: 'error', message });
  const warn = (message: string) => issues.push({ severity: 'warning', message });

  let map: GameMap | null = null;
  try {
    map = mapFromLevel(level);
  } catch (e) {
    err((e as Error).message);
  }

  if (!level.id?.trim()) err('关卡缺少 id');
  if (!level.name?.trim()) warn('关卡缺少名称');

  const ids = new Set<number>();
  for (const p of level.players) {
    if (p.id < 1) err(`玩家 id 必须 >= 1（发现 ${p.id}）`);
    if (ids.has(p.id)) err(`玩家 id 重复：${p.id}`);
    ids.add(p.id);
  }
  if (level.players.length < 2) err('至少需要两名玩家');
  if (!level.players.some((p) => p.controller === 'human')) warn('没有任何人类玩家');

  const seen = new Set<number>();
  for (const u of level.units) {
    if (!UnitTypes.has(u.unit)) err(`未知兵种 "${u.unit}"`);
    if (!ids.has(u.owner)) err(`单位 ${u.unit}@${u.x},${u.y} 的归属玩家 ${u.owner} 不存在`);
    if (map) {
      if (u.x < 0 || u.y < 0 || u.x >= map.width || u.y >= map.height) {
        err(`单位越界：${u.x},${u.y}`);
        continue;
      }
      const key = u.y * map.width + u.x;
      if (seen.has(key)) err(`格子 ${u.x},${u.y} 上有多个单位`);
      seen.add(key);
      const t = Terrains.get(map.tiles[key]);
      const def = UnitTypes.tryGet(u.unit);
      if (def && t.cost[def.movementClass] === null) {
        err(`${def.name} 无法站在 ${t.name} 上（${u.x},${u.y}）`);
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
        (owner, i) => owner === p.id && Terrains.get(map!.tiles[i]).produces.length > 0,
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
    schema: 1,
    id: 'untitled',
    name: '未命名关卡',
    description: '',
    width,
    height,
    terrain: new Array(height).fill(row),
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
  return { id, name, team: id, color, controller, funds: 0, ai: { aggression: 0.5 } };
}

export const DEFAULT_VICTORY: Objective[] = [{ type: 'routEnemies' }, { type: 'captureHQ' }];

/** Normalise a loaded blob into a fully-populated LevelData. */
export function normaliseLevel(raw: unknown): LevelData {
  if (typeof raw !== 'object' || raw === null) throw new LevelFormatError('关卡数据不是对象');
  const o = raw as Partial<LevelData>;
  if (o.schema !== 1) throw new LevelFormatError(`不支持的 schema：${String(o.schema)}`);
  if (!Array.isArray(o.terrain)) throw new LevelFormatError('缺少 terrain');
  const height = o.height ?? o.terrain.length;
  const width = o.width ?? (o.terrain[0]?.length ?? 0);
  return {
    schema: 1,
    id: o.id ?? 'untitled',
    name: o.name ?? '未命名关卡',
    author: o.author,
    description: o.description ?? '',
    width,
    height,
    terrain: o.terrain,
    owners: o.owners ?? [],
    units: o.units ?? [],
    players: o.players ?? emptyLevel().players,
    rules: o.rules ?? {},
    victory: o.victory ?? DEFAULT_VICTORY,
    extra: o.extra,
  };
}

export const resolveRules = (level: LevelData) => ({ ...DEFAULT_RULES, ...(level.rules ?? {}) });
