import type { CommanderState, PlayerId } from '../types';

/**
 * Rich runtime façade over a commander record.
 *
 * The last thing in `GameState` that had no entity, which is exactly why its
 * two lifecycle rules were written wherever they happened to be needed: the
 * per-turn tactic reset lived in the turn grant loop, and "a commander's record
 * follows its unit's side" lived in the module that puts a fallen unit back on
 * the field. Neither is about where it was written.
 */
export class CommanderEntity {
  constructor(readonly state: CommanderState) {}

  get id(): string {
    return this.state.id;
  }

  /** A new turn returns every tactic this commander spent last turn. */
  refreshTactics(): void {
    this.state.usedTactics = [];
  }

  /**
   * Whose commander this is. A record whose side disagreed with its unit's kept
   * granting its aura to the army that no longer had it.
   */
  changeOwner(owner: PlayerId): PlayerId {
    const previous = this.state.owner;
    this.state.owner = owner;
    return previous;
  }
}
