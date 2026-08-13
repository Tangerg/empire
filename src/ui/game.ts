import { IllegalActionError } from '../core/actions';
import { tacticOptions } from '../core/commanders';
import { Terrains } from '../core/data/terrain';
import { unitDef } from '../core/data/units';
import { careerDef } from '../core/data/careers';
import { idx } from '../core/grid';
import { GameSession } from '../core/session';
import { areEnemies, recruitOptions, unitAt, unitsOf } from '../core/state';
import type {
  Action,
  Coord,
  Direction,
  GameEvent,
  GameState,
  LevelData,
  ReactionStance,
  Unit,
  WeaponId,
} from '../core/types';
import { BoardView, emptyOverlay, type BoardOverlay } from './board';
import { Hud, type HudView } from './hud';

export interface BattleCompletionSnapshot {
  state: GameState;
  events: GameEvent[];
}

export interface GameControllerOptions {
  exitLabel?: string;
  completionLabel?: string;
  onComplete?: (snapshot: BattleCompletionSnapshot) => void;
}

type Mode =
  | { kind: 'idle' }
  | { kind: 'unit'; unit: number }
  | { kind: 'dest'; unit: number; dest: Coord; path: Coord[] }
  | {
      kind: 'target';
      unit: number;
      dest: Coord;
      path: Coord[];
      ability: string;
      weapon?: WeaponId;
      targets: Coord[];
    }
  | { kind: 'tacticTarget'; commander: string; tactic: string; targets: Coord[] }
  | { kind: 'recruit'; at: Coord };

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

  private mode: Mode = { kind: 'idle' };
  private cursor: Coord | null = null;
  private inspect: Unit | null = null;
  private hoverTarget: Coord | null = null;
  private busy = false;
  private messages: string[] = [];
  private aiRunning = false;
  private disposed = false;
  private resizeObserver: ResizeObserver | null = null;

  constructor(
    level: LevelData,
    private readonly onExit: () => void,
    private readonly options: GameControllerOptions = {},
  ) {
    this.session = new GameSession(level);
    this.root.className = 'game-root';

    this.board = new BoardView(this.session.state, {
      onTileClick: (c) => void this.handleClick(c),
      onTileEnter: (c) => this.handleHover(c),
      onLeave: () => {
        this.cursor = null;
        this.hoverTarget = null;
        this.refresh();
      },
      onSecondary: () => this.cancel(),
    });

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
    if (map[k] && this.mode.kind === 'dest') void this.chooseCommand(map[k]);
  };

  /* ------------------------------------------------------------------ input */

  private handleHover(c: Coord): void {
    this.cursor = c;
    const u = unitAt(this.state, c.x, c.y);
    if (u && this.isVisible(u)) this.inspect = u;
    else if (!this.selectedUnit) this.inspect = null;

    if (this.mode.kind === 'target' || this.mode.kind === 'tacticTarget') {
      this.hoverTarget = this.mode.targets.some((t) => t.x === c.x && t.y === c.y) ? c : null;
    } else {
      this.hoverTarget = null;
    }
    this.refresh();
  }

  private get selectedUnit(): Unit | null {
    if (
      this.mode.kind === 'idle' ||
      this.mode.kind === 'recruit' ||
      this.mode.kind === 'tacticTarget'
    ) return null;
    return this.session.unit(this.mode.unit) ?? null;
  }

  private async handleClick(c: Coord): Promise<void> {
    if (this.busy || this.state.phase !== 'playing') return;
    const s = this.state;

    if (this.mode.kind === 'tacticTarget') {
      if (this.mode.targets.some((target) => target.x === c.x && target.y === c.y)) {
        await this.dispatch({
          kind: 'tactic',
          commander: this.mode.commander,
          tactic: this.mode.tactic,
          target: c,
        });
      } else {
        this.cancel();
      }
      return;
    }

    if (this.mode.kind === 'target') {
      if (this.mode.targets.some((t) => t.x === c.x && t.y === c.y)) {
        const { unit, path, ability, weapon } = this.mode;
        await this.dispatch({ kind: 'command', unit, path, command: { ability, weapon, target: c } });
      } else {
        this.cancel();
      }
      return;
    }

    const clicked = unitAt(s, c.x, c.y);

    if (this.mode.kind === 'unit' || this.mode.kind === 'dest') {
      const unit = this.selectedUnit;
      if (unit && this.isHumanTurn && unit.owner === s.currentPlayer && !unit.done) {
        const field = this.session.moveField(unit);
        const i = idx(s.map, c.x, c.y);

        // Clicking an enemy picks a firing position and arms the attack; the
        // forecast appears first, and a second click on the target confirms.
        if (clicked && areEnemies(s, clicked.owner, unit.owner) && this.isVisible(clicked)) {
          const choice = this.bestAttackSpot(unit, clicked);
          if (choice) {
            const path = this.session.pathTo(unit, choice.at) ?? [{ x: unit.x, y: unit.y }];
            const target = { x: clicked.x, y: clicked.y };
            this.mode = {
              kind: 'target',
              unit: unit.id,
              dest: choice.at,
              path,
              ability: 'attack',
              weapon: choice.weapon,
              targets: choice.targets,
            };
            this.hoverTarget = target;
            this.refresh();
            return;
          }
        }

        if (field.stops.has(i)) {
          const path = this.session.pathTo(unit, c);
          if (path) {
            const commands = this.session.commandsAt(unit, c);
            // A lone "wait" needs no menu round-trip.
            if (commands.length === 1 && commands[0].ability === 'wait') {
              await this.dispatch({
                kind: 'command',
                unit: unit.id,
                path,
                command: { ability: 'wait' },
              });
              return;
            }
            this.mode = { kind: 'dest', unit: unit.id, dest: c, path };
            this.refresh();
            return;
          }
        }
      }
    }

    // Fresh selection.
    if (clicked && clicked.owner === s.currentPlayer && !clicked.done && this.isHumanTurn) {
      this.mode = { kind: 'unit', unit: clicked.id };
      this.inspect = clicked;
      this.refresh();
      return;
    }

    if (clicked) {
      this.mode = { kind: 'idle' };
      this.inspect = this.isVisible(clicked) ? clicked : null;
      this.refresh();
      return;
    }

    // Empty production building we own: recruit.
    const i = idx(s.map, c.x, c.y);
    const terrain = Terrains.get(s.map.tiles[i]);
    if (
      this.isHumanTurn &&
      terrain.produces.length > 0 &&
      s.map.owners[i] === s.currentPlayer &&
      recruitOptions(s, c).length > 0
    ) {
      this.mode = { kind: 'recruit', at: c };
      this.refresh();
      return;
    }

    this.mode = { kind: 'idle' };
    this.inspect = null;
    this.refresh();
  }

  /** Reachable tile from which `target` can be hit, preferring safe cover. */
  private bestAttackSpot(
    unit: Unit,
    target: Unit,
  ): { at: Coord; weapon: WeaponId; targets: Coord[]; score: number } | null {
    const s = this.state;
    const field = this.session.moveField(unit);
    let best: { at: Coord; weapon: WeaponId; targets: Coord[]; score: number } | null = null;
    for (const i of field.stops) {
      const at = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
      const moved = at.x !== unit.x || at.y !== unit.y;
      for (const option of this.session.commandsAt(unit, at).filter((entry) => entry.ability === 'attack')) {
        if (!option.weapon) continue;
        if (!option.targets.some((cell) => cell.x === target.x && cell.y === target.y)) continue;
        const plan = this.session.attackPlan(unit, target, at, option.weapon);
        const fc = plan.primaryUnit!;
        const splash = plan.unitHits
          .filter((hit) => !hit.primary)
          .reduce((sum, hit) => sum + hit.damage.damage, 0) +
          plan.structureHits.filter((hit) => !hit.primary).reduce((sum, hit) => sum + hit.forecast.damage, 0);
        const terrain = Terrains.get(s.map.tiles[i]);
        const score =
          fc.strike.damage * 2 +
          splash * 1.25 +
          terrain.defense * 60 -
          (fc.counter?.damage ?? 0) * 1.5 -
          (moved ? 1 : 0);
        if (!best || score > best.score) {
          best = { at, weapon: option.weapon, targets: option.targets, score };
        }
      }
    }
    return best;
  }

  private async chooseCommand(optionKeyOrAbility: string): Promise<void> {
    if (this.mode.kind !== 'dest' || this.busy) return;
    const { unit, dest, path } = this.mode;
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
    this.mode = { kind: 'target', unit, dest, path, ability, weapon, targets: option.targets };
    this.refresh();
  }

  private async chooseTactic(key: string): Promise<void> {
    if (this.busy || !this.isHumanTurn) return;
    const separator = key.indexOf(':');
    if (separator < 1) return;
    const commander = key.slice(0, separator);
    const tactic = key.slice(separator + 1);
    const option = tacticOptions(this.state, commander).find((candidate) => candidate.id === tactic);
    if (!option) return;
    if (option.targets.length === 1) {
      await this.dispatch({ kind: 'tactic', commander, tactic, target: option.targets[0] });
      return;
    }
    this.mode = { kind: 'tacticTarget', commander, tactic, targets: option.targets };
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
    if (this.mode.kind !== 'recruit') return;
    await this.dispatch({ kind: 'recruit', at: this.mode.at, unit: unitType });
  }

  private cancel(): void {
    if (this.busy) return;
    switch (this.mode.kind) {
      case 'target':
        this.mode = { kind: 'dest', unit: this.mode.unit, dest: this.mode.dest, path: this.mode.path };
        break;
      case 'tacticTarget':
        this.mode = { kind: 'idle' };
        break;
      case 'dest':
        this.mode = { kind: 'unit', unit: this.mode.unit };
        break;
      default:
        this.mode = { kind: 'idle' };
        this.inspect = null;
    }
    this.hoverTarget = null;
    this.refresh();
  }

  private cycleIdleUnit(): void {
    const idle = unitsOf(this.state, this.state.currentPlayer).filter((u) => !u.done);
    if (idle.length === 0) return;
    const current = this.mode.kind === 'unit' ? this.mode.unit : -1;
    const at = idle.findIndex((u) => u.id === current);
    const next = idle[(at + 1) % idle.length];
    this.mode = { kind: 'unit', unit: next.id };
    this.inspect = next;
    this.board.centerOn({ x: next.x, y: next.y }, this.scroller);
    this.refresh();
  }

  private undo(): void {
    if (this.busy || !this.session.canUndo) return;
    this.session.undo();
    this.board.setState(this.state);
    this.mode = { kind: 'idle' };
    this.inspect = null;
    this.pushMessage('已撤销上一步');
    this.refresh();
  }

  private restart(): void {
    this.session.restart();
    this.board.setState(this.state);
    this.mode = { kind: 'idle' };
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
    this.mode = { kind: 'idle' };
    this.hoverTarget = null;
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
      this.pushMessage(describeEvent(this.state, e));
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

    const unit = this.selectedUnit;
    if (unit && !this.busy) {
      o.selected = { x: unit.x, y: unit.y };
      if (unit.owner === s.currentPlayer && !unit.done && this.isHumanTurn) {
        if (this.mode.kind === 'unit') {
          for (const i of this.session.moveField(unit).stops) o.move.add(i);
          for (const i of this.session.threatOf(unit)) {
            if (!o.move.has(i)) o.attack.add(i);
          }
          if (this.cursor) {
            const path = this.session.pathTo(unit, this.cursor);
            if (path && this.session.moveField(unit).stops.has(idx(s.map, this.cursor.x, this.cursor.y))) {
              o.path = path;
            }
          }
        } else if (this.mode.kind === 'dest' || this.mode.kind === 'target') {
          o.selected = this.mode.dest;
          o.path = this.mode.path;
          if (this.mode.kind === 'target') {
            const set = this.mode.ability === 'heal' ? o.heal : o.attack;
            for (const t of this.mode.targets) set.add(idx(s.map, t.x, t.y));
            if (this.mode.ability === 'attack' && this.hoverTarget) {
              try {
                const plan = this.session.attackPlan(unit, this.hoverTarget, this.mode.dest, this.mode.weapon);
                for (const cell of plan.affectedCells) o.attack.add(idx(s.map, cell.x, cell.y));
              } catch {
                // Hover may move between valid targets while the overlay refreshes.
              }
            }
          }
        }
      }
    } else if (this.inspect && !this.busy) {
      // Hovering an enemy shows what it can hit next turn.
      if (areEnemies(s, this.inspect.owner, viewer)) {
        for (const i of this.session.threatOf(this.inspect)) o.threat.add(i);
        o.selected = { x: this.inspect.x, y: this.inspect.y };
      }
    }
    if (this.mode.kind === 'tacticTarget') {
      for (const target of this.mode.targets) o.heal.add(idx(s.map, target.x, target.y));
    }

    return o;
  }

  private hudView(): HudView {
    const s = this.state;
    let fcView: HudView['forecast'] = null;

    const unit = this.selectedUnit;
    if (unit && this.mode.kind === 'target' && this.hoverTarget && this.mode.ability === 'attack') {
      const defender = unitAt(s, this.hoverTarget.x, this.hoverTarget.y);
      if (defender) {
        const plan = this.session.attackPlan(unit, this.hoverTarget, this.mode.dest, this.mode.weapon);
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

    const commands =
      this.mode.kind === 'dest' && unit && !this.busy
        ? this.session.commandsAt(unit, this.mode.dest)
        : null;
    const tactics =
      this.isHumanTurn && !this.busy && this.mode.kind === 'idle'
        ? s.commanders
            .filter((commander) => commander.owner === s.currentPlayer)
            .flatMap((commander) =>
              tacticOptions(s, commander.id, this.session.engine.rules.resources).map((option) => ({
                ...option,
                key: `${commander.id}:${option.id}`,
                commander: commander.id,
              })),
            )
        : [];

    return {
      state: s,
      resources: this.session.engine.rules.resources,
      inspect: this.inspect,
      tile: this.cursor,
      forecast: fcView,
      commands,
      tactics,
      reactionUnit:
        unit && unit.owner === s.currentPlayer && !unit.done && this.isHumanTurn ? unit.id : null,
      rankNextThreshold: this.inspect
        ? this.session.engine.rules.progression.nextThreshold(this.inspect.rank)
        : null,
      careerOptions: unit && unit.owner === s.currentPlayer && !unit.done && this.isHumanTurn
        ? this.session.careerOptions(unit)
        : [],
      targeting:
        this.mode.kind === 'target'
          ? this.mode.ability
          : this.mode.kind === 'tacticTarget'
            ? this.mode.tactic
            : null,
      recruitAt: this.mode.kind === 'recruit' ? this.mode.at : null,
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
    switch (this.mode.kind) {
      case 'unit':
        return '点击蓝色格移动，点击敌人直接发起攻击。';
      case 'idle':
        return '点击你的单位开始行动；点击自己的城堡/兵营可以征募（空格切换待命单位）。';
      default:
        return '';
    }
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

function describeEvent(s: GameState, e: GameEvent): string {
  const name = (id: number) => {
    const u = s.units.find((x) => x.id === id);
    return u ? unitDef(u.type).name : '单位';
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
      return `${name(e.unit)} 转职为 ${careerDef(e.to).name}`;
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
