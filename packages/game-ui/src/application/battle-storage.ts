import type { BattleSave } from '@empire/battle-engine';
import type { BattleSaveStore } from '../ui/game';

const keyFor = (levelId: string): string => `empire:battle:${levelId}`;

/**
 * One browser slot per level.
 *
 * Per level rather than one global slot: putting down a chapter to try another
 * and finding the first one gone is not a save system. The adapter deliberately
 * stores the document verbatim and never inspects it — deciding whether a save
 * is usable belongs to the ruleset that would have to run it, which is why the
 * store hands the raw value back and lets the session refuse it.
 */
export function browserBattleSaves(levelId: string): BattleSaveStore {
  const key = keyFor(levelId);
  return {
    write: (save: BattleSave) => localStorage.setItem(key, JSON.stringify(save)),
    read: () => {
      const raw = localStorage.getItem(key);
      if (!raw) return null;
      // A slot holding unparseable text is reported as a refusal, not as an
      // empty slot: "you never saved" is the one thing it certainly is not.
      return JSON.parse(raw) as unknown;
    },
    has: () => localStorage.getItem(key) !== null,
  };
}

