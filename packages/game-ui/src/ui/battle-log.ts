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
  private readonly entries: { line: string; times: number }[] = [];

  /** How many lines are kept. Beyond this the oldest are dropped. */
  constructor(private readonly depth = 60) {}

  /**
   * Nothing to say is not an event, and saying it again is not a second one.
   *
   * The screen shows six lines. Turning four units to face the same way filled all
   * six with `剑士 调整了朝向` — the log was technically complete and told the
   * player nothing, because the four things that *did* happen had scrolled off the
   * top of it. A run of the same line is one line with a count, which is what the
   * player would have written down.
   *
   * Consecutive only: `A 阵亡 / B 阵亡 / A 阵亡` is three things happening, and the
   * order they happened in is the whole point of a log.
   */
  add(line: string): void {
    if (!line) return;
    const last = this.entries[this.entries.length - 1];
    if (last && last.line === line) {
      last.times += 1;
      return;
    }
    this.entries.push({ line, times: 1 });
    if (this.entries.length > this.depth) this.entries.splice(0, this.entries.length - this.depth);
  }

  clear(): void {
    this.entries.length = 0;
  }

  /** What the screen reads. A copy, so the screen cannot write the log. */
  recent(): string[] {
    return this.entries.map(({ line, times }) => (times > 1 ? `${line} ×${times}` : line));
  }
}
