import { IllegalActionError } from './domain/errors';
import { Battlefield } from './domain/battlefield';
import { UnitEntity } from './domain/unit-entity';
import { sameCoord } from './grid';
import { unitAt } from './state';
import type { ContentCatalog } from './content-pack';
import type { Coord, GameEvent, GameState, PlayerId, Unit } from './types';

/** Port declared by this module; `BattleRuleServices` satisfies it. */
export interface DeploymentRules {
  readonly content: ContentCatalog;
}

/** Port declared by this module; `BattleLifecycle` satisfies it. */
export interface DeploymentLifecycle {
  /** Open the battle: turn one begins and the deployment state is retired. */
  beginPlaying(): void;
}

/** The units one player arranges before the battle, and the cells they may use. */
export interface DeploymentRoster {
  readonly player: PlayerId;
  /** Units this player arranges, in the order the level's zones list them. */
  readonly units: readonly Unit[];
  /**
   * Every cell of every zone this player was given, for highlighting the area.
   *
   * A union, because a level may assign one player several zones — a garrison
   * on the wall and a relief column at the gate. Where a *particular* unit may
   * stand is narrower, and only `deploymentSpots` knows it.
   */
  readonly zone: readonly Coord[];
}

/** One cell a unit may be placed on, and what placing it there would displace. */
export interface DeploymentSpot {
  readonly at: Coord;
  /** The unit standing here, which placing swaps back to the mover's cell. */
  readonly swaps: Unit | null;
}

/** The cells the zone this unit was assigned to offers, or `null` if unassigned. */
function assignedZone(state: GameState, unit: Unit): readonly Coord[] | null {
  const assignment = state.deployment?.assignments.find((entry) =>
    entry.player === state.currentPlayer && entry.unitIds.includes(unit.id));
  if (!assignment) return null;
  return state.scenario.zones[assignment.zone] ?? [];
}

/**
 * The deployment currently open, or `null` when the battle is not deploying.
 *
 * Deployment rearranges units the level already placed; nobody waits in reserve.
 * So a roster is an arrangeable set rather than a queue to be emptied, and
 * confirming is always legal — which is why there is no `canFinish` beside it.
 *
 * Takes no rules: who must be arranged is recorded in the state, and a port
 * field this module never reads would be a lie about what deployment depends on.
 */
export function deploymentRoster(state: GameState): DeploymentRoster | null {
  const deployment = state.deployment;
  if (state.phase !== 'deployment' || !deployment) return null;
  const mine = deployment.assignments.filter((entry) => entry.player === state.currentPlayer);
  if (mine.length === 0) return null;
  const byId = new Map(state.units.map((unit) => [unit.id, unit]));
  return {
    player: state.currentPlayer,
    units: mine.flatMap((entry) => entry.unitIds.flatMap((id) => {
      const unit = byId.get(id);
      return unit ? [unit] : [];
    })),
    zone: mine.flatMap((entry) => state.scenario.zones[entry.zone] ?? []),
  };
}

/**
 * Why this unit may not be placed here, or `null` when it may.
 *
 * The one place the rule lives. The menu keeps the cells this answers `null`
 * for, and the action throws what it answers — so a spot the board offers and a
 * placement the engine accepts cannot drift apart, while each refusal still
 * names its own reason instead of collapsing into "illegal".
 */
export function deploymentRefusal(
  rules: DeploymentRules,
  state: GameState,
  unit: Unit,
  at: Coord,
): string | null {
  if (state.phase !== 'deployment' || !state.deployment) return '当前不在战前部署阶段';
  const zone = assignedZone(state, unit);
  if (!zone) return '该单位不在当前部署编组中';
  if (!zone.some((cell) => sameCoord(cell, at))) return '目标格不在该单位的部署区域内';

  const battlefield = new Battlefield(state, rules.content);
  const occupant = unitAt(state, at);
  const swapping = occupant !== undefined && occupant.id !== unit.id;
  if (swapping && !assignedZone(state, occupant)) return '目标格已被其他单位占据';
  if (!battlefield.cell(at).admits(rules.content.units.get(unit.type).movementClass)) {
    return '该单位无法部署到目标格';
  }
  if (!swapping) return null;

  // A swap has to be legal in both directions, or confirming would strand the
  // displaced unit outside its zone — or on ground it cannot hold.
  const from = { x: unit.x, y: unit.y };
  if (!assignedZone(state, occupant)?.some((cell) => sameCoord(cell, from))) {
    return '交换后会使另一单位离开部署区域';
  }
  if (!battlefield.cell(from).admits(rules.content.units.get(occupant.type).movementClass)) {
    return '交换后另一单位无法站在腾出的格子上';
  }
  return null;
}

/** Where this unit may be placed right now, in the zone's declared order. */
export function deploymentSpots(
  rules: DeploymentRules,
  state: GameState,
  unit: Unit,
): DeploymentSpot[] {
  return (assignedZone(state, unit) ?? []).flatMap((at) => {
    if (deploymentRefusal(rules, state, unit, at)) return [];
    const occupant = unitAt(state, at);
    return [{ at: { ...at }, swaps: occupant && occupant.id !== unit.id ? occupant : null }];
  });
}

/** Place a deploying unit, swapping with whoever holds the cell. */
export function deployUnit(
  rules: DeploymentRules,
  state: GameState,
  unit: Unit,
  at: Coord,
  emit: (event: GameEvent) => void,
): void {
  const refusal = deploymentRefusal(rules, state, unit, at);
  if (refusal) throw new IllegalActionError(refusal);
  const from = { x: unit.x, y: unit.y };
  if (sameCoord(from, at)) return;
  const occupant = unitAt(state, at);
  if (occupant && occupant.id !== unit.id) new UnitEntity(occupant).moveTo(from);
  new UnitEntity(unit).moveTo(at);
  emit({ type: 'unitDeployed', unit: unit.id, from, to: { ...at } });
}

/**
 * Confirm one side's arrangement; the last side to confirm opens the battle.
 *
 * Lives here rather than in the action handler so that `state.deployment` has
 * exactly one module reading it. The handler kept the order and the index, and
 * the menu kept the assignments, which is two owners for one aggregate.
 */
export function confirmDeployment(
  lifecycle: DeploymentLifecycle,
  state: GameState,
  emit: (event: GameEvent) => void,
): void {
  const deployment = state.deployment;
  if (state.phase !== 'deployment' || !deployment) {
    throw new IllegalActionError('当前不在战前部署阶段');
  }
  emit({ type: 'deploymentConfirmed', player: state.currentPlayer });
  deployment.currentIndex++;
  if (deployment.currentIndex < deployment.order.length) {
    state.currentPlayer = deployment.order[deployment.currentIndex];
    return;
  }
  lifecycle.beginPlaying();
}
