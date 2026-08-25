/**
 * The last things worth telling the player, in the order they happened.
 *
 * Three lines of policy that lived among the battle controller's twenty-one
 * fields: an empty line is not an event, and the list is bounded because a long
 * battle produces thousands of them and the screen shows six. Small, but it is
 * *policy* — the controller reading `messages.length > 60` beside its selection
 * state and its animation bookkeeping is how a bound becomes a magic number
 * somebody later "cleans up".
 */
export class BattleLog {
  private readonly lines: string[] = [];

  /** How many lines are kept. Beyond this the oldest are dropped. */
  constructor(private readonly depth = 60) {}

  /** Nothing to say is not an event. */
  add(line: string): void {
    if (!line) return;
    this.lines.push(line);
    if (this.lines.length > this.depth) this.lines.splice(0, this.lines.length - this.depth);
  }

  clear(): void {
    this.lines.length = 0;
  }

  /** What the screen reads. A copy, so the screen cannot write the log. */
  recent(): string[] {
    return [...this.lines];
  }
}
