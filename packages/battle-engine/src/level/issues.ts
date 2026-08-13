export interface LevelIssue {
  severity: 'error' | 'warning';
  message: string;
}

/**
 * What one inspection found, in the order it was found.
 *
 * Trivial on purpose. It exists because the two closures that used to collect
 * these — `err` and `warn`, captured from an enclosing scope — were the reason
 * thirty unrelated checks had to live inside one four-hundred-line function
 * body: nothing could be moved out without losing them.
 */
export class LevelIssueLog {
  private readonly found: LevelIssue[] = [];

  /** The level cannot be played as written. */
  error(message: string): void {
    this.found.push({ severity: 'error', message });
  }

  /** The level will run, but probably not as its author intended. */
  warn(message: string): void {
    this.found.push({ severity: 'warning', message });
  }

  get issues(): LevelIssue[] {
    return [...this.found];
  }
}
