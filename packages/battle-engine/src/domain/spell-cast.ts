import type { Coord, PendingCast } from '../types';
import { DomainInvariantError } from './errors';

/**
 * Rich runtime façade over one charging strike.
 *
 * The data object stays the save/replay format; every derived reading a caller
 * might want — how long is left, how far along, has it landed — comes from here,
 * so the HUD, the AI and the resolver cannot disagree about the arithmetic.
 */
export class SpellCastEntity {
  constructor(readonly state: PendingCast) {
    if (state.resolveAt <= state.declaredAt) {
      throw new DomainInvariantError(
        `a cast must resolve after it is declared, got ${state.declaredAt} -> ${state.resolveAt}`,
      );
    }
  }

  get caster(): number {
    return this.state.caster;
  }

  get target(): Coord {
    return this.state.target;
  }

  /** Actor turns the cast takes in total. */
  get duration(): number {
    return this.state.resolveAt - this.state.declaredAt;
  }

  /** Actor turns still to wait; zero once it is due. */
  remainingAt(actorTurns: number): number {
    return Math.max(0, this.state.resolveAt - actorTurns);
  }

  isDueAt(actorTurns: number): boolean {
    return actorTurns >= this.state.resolveAt;
  }

  /** Charge completion 0..1, for a progress readout. */
  progressAt(actorTurns: number): number {
    const elapsed = actorTurns - this.state.declaredAt;
    return Math.min(1, Math.max(0, elapsed / this.duration));
  }
}
