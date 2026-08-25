import { normaliseLevel, StoredDocumentError, type LevelData } from '@empire/battle-engine';

export const CUSTOM_LEVELS_KEY = 'empire.customLevels';
const PLAYTEST_KEY = 'empire.playtest';

export interface StoredLevel {
  level: LevelData;
  savedAt: number;
}

export interface LoadedCustomLevels {
  levels: StoredLevel[];
  /** Entries that could not be read, e.g. an unsupported schema. */
  rejected: { id: string; reason: string }[];
}

/** What the slot holds, before anybody tries to read a level out of it. */
type StoredSlot =
  | { readonly entries: readonly unknown[] }
  | { readonly unreadable: string };

const idOf = (stored: unknown): unknown => (stored as { level?: { id?: unknown } })?.level?.id;

/**
 * The slot's entries exactly as stored, read in one place.
 *
 * Three functions used to parse this blob with three different failure
 * policies. The reader reported a bad blob per entry; the writer answered
 * `existing = []` and then wrote — so one corrupt byte plus one save erased
 * every level the author had, directly under a comment promising that unreadable
 * entries are kept verbatim. Deleting one level did the same thing by going
 * through the *parsed* list, which is the readable subset by construction.
 *
 * An unreadable slot has no safe rewrite, so it is reported as such and the
 * writers refuse rather than replacing what they could not read.
 */
function storedSlot(): StoredSlot {
  const raw = localStorage.getItem(CUSTOM_LEVELS_KEY);
  if (!raw) return { entries: [] };
  try {
    const value: unknown = JSON.parse(raw);
    return Array.isArray(value)
      ? { entries: value }
      : { unreadable: '自定义关卡存储必须是数组' };
  } catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
    return { unreadable: error.message };
  }
}

/** Refuses rather than overwriting a slot whose contents could not be read. */
function writableEntries(): readonly unknown[] {
  const slot = storedSlot();
  if ('unreadable' in slot) {
    throw new StoredDocumentError(`自定义关卡存储无法解析，未覆盖：${slot.unreadable}`);
  }
  return slot.entries;
}

/**
 * Browser persistence adapter. The battle engine never imports Web Storage.
 *
 * Failure granularity is one entry, not the whole collection: a single level the
 * current schema cannot read used to make every saved level disappear behind an
 * innocent-looking empty state.
 */
export function readCustomLevels(): LoadedCustomLevels {
  const slot = storedSlot();
  if ('unreadable' in slot) return { levels: [], rejected: [{ id: '*', reason: slot.unreadable }] };

  const levels: StoredLevel[] = [];
  const rejected: { id: string; reason: string }[] = [];
  for (const stored of slot.entries as StoredLevel[]) {
    const id = String(idOf(stored) ?? '未命名');
    try {
      levels.push({ savedAt: stored.savedAt ?? 0, level: normaliseLevel(stored.level) });
    } catch (error) {
      if (!(error instanceof StoredDocumentError)) throw error;
      rejected.push({ id, reason: error.message });
    }
  }
  levels.sort((left, right) => right.savedAt - left.savedAt);
  return { levels, rejected };
}

export function loadCustomLevels(): StoredLevel[] {
  return readCustomLevels().levels;
}

/**
 * Writes one level, keeping every other entry exactly as it was stored.
 *
 * Verbatim on purpose: an entry this schema cannot read may still be manually
 * recoverable. Rewriting the slot from only the parsed levels would destroy
 * unrelated stored bytes merely because this version refused to interpret them.
 */
export function saveCustomLevel(level: LevelData): void {
  const kept = writableEntries().filter((stored) => idOf(stored) !== level.id);
  localStorage.setItem(
    CUSTOM_LEVELS_KEY,
    JSON.stringify([{ level, savedAt: Date.now() }, ...kept].slice(0, 40)),
  );
}

export function deleteCustomLevel(id: string): void {
  const kept = writableEntries().filter((stored) => idOf(stored) !== id);
  localStorage.setItem(CUSTOM_LEVELS_KEY, JSON.stringify(kept));
}

/** Hand-off slot used by the editor's playtest command. */
export function stashPlaytest(level: LevelData): void {
  sessionStorage.setItem(PLAYTEST_KEY, JSON.stringify(level));
}

export interface LoadedPlaytest {
  readonly level: LevelData | null;
  /** Why the hand-off from the editor was refused; null when there was none. */
  readonly rejected: string | null;
}

export function takePlaytest(): LoadedPlaytest {
  const raw = sessionStorage.getItem(PLAYTEST_KEY);
  if (!raw) return { level: null, rejected: null };
  sessionStorage.removeItem(PLAYTEST_KEY);
  let document: unknown;
  try {
    document = JSON.parse(raw);
  } catch (error) {
    if (!(error instanceof SyntaxError)) throw error;
    return { level: null, rejected: error.message };
  }
  try {
    return { level: normaliseLevel(document), rejected: null };
  } catch (error) {
    // Silently dropping the player back to the menu made a level the editor
    // considered valid look like a click that did nothing. Only unreadable
    // documents belong here; a normaliser defect must remain visible.
    if (!(error instanceof StoredDocumentError)) throw error;
    return { level: null, rejected: error.message };
  }
}
