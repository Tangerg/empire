import { PlayerEntity } from './domain/player-entity';
import { idx } from './grid';
import {
  ObjectiveHandlers,
  type ObjectiveHandlerRegistry,
  type ObjectiveOutcome,
} from './objective-system';
import { Battlefield } from './domain/battlefield';
import { player, productionTilesOf, unitsOf } from './state';
import type { GameEvent, GameState, Objective, PlayerId, ResourceAmount } from './types';
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';

export type { ObjectiveOutcome } from './objective-system';
export { ObjectiveHandlers, ObjectiveHandlerRegistry } from './objective-system';

export interface VictoryResult {
  team: number | null;
  reason: string;
}

/** A player is out when they have neither units nor any way to make more. */
export function isDefeated(state: GameState, id: PlayerId, content: ContentCatalog = GlobalContentCatalog): boolean {
  return unitsOf(state, id).length === 0 && productionTilesOf(state, id, content).length === 0;
}

function objectiveStatus(state: GameState, owner: PlayerId, objective: Objective) {
  return player(state, owner).objectiveStates[objective.id!]?.status ?? 'active';
}

export function objectiveOutcome(
  state: GameState,
  owner: PlayerId,
  objective: Objective,
  handlers: ObjectiveHandlerRegistry = ObjectiveHandlers,
  content: ContentCatalog = GlobalContentCatalog,
): ObjectiveOutcome {
  return handlers.evaluate(state, owner, objective, content);
}

function transitionObjective(
  state: GameState,
  owner: PlayerId,
  objective: Objective,
  outcome: ObjectiveOutcome,
  emit: (event: GameEvent) => void,
): void {
  if (outcome === 'pending') return;
  const runtime = new PlayerEntity(player(state, owner)).objective(objective.id!);
  if (!runtime.resolve(outcome)) return;
  emit({
    type: 'objectiveChanged',
    player: owner,
    objective: runtime.id,
    status: runtime.status,
    hidden: runtime.hidden,
  });
}

function refreshOne(
  state: GameState,
  owner: PlayerId,
  objective: Objective,
  emit: (event: GameEvent) => void,
  handlers: ObjectiveHandlerRegistry,
  content: ContentCatalog,
): void {
  if (objectiveStatus(state, owner, objective) !== 'active') return;
  const children = handlers.children(objective);
  if (handlers.refreshMode(objective) === 'children') {
    for (const child of children) refreshOne(state, owner, child, emit, handlers, content);
  } else if (handlers.refreshMode(objective) === 'sequence') {
    for (const child of children) {
      const status = objectiveStatus(state, owner, child);
      if (status === 'completed' || status === 'cancelled') continue;
      refreshOne(state, owner, child, emit, handlers, content);
      if (objectiveStatus(state, owner, child) !== 'completed') break;
    }
  }
  transitionObjective(state, owner, objective, handlers.evaluate(state, owner, objective, content), emit);
}

export function refreshObjectiveStates(
  state: GameState,
  emit: (event: GameEvent) => void = () => {},
  handlers: ObjectiveHandlerRegistry = ObjectiveHandlers,
  content: ContentCatalog = GlobalContentCatalog,
): void {
  for (const owner of state.players) {
    for (const objective of owner.objectives) refreshOne(state, owner.id, objective, emit, handlers, content);
  }
}

export function describeObjective(
  objective: Objective,
  handlers: ObjectiveHandlerRegistry = ObjectiveHandlers,
): string {
  return handlers.describe(objective);
}

export function objectiveProgress(
  state: GameState,
  owner: PlayerId,
  objective: Objective,
  handlers: ObjectiveHandlerRegistry = ObjectiveHandlers,
  content: ContentCatalog = GlobalContentCatalog,
): string {
  return handlers.progress(state, owner, objective, content);
}

export function evaluateVictory(
  state: GameState,
  emit: (event: GameEvent) => void = () => {},
  handlers: ObjectiveHandlerRegistry = ObjectiveHandlers,
  content: ContentCatalog = GlobalContentCatalog,
): VictoryResult {
  refreshObjectiveStates(state, emit, handlers, content);

  for (const owner of state.players) {
    if (!owner.alive) continue;
    if (isDefeated(state, owner.id, content)) new PlayerEntity(owner).defeat();
    const criticalFailure = owner.objectives.some(
      (objective) => handlers.role(objective) === 'critical' && objectiveStatus(state, owner.id, objective) === 'failed',
    );
    if (criticalFailure) new PlayerEntity(owner).defeat();
  }

  const living = state.players.filter((owner) => owner.alive);
  const teams = new Set(living.map((owner) => owner.team));
  if (living.length === 0) return { team: null, reason: '全员覆灭' };
  if (teams.size === 1) return { team: living[0].team, reason: '敌军已被全歼' };

  for (const owner of living) {
    const completed = owner.objectives.find(
      (objective) => handlers.role(objective) !== 'optional' && objectiveStatus(state, owner.id, objective) === 'completed',
    );
    if (completed) {
      return { team: owner.team, reason: `${owner.name} 完成目标：${handlers.describe(completed)}` };
    }
  }

  const limit = state.rules.turnLimit;
  if (limit !== null && state.turn > limit) return { team: null, reason: '回合数耗尽，平局' };
  return { team: null, reason: '' };
}

/** Resource grants a player collects at the start of their turn. */
export function turnResourceGrantsFor(
  state: GameState,
  owner: PlayerId,
  content: ContentCatalog = GlobalContentCatalog,
): ResourceAmount[] {
  const totals = new Map<string, number>();
  const add = (grant: ResourceAmount): void => {
    totals.set(grant.resource, (totals.get(grant.resource) ?? 0) + grant.amount);
  };
  for (const grant of state.rules.baseResourceGrants) add(grant);
  for (let index = 0; index < state.map.owners.length; index++) {
    if (state.map.owners[index] !== owner) continue;
    const terrain = content.terrains.get(state.map.tiles[index]);
    for (const grant of terrain.ownerTurnGrants) {
      add({
        resource: grant.resource,
        amount: state.rules.siteResourceOverrides[grant.resource] ?? grant.amount,
      });
    }
  }
  return [...totals].map(([resource, amount]) => ({ resource, amount }));
}

export function healRateAt(
  state: GameState,
  x: number,
  y: number,
  owner: PlayerId,
  content: ContentCatalog = GlobalContentCatalog,
): number {
  const index = idx(state.map, x, y);
  if (!state.rules.healOnOwnedBuilding) return 0;
  if (state.map.owners[index] !== owner) return 0;
  return new Battlefield(state, content).cellAt(x, y).heal;
}
