/**
 * Undo and redo over serialised document snapshots.
 *
 * Two arrays and a cap, which is exactly why it was worth taking out of the app:
 * the invariant that a fresh edit discards the redo branch was written inline
 * next to the cap, next to the draft autosave, next to the document swap — four
 * unrelated concerns in one three-line method.
 *
 * Snapshots stay opaque strings: history does not need to know what a document
 * is, only that one can be written down and read back.
 */
export class EditorHistory {
  private readonly past: string[] = [];
  private readonly future: string[] = [];

  constructor(private readonly limit = 80) {}

  get canUndo(): boolean {
    return this.past.length > 0;
  }

  get canRedo(): boolean {
    return this.future.length > 0;
  }

  /** Opens a new edit. Anything undone is no longer reachable from here. */
  record(snapshot: string): void {
    this.past.push(snapshot);
    if (this.past.length > this.limit) this.past.shift();
    this.future.length = 0;
  }

  /** The state to restore, given the state being left behind. */
  undo(current: string): string | null {
    const previous = this.past.pop();
    if (previous === undefined) return null;
    this.future.push(current);
    return previous;
  }

  redo(current: string): string | null {
    const next = this.future.pop();
    if (next === undefined) return null;
    this.past.push(current);
    return next;
  }
}
