import type { LevelData } from '@empire/battle-engine';

/**
 * Moving a level document across the browser's edge: to a file, to the clipboard,
 * back from a file.
 *
 * These three lived inside the editor's controller, next to its stroke handling
 * and its render passes — a `Blob`, an `URL.createObjectURL`, a synthetic click,
 * a `navigator.clipboard`, an `<input type="file">`. Every other place this
 * repository touches the browser's edge is in this folder, with the refusals
 * spelled out, and the editor's copy was the one exception.
 *
 * Reading is a callback rather than a promise on purpose: a file picker that the
 * person closes fires no event at all, so a promise for the text would never
 * settle and never reject.
 */

const asJson = (level: LevelData): string => JSON.stringify(level, null, 2);

/** Offers the document as a download named after the level. */
export function downloadLevel(level: LevelData): void {
  const blob = new Blob([asJson(level)], { type: 'application/json' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = `${level.id || 'level'}.json`;
  link.click();
  URL.revokeObjectURL(link.href);
}

/** Puts the document on the clipboard, or throws what the browser refused with. */
export async function copyLevelJson(level: LevelData): Promise<void> {
  await navigator.clipboard.writeText(asJson(level));
}

/**
 * Asks for a JSON file and hands over its text.
 *
 * `read` may throw — a chosen file need not contain a level — and the caller is
 * the one that knows how to say so, so nothing is caught here.
 */
export function pickJsonFile(read: (text: string) => void): void {
  const input = document.createElement('input');
  input.type = 'file';
  input.accept = '.json,application/json';
  input.onchange = async () => {
    const file = input.files?.[0];
    if (file) read(await file.text());
  };
  input.click();
}
