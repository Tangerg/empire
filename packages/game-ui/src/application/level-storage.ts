import { normaliseLevel } from '@empire/battle-engine/mapio';
import type { LevelData } from '@empire/battle-engine/types';

export const CUSTOM_LEVELS_KEY = 'empire.customLevels';
export const PLAYTEST_KEY = 'empire.playtest';

export interface StoredLevel {
  level: LevelData;
  savedAt: number;
}

export interface LoadedCustomLevels {
  levels: StoredLevel[];
  /** Entries that could not be read, e.g. an unsupported schema. */
  rejected: { id: string; reason: string }[];
}

/**
 * Browser persistence adapter. The battle engine never imports Web Storage.
 *
 * Failure granularity is one entry, not the whole collection: a single level the
 * current schema cannot read used to make every saved level disappear behind an
 * innocent-looking empty state.
 */
export function readCustomLevels(): LoadedCustomLevels {
  const levels: StoredLevel[] = [];
  const rejected: { id: string; reason: string }[] = [];
  let parsed: StoredLevel[];
  try {
    const raw = localStorage.getItem(CUSTOM_LEVELS_KEY);
    if (!raw) return { levels, rejected };
    const value = JSON.parse(raw);
    parsed = Array.isArray(value) ? (value as StoredLevel[]) : [];
  } catch (error) {
    return { levels, rejected: [{ id: '*', reason: (error as Error).message }] };
  }

  for (const stored of parsed) {
    const id = String((stored?.level as { id?: unknown } | undefined)?.id ?? '未命名');
    try {
      levels.push({ savedAt: stored.savedAt ?? 0, level: normaliseLevel(stored.level) });
    } catch (error) {
      rejected.push({ id, reason: (error as Error).message });
    }
  }
  levels.sort((left, right) => right.savedAt - left.savedAt);
  return { levels, rejected };
}

export function loadCustomLevels(): StoredLevel[] {
  return readCustomLevels().levels;
}

export function saveCustomLevel(level: LevelData): void {
  // Unreadable entries are kept verbatim rather than dropped on the next save:
  // a future migration may still recover them.
  const raw = localStorage.getItem(CUSTOM_LEVELS_KEY);
  let existing: StoredLevel[] = [];
  try {
    const value = raw ? JSON.parse(raw) : [];
    existing = Array.isArray(value) ? value : [];
  } catch {
    existing = [];
  }
  const kept = existing.filter((stored) => (stored?.level as { id?: unknown })?.id !== level.id);
  localStorage.setItem(
    CUSTOM_LEVELS_KEY,
    JSON.stringify([{ level, savedAt: Date.now() }, ...kept].slice(0, 40)),
  );
}

export function deleteCustomLevel(id: string): void {
  const all = loadCustomLevels().filter((stored) => stored.level.id !== id);
  localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify(all));
}

/** Hand-off slot used by the editor's playtest command. */
export function stashPlaytest(level: LevelData): void {
  sessionStorage.setItem(PLAYTEST_KEY, JSON.stringify(level));
}

export function takePlaytest(): LevelData | null {
  const raw = sessionStorage.getItem(PLAYTEST_KEY);
  if (!raw) return null;
  sessionStorage.removeItem(PLAYTEST_KEY);
  try {
    return normaliseLevel(JSON.parse(raw));
  } catch {
    return null;
  }
}
