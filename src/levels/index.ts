import { normaliseLevel } from '../core/mapio';
import type { LevelData } from '../core/types';
import twinHills from './01-twin-hills.json';
import threeBridges from './02-three-bridges.json';
import siege from './03-siege.json';

/**
 * Built-in campaign. These files are plain editor documents — open any of them
 * in the map editor (载入 → 内置关卡) to tweak or fork them.
 */
export const BUILTIN_LEVELS: LevelData[] = [twinHills, threeBridges, siege].map((raw) =>
  normaliseLevel(raw),
);

export const CUSTOM_LEVELS_KEY = 'empire.customLevels';
export const PLAYTEST_KEY = 'empire.playtest';

export interface StoredLevel {
  level: LevelData;
  savedAt: number;
}

export function loadCustomLevels(): StoredLevel[] {
  try {
    const raw = localStorage.getItem(CUSTOM_LEVELS_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as StoredLevel[];
    return parsed
      .map((s) => ({ savedAt: s.savedAt ?? 0, level: normaliseLevel(s.level) }))
      .sort((a, b) => b.savedAt - a.savedAt);
  } catch {
    return [];
  }
}

export function saveCustomLevel(level: LevelData): void {
  const all = loadCustomLevels().filter((s) => s.level.id !== level.id);
  all.unshift({ level, savedAt: Date.now() });
  localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify(all.slice(0, 40)));
}

export function deleteCustomLevel(id: string): void {
  const all = loadCustomLevels().filter((s) => s.level.id !== id);
  localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify(all));
}

/** Hand-off slot used by the editor's "试玩" button. */
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
