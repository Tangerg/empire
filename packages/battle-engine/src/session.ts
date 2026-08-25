import { IllegalActionError } from './domain/errors';
import type { AiOptions } from './ai';
import type { BattleEngine } from './engine';
import type { BattleSave } from './battle-save';
import type { MoveField } from './movement';
import { unitById } from './state';
import type { Action, Coord, GameEvent, GameState, LevelData, PlayerId, Unit, WeaponId } from './types';

/**
 * Thin stateful shell around the pure reducer: undo snapshots, the semantic
 * event log and memoised move fields. The UI talks only to this; the engine
 * below it stays free of any notion of selection or presentation.
 */
export class GameSession {
  state: GameState;
  readonly level: LevelData;
  readonly log: GameEvent[] = [];

  private undoStack: Array<{ state: GameState; logLength: number }> = [];
  private fieldCache = new Map<number, { field: MoveField; stamp: number }>();
  private stamp = 0;

  constructor(
    level: LevelData,
    /** The composed ruleset this battle runs on; never an ambient default. */
    readonly engine: BattleEngine,
  ) {
    this.level = structuredClone(level);
    this.state = engine.createState(this.level);
  }

  /** Ruleset of this battle; the UI renders from here, not from ambient state. */
  get content() {
    return this.engine.content;
  }

  get rules() {
    return this.engine.rules;
  }

  /* ---------------------------------------------------------------- queries */

  moveField(unit: Unit): MoveField {
    const hit = this.fieldCache.get(unit.id);
    if (hit && hit.stamp === this.stamp) return hit.field;
    const field = this.engine.space.moveField(this.state, unit);
    this.fieldCache.set(unit.id, { field, stamp: this.stamp });
    return field;
  }

  pathTo(unit: Unit, to: Coord): Coord[] | null {
    return this.engine.space.pathTo(this.moveField(unit), this.state, to);
  }

  commandsAt(unit: Unit, at: Coord) {
    return this.engine.commandsAt(this.state, unit, at);
  }

  careerOptions(unit: Unit) {
    return this.engine.careerOptions(this.state, unit);
  }

  formationOptions(unit: Unit) {
    return this.engine.formationOptions(this.state, unit);
  }

  carrierOptions(unit: Unit) {
    return this.engine.carrierOptions(this.state, unit);
  }

  passengerOptions(carrier: Unit) {
    return this.engine.passengerOptions(this.state, carrier);
  }

  deploymentRoster() {
    return this.engine.deploymentRoster(this.state);
  }

  deploymentSpots(unit: Unit) {
    return this.engine.deploymentSpots(this.state, unit);
  }

  threatOf(unit: Unit): Set<number> {
    return this.engine.space.threatOf(this.state, unit, this.moveField(unit));
  }

  controlZoneAgainst(unit: Unit): Set<number> {
    return this.engine.space.controlZoneAgainst(this.state, unit);
  }

  visibleTiles(viewer: PlayerId): Set<number> {
    return this.engine.space.visibleTiles(this.state, viewer);
  }

  isUnitVisible(viewer: PlayerId, unit: Unit, seen?: Set<number>): boolean {
    return this.engine.space.isUnitVisible(this.state, viewer, unit, seen);
  }

  visibleUnits(viewer: PlayerId): Unit[] {
    return this.engine.space.visibleUnits(this.state, viewer);
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
  dispatch(action: Action): readonly GameEvent[] {
    const handsOff = this.engine.actionHandlers.handsOffTurn(action);
    const { events, before } = this.engine.dispatchWithReceipt(this.state, action);

    // No rewinding across a turn boundary; the order itself says whether it is one.
    if (handsOff) this.undoStack = [];
    else this.undoStack.push({ state: before, logLength: this.log.length });

    this.stamp++;
    this.fieldCache.clear();
    this.log.push(...events);
    return events;
  }

  /** Same as dispatch but swallows illegal actions, returning null. */
  tryDispatch(action: Action): readonly GameEvent[] | null {
    try {
      return this.dispatch(action);
    } catch (error) {
      if (error instanceof IllegalActionError) return null;
      throw error;
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
    return true;
  }

  /** The battle as a document, for a save slot. */
  save(savedAt?: string): BattleSave {
    return this.engine.saveBattle(this.state, savedAt);
  }

  /**
   * Resumes a saved battle in place.
   *
   * The undo stack and the message log stay behind on purpose: they are what
   * happened in *this* sitting, and a resumed battle has not had one yet. The
   * refusal comes from the engine, so a save this ruleset cannot honour is
   * reported before anything is replaced.
   */
  load(raw: unknown): void {
    const resumed = this.engine.loadBattle(raw);
    this.state = resumed;
    this.undoStack = [];
    this.log.length = 0;
    this.stamp++;
    this.fieldCache.clear();
  }

  restart(): void {
    this.state = this.engine.createState(this.level);
    this.undoStack = [];
    this.log.length = 0;
    this.stamp++;
    this.fieldCache.clear();
  }
}
