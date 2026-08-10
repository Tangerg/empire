import { applyAction, commandOptions, IllegalActionError } from './actions';
import { computeMoveField, pathTo, threatTiles, type MoveField } from './movement';
import { cloneState, createState, unitById } from './state';
import type { Action, Coord, GameEvent, GameState, LevelData, Unit } from './types';

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

  private undoStack: GameState[] = [];
  private listeners = new Set<SessionListener>();
  private fieldCache = new Map<number, { field: MoveField; stamp: number }>();
  private stamp = 0;

  constructor(level: LevelData) {
    this.level = level;
    this.state = createState(level);
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
    const field = computeMoveField(this.state, unit);
    this.fieldCache.set(unit.id, { field, stamp: this.stamp });
    return field;
  }

  pathTo(unit: Unit, to: Coord): Coord[] | null {
    return pathTo(this.moveField(unit), this.state.map, to);
  }

  commandsAt(unit: Unit, at: Coord) {
    return commandOptions(this.state, unit, at);
  }

  threatOf(unit: Unit): Set<number> {
    return threatTiles(this.state, unit, this.moveField(unit));
  }

  unit(id: number): Unit | undefined {
    return unitById(this.state, id);
  }

  /* --------------------------------------------------------------- commands */

  /** Applies an action. Returns the event list, or throws IllegalActionError. */
  dispatch(action: Action): GameEvent[] {
    const undoable = action.kind !== 'endTurn';
    const snapshot = undoable ? cloneState(this.state) : null;

    let events: GameEvent[];
    try {
      events = applyAction(this.state, action);
    } catch (e) {
      if (e instanceof IllegalActionError && snapshot) this.state = snapshot;
      throw e;
    }

    if (snapshot) this.undoStack.push(snapshot);
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
    return this.undoStack.length > 0 && this.state.phase === 'playing';
  }

  undo(): boolean {
    const prev = this.undoStack.pop();
    if (!prev) return false;
    this.state = prev;
    this.stamp++;
    this.fieldCache.clear();
    this.notify([]);
    return true;
  }

  restart(): void {
    this.state = createState(this.level);
    this.undoStack = [];
    this.log.length = 0;
    this.stamp++;
    this.fieldCache.clear();
    this.notify([]);
  }
}
