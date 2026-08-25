import { idx } from './grid';
import {
  type ObjectiveHandlerRegistry,
  type ObjectiveKind,
  type ObjectiveOf,
} from './objective-system';
import { selectUnits } from './scenario';
import { areEnemies, player, unitsOf } from './state';
import type {
  Coord,
  GameState,
  Objective,
  PlayerId,
  StructureId,
} from './types';
import { type ContentCatalog } from './content-pack';
import { KeyedRegistry } from './registry';

export interface AiDestination {
  at: Coord;
  weight: number;
  /** null means every allied unit may pursue this destination. */
  unitIds: ReadonlySet<number> | null;
  /** Non-capturing units ignore ownership-changing destinations. */
  captureOnly: boolean;
  reason: string;
}

/** Story-neutral tactical intent distilled from the active mission graph. */
export interface AiMissionIntent {
  destinations: AiDestination[];
  priorityUnits: ReadonlyMap<number, number>;
  priorityStructures: ReadonlyMap<StructureId, number>;
  protectedUnits: ReadonlyMap<number, number>;
}

export class AiObjectiveAdviceContext {
  readonly destinations: AiDestination[] = [];
  readonly priorityUnits = new Map<number, number>();
  readonly priorityStructures = new Map<StructureId, number>();
  readonly protectedUnits = new Map<number, number>();

  constructor(
    readonly state: GameState,
    readonly owner: PlayerId,
    readonly weight: number,
    readonly content: ContentCatalog,
  ) {}

  destination(
    at: Coord,
    weight: number,
    reason: string,
    options: { unitIds?: Iterable<number>; captureOnly?: boolean } = {},
  ): void {
    this.destinations.push({
      at: { ...at },
      weight: weight * this.weight,
      unitIds: options.unitIds ? new Set(options.unitIds) : null,
      captureOnly: options.captureOnly ?? false,
      reason,
    });
  }

  priorityUnit(id: number, weight: number): void {
    this.priorityUnits.set(id, Math.max(this.priorityUnits.get(id) ?? 0, weight * this.weight));
  }

  priorityStructure(id: StructureId, weight: number): void {
    this.priorityStructures.set(
      id,
      Math.max(this.priorityStructures.get(id) ?? 0, weight * this.weight),
    );
  }

  protect(id: number, weight: number): void {
    this.protectedUnits.set(id, Math.max(this.protectedUnits.get(id) ?? 0, weight * this.weight));
  }
}

export interface AiObjectiveAdvisor<K extends ObjectiveKind = ObjectiveKind> {
  kind: K;
  advise(context: AiObjectiveAdviceContext, objective: ObjectiveOf<K>): void;
}

/** Open strategy registry: content-defined objective kinds can teach the AI their intent. */
export class AiObjectiveAdvisorRegistry extends KeyedRegistry<ObjectiveKind, AiObjectiveAdvisor> {
  constructor() {
    super('AI objective advisor');
  }

  protected keyOf(advisor: AiObjectiveAdvisor): ObjectiveKind {
    return advisor.kind;
  }

  override register<K extends ObjectiveKind>(advisor: AiObjectiveAdvisor<K>): this {
    return super.register(advisor as AiObjectiveAdvisor);
  }

  override replace<K extends ObjectiveKind>(advisor: AiObjectiveAdvisor<K>): this {
    return super.replace(advisor as AiObjectiveAdvisor);
  }

  clone(): AiObjectiveAdvisorRegistry {
    return this.copyInto(new AiObjectiveAdvisorRegistry());
  }
}

function advisor<K extends ObjectiveKind>(
  kind: K,
  advise: AiObjectiveAdvisor<K>['advise'],
): AiObjectiveAdvisor<K> {
  return { kind, advise };
}

function zoneCells(state: GameState, zone: string): Coord[] {
  return state.scenario.zones[zone] ?? [];
}

export const DefaultAiObjectiveAdvisors = new AiObjectiveAdvisorRegistry()
  .register(advisor('routEnemies', (context) => {
    for (const enemy of context.state.units.filter((unit) =>
      areEnemies(context.state, unit.owner, context.owner))) {
      context.priorityUnit(enemy.id, 0.3);
    }
  }))
  .register(advisor('captureHQ', (context) => {
    for (let index = 0; index < context.state.map.tiles.length; index++) {
      const terrain = context.content.terrains.get(context.state.map.tiles[index]);
      const owner = context.state.map.owners[index];
      if (!terrain.hq || !areEnemies(context.state, owner, context.owner)) continue;
      context.destination(
        { x: index % context.state.map.width, y: Math.floor(index / context.state.map.width) },
        8,
        '攻占敌方总部',
        { captureOnly: true },
      );
    }
  }))
  .register(advisor('holdAllVillages', (context) => {
    for (let index = 0; index < context.state.map.tiles.length; index++) {
      const terrain = context.content.terrains.get(context.state.map.tiles[index]);
      if (!terrain.capturable || context.state.map.owners[index] === context.owner) continue;
      context.destination(
        { x: index % context.state.map.width, y: Math.floor(index / context.state.map.width) },
        terrain.hq ? 7 : 4,
        '控制据点',
        { captureOnly: true },
      );
    }
  }))
  .register(advisor('surviveTurns', (context) => {
    for (const unit of unitsOf(context.state, context.owner)) context.protect(unit.id, 1);
  }))
  .register(advisor('eliminate', (context, objective) => {
    for (const target of selectUnits(context.content, context.state, objective.selector)) {
      if (!areEnemies(context.state, target.owner, context.owner)) continue;
      context.priorityUnit(target.id, 5);
      context.destination(target, 3, '追击指定目标');
    }
  }))
  .register(advisor('destroy', (context, objective) => {
    for (const id of objective.structures) {
      const structure = context.state.structures.find((candidate) => candidate.id === id && candidate.hp > 0);
      if (!structure) continue;
      context.priorityStructure(id, 5);
      context.destination(structure, 5, '摧毁任务结构');
    }
  }))
  .register(advisor('neutralizeComposite', (context, objective) => {
    const composite = context.state.composites.find((candidate) => candidate.id === objective.composite);
    if (!composite) return;
    for (const id of composite.parts) {
      const structure = context.state.structures.find((candidate) =>
        candidate.id === id && candidate.hp > 0 && !candidate.disabled);
      if (!structure) continue;
      context.priorityStructure(id, 6);
      context.destination(structure, 6, '瘫痪复合目标部件');
    }
  }))
  .register(advisor('protect', (context, objective) => {
    for (const target of selectUnits(context.content, context.state, objective.selector)) {
      if (target.owner !== context.owner) continue;
      context.protect(target.id, 5);
      context.destination(target, 2.5, '护卫关键单位', { unitIds: unitsOf(context.state, context.owner)
        .filter((unit) => unit.id !== target.id)
        .map((unit) => unit.id) });
    }
  }))
  .register(advisor('escort', (context, objective) => {
    const cells = zoneCells(context.state, objective.zone);
    const escorted = selectUnits(context.content, context.state, objective.selector)
      .filter((unit) => unit.owner === context.owner);
    const unarrived = escorted.filter((unit) =>
      !cells.some((cell) => cell.x === unit.x && cell.y === unit.y));
    for (const unit of unarrived) context.protect(unit.id, 6);
    for (const cell of cells) {
      context.destination(cell, 12, '护送目标撤离', { unitIds: unarrived.map((unit) => unit.id) });
      context.destination(cell, 1.5, '封锁护送终点', {
        unitIds: unitsOf(context.state, context.owner)
          .filter((unit) => !escorted.some((target) => target.id === unit.id))
          .map((unit) => unit.id),
      });
    }
  }))
  .register(advisor('control', (context, objective) => {
    for (const cell of zoneCells(context.state, objective.zone)) {
      if (context.state.map.owners[idx(context.state.map, cell.x, cell.y)] === context.owner) continue;
      context.destination(cell, 8, '控制任务区域', { captureOnly: true });
    }
  }));
DefaultAiObjectiveAdvisors.seal();

function runtimeStatus(state: GameState, owner: PlayerId, objective: Objective) {
  return objective.id ? player(state, owner).objectiveStates[objective.id]?.status ?? 'active' : 'active';
}

function objectiveWeight(handlers: ObjectiveHandlerRegistry, objective: Objective): number {
  const role = handlers.role(objective);
  if (role === 'critical') return 1.4;
  if (role === 'optional') return 0.55;
  return 1;
}

function activeChildren(
  handlers: ObjectiveHandlerRegistry,
  state: GameState,
  owner: PlayerId,
  objective: Objective,
): Objective[] {
  const children = handlers.children(objective);
  const pending = children.filter((child) => {
    const status = runtimeStatus(state, owner, child);
    return status !== 'completed' && status !== 'failed' && status !== 'cancelled' && status !== 'inactive';
  });
  return handlers.refreshMode(objective) === 'sequence' ? pending.slice(0, 1) : pending;
}

/**
 * Port declared by mission planning: the objectives a side is playing for, the
 * catalog their names resolve in, and how each kind advises a planner.
 *
 * Shaped so that `AiPlanningDependencies` — the aggregate every AI entry point
 * is already handed — satisfies it structurally. It used to be three separate
 * parameters *after* the subjects, which is a second call shape for the same
 * three services.
 */
export interface AiMissionRules {
  readonly rules: {
    readonly content: ContentCatalog;
    readonly objectives: ObjectiveHandlerRegistry;
  };
  readonly objectiveAdvisors: AiObjectiveAdvisorRegistry;
}

/** Converts the active objective tree into deterministic, scoreable tactical hints. */
export function buildAiMissionIntent(
  planning: AiMissionRules,
  state: GameState,
  owner: PlayerId,
): AiMissionIntent {
  const { objectiveAdvisors: advisors, rules: { content, objectives: handlers } } = planning;
  const destinations: AiDestination[] = [];
  const priorityUnits = new Map<number, number>();
  const priorityStructures = new Map<StructureId, number>();
  const protectedUnits = new Map<number, number>();

  const visit = (objective: Objective, inheritedWeight: number): void => {
    if (runtimeStatus(state, owner, objective) !== 'active') return;
    const weight = inheritedWeight * objectiveWeight(handlers, objective);
    const children = activeChildren(handlers, state, owner, objective);
    if (children.length > 0) {
      for (const child of children) visit(child, weight);
      return;
    }
    const strategy = advisors.tryGet(objective.type);
    if (!strategy) return;
    const context = new AiObjectiveAdviceContext(state, owner, weight, content);
    strategy.advise(context, objective as never);
    destinations.push(...context.destinations);
    for (const [id, value] of context.priorityUnits) {
      priorityUnits.set(id, Math.max(priorityUnits.get(id) ?? 0, value));
    }
    for (const [id, value] of context.priorityStructures) {
      priorityStructures.set(id, Math.max(priorityStructures.get(id) ?? 0, value));
    }
    for (const [id, value] of context.protectedUnits) {
      protectedUnits.set(id, Math.max(protectedUnits.get(id) ?? 0, value));
    }
  };

  for (const objective of player(state, owner).objectives) visit(objective, 1);
  return { destinations, priorityUnits, priorityStructures, protectedUnits };
}
