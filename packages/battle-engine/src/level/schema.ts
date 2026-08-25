import { StoredDocumentError } from '../domain/errors';
import type { LevelData } from '../types';
import { defaultPlayers, defaultVictory } from './defaults';

/** The level document is malformed — not merely questionable, unreadable. */
/** A level document this build cannot read; one kind of `StoredDocumentError`. */
export class LevelFormatError extends StoredDocumentError {}

export const CURRENT_LEVEL_SCHEMA = 2;

/** Normalise a loaded blob into a fully-populated LevelData. */
export function normaliseLevel(raw: unknown): LevelData {
  if (typeof raw !== 'object' || raw === null) throw new LevelFormatError('关卡数据不是对象');
  const loaded = structuredClone(raw) as Partial<LevelData>;
  if (loaded.schema !== CURRENT_LEVEL_SCHEMA) {
    throw new LevelFormatError(`关卡 schema 必须是 ${CURRENT_LEVEL_SCHEMA}，收到 ${String(loaded.schema)}`);
  }
  if (!Array.isArray(loaded.terrain)) throw new LevelFormatError('缺少 terrain');
  const height = loaded.height ?? loaded.terrain.length;
  const width = loaded.width ?? (loaded.terrain[0]?.length ?? 0);
  const players = loaded.players ?? defaultPlayers();
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
    victory: loaded.victory ?? defaultVictory(),
    scenario: loaded.scenario,
    deployment: loaded.deployment,
    extra: loaded.extra,
  };
}
