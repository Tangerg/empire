import { chooseAction } from '../core/ai';
import { commandOptions, IllegalActionError } from '../core/actions';
import { forecast } from '../core/combat';
import { Terrains } from '../core/data/terrain';
import { unitDef } from '../core/data/units';
import { idx } from '../core/grid';
import { GameSession } from '../core/session';
import { areEnemies, recruitOptions, unitAt, unitsOf } from '../core/state';
import { visibleTiles, isUnitVisible } from '../core/vision';
import type { Action, Coord, GameEvent, GameState, LevelData, Unit } from '../core/types';
import { BoardView, emptyOverlay, type BoardOverlay } from './board';
import { Hud, type HudView } from './hud';

type Mode =
  | { kind: 'idle' }
  | { kind: 'unit'; unit: number }
  | { kind: 'dest'; unit: number; dest: Coord; path: Coord[] }
  | { kind: 'target'; unit: number; dest: Coord; path: Coord[]; ability: string; targets: Coord[] }
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

  constructor(
    level: LevelData,
    private readonly onExit: () => void,
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
      onCancel: () => this.cancel(),
      onEndTurn: () => void this.endTurn(),
      onUndo: () => this.undo(),
      onRestart: () => this.restart(),
      onRecruit: (u) => void this.recruit(u),
      onExit: () => this.exit(),
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
  }

  dispose(): void {
    this.disposed = true;
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

    if (this.mode.kind === 'target') {
      this.hoverTarget = this.mode.targets.some((t) => t.x === c.x && t.y === c.y) ? c : null;
    } else {
      this.hoverTarget = null;
    }
    this.refresh();
  }

  private get selectedUnit(): Unit | null {
    if (this.mode.kind === 'idle' || this.mode.kind === 'recruit') return null;
    return this.session.unit(this.mode.unit) ?? null;
  }

  private async handleClick(c: Coord): Promise<void> {
    if (this.busy || this.state.phase !== 'playing') return;
    const s = this.state;

    if (this.mode.kind === 'target') {
      if (this.mode.targets.some((t) => t.x === c.x && t.y === c.y)) {
        const { unit, path, ability } = this.mode;
        await this.dispatch({ kind: 'command', unit, path, command: { ability, target: c } });
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

        // Clicking an enemy inside reach: auto-pick a firing position.
        if (clicked && areEnemies(s, clicked.owner, unit.owner) && this.isVisible(clicked)) {
          const spot = this.bestAttackSpot(unit, clicked);
          if (spot) {
            const path = this.session.pathTo(unit, spot) ?? [{ x: unit.x, y: unit.y }];
            await this.dispatch({
              kind: 'command',
              unit: unit.id,
              path,
              command: { ability: 'attack', target: { x: clicked.x, y: clicked.y } },
            });
            return;
          }
        }

        if (field.stops.has(i)) {
          const path = this.session.pathTo(unit, c);
          if (path) {
            const commands = commandOptions(s, unit, c);
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
  private bestAttackSpot(unit: Unit, target: Unit): Coord | null {
    const s = this.state;
    const def = unitDef(unit.type);
    const field = this.session.moveField(unit);
    let best: { at: Coord; score: number } | null = null;
    for (const i of field.stops) {
      const at = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
      const moved = at.x !== unit.x || at.y !== unit.y;
      if (moved && !def.attackAfterMove) continue;
      const d = Math.abs(at.x - target.x) + Math.abs(at.y - target.y);
      if (d < def.minRange || d > def.maxRange) continue;
      const fc = forecast(s, unit, target, at);
      const terrain = Terrains.get(s.map.tiles[i]);
      const score =
        fc.strike.damage * 2 +
        terrain.defense * 60 -
        (fc.counter?.damage ?? 0) * 1.5 -
        (moved ? 1 : 0);
      if (!best || score > best.score) best = { at, score };
    }
    return best?.at ?? null;
  }

  private async chooseCommand(ability: string): Promise<void> {
    if (this.mode.kind !== 'dest' || this.busy) return;
    const { unit, dest, path } = this.mode;
    const u = this.session.unit(unit);
    if (!u) return;
    const option = commandOptions(this.state, u, dest).find((o) => o.ability === ability);
    if (!option) return;

    if (option.selfTargeted) {
      await this.dispatch({ kind: 'command', unit, path, command: { ability } });
      return;
    }
    if (option.targets.length === 1) {
      await this.dispatch({
        kind: 'command',
        unit,
        path,
        command: { ability, target: option.targets[0] },
      });
      return;
    }
    this.mode = { kind: 'target', unit, dest, path, ability, targets: option.targets };
    this.refresh();
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
          if (at) await this.board.animateHit(at, e.damage, e.killed);
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
        const action = chooseAction(this.state);

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
    return isUnitVisible(this.state, me, u);
  }

  private overlay(): BoardOverlay {
    const o = emptyOverlay();
    const s = this.state;
    const viewer = this.human?.id ?? s.currentPlayer;

    if (s.rules.fog) {
      o.visible = visibleTiles(s, viewer);
      for (const u of s.units) if (!isUnitVisible(s, viewer, u, o.visible)) o.hiddenUnits.add(u.id);
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

    return o;
  }

  private hudView(): HudView {
    const s = this.state;
    let fcView: HudView['forecast'] = null;

    const unit = this.selectedUnit;
    if (unit && this.mode.kind === 'target' && this.hoverTarget && this.mode.ability === 'attack') {
      const defender = unitAt(s, this.hoverTarget.x, this.hoverTarget.y);
      if (defender) {
        fcView = { fc: forecast(s, unit, defender, this.mode.dest), attacker: unit, defender };
      }
    }

    const commands =
      this.mode.kind === 'dest' && unit && !this.busy
        ? commandOptions(s, unit, this.mode.dest)
        : null;

    return {
      state: s,
      inspect: this.inspect,
      tile: this.cursor,
      forecast: fcView,
      commands,
      targeting: this.mode.kind === 'target' ? this.mode.ability : null,
      recruitAt: this.mode.kind === 'recruit' ? this.mode.at : null,
      hint: this.hint(),
      busy: this.busy,
      canUndo: this.session.canUndo,
      messages: this.messages,
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
    case 'counter':
      return `反击造成 ${e.damage} 点伤害${e.killed ? '，我方阵亡' : ''}`;
    case 'heal':
      return `${name(e.source)} 治疗了 ${e.amount} 点生命`;
    case 'capture':
      return e.captured ? `${pname(e.player)} 占领了 (${e.at.x}, ${e.at.y})` : '占领进度提升';
    case 'recruit':
      return `${pname(s.currentPlayer)} 征募了 ${name(e.unit)}`;
    case 'income':
      return `${pname(e.player)} 获得 ${e.amount} 金币`;
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
