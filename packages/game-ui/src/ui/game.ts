import { IllegalActionError } from '@empire/battle-engine/actions';
import { StoredDocumentError } from '@empire/battle-engine/domain';
import { activeCasts } from '@empire/battle-engine/casting';
import { SpellCastEntity } from '@empire/battle-engine/domain/spell-cast';
import { tacticOptions } from '@empire/battle-engine/commanders';
import { idx } from '@empire/battle-engine/grid';
import type { BattleEngine } from '@empire/battle-engine/engine';
import { GameSession } from '@empire/battle-engine/session';
import type { BattleSave } from '@empire/battle-engine/battle-save';
import { areEnemies, unitAt } from '@empire/battle-engine/state';
import type {
  Action,
  Coord,
  Direction,
  GameEvent,
  GameState,
  LevelData,
  ReactionStance,
  Unit,
} from '@empire/battle-engine/types';
import { GENERIC_ART, type ArtDirection } from '../art/direction';
import { BoardView, emptyOverlay, type BoardOverlay } from './board';
import {
  DefaultBattleEventPresenters,
  SessionBattleStage,
  type BattleEventPresenterRegistry,
  type BattleLogContext,
} from './event-presentation';
import { Hud, type HudView } from './hud';
import {
  DEPLOYING,
  DeploymentSelection,
  DestinationSelection,
  DisembarkSelection,
  IDLE,
  TacticTargetSelection,
  TargetSelection,
  UnitSelection,
  type Selection,
  type SelectionContext,
} from './selection';

export interface BattleCompletionSnapshot {
  state: GameState;
  events: GameEvent[];
}

/**
 * Where an interrupted battle is kept.
 *
 * A port, not a dependency: whether a battle can be put down is the shell's
 * business — a browser slot, a file, a server — and the controller only needs to
 * know whether one exists. The campaign shell deliberately passes none, because
 * a campaign battle is resumed through the campaign's own save.
 */
export interface BattleSaveStore {
  /** Records the battle, replacing whatever this store keeps. */
  write(save: BattleSave): void;
  /** The saved battle as it was written down, or null when there is none. */
  read(): unknown | null;
  /** Whether anything is stored, without parsing it. */
  has(): boolean;
}

export interface GameControllerOptions {
  /** Ruleset this battle runs on. Required: there is no ambient fallback. */
  engine: BattleEngine;
  /** How each battle event looks and reads; a content pack may replace entries. */
  eventPresenters?: BattleEventPresenterRegistry;
  exitLabel?: string;
  completionLabel?: string;
  /** Absent means this shell offers no save slot, and the entry stays hidden. */
  saves?: BattleSaveStore;
  /**
   * The art to draw with. Absent means plain shapes and a ruled board — the
   * theme is composed by the application root, never registered globally.
   */
  art?: ArtDirection;
  onComplete?: (snapshot: BattleCompletionSnapshot) => void;
}

const sleep = (ms: number) => new Promise<void>((r) => setTimeout(r, ms));

/**
 * Game controller: owns selection state, translates clicks into engine actions,
 * sequences animations around each dispatch, and drives the AI turns.
 */
export class GameController {
  readonly root = document.createElement('div');
  private readonly session: GameSession;
  private readonly board: BoardView;
  private readonly hud: Hud;
  private readonly scroller = document.createElement('div');
  private readonly presenters: BattleEventPresenterRegistry;
  private readonly stage: SessionBattleStage;
  /** Positions remembered one frame back, so a killed unit's number still lands. */
  private readonly lastSeenPositions = new Map<number, Coord>();

  private selection: Selection = IDLE;
  private cursor: Coord | null = null;
  private inspect: Unit | null = null;
  private busy = false;
  private messages: string[] = [];
  private aiRunning = false;
  private disposed = false;
  private resizeObserver: ResizeObserver | null = null;

  private readonly art: ArtDirection;

  constructor(
    level: LevelData,
    private readonly onExit: () => void,
    private readonly options: GameControllerOptions,
  ) {
    this.session = new GameSession(level, options.engine);
    this.art = options.art ?? GENERIC_ART;
    this.root.className = 'game-root';

    this.board = new BoardView(this.session.state, {
      onTileClick: (c) => void this.handleClick(c),
      onTileEnter: (c) => this.handleHover(c),
      onLeave: () => {
        this.cursor = null;
        this.refresh();
      },
      onSecondary: () => this.cancel(),
    }, this.session.content, this.session.rules.space.board(this.session.state).grid, this.art);

    this.hud = new Hud(this.art, {
      onCommand: (a) => void this.chooseCommand(a),
      onTactic: (key) => void this.chooseTactic(key),
      onReaction: (stance) => void this.chooseReaction(stance),
      onFacing: (facing) => void this.chooseFacing(facing),
      onCareer: (career) => void this.chooseCareer(career),
      onFormation: (formation) => void this.chooseFormation(formation),
      onEmbark: (carrier) => void this.embark(carrier),
      onDisembark: (passenger) => this.chooseDisembark(passenger),
      onDeployPick: (unit) => this.pickDeployUnit(unit),
      onConfirmDeployment: () => void this.confirmDeployment(),
      onCancel: () => this.cancel(),
      onEndTurn: () => void this.endTurn(),
      onUndo: () => this.undo(),
      onRestart: () => this.restart(),
      onSave: () => this.saveBattle(),
      onResume: () => this.resumeBattle(),
      onRecruit: (u) => void this.recruit(u),
      onExit: () => this.exit(),
      onContinue: () => this.continueAfterBattle(),
      onZoom: (d) => {
        this.board.setZoom(this.board.zoomLevel + d);
      },
    });

    this.presenters = options.eventPresenters ?? DefaultBattleEventPresenters.clone();
    this.stage = new SessionBattleStage(
      this.board,
      this.session,
      this.lastSeenPositions,
      () => this.board.render(this.overlay()),
    );

    this.scroller.className = 'board-scroll';
    this.scroller.append(this.board.el);

    const stage = document.createElement('div');
    stage.className = 'stage';
    stage.append(this.scroller, this.hud.panelEl);

    this.root.append(this.hud.topEl, stage, this.hud.modalEl);

    const fitBoard = () => this.board.fitWithin(this.scroller.clientWidth, this.scroller.clientHeight);
    requestAnimationFrame(fitBoard);
    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(fitBoard);
      this.resizeObserver.observe(this.scroller);
    }

    this.board.el.addEventListener(
      'wheel',
      (ev) => {
        if (!ev.ctrlKey && !ev.metaKey) return;
        ev.preventDefault();
        this.board.setZoom(this.board.zoomLevel - Math.sign(ev.deltaY) * 0.1);
      },
      { passive: false },
    );

    document.addEventListener('keydown', this.onKey);
    this.selection = this.restingSelection;
    this.pushMessage(this.state.phase === 'deployment'
      ? '战前部署 · 调整站位后确认部署'
      : `第 1 回合 · ${this.human?.name ?? ''} 开始行动`);
    this.refresh();

    // A level may open on an AI player (or have no human at all).
    this.maybeRunAi();
  }

  dispose(): void {
    this.disposed = true;
    this.resizeObserver?.disconnect();
    this.board.dispose();
    document.removeEventListener('keydown', this.onKey);
  }

  /* --------------------------------------------------------------- shortcuts */

  private get state(): GameState {
    return this.session.state;
  }

  private get human() {
    return this.state.players.find((p) => p.controller === 'human');
  }

  /** The human may issue orders: their side is up and the battle is under way. */
  private get isHumanTurn(): boolean {
    const p = this.state.players.find((x) => x.id === this.state.currentPlayer);
    return !!p && p.controller === 'human' && this.state.phase === 'playing';
  }

  /**
   * The board belongs to the human right now — including before the first turn.
   *
   * Separate from `isHumanTurn` because arranging a line and ordering it about
   * are different rights: deployment accepts clicks from a player who has no
   * turn yet, and every ability gate must keep saying no until the battle opens.
   */
  private get isHumanInput(): boolean {
    const p = this.state.players.find((x) => x.id === this.state.currentPlayer);
    return !!p && p.controller === 'human' && this.state.phase !== 'over';
  }

  /** The resting selection for the phase the battle is in. */
  private get restingSelection(): Selection {
    return this.state.phase === 'deployment' ? DEPLOYING : IDLE;
  }

  /** Hand the board to the AI driver whenever the side to act is not human. */
  private maybeRunAi(): void {
    if (this.state.phase !== 'over' && !this.isHumanInput) void this.runAiTurns();
  }

  private onKey = (ev: KeyboardEvent): void => {
    if (this.disposed || this.busy) return;
    const k = ev.key.toLowerCase();
    if (k === 'escape') return this.cancel();
    if (!this.isHumanTurn) return;
    if (k === 'e') return void this.endTurn();
    if (k === 'u') return this.undo();
    if (k === ' ' || k === 'tab') {
      ev.preventDefault();
      return this.cycleIdleUnit();
    }
    const map: Record<string, string> = { a: 'attack', c: 'capture', h: 'heal', w: 'wait' };
    if (map[k] && this.selection instanceof DestinationSelection) void this.chooseCommand(map[k]);
  };

  /* ------------------------------------------------------------------ input */

  private handleHover(at: Coord): void {
    this.cursor = at;
    const u = unitAt(this.state, { x: at.x, y: at.y });
    if (u && this.isVisible(u)) this.inspect = u;
    else if (!this.selectedUnit) this.inspect = null;
    this.refresh();
  }

  private get selectedUnit(): Unit | null {
    return this.selection.unitIn(this.selectionContext());
  }

  /**
   * The selected unit, but only while the player may actually order it about.
   *
   * Three panels and the overlay each spelled the three conditions out; the
   * fourth thing to ask would have been the fourth place to get it wrong.
   */
  private get commandableUnit(): Unit | null {
    const unit = this.selectedUnit;
    return unit && this.isHumanTurn && this.session.engine.canAct(this.state, unit) ? unit : null;
  }

  /**
   * The cursor, but only when it is one of the current selection's targets.
   *
   * Derived rather than stored: it used to be a field that three unrelated
   * methods had to remember to clear, and forgetting left a forecast on screen
   * for an order that no longer existed.
   */
  private get hoverTarget(): Coord | null {
    const cursor = this.cursor;
    if (!cursor) return null;
    return this.selection.targets.some((t) => t.x === cursor.x && t.y === cursor.y) ? cursor : null;
  }

  private selectionContext(): SelectionContext {
    return {
      session: this.session,
      state: this.state,
      isHumanTurn: this.isHumanTurn,
      cursor: this.cursor,
      hoverTarget: this.hoverTarget,
      canAct: (unit) => this.session.engine.canAct(this.state, unit),
      isVisible: (unit) => this.isVisible(unit),
    };
  }

  private async handleClick(at: Coord): Promise<void> {
    if (this.busy || !this.isHumanInput) return;
    const outcome = this.selection.click(this.selectionContext(), at);
    if (outcome.action) {
      await this.dispatch(outcome.action);
      return;
    }
    this.selection = outcome.selection;
    const clicked = unitAt(this.state, { x: at.x, y: at.y });
    this.inspect = clicked && this.isVisible(clicked) ? clicked : null;
    this.refresh();
  }

  private async chooseCommand(optionKeyOrAbility: string): Promise<void> {
    const selection = this.selection;
    if (!(selection instanceof DestinationSelection) || this.busy) return;
    const { dest, path } = selection;
    const unit = selection.unitId;
    const u = this.session.unit(unit);
    if (!u) return;
    const options = this.session.commandsAt(u, dest);
    const option = options.find((o) => o.key === optionKeyOrAbility) ??
      options.find((o) => o.ability === optionKeyOrAbility);
    if (!option) return;
    const { ability, weapon } = option;

    if (option.selfTargeted) {
      await this.dispatch({ kind: 'command', unit, path, command: { ability, weapon } });
      return;
    }
    if (option.targets.length === 1) {
      await this.dispatch({
        kind: 'command',
        unit,
        path,
        command: { ability, weapon, target: option.targets[0] },
      });
      return;
    }
    this.selection = new TargetSelection(unit, dest, path, ability, weapon, option.targets);
    this.refresh();
  }

  private async chooseTactic(key: string): Promise<void> {
    if (this.busy || !this.isHumanTurn) return;
    const separator = key.indexOf(':');
    if (separator < 1) return;
    const commander = key.slice(0, separator);
    const tactic = key.slice(separator + 1);
    const option = tacticOptions(this.session.rules, this.state, commander).find((candidate) => candidate.id === tactic);
    if (!option) return;
    if (option.targets.length === 1) {
      await this.dispatch({ kind: 'tactic', commander, tactic, target: option.targets[0] });
      return;
    }
    this.selection = new TacticTargetSelection(commander, tactic, option.targets);
    this.refresh();
  }

  private async chooseReaction(stance: ReactionStance): Promise<void> {
    const unit = this.selectedUnit;
    if (!unit || this.busy || !this.isHumanTurn) return;
    await this.dispatch({ kind: 'reaction', unit: unit.id, stance });
  }

  private async chooseFacing(facing: Direction): Promise<void> {
    const unit = this.selectedUnit;
    if (!unit || this.busy || !this.isHumanTurn) return;
    await this.dispatch({ kind: 'face', unit: unit.id, facing });
  }

  private async chooseFormation(formation: string | null): Promise<void> {
    const unit = this.selectedUnit;
    if (!unit || this.busy || !this.isHumanTurn) return;
    await this.dispatch({ kind: 'changeFormation', unit: unit.id, formation });
  }

  private async chooseCareer(career: string): Promise<void> {
    const unit = this.selectedUnit;
    if (!unit || this.busy || !this.isHumanTurn) return;
    await this.dispatch({ kind: 'changeCareer', unit: unit.id, career });
  }

  private async recruit(unitType: string): Promise<void> {
    const at = this.selection.recruitAt;
    if (!at) return;
    await this.dispatch({ kind: 'recruit', at, unit: unitType });
  }

  private cancel(): void {
    if (this.busy) return;
    const previous = this.selection.back();
    if (previous === this.restingSelection) this.inspect = null;
    this.selection = previous;
    this.refresh();
  }

  private cycleIdleUnit(): void {
    const idle = this.session.engine.actors(this.state);
    if (idle.length === 0) return;
    const current = this.selection instanceof UnitSelection ? this.selection.unitId : -1;
    const at = idle.findIndex((u) => u.id === current);
    const next = idle[(at + 1) % idle.length];
    this.selection = new UnitSelection(next.id);
    this.inspect = next;
    this.board.centerOn({ x: next.x, y: next.y }, this.scroller);
    this.refresh();
  }

  private undo(): void {
    if (this.busy || !this.session.canUndo) return;
    this.session.undo();
    this.board.setState(this.state);
    this.selection = this.restingSelection;
    this.inspect = null;
    this.pushMessage('已撤销上一步');
    this.refresh();
  }

  private restart(): void {
    this.session.restart();
    this.reopen('');
  }

  private saveBattle(): void {
    const store = this.options.saves;
    if (!store || this.busy || this.state.phase !== 'playing') return;
    store.write(this.session.save());
    this.pushMessage(`已保存第 ${this.state.turn} 回合的进度`);
    this.refresh();
  }

  /**
   * Picks the battle up where it was left.
   *
   * A save the composed ruleset cannot honour is a message, not a crash: the
   * session refuses it before replacing anything, so the battle on screen is
   * still the one the player was playing.
   */
  private resumeBattle(): void {
    const store = this.options.saves;
    if (!store || this.busy) return;
    const raw = store.read();
    if (raw === null) return;
    try {
      this.session.load(raw);
    } catch (error) {
      // Only the document is allowed to be at fault here. This used to catch
      // everything, so any defect thrown anywhere under `loadBattle` was
      // reported to the player as "your save is unreadable" — and the save,
      // which was fine, looked like the thing to delete.
      if (!(error instanceof StoredDocumentError)) throw error;
      this.pushMessage(`无法读取存档：${error.message}`);
      this.refresh();
      return;
    }
    this.reopen(`已读取第 ${this.state.turn} 回合的存档`);
    this.maybeRunAi();
  }

  /** Whatever the session now holds, shown from a clean slate. */
  private reopen(message: string): void {
    this.board.setState(this.state);
    this.selection = this.restingSelection;
    this.inspect = null;
    this.messages = [];
    this.pushMessage(message);
    this.refresh();
  }

  private exit(): void {
    this.dispose();
    this.onExit();
  }

  private continueAfterBattle(): void {
    if (this.state.phase !== 'over' || !this.options.onComplete) return;
    const snapshot: BattleCompletionSnapshot = {
      state: structuredClone(this.state),
      events: structuredClone(this.session.log),
    };
    this.dispose();
    this.options.onComplete(snapshot);
  }

  /* -------------------------------------------------------------- dispatching */

  /**
   * Walks a unit along the path its order carries, if it carries one.
   *
   * `follow` is the difference between watching your own order and being shown
   * someone else's: the camera chases the AI, never the player.
   */
  private async march(action: Action, follow: boolean, pace?: number): Promise<void> {
    if (action.kind !== 'command' || action.path.length < 2) return;
    const unit = this.session.unit(action.unit);
    if (!unit) return;
    if (follow) this.board.centerOn(action.path[action.path.length - 1], this.scroller);
    await this.board.animateMove(unit, action.path, pace);
  }

  /**
   * Plays what an order caused and leaves the board on the settled state.
   *
   * The player's path and the AI loop each wrote this out, and had drifted onto
   * different sides of the animation — inert only because a dispatch mutates
   * the state in place, which is not something a caller should have to know.
   */
  private async settle(events: GameEvent[]): Promise<void> {
    await this.playEvents(events);
    this.board.setState(this.state);
    this.refresh();
  }

  /** Runs an action with animation: move first, then resolve, then effects. */
  private async dispatch(action: Action): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.selection = this.restingSelection;
    this.refresh();

    try {
      await this.march(action, false);
      let events: GameEvent[];
      try {
        events = this.session.dispatch(action);
      } catch (e) {
        // The player is told why an order was refused; anything else is a bug.
        if (!(e instanceof IllegalActionError)) throw e;
        this.pushMessage(`无法执行：${e.message}`);
        events = [];
      }
      await this.settle(events);
    } finally {
      this.busy = false;
      // Re-read the resting selection: `finishDeployment` opens the battle, and
      // leaving the deployment selection in place would go on offering an
      // arrangement that is over.
      this.selection = this.restingSelection;
      this.refresh();
    }

    this.maybeRunAi();
  }

  /** One batch of events comes from one order, so one log context serves it. */
  private async playEvents(events: GameEvent[]): Promise<void> {
    const log = this.logContext();
    for (const event of events) {
      if (this.disposed) return;
      await this.presenters.animate(this.stage, event);
      this.pushMessage(this.presenters.describe(log, event));
    }
  }

  private logContext(): BattleLogContext {
    const state = this.state;
    return {
      state,
      content: this.session.content,
      unitName: (id) => {
        const unit = state.units.find((candidate) => candidate.id === id);
        return unit ? this.session.content.units.get(unit.type).name : '单位';
      },
      playerName: (id) => state.players.find((player) => player.id === id)?.name ?? '？',
    };
  }

  /* ----------------------------------------------------------------- ai loop */

  private async endTurn(): Promise<void> {
    if (this.busy || !this.isHumanTurn) return;
    await this.dispatch({ kind: 'endTurn' });
  }

  /** Put the unit under command aboard an adjacent carrier. */
  private async embark(carrier: number): Promise<void> {
    const unit = this.commandableUnit;
    if (!unit || this.busy || !this.isHumanTurn) return;
    await this.dispatch({ kind: 'embark', unit: unit.id, carrier });
  }

  /** Arm the landing cells for one passenger of the carrier under command. */
  private chooseDisembark(passenger: number): void {
    const carrier = this.commandableUnit;
    if (!carrier || this.busy || !this.isHumanTurn) return;
    const option = this.session.passengerOptions(carrier).find((entry) => entry.unit.id === passenger);
    if (!option || option.spots.length === 0) return;
    this.selection = new DisembarkSelection(carrier.id, passenger, option.spots);
    this.refresh();
  }

  /** Hand the arranged line over and let the battle begin. */
  private async confirmDeployment(): Promise<void> {
    if (this.busy || this.state.phase !== 'deployment' || !this.isHumanInput) return;
    await this.dispatch({ kind: 'finishDeployment' });
  }

  /** Take up one of the units being arranged, named from the roster. */
  private pickDeployUnit(id: number): void {
    if (this.busy || this.state.phase !== 'deployment' || !this.isHumanInput) return;
    const unit = this.session.unit(id);
    if (!unit) return;
    this.selection = new DeploymentSelection(unit.id);
    this.inspect = unit;
    this.board.centerOn({ x: unit.x, y: unit.y }, this.scroller);
    this.refresh();
  }

  private async runAiTurns(): Promise<void> {
    if (this.aiRunning) return;
    this.aiRunning = true;
    this.busy = true;
    this.refresh();
    try {
      let guard = 0;
      while (!this.disposed && this.state.phase !== 'over' && !this.isHumanInput) {
        if (++guard > 2000) break;
        const action = this.session.chooseAiAction();
        await this.march(action, true, 65);
        let events: GameEvent[];
        try {
          events = this.session.dispatch(action);
        } catch (error) {
          // A desynced AI suggestion should never stall the game — but only a
          // refused *order* is a desync. This used to catch everything, so any
          // defect thrown from anywhere in the rules became an invisible pass.
          if (!(error instanceof IllegalActionError)) throw error;
          this.pushMessage(`AI 的行动被拒绝：${error.message}`);
          events = this.session.tryDispatch({ kind: 'endTurn' }) ?? [];
        }
        await this.settle(events);
        // A longer beat after an order that hands the turn over, asked of the
        // handler rather than matched against `'endTurn'`: a pack's own "sound
        // the retreat" ends a turn too, and deserves the same pause.
        await sleep(this.session.engine.actionHandlers.handsOffTurn(action) ? 120 : 90);
      }
    } finally {
      this.aiRunning = false;
      this.busy = false;
      this.board.setState(this.state);
      this.refresh();
    }
  }

  /* ---------------------------------------------------------------- painting */

  private isVisible(unit: Unit): boolean {
    const me = this.human?.id ?? this.state.currentPlayer;
    return this.session.isUnitVisible(me, unit);
  }

  private overlay(): BoardOverlay {
    const o = emptyOverlay();
    const state = this.state;
    const viewer = this.human?.id ?? state.currentPlayer;

    if (state.rules.fog) {
      o.visible = this.session.visibleTiles(viewer);
      for (const u of state.units) {
        if (!this.session.isUnitVisible(viewer, u, o.visible)) o.hiddenUnits.add(u.id);
      }
    }

    o.cursor = this.cursor;

    // Marked tiles are public the moment a cast begins, so they show whoever is
    // looking and whatever else the overlay is doing.
    for (const cast of activeCasts(state)) {
      const weapon = this.session.content.weapons.get(cast.weapon);
      const remaining = new SpellCastEntity(cast).remainingAt(state.actorTurns);
      const board = this.session.rules.space.board(state);
      for (const cell of this.session.rules.areaShapes.coverage(board, cast.origin, cast.target, weapon.area)) {
        o.incoming.set(idx(state.map, cell.x, cell.y), remaining);
      }
    }

    if (this.busy) return o;
    const unit = this.selectedUnit;

    if (unit) {
      o.selected = { x: unit.x, y: unit.y };
      if (this.commandableUnit) this.selection.paint(this.selectionContext(), o);
      return o;
    }

    // A selection with no unit of its own still has something to show — a
    // tactic's reach — and it shows it whether or not an enemy is inspected.
    this.selection.paint(this.selectionContext(), o);
    if (this.inspect && areEnemies(state, this.inspect.owner, viewer)) {
      // Hovering an enemy shows what it can hit next turn.
      for (const i of this.session.threatOf(this.inspect)) o.threat.add(i);
      o.selected = { x: this.inspect.x, y: this.inspect.y };
    }

    return o;
  }

  /** The intents the HUD answers, so a test can compare them with the markup. */
  get handledIntents(): string[] {
    return this.hud.handledIntents;
  }

  private hudView(): HudView {
    const state = this.state;
    let forecast: HudView['forecast'] = null;

    const unit = this.selectedUnit;
    const commandable = this.commandableUnit;
    const aiming = this.selection instanceof TargetSelection ? this.selection : null;
    // A forecast is an exchange of blows, so it belongs to an order that fires
    // a weapon — not to the one ability whose id happens to be `attack`.
    if (unit && aiming && this.hoverTarget && aiming.weapon) {
      const defender = unitAt(state, { x: this.hoverTarget.x, y: this.hoverTarget.y });
      if (defender) {
        const plan = this.session.attackPlan(unit, this.hoverTarget, aiming.dest, aiming.weapon);
        // No primary hit means the aimed tile is not the one this shot lands
        // on, and there is no exchange to preview.
        const exchange = plan.primaryUnit;
        if (exchange) {
          const recipient = state.units.find((candidate) => candidate.id === exchange.damageRecipient) ?? defender;
          forecast = { plan, exchange, attacker: unit, defender, recipient };
        }
      }
    }

    const menuAt = this.selection instanceof DestinationSelection ? this.selection.dest : null;
    const commands = menuAt && unit && !this.busy ? this.session.commandsAt(unit, menuAt) : null;
    const tactics =
      this.isHumanTurn && !this.busy && this.selection === IDLE
        ? state.commanders
            .filter((commander) => commander.owner === state.currentPlayer)
            .flatMap((commander) =>
              tacticOptions(this.session.rules, state, commander.id).map((option) => ({
                ...option,
                key: `${commander.id}:${option.id}`,
                commander: commander.id,
              })),
            )
        : [];

    // Null once the battle is under way, which is what turns the panel and the
    // primary button back into the ordinary ones.
    const roster = this.isHumanInput ? this.session.deploymentRoster() : null;
    const orderPreview = this.session.engine.turnOrderPreview(state, 6);
    return {
      state: state,
      rules: this.session.rules,
      turnOrder: {
        // A side-turn policy previews a whole army, which is not an order: only
        // show the strip when the policy entitles one unit at a time.
        units: state.turnOrder.activeUnit === null ? [] : orderPreview,
        activeUnit: state.turnOrder.activeUnit,
      },
      casts: activeCasts(state),
      resources: this.session.rules.resources,
      inspect: this.inspect,
      tile: this.cursor,
      forecast,
      commands,
      tactics,
      reactionUnit: commandable?.id ?? null,
      rankNextThreshold: this.inspect
        ? this.session.engine.rules.progression.nextThreshold(this.inspect.rank)
        : null,
      careerOptions: commandable ? this.session.careerOptions(commandable) : [],
      formationOptions: commandable ? this.session.formationOptions(commandable) : [],
      carrierOptions: commandable ? this.session.carrierOptions(commandable) : [],
      // Passengers belong to the carrier whether or not it may still act, but
      // only a commandable carrier can be told to put them down.
      passengerOptions: commandable ? this.session.passengerOptions(commandable) : [],
      deployment: roster && { units: [...roster.units], selected: this.selection.unitId },
      targeting: this.selection.targetingLabel,
      recruitAt: this.selection.recruitAt,
      hint: this.hint(),
      busy: this.busy,
      canUndo: this.session.canUndo,
      saves: this.options.saves
        ? { canSave: !this.busy && state.phase === 'playing', canResume: !this.busy && this.options.saves.has() }
        : null,
      messages: this.messages,
      exitLabel: this.options.exitLabel,
      completionLabel: this.options.completionLabel,
    };
  }

  private hint(): string {
    if (this.state.phase === 'over') return '对局结束。';
    if (!this.isHumanInput) return 'AI 正在思考…';
    return this.selection.hint;
  }

  private pushMessage(m: string): void {
    if (!m) return;
    this.messages.push(m);
    if (this.messages.length > 60) this.messages.splice(0, this.messages.length - 60);
  }

  private refresh(): void {
    if (this.disposed) return;
    for (const unit of this.state.units) this.lastSeenPositions.set(unit.id, { x: unit.x, y: unit.y });
    this.board.render(this.overlay());
    this.hud.render(this.hudView());
  }
}

