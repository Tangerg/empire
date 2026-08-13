import type { LevelData } from '../types';
import { DEFAULT_VICTORY, emptyLevel } from './defaults';

/** The level document is malformed — not merely questionable, unreadable. */
export class LevelFormatError extends Error {}

export const CURRENT_LEVEL_SCHEMA = 2;

/** One step of an upgrade path between adjacent level schema versions. */
export interface LevelMigration {
  readonly from: number;
  readonly to: number;
  migrate(raw: Record<string, unknown>): Record<string, unknown>;
}

/**
 * Versioned upgrade path for stored levels.
 *
 * A schema bump used to be a hard break: `normaliseLevel` rejected anything
 * older, so every level a player had saved became unreadable. Registering the
 * step instead keeps old content loadable and gives future bumps a defined
 * place to live.
 */
export const LevelMigrations: LevelMigration[] = [
  {
    from: 1,
    to: 2,
    migrate: (raw) => {
      const players = Array.isArray(raw.players) ? raw.players : [];
      const rules = (raw.rules ?? {}) as Record<string, unknown>;
      const baseIncome = Number(rules.baseIncome ?? 0);
      const incomeOverride = rules.incomeOverride;
      const migratedRules: Record<string, unknown> = { ...rules };
      delete migratedRules.baseIncome;
      delete migratedRules.incomeOverride;
      if (baseIncome > 0) {
        migratedRules.baseResourceGrants = [{ resource: 'funds', amount: baseIncome }];
      }
      if (typeof incomeOverride === 'number') {
        migratedRules.siteResourceOverrides = { funds: incomeOverride };
      }
      return {
        ...raw,
        schema: 2,
        rules: migratedRules,
        players: players.map((entry) => {
          const player = entry as Record<string, unknown>;
          if (player.resources) return player;
          const funds = Number(player.funds ?? 0);
          const migrated: Record<string, unknown> = {
            ...player,
            resources: { funds: { current: funds, capacity: null } },
          };
          delete migrated.funds;
          return migrated;
        }),
      };
    },
  },
];

/** Applies the registered upgrade path until the level reaches the current schema. */
export function migrateLevel(raw: unknown): unknown {
  if (typeof raw !== 'object' || raw === null) return raw;
  let current = raw as Record<string, unknown>;
  for (let step = 0; step <= LevelMigrations.length; step++) {
    const schema = Number(current.schema);
    if (schema === CURRENT_LEVEL_SCHEMA) return current;
    const migration = LevelMigrations.find((candidate) => candidate.from === schema);
    if (!migration) return current;
    current = migration.migrate(current);
  }
  return current;
}

/** Normalise a loaded blob into a fully-populated LevelData. */
export function normaliseLevel(raw: unknown): LevelData {
  if (typeof raw !== 'object' || raw === null) throw new LevelFormatError('关卡数据不是对象');
  const loaded = migrateLevel(raw) as Partial<LevelData>;
  if (loaded.schema !== CURRENT_LEVEL_SCHEMA) {
    throw new LevelFormatError(
      `不支持的 schema：${String((raw as Partial<LevelData>).schema)}；当前需要 schema ${CURRENT_LEVEL_SCHEMA}，且没有可用的升级路径`,
    );
  }
  if (!Array.isArray(loaded.terrain)) throw new LevelFormatError('缺少 terrain');
  const height = loaded.height ?? loaded.terrain.length;
  const width = loaded.width ?? (loaded.terrain[0]?.length ?? 0);
  const players = loaded.players ?? emptyLevel().players;
  for (const candidate of players) {
    if (!candidate.resources || typeof candidate.resources !== 'object') {
      throw new LevelFormatError(
        `玩家 ${candidate.id ?? '?'} 缺少 resources；旧资金/指挥点字段已停止支持`,
      );
    }
  }
  return {
    schema: 2,
    id: loaded.id ?? 'untitled',
    name: loaded.name ?? '未命名关卡',
    author: loaded.author,
    description: loaded.description ?? '',
    width,
    height,
    terrain: loaded.terrain,
    elevation: Array.isArray(loaded.elevation) ? loaded.elevation.map(Number) : new Array(width * height).fill(0),
    cliffs: Array.isArray(loaded.cliffs) ? loaded.cliffs : [],
    directionalCover: Array.isArray(loaded.directionalCover) ? loaded.directionalCover : [],
    owners: loaded.owners ?? [],
    units: loaded.units ?? [],
    commanders: loaded.commanders ?? [],
    structures: loaded.structures ?? [],
    composites: loaded.composites ?? [],
    players,
    rules: loaded.rules ?? {},
    victory: loaded.victory ?? DEFAULT_VICTORY,
    scenario: loaded.scenario,
    deployment: loaded.deployment,
    extra: loaded.extra,
  };
}
