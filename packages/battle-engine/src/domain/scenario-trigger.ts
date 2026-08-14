import type { GameState, PlayerId, ScenarioState, ScenarioTrigger } from '../types';

export type ScenarioTiming = ScenarioTrigger['timing'];

/**
 * One moment a trigger sweep can happen in: this player's turn-start of this
 * round, this turn-end, the gap after this action.
 *
 * A repeating trigger fires at most once per occurrence, so the moment needs an
 * identity. It used to be an interpolated string built in the sweep and compared
 * against a string stored in the state, with the format written in two places
 * and no name in either.
 */
export class TriggerOccurrence {
  constructor(
    readonly turn: number,
    readonly player: PlayerId,
    readonly timing: ScenarioTiming,
  ) {}

  static of(state: GameState, timing: ScenarioTiming): TriggerOccurrence {
    return new TriggerOccurrence(state.turn, state.currentPlayer, timing);
  }

  /** Stable identity, stored in the trigger ledger. */
  get key(): string {
    return `${this.turn}:${this.player}:${this.timing}`;
  }
}

/** What a trigger's `repeat` block says, asked instead of unpacked. */
export class TriggerSchedule {
  constructor(private readonly repeat: NonNullable<ScenarioTrigger['repeat']>) {}

  /**
   * Why this cadence cannot be honoured, in the author's terms.
   *
   * The level linter used to state these four rules itself, which left the
   * runtime and the linter each holding their own copy of "a cadence is a whole
   * number of rounds" — and disagreeing about the consequence: the linter
   * refused the level, the runtime silently never fired. The linter supplies the
   * trigger's name; the schedule supplies the fault.
   */
  get faults(): string[] {
    const found: string[] = [];
    if (this.cadence === null) found.push('循环间隔必须是正整数');
    if (this.repeat.startTurn !== undefined && this.repeat.startTurn < 1) {
      found.push('起始回合必须 >= 1');
    }
    if (this.repeat.endTurn !== undefined && this.repeat.startTurn !== undefined &&
      this.repeat.endTurn < this.repeat.startTurn) {
      found.push('结束回合早于起始回合');
    }
    if (this.repeat.maxFirings !== undefined &&
      (!Number.isInteger(this.repeat.maxFirings) || this.repeat.maxFirings < 1)) {
      found.push('最大触发次数必须是正整数');
    }
    return found;
  }

  /** A cadence has to be a whole number of rounds; anything else never fires. */
  private get cadence(): number | null {
    const { everyRounds } = this.repeat;
    return Number.isInteger(everyRounds) && everyRounds >= 1 ? everyRounds : null;
  }

  dueOn(turn: number): boolean {
    const cadence = this.cadence;
    if (cadence === null) return false;
    const start = this.repeat.startTurn ?? 1;
    if (turn < start) return false;
    if (this.repeat.endTurn !== undefined && turn > this.repeat.endTurn) return false;
    return (turn - start) % cadence === 0;
  }

  spent(firings: number): boolean {
    return this.repeat.maxFirings !== undefined && firings >= this.repeat.maxFirings;
  }
}

/** The cadence a trigger declares, or null when it is one-shot. */
export const scheduleOf = (trigger: ScenarioTrigger): TriggerSchedule | null =>
  trigger.repeat ? new TriggerSchedule(trigger.repeat) : null;

/** How often a trigger has gone off, and when it last did. */
interface TriggerLedgerEntry {
  count: number;
  lastOccurrence: string;
}

/**
 * One trigger of one battle: its declaration plus the bookkeeping that decides
 * whether it may go off again.
 *
 * The sweep used to hold all of it — six inline conditions for a repeat block, a
 * one-shot ledger it kept a private `Set` copy of, and the same defaulted
 * `{ count, lastOccurrence }` read written out twice, once to test and once to
 * update. Whether a trigger is due is the trigger's own question.
 */
export class ScenarioTriggerEntity {
  private readonly schedule: TriggerSchedule | null;

  constructor(
    private readonly scenario: ScenarioState,
    readonly declaration: ScenarioTrigger,
  ) {
    this.schedule = scheduleOf(declaration);
  }

  get id(): string {
    return this.declaration.id;
  }

  /** An omitted `repeat` block means one-shot, for the whole battle. */
  get repeats(): boolean {
    return this.schedule !== null;
  }

  /**
   * Everything except the condition: right timing, still allowed to fire, and
   * not already spent for this battle or this occurrence.
   */
  dueAt(occurrence: TriggerOccurrence): boolean {
    if (this.declaration.timing !== occurrence.timing) return false;
    if (!this.schedule) return !this.scenario.firedTriggerIds.includes(this.id);
    const ledger = this.ledger;
    return this.schedule.dueOn(occurrence.turn) &&
      !this.schedule.spent(ledger.count) &&
      ledger.lastOccurrence !== occurrence.key;
  }

  /** Records a firing, in whichever ledger this trigger keeps. */
  recordFiring(occurrence: TriggerOccurrence): void {
    if (!this.schedule) {
      this.scenario.firedTriggerIds.push(this.id);
      return;
    }
    const ledger = this.ledger;
    this.scenario.triggerRuntime[this.id] = {
      count: ledger.count + 1,
      lastOccurrence: occurrence.key,
    };
  }

  private get ledger(): TriggerLedgerEntry {
    return this.scenario.triggerRuntime[this.id] ?? { count: 0, lastOccurrence: '' };
  }
}
