import type { ContentCatalog } from '@empire/battle-engine';
import { IllegalActionError } from '@empire/battle-engine/actions';
import { activeCasts } from '@empire/battle-engine/casting';
import { SpellCastEntity } from '@empire/battle-engine/domain/spell-cast';
import { weaponAreaCells } from '@empire/battle-engine/combat-plan';
import { tacticOptions } from '@empire/battle-engine/commanders';
import { idx } from '@empire/battle-engine/grid';
import type { BattleEngine } from '@empire/battle-engine/engine';
import { GameSession } from '@empire/battle-engine/session';
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
import { BoardView, emptyOverlay, type BoardOverlay } from './board';
import { Hud, type HudView } from './hud';
import {
  DestinationSelection,
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

export interface GameControllerOptions {
  /** Ruleset this battle runs on. Required: there is no ambient fallback. */
  engine: BattleEngine;
  exitLabel?: string;
  completionLabel?: string;
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

  private selection: Selection = IDLE;
  private cursor: Coord | null = null;
  private inspect: Unit | null = null;
  private busy = false;
  private messages: string[] = [];
  private aiRunning = false;
  private disposed = false;
  private resizeObserver: ResizeObserver | null = null;

  constructor(
    level: LevelData,
    private readonly onExit: () => void,
    private readonly options: GameControllerOptions,
  ) {
    this.session = new GameSession(level, options.engine);
    this.root.className = 'game-root';

    this.board = new BoardView(this.session.state, {
      onTileClick: (c) => void this.handleClick(c),
      onTileEnter: (c) => this.handleHover(c),
      onLeave: () => {
        this.cursor = null;
        this.refresh();
      },
      onSecondary: () => this.cancel(),
    }, this.session.content);

    this.hud = new Hud({
      onCommand: (a) => void this.chooseCommand(a),
      onTactic: (key) => void this.chooseTactic(key),
      onReaction: (stance) => void this.chooseReaction(stance),
      onFacing: (facing) => void this.chooseFacing(facing),
      onCareer: (career) => void this.chooseCareer(career),
      onCancel: () => this.cancel(),
      onEndTurn: () => void this.endTurn(),
      onUndo: () => this.undo(),
      onRestart: () => this.restart(),
      onRecruit: (u) => void this.recruit(u),
      onExit: () => this.exit(),
      onContinue: () => this.continueAfterBattle(),
      onZoom: (d) => {
        this.board.setZoom(this.board.zoomLevel + d);
      },
    });

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
    this.pushMessage(`第 1 回合 · ${this.human?.name ?? ''} 开始行动`);
    this.refresh();

    // A level may open on an AI player (or have no human at all).
    if (!this.isHumanTurn && this.state.phase === 'playing') void this.runAiTurns();
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

  private get isHumanTurn(): boolean {
    const p = this.state.players.find((x) => x.id === this.state.currentPlayer);
    return !!p && p.controller === 'human' && this.state.phase === 'playing';
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

  private handleHover(c: Coord): void {
    this.cursor = c;
    const u = unitAt(this.state, c.x, c.y);
    if (u && this.isVisible(u)) this.inspect = u;
    else if (!this.selectedUnit) this.inspect = null;
    this.refresh();
  }

  private get selectedUnit(): Unit | null {
    return this.selection.unitIn(this.selectionContext());
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

  private async handleClick(c: Coord): Promise<void> {
    if (this.busy || this.state.phase !== 'playing') return;
    const outcome = this.selection.click(this.selectionContext(), c);
    if (outcome.action) {
      await this.dispatch(outcome.action);
      return;
    }
    this.selection = outcome.selection;
    // One rule where there were five: the inspector shows what you just
    // clicked, if you are allowed to see it, and nothing otherwise.
    const clicked = unitAt(this.state, c.x, c.y);
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
    if (previous === IDLE) this.inspect = null;
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
    this.selection = IDLE;
    this.inspect = null;
    this.pushMessage('已撤销上一步');
    this.refresh();
  }

  private restart(): void {
    this.session.restart();
    this.board.setState(this.state);
    this.selection = IDLE;
    this.inspect = null;
    this.messages = [];
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

  /** Runs an action with animation: move first, then resolve, then effects. */
  private async dispatch(action: Action): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.selection = IDLE;
    this.refresh();

    try {
      if (action.kind === 'command' && action.path.length > 1) {
        const unit = this.session.unit(action.unit);
        if (unit) await this.board.animateMove(unit, action.path);
      }

      let events: GameEvent[];
      try {
        events = this.session.dispatch(action);
      } catch (e) {
        if (e instanceof IllegalActionError) {
          this.pushMessage(`无法执行：${e.message}`);
          this.board.setState(this.state);
          this.refresh();
          return;
        }
        throw e;
      }

      await this.playEvents(events);
      this.board.setState(this.state);
      this.refresh();
    } finally {
      this.busy = false;
      this.refresh();
    }

    if (!this.isHumanTurn && this.state.phase === 'playing') void this.runAiTurns();
  }

  private async playEvents(events: GameEvent[]): Promise<void> {
    for (const e of events) {
      if (this.disposed) return;
      switch (e.type) {
        case 'attack':
        case 'counter': {
          const attacker = this.session.unit(e.attacker);
          const defender = this.session.unit(e.defender);
          const at = defender
            ? { x: defender.x, y: defender.y }
            : this.lastKnownPosition(e.defender);
          if (attacker) await this.board.animateStrike(attacker, at ?? { x: attacker.x, y: attacker.y });
          if (at) await this.board.animateHit(at, e.damage, e.killed, e.weapon);
          break;
        }
        case 'partingShot': {
          // The defender has already moved on; the blow lands on the tile it left.
          const attacker = this.session.unit(e.attacker);
          if (attacker) await this.board.animateStrike(attacker, e.at);
          await this.board.animateHit(e.at, e.damage, e.killed, e.weapon);
          break;
        }
        case 'supportAttack': {
          const supporter = this.session.unit(e.attacker);
          const defender = this.session.unit(e.defender);
          const at = defender
            ? { x: defender.x, y: defender.y }
            : this.lastKnownPosition(e.defender);
          if (supporter && at) await this.board.animateStrike(supporter, at);
          if (at) await this.board.animateHit(at, e.damage, e.killed, e.weapon);
          break;
        }
        case 'areaAttack': {
          const defender = this.session.unit(e.defender);
          const at = defender
            ? { x: defender.x, y: defender.y }
            : this.lastKnownPosition(e.defender);
          if (at) await this.board.animateHit(at, e.damage, e.killed, e.weapon);
          break;
        }
        case 'attackStructure': {
          const attacker = this.session.unit(e.attacker);
          const structure = this.state.structures.find((candidate) => candidate.id === e.structure);
          const at = structure ? { x: structure.x, y: structure.y } : null;
          if (attacker && at) await this.board.animateStrike(attacker, at);
          if (at) await this.board.animateHit(at, e.damage, e.destroyed, e.weapon);
          break;
        }
        case 'areaAttackStructure': {
          const structure = this.state.structures.find((candidate) => candidate.id === e.structure);
          if (structure) await this.board.animateHit({ x: structure.x, y: structure.y }, e.damage, e.destroyed, e.weapon);
          break;
        }
        case 'death':
          await this.board.animateDeath(e.unit);
          break;
        case 'heal': {
          const target = this.session.unit(e.target);
          if (target) await this.board.animateHeal({ x: target.x, y: target.y }, e.amount);
          break;
        }
        case 'recruit':
          this.board.render(this.overlay());
          await this.board.animateSpawn(e.unit);
          break;
        case 'capture':
          if (e.captured) this.board.syncTerrain();
          break;
        case 'turnStart': {
          const p = this.state.players.find((x) => x.id === e.player)!;
          this.board.render(this.overlay());
          await this.board.announce(
            `${p.name} · 第 ${e.turn} 回合`,
            p.color,
          );
          break;
        }
        case 'gameOver':
          await sleep(220);
          break;
        default:
          break;
      }
      this.pushMessage(describeEvent(this.session.content, this.state, e));
    }
  }

  private deathPositions = new Map<number, Coord>();

  private lastKnownPosition(unitId: number): Coord | null {
    return this.deathPositions.get(unitId) ?? null;
  }

  /* ----------------------------------------------------------------- ai loop */

  private async endTurn(): Promise<void> {
    if (this.busy || !this.isHumanTurn) return;
    await this.dispatch({ kind: 'endTurn' });
  }

  private async runAiTurns(): Promise<void> {
    if (this.aiRunning) return;
    this.aiRunning = true;
    this.busy = true;
    this.refresh();
    try {
      let guard = 0;
      while (!this.disposed && this.state.phase === 'playing' && !this.isHumanTurn) {
        if (++guard > 2000) break;
        const action = this.session.chooseAiAction();

        if (action.kind === 'command' && action.path.length > 1) {
          const unit = this.session.unit(action.unit);
          if (unit) {
            this.board.centerOn(action.path[action.path.length - 1], this.scroller);
            await this.board.animateMove(unit, action.path, 65);
          }
        }
        let events: GameEvent[];
        try {
          events = this.session.dispatch(action);
        } catch {
          // A desynced AI suggestion should never stall the game.
          events = this.session.tryDispatch({ kind: 'endTurn' }) ?? [];
        }
        this.board.setState(this.state);
        await this.playEvents(events);
        this.refresh();
        await sleep(action.kind === 'endTurn' ? 120 : 90);
      }
    } finally {
      this.aiRunning = false;
      this.busy = false;
      this.board.setState(this.state);
      this.refresh();
    }
  }

  /* ---------------------------------------------------------------- painting */

  private isVisible(u: Unit): boolean {
    const me = this.human?.id ?? this.state.currentPlayer;
    return this.session.isUnitVisible(me, u);
  }

  private overlay(): BoardOverlay {
    const o = emptyOverlay();
    const s = this.state;
    const viewer = this.human?.id ?? s.currentPlayer;

    if (s.rules.fog) {
      o.visible = this.session.visibleTiles(viewer);
      for (const u of s.units) {
        if (!this.session.isUnitVisible(viewer, u, o.visible)) o.hiddenUnits.add(u.id);
      }
    }

    o.cursor = this.cursor;

    // Marked tiles are public the moment a cast begins, so they show whoever is
    // looking and whatever else the overlay is doing.
    for (const cast of activeCasts(s)) {
      const weapon = this.session.content.weapons.get(cast.weapon);
      const remaining = new SpellCastEntity(cast).remainingAt(s.actorTurns);
      for (const cell of weaponAreaCells(s, cast.origin, cast.target, weapon)) {
        o.incoming.set(idx(s.map, cell.x, cell.y), remaining);
      }
    }

    if (this.busy) return o;
    const unit = this.selectedUnit;

    if (unit) {
      o.selected = { x: unit.x, y: unit.y };
      if (this.isHumanTurn && this.session.engine.canAct(s, unit)) {
        this.selection.paint(this.selectionContext(), o);
      }
      return o;
    }

    // A selection with no unit of its own still has something to show — a
    // tactic's reach — and it shows it whether or not an enemy is inspected.
    this.selection.paint(this.selectionContext(), o);
    if (this.inspect && areEnemies(s, this.inspect.owner, viewer)) {
      // Hovering an enemy shows what it can hit next turn.
      for (const i of this.session.threatOf(this.inspect)) o.threat.add(i);
      o.selected = { x: this.inspect.x, y: this.inspect.y };
    }

    return o;
  }

  private hudView(): HudView {
    const s = this.state;
    let fcView: HudView['forecast'] = null;

    const unit = this.selectedUnit;
    const aiming = this.selection instanceof TargetSelection ? this.selection : null;
    if (unit && aiming && this.hoverTarget && aiming.ability === 'attack') {
      const defender = unitAt(s, this.hoverTarget.x, this.hoverTarget.y);
      if (defender) {
        const plan = this.session.attackPlan(unit, this.hoverTarget, aiming.dest, aiming.weapon);
        const fc = plan.primaryUnit!;
        const recipient = s.units.find((candidate) => candidate.id === fc.damageRecipient) ?? defender;
        fcView = {
          plan,
          fc,
          attacker: unit,
          defender,
          recipient,
        };
      }
    }

    const menuAt = this.selection instanceof DestinationSelection ? this.selection.dest : null;
    const commands = menuAt && unit && !this.busy ? this.session.commandsAt(unit, menuAt) : null;
    const tactics =
      this.isHumanTurn && !this.busy && this.selection === IDLE
        ? s.commanders
            .filter((commander) => commander.owner === s.currentPlayer)
            .flatMap((commander) =>
              tacticOptions(this.session.rules, s, commander.id).map((option) => ({
                ...option,
                key: `${commander.id}:${option.id}`,
                commander: commander.id,
              })),
            )
        : [];

    const orderPreview = this.session.engine.turnOrderPreview(s, 6);
    return {
      state: s,
      rules: this.session.rules,
      turnOrder: {
        // A side-turn policy previews a whole army, which is not an order: only
        // show the strip when the policy entitles one unit at a time.
        units: s.turnOrder.activeUnit === null ? [] : orderPreview,
        activeUnit: s.turnOrder.activeUnit,
      },
      casts: activeCasts(s),
      resources: this.session.rules.resources,
      inspect: this.inspect,
      tile: this.cursor,
      forecast: fcView,
      commands,
      tactics,
      reactionUnit:
        unit && this.isHumanTurn && this.session.engine.canAct(s, unit) ? unit.id : null,
      rankNextThreshold: this.inspect
        ? this.session.engine.rules.progression.nextThreshold(this.inspect.rank)
        : null,
      careerOptions: unit && this.isHumanTurn && this.session.engine.canAct(s, unit)
        ? this.session.careerOptions(unit)
        : [],
      targeting: this.selection.targetingLabel,
      recruitAt: this.selection.recruitAt,
      hint: this.hint(),
      busy: this.busy,
      canUndo: this.session.canUndo,
      messages: this.messages,
      exitLabel: this.options.exitLabel,
      completionLabel: this.options.completionLabel,
    };
  }

  private hint(): string {
    if (this.state.phase === 'over') return '对局结束。';
    if (!this.isHumanTurn) return 'AI 正在思考…';
    return this.selection.hint;
  }

  private pushMessage(m: string): void {
    if (!m) return;
    this.messages.push(m);
    if (this.messages.length > 60) this.messages.splice(0, this.messages.length - 60);
  }

  private refresh(): void {
    if (this.disposed) return;
    // Remember positions so a killed unit's damage number can still be placed.
    for (const u of this.state.units) this.deathPositions.set(u.id, { x: u.x, y: u.y });
    this.board.render(this.overlay());
    this.hud.render(this.hudView());
  }
}

/* --------------------------------------------------------------- event text */

function describeEvent(content: ContentCatalog, s: GameState, e: GameEvent): string {
  const name = (id: number) => {
    const u = s.units.find((x) => x.id === id);
    return u ? content.units.get(u.type).name : '单位';
  };
  const pname = (id: number) => s.players.find((p) => p.id === id)?.name ?? '？';
  switch (e.type) {
    case 'attack':
      return `${name(e.attacker)} 造成 ${e.damage} 点伤害${e.killed ? '，目标阵亡' : ''}`;
    case 'areaAttack':
      return `范围攻击对 ${name(e.defender)} 造成 ${e.damage} 点伤害${e.killed ? '，目标阵亡' : ''}`;
    case 'counter':
      return `反击造成 ${e.damage} 点伤害${e.killed ? '，我方阵亡' : ''}`;
    case 'supportAttack':
      return `${name(e.attacker)} 援护攻击造成 ${e.damage} 点伤害${e.killed ? '，目标阵亡' : ''}`;
    case 'partingShot':
      return `${name(e.attacker)} 借机攻击造成 ${e.damage} 点伤害${e.killed ? '，目标阵亡' : ''}`;
    case 'attackStructure':
      return `对结构造成 ${e.damage} 点伤害${e.destroyed ? '，结构被摧毁' : ''}`;
    case 'areaAttackStructure':
      return `范围攻击对结构造成 ${e.damage} 点伤害${e.destroyed ? '，结构被摧毁' : ''}`;
    case 'heal':
      return `${name(e.source)} 治疗了 ${e.amount} 点生命`;
    case 'capture':
      return e.captured ? `${pname(e.player)} 占领了 (${e.at.x}, ${e.at.y})` : '占领进度提升';
    case 'recruit':
      return `${pname(s.currentPlayer)} 征募了 ${name(e.unit)}`;
    case 'resourceChanged': {
      const resource = e.resource === 'funds' ? '资金' : e.resource === 'command_points' ? '指挥点' : e.resource === 'momentum' ? '气势' : e.resource;
      const subject = e.subject.kind === 'player' ? pname(e.subject.id) : e.subject.kind === 'unit' ? name(e.subject.id) : name(e.subject.unit);
      return `${subject} ${resource} ${e.amount >= 0 ? '+' : ''}${e.amount}（${e.current}）`;
    }
    case 'rankChanged':
      return `${name(e.unit)} 晋升为 ${(['新兵', '老兵', '精英'] as const)[e.to]}`;
    case 'careerChanged':
      return `${name(e.unit)} 转职为 ${content.careers.get(e.to).name}`;
    case 'facingChanged':
      return `${name(e.unit)} 调整了朝向`;
    case 'turnStart':
      return `第 ${e.turn} 回合 · ${pname(e.player)}`;
    case 'defeat':
      return `${pname(e.player)} 已被击败`;
    case 'gameOver':
      return e.reason;
    default:
      return '';
  }
}
