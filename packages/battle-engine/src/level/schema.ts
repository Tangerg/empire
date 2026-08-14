import { StoredDocumentError } from '../domain/errors';
import { SchemaMigrator } from '../save-schema';
import type { LevelData } from '../types';
import { DEFAULT_VICTORY, emptyLevel } from './defaults';

/** The level document is malformed — not merely questionable, unreadable. */
/** A level document this build cannot read; one kind of `StoredDocumentError`. */
export class LevelFormatError extends StoredDocumentError {}

export const CURRENT_LEVEL_SCHEMA = 2;

/**
 * Versioned upgrade path for stored levels.
 *
 * A schema bump used to be a hard break: `normaliseLevel` rejected anything
 * older, so every level a player had saved became unreadable. Registering the
 * step instead keeps old content loadable and gives future bumps a defined
 * place to live — on the same ladder the campaign and battle saves climb, which
 * is what refuses a missing step and a migration that fails to advance. This
 * module used to walk the versions itself, and its loop did neither.
 */
const LEVEL_SCHEMA = new SchemaMigrator<LevelData & { schema: number }>('关卡', CURRENT_LEVEL_SCHEMA)
  .register(1, (raw) => {
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
  });

/** Applies the registered upgrade path, or says why it cannot. */
export function migrateLevel(raw: unknown): unknown {
  try {
    return LEVEL_SCHEMA.load(raw);
  } catch (error) {
    // The ladder reports in engine terms; a level document is authored, so the
    // refusal is restated in the author's.
    throw new LevelFormatError(`关卡 schema 无法升级到 ${CURRENT_LEVEL_SCHEMA}：${(error as Error).message}`);
  }
}

/** Normalise a loaded blob into a fully-populated LevelData. */
export function normaliseLevel(raw: unknown): LevelData {
  if (typeof raw !== 'object' || raw === null) throw new LevelFormatError('关卡数据不是对象');
  const loaded = migrateLevel(raw) as Partial<LevelData>;
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
