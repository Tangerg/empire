import { idx } from '@empire/battle-engine/grid';
import type { GameSession } from '@empire/battle-engine/session';
import { areEnemies, recruitOptions, unitAt } from '@empire/battle-engine/state';
import type { Action, Coord, GameState, Unit, WeaponId } from '@empire/battle-engine/types';
import type { BoardOverlay } from './board';

/**
 * What the player has picked so far.
 *
 * This used to be a `Mode` union that eleven controller methods took apart:
 * a click meant one thing in one branch, cancelling meant another somewhere
 * else, and the overlay derived a third from the same tag. Thirty-nine sites
 * asked `mode.kind` — so adding a seventh way to select something meant
 * finding all of them, and forgetting one produced a selection that looked
 * fine and behaved like the wrong one.
 *
 * A selection now answers for itself. Each subclass overrides only what it
 * actually means; the base says "no opinion" for the rest, which is the honest
 * answer for a selection with no destination or no target list — not a default
 * rule hiding in the controller.
 */

export interface SelectionContext {
  readonly session: GameSession;
  readonly state: GameState;
  readonly isHumanTurn: boolean;
  /** Tile under the pointer, or null when the pointer left the board. */
  readonly cursor: Coord | null;
  /** Cursor, but only when it is one of this selection's targets. */
  readonly hoverTarget: Coord | null;
  canAct(unit: Unit): boolean;
  isVisible(unit: Unit): boolean;
}

/** Where the selection goes next, plus any order the click completed. */
export interface ClickOutcome {
  readonly selection: Selection;
  readonly action?: Action;
}

const sameCoord = (left: Coord, right: Coord): boolean => left.x === right.x && left.y === right.y;

export abstract class Selection {
  /** Unit this selection is about, when it is about one. */
  get unitId(): number | null {
    return null;
  }

  /** Tiles a click would accept. Hovering one of them previews the outcome. */
  get targets(): readonly Coord[] {
    return [];
  }

  /** Ability or tactic the HUD says we are aiming, while we are aiming it. */
  get targetingLabel(): string | null {
    return null;
  }

  get recruitAt(): Coord | null {
    return null;
  }

  get hint(): string {
    return '';
  }

  /** One step back out of a half-composed order. */
  back(): Selection {
    return IDLE;
  }

  unitIn(context: SelectionContext): Unit | null {
    const id = this.unitId;
    return id === null ? null : context.session.unit(id) ?? null;
  }

  abstract click(context: SelectionContext, at: Coord): ClickOutcome;

  /** What this selection adds to the board. Most selections add nothing. */
  paint(_context: SelectionContext, _overlay: BoardOverlay): void {}
}

/**
 * Nothing picked: a click either selects, inspects, or opens a barracks.
 *
 * Every other selection falls back here, so this is also where the rule
 * "clicking somewhere meaningless clears the board" is written once.
 */
class IdleSelection extends Selection {
  override get hint(): string {
    return '点击你的单位开始行动；点击自己的城堡/兵营可以征募（空格切换待命单位）。';
  }

  click(context: SelectionContext, at: Coord): ClickOutcome {
    return { selection: freshSelection(context, at) };
  }
}

export const IDLE: Selection = new IdleSelection();

/** A unit is picked; the board shows where it can go and what it threatens. */
export class UnitSelection extends Selection {
  constructor(private readonly unit: number) {
    super();
  }

  override get unitId(): number {
    return this.unit;
  }

  override get hint(): string {
    return '点击蓝色格移动，点击敌人直接发起攻击。';
  }

  click(context: SelectionContext, at: Coord): ClickOutcome {
    return commandClick(this, context, at);
  }

  override paint(context: SelectionContext, overlay: BoardOverlay): void {
    const unit = this.unitIn(context);
    if (!unit) return;
    const { session, state, cursor } = context;
    for (const index of session.controlZoneAgainst(unit)) overlay.controlled.add(index);
    const field = session.moveField(unit);
    for (const index of field.stops) overlay.move.add(index);
    for (const index of session.threatOf(unit)) {
      if (!overlay.move.has(index)) overlay.attack.add(index);
    }
    if (cursor && field.stops.has(idx(state.map, cursor.x, cursor.y))) {
      overlay.path = session.pathTo(unit, cursor) ?? overlay.path;
    }
  }
}

/** A destination is chosen and the command menu is open. */
export class DestinationSelection extends Selection {
  constructor(
    private readonly unit: number,
    readonly dest: Coord,
    readonly path: Coord[],
  ) {
    super();
  }

  override get unitId(): number {
    return this.unit;
  }

  override back(): Selection {
    return new UnitSelection(this.unit);
  }

  click(context: SelectionContext, at: Coord): ClickOutcome {
    return commandClick(this, context, at);
  }

  override paint(_context: SelectionContext, overlay: BoardOverlay): void {
    overlay.selected = this.dest;
    overlay.path = this.path;
  }
}

/** The order is chosen and waiting for the tile it lands on. */
export class TargetSelection extends Selection {
  constructor(
    private readonly unit: number,
    readonly dest: Coord,
    readonly path: Coord[],
    readonly ability: string,
    readonly weapon: WeaponId | undefined,
    private readonly candidates: readonly Coord[],
  ) {
    super();
  }

  override get unitId(): number {
    return this.unit;
  }

  override get targets(): readonly Coord[] {
    return this.candidates;
  }

  override get targetingLabel(): string {
    return this.ability;
  }

  override back(): Selection {
    return new DestinationSelection(this.unit, this.dest, this.path);
  }

  click(_context: SelectionContext, at: Coord): ClickOutcome {
    if (!this.candidates.some((candidate) => sameCoord(candidate, at))) return { selection: IDLE };
    return {
      selection: this,
      action: {
        kind: 'command',
        unit: this.unit,
        path: this.path,
        command: { ability: this.ability, weapon: this.weapon, target: at },
      },
    };
  }

  override paint(context: SelectionContext, overlay: BoardOverlay): void {
    overlay.selected = this.dest;
    overlay.path = this.path;
    const set = this.ability === 'heal' ? overlay.heal : overlay.attack;
    for (const target of this.candidates) set.add(idx(context.state.map, target.x, target.y));

    const unit = this.unitIn(context);
    if (this.ability !== 'attack' || !context.hoverTarget || !unit) return;
    const plan = context.session.attackPlan(unit, context.hoverTarget, this.dest, this.weapon);
    for (const cell of plan.affectedCells) overlay.attack.add(idx(context.state.map, cell.x, cell.y));
  }
}

/** A commander's tactic is waiting for the tile it applies to. */
export class TacticTargetSelection extends Selection {
  constructor(
    readonly commander: string,
    readonly tactic: string,
    private readonly candidates: readonly Coord[],
  ) {
    super();
  }

  override get targets(): readonly Coord[] {
    return this.candidates;
  }

  override get targetingLabel(): string {
    return this.tactic;
  }

  click(_context: SelectionContext, at: Coord): ClickOutcome {
    if (!this.candidates.some((candidate) => sameCoord(candidate, at))) return { selection: IDLE };
    return {
      selection: this,
      action: { kind: 'tactic', commander: this.commander, tactic: this.tactic, target: at },
    };
  }

  override paint(context: SelectionContext, overlay: BoardOverlay): void {
    for (const target of this.candidates) overlay.heal.add(idx(context.state.map, target.x, target.y));
  }
}

/** A production site we own, with the recruitment menu open. */
export class RecruitSelection extends Selection {
  constructor(private readonly at: Coord) {
    super();
  }

  override get recruitAt(): Coord {
    return this.at;
  }

  click(context: SelectionContext, at: Coord): ClickOutcome {
    return { selection: freshSelection(context, at) };
  }
}

/* ------------------------------------------------------------------ helpers */

/**
 * What clicking a tile means when no order is half-composed: take command of a
 * unit, look at one, open a barracks, or clear the board.
 */
function freshSelection(context: SelectionContext, at: Coord): Selection {
  const { state, session } = context;
  const clicked = unitAt(state, { x: at.x, y: at.y });
  if (clicked) {
    return context.isHumanTurn && context.canAct(clicked) ? new UnitSelection(clicked.id) : IDLE;
  }

  const index = idx(state.map, at.x, at.y);
  const terrain = session.content.terrains.get(state.map.tiles[index]);
  const canRecruit = context.isHumanTurn &&
    terrain.produces.length > 0 &&
    state.map.owners[index] === state.currentPlayer &&
    recruitOptions(session.rules, state, at).length > 0;
  return canRecruit ? new RecruitSelection(at) : IDLE;
}

/**
 * Clicking while a unit is under command.
 *
 * Clicking an enemy arms an attack from the best firing position rather than
 * making the player walk there first; clicking a reachable tile proposes the
 * move; anything else is a fresh selection.
 */
function commandClick(selection: Selection, context: SelectionContext, at: Coord): ClickOutcome {
  const unit = selection.unitIn(context);
  const { state, session } = context;
  if (!unit || !context.isHumanTurn || !context.canAct(unit)) {
    return { selection: freshSelection(context, at) };
  }

  const clicked = unitAt(state, { x: at.x, y: at.y });
  if (clicked && areEnemies(state, clicked.owner, unit.owner) && context.isVisible(clicked)) {
    const choice = bestAttackSpot(context, unit, clicked);
    if (choice) {
      const path = session.pathTo(unit, choice.at) ?? [{ x: unit.x, y: unit.y }];
      return {
        selection: new TargetSelection(unit.id, choice.at, path, 'attack', choice.weapon, choice.targets),
      };
    }
  }

  if (session.moveField(unit).stops.has(idx(state.map, at.x, at.y))) {
    const path = session.pathTo(unit, at);
    if (path) {
      const commands = session.commandsAt(unit, at);
      // A lone "wait" needs no menu round-trip.
      if (commands.length === 1 && commands[0].ability === 'wait') {
        return {
          selection: IDLE,
          action: { kind: 'command', unit: unit.id, path, command: { ability: 'wait' } },
        };
      }
      return { selection: new DestinationSelection(unit.id, at, path) };
    }
  }

  return { selection: freshSelection(context, at) };
}

interface AttackSpot {
  at: Coord;
  weapon: WeaponId;
  targets: Coord[];
  score: number;
}

/** Reachable tile from which `target` can be hit, preferring safe cover. */
function bestAttackSpot(context: SelectionContext, unit: Unit, target: Unit): AttackSpot | null {
  const { state, session } = context;
  let best: AttackSpot | null = null;
  for (const index of session.moveField(unit).stops) {
    const at = { x: index % state.map.width, y: Math.floor(index / state.map.width) };
    const moved = at.x !== unit.x || at.y !== unit.y;
    for (const option of session.commandsAt(unit, at).filter((entry) => entry.ability === 'attack')) {
      if (!option.weapon) continue;
      if (!option.targets.some((cell) => sameCoord(cell, target))) continue;
      const plan = session.attackPlan(unit, target, at, option.weapon);
      const forecast = plan.primaryUnit;
      if (!forecast) continue;
      const splash = plan.unitHits
        .filter((hit) => !hit.primary)
        .reduce((sum, hit) => sum + hit.damage.damage, 0) +
        plan.structureHits.filter((hit) => !hit.primary).reduce((sum, hit) => sum + hit.forecast.damage, 0);
      const terrain = session.content.terrains.get(state.map.tiles[index]);
      const score =
        forecast.strike.damage * 2 +
        splash * 1.25 +
        terrain.defense * 60 -
        (forecast.counter?.damage ?? 0) * 1.5 -
        (moved ? 1 : 0);
      if (!best || score > best.score) best = { at, weapon: option.weapon, targets: option.targets, score };
    }
  }
  return best;
}
