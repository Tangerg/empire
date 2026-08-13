import { IllegalActionError } from './action-system';
import type { AiOptions } from './ai';
import type { BattleEngine } from './engine';
import type { MoveField } from './movement';
import { unitById } from './state';
import type { Action, Coord, GameEvent, GameState, LevelData, PlayerId, Unit, WeaponId } from './types';

export type SessionListener = (events: GameEvent[], state: GameState) => void;

/**
 * Thin stateful shell around the pure reducer: undo snapshots, memoised move
 * fields, and a listener hook. The UI talks only to this; the engine below it
 * stays free of any notion of selection or presentation.
 */
export class GameSession {
  state: GameState;
  readonly level: LevelData;
  readonly log: GameEvent[] = [];

  private undoStack: Array<{ state: GameState; logLength: number }> = [];
  private listeners = new Set<SessionListener>();
  private fieldCache = new Map<number, { field: MoveField; stamp: number }>();
  private stamp = 0;

  constructor(
    level: LevelData,
    /** The composed ruleset this battle runs on; never an ambient default. */
    readonly engine: BattleEngine,
  ) {
    this.level = level;
    this.state = engine.createState(level);
  }

  /** Ruleset of this battle; the UI renders from here, not from ambient state. */
  get content() {
    return this.engine.content;
  }

  get rules() {
    return this.engine.rules;
  }

  /* --------------------------------------------------------- subscriptions */

  subscribe(fn: SessionListener): () => void {
    this.listeners.add(fn);
    return () => this.listeners.delete(fn);
  }

  private notify(events: GameEvent[]): void {
    this.log.push(...events);
    for (const fn of this.listeners) fn(events, this.state);
  }

  /* ---------------------------------------------------------------- queries */

  moveField(unit: Unit): MoveField {
    const hit = this.fieldCache.get(unit.id);
    if (hit && hit.stamp === this.stamp) return hit.field;
    const field = this.engine.moveField(this.state, unit);
    this.fieldCache.set(unit.id, { field, stamp: this.stamp });
    return field;
  }

  pathTo(unit: Unit, to: Coord): Coord[] | null {
    return this.engine.pathTo(this.moveField(unit), this.state, to);
  }

  commandsAt(unit: Unit, at: Coord) {
    return this.engine.commandsAt(this.state, unit, at);
  }

  careerOptions(unit: Unit) {
    return this.engine.careerOptions(this.state, unit);
  }

  threatOf(unit: Unit): Set<number> {
    return this.engine.threatOf(this.state, unit, this.moveField(unit));
  }

  visibleTiles(viewer: PlayerId): Set<number> {
    return this.engine.visibleTiles(this.state, viewer);
  }

  isUnitVisible(viewer: PlayerId, unit: Unit, seen?: Set<number>): boolean {
    return this.engine.isUnitVisible(this.state, viewer, unit, seen);
  }

  visibleUnits(viewer: PlayerId): Unit[] {
    return this.engine.visibleUnits(this.state, viewer);
  }

  forecast(attacker: Unit, defender: Unit, at?: Coord, weapon?: WeaponId) {
    return this.engine.forecast(this.state, attacker, defender, at, weapon);
  }

  attackPlan(attacker: Unit, aimedAt: Coord, at?: Coord, weapon?: WeaponId) {
    return this.engine.attackPlan(this.state, attacker, aimedAt, at, weapon);
  }

  chooseAiAction(options?: Partial<AiOptions>): Action {
    return this.engine.chooseAiAction(this.state, options);
  }

  unit(id: number): Unit | undefined {
    return unitById(this.state, id);
  }

  /* --------------------------------------------------------------- commands */

  /** Applies an action. Returns the event list, or throws IllegalActionError. */
  dispatch(action: Action): GameEvent[] {
    const undoable = action.kind !== 'endTurn' && action.kind !== 'finishDeployment';
    const { events, before } = this.engine.dispatchWithReceipt(this.state, action);

    if (undoable) this.undoStack.push({ state: before, logLength: this.log.length });
    else this.undoStack = []; // no rewinding across a turn boundary

    this.stamp++;
    this.fieldCache.clear();
    this.notify(events);
    return events;
  }

  /** Same as dispatch but swallows illegal actions, returning null. */
  tryDispatch(action: Action): GameEvent[] | null {
    try {
      return this.dispatch(action);
    } catch (e) {
      if (e instanceof IllegalActionError) return null;
      throw e;
    }
  }

  get canUndo(): boolean {
    return this.undoStack.length > 0 && this.state.phase !== 'over';
  }

  undo(): boolean {
    const checkpoint = this.undoStack.pop();
    if (!checkpoint) return false;
    this.state = checkpoint.state;
    this.log.length = checkpoint.logLength;
    this.stamp++;
    this.fieldCache.clear();
    this.notify([]);
    return true;
  }

  restart(): void {
    this.state = this.engine.createState(this.level);
    this.undoStack = [];
    this.log.length = 0;
    this.stamp++;
    this.fieldCache.clear();
    this.notify([]);
  }
}
