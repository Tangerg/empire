import { normaliseLevel } from '../core/mapio';
import type { LevelData } from '../core/types';

export const CUSTOM_LEVELS_KEY = 'empire.customLevels';
export const PLAYTEST_KEY = 'empire.playtest';

export interface StoredLevel {
  level: LevelData;
  savedAt: number;
}

/** Browser persistence adapter. The battle engine never imports Web Storage. */
export function loadCustomLevels(): StoredLevel[] {
  try {
    const raw = localStorage.getItem(CUSTOM_LEVELS_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as StoredLevel[];
    return parsed
      .map((stored) => ({ savedAt: stored.savedAt ?? 0, level: normaliseLevel(stored.level) }))
      .sort((left, right) => right.savedAt - left.savedAt);
  } catch {
    return [];
  }
}

export function saveCustomLevel(level: LevelData): void {
  const all = loadCustomLevels().filter((stored) => stored.level.id !== level.id);
  all.unshift({ level, savedAt: Date.now() });
  localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify(all.slice(0, 40)));
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
