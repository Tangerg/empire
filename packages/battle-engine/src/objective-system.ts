import { idx } from './grid';
import { conditionMet, selectUnits } from './scenario';
import { areEnemies, hqTilesOf, player, unitsOf } from './state';
import type {
  GameState,
  Objective,
  ObjectiveKindMap,
  ObjectiveMeta,
  ObjectiveOutcome,
  ObjectiveStatus,
  PlayerId,
} from './types';
import { type ContentCatalog } from './content-pack';
import type { ScenarioConditionHandlerRegistry } from './scenario';
import { compositeStatus, requireComposite } from './composites';
import { KeyedRegistry } from './registry';

export type { ObjectiveOutcome } from './types';
export type ObjectiveRole = 'primary' | 'optional' | 'critical';
export type ObjectiveRefreshMode = 'self' | 'children' | 'sequence';

/** The discriminant every objective is keyed by, named once for every consumer. */
export type ObjectiveKind = Extract<keyof ObjectiveKindMap, string>;

type ObjectiveOf<K extends ObjectiveKind> = ObjectiveMeta & ObjectiveKindMap[K];

/**
 * Port declared by this module: `failOn` embeds a scenario condition, so
 * objective evaluation legitimately depends on the condition registry.
 */
export interface ObjectiveRules {
  readonly content: ContentCatalog;
  readonly scenarioConditions: ScenarioConditionHandlerRegistry;
}

export interface ObjectiveEvaluationContext {
  readonly state: GameState;
  readonly owner: PlayerId;
  readonly handlers: ObjectiveHandlerRegistry;
  readonly content: ContentCatalog;
  readonly scenarioConditions: ScenarioConditionHandlerRegistry;
  status(objective: Objective): ObjectiveStatus;
  outcome(objective: Objective): ObjectiveOutcome;
}

/** One cohesive strategy owns calculation, presentation, and traversal metadata. */
export interface ObjectiveHandler<K extends ObjectiveKind = ObjectiveKind> {
  readonly kind: K;
  readonly role?: ObjectiveRole;
  readonly refresh?: ObjectiveRefreshMode;
  children?(objective: ObjectiveOf<K>): Objective[];
  outcome(context: ObjectiveEvaluationContext, objective: ObjectiveOf<K>): ObjectiveOutcome;
  describe(objective: ObjectiveOf<K>, handlers: ObjectiveHandlerRegistry): string;
  progress(context: ObjectiveEvaluationContext, objective: ObjectiveOf<K>): string;
}

function terminalOutcome(status: ObjectiveStatus): ObjectiveOutcome | null {
  if (status === 'completed') return 'success';
  if (status === 'failed') return 'failure';
  if (status === 'inactive' || status === 'cancelled') return 'pending';
  return null;
}

function terminalProgress(status: ObjectiveStatus): string | null {
  if (status === 'completed') return '已完成';
  if (status === 'failed') return '已失败';
  if (status === 'inactive') return '未激活';
  if (status === 'cancelled') return '已取消';
  return null;
}

export class ObjectiveHandlerRegistry extends KeyedRegistry<ObjectiveKind, ObjectiveHandler> {
  constructor() {
    super('objective handler');
  }

  protected keyOf(handler: ObjectiveHandler): ObjectiveKind {
    return handler.kind;
  }

  override register<K extends ObjectiveKind>(handler: ObjectiveHandler<K>): this {
    return super.register(handler as ObjectiveHandler);
  }

  override replace<K extends ObjectiveKind>(handler: ObjectiveHandler<K>): this {
    return super.replace(handler as ObjectiveHandler);
  }

  clone(): ObjectiveHandlerRegistry {
    return this.copyInto(new ObjectiveHandlerRegistry());
  }

  role(objective: Objective): ObjectiveRole {
    return this.get(objective.type).role ?? 'primary';
  }

  refreshMode(objective: Objective): ObjectiveRefreshMode {
    return this.get(objective.type).refresh ?? 'self';
  }

  children(objective: Objective): Objective[] {
    return this.get(objective.type).children?.(objective as never) ?? [];
  }

  evaluate(
    rules: ObjectiveRules,
    state: GameState,
    owner: PlayerId,
    objective: Objective,
  ): ObjectiveOutcome {
    const status = player(state, owner).objectiveStates[objective.id!]?.status ?? 'active';
    const terminal = terminalOutcome(status);
    if (terminal) return terminal;
    const context = this.context(rules, state, owner);
    return this.get(objective.type).outcome(context, objective as never);
  }

  describe(objective: Objective): string {
    if (objective.label) return objective.label;
    return this.get(objective.type).describe(objective as never, this);
  }

  progress(rules: ObjectiveRules, state: GameState, owner: PlayerId, objective: Objective): string {
    const context = this.context(rules, state, owner);
    const terminal = terminalProgress(context.status(objective));
    if (terminal) return terminal;
    return this.get(objective.type).progress(context, objective as never);
  }

  private context(
    rules: ObjectiveRules,
    state: GameState,
    owner: PlayerId,
  ): ObjectiveEvaluationContext {
    return {
      state,
      owner,
      handlers: this,
      content: rules.content,
      scenarioConditions: rules.scenarioConditions,
      status: (objective) => player(state, owner).objectiveStates[objective.id!]?.status ?? 'active',
      outcome: (objective) => this.evaluate(rules, state, owner, objective),
    };
  }
}

function objectiveHandler<K extends keyof ObjectiveKindMap>(
  kind: K,
  behavior: Omit<ObjectiveHandler<K>, 'kind'>,
): ObjectiveHandler<K> {
  return { kind, ...behavior };
}

function zoneCells(state: GameState, id: string) {
  return state.scenario.zones[id] ?? [];
}

function lostHQ(state: GameState, id: PlayerId, content: ContentCatalog): boolean {
  const owner = state.players.find((candidate) => candidate.id === id);
  return Boolean(owner?.startedWithHQ) && hqTilesOf(state, id, content).length === 0;
}

function activeChildren(context: ObjectiveEvaluationContext, objective: Objective): Objective[] {
  return context.handlers.children(objective).filter((child) => {
    const status = context.status(child);
    return context.handlers.role(child) !== 'optional' && status !== 'inactive' && status !== 'cancelled';
  });
}

const pendingProgress = () => '进行中';

/**
 * How the three compound objectives fold their children.
 *
 * `all` and `sequence` are the same fold under different refresh rules, and
 * were written out twice, character for character; all three shared one line of
 * progress text in three copies. A compound objective differs from its siblings
 * in exactly one thing — whether one success is enough — so that is the only
 * thing stated per kind.
 */
interface CompoundObjective {
  readonly objectives: Objective[];
}

const foldChildren = (settle: (outcomes: ObjectiveOutcome[]) => ObjectiveOutcome) =>
  (context: ObjectiveEvaluationContext, objective: CompoundObjective & Objective): ObjectiveOutcome => {
    const children = activeChildren(context, objective);
    if (children.length === 0) return 'pending';
    return settle(children.map(context.outcome));
  };

/** Every child must succeed, and one failure decides the whole immediately. */
const everyChildSucceeds = foldChildren((outcomes) => {
  if (outcomes.includes('failure')) return 'failure';
  return outcomes.every((outcome) => outcome === 'success') ? 'success' : 'pending';
});

/** One success is enough; only a clean sweep of failures loses. */
const someChildSucceeds = foldChildren((outcomes) => {
  if (outcomes.includes('success')) return 'success';
  return outcomes.every((outcome) => outcome === 'failure') ? 'failure' : 'pending';
});

const stageProgress = (context: ObjectiveEvaluationContext, objective: CompoundObjective): string => {
  const done = objective.objectives.filter((child) => context.status(child) === 'completed').length;
  return `${done}/${objective.objectives.length} 阶段`;
};

export const ObjectiveHandlers = new ObjectiveHandlerRegistry()
  .register(objectiveHandler('routEnemies', {
    outcome: ({ state, owner }) => state.players
      .filter((candidate) => areEnemies(state, candidate.id, owner))
      .every((candidate) => unitsOf(state, candidate.id).length === 0) ? 'success' : 'pending',
    describe: () => '歼灭所有敌军',
    progress: ({ state, owner }) => {
      const left = state.players
        .filter((candidate) => areEnemies(state, candidate.id, owner))
        .reduce((sum, candidate) => sum + unitsOf(state, candidate.id).length, 0);
      return `剩余敌军 ${left}`;
    },
  }))
  .register(objectiveHandler('captureHQ', {
    outcome: ({ state, owner, content }) => {
      const contenders = state.players.filter(
        (candidate) => areEnemies(state, candidate.id, owner) && candidate.startedWithHQ,
      );
      return contenders.length > 0 && contenders.every((candidate) => lostHQ(state, candidate.id, content))
        ? 'success' : 'pending';
    },
    describe: () => '攻占敌方城堡',
    progress: ({ state, owner, content }) => {
      const left = state.players
        .filter((candidate) => areEnemies(state, candidate.id, owner))
        .reduce((sum, candidate) => sum + hqTilesOf(state, candidate.id, content).length, 0);
      return `敌方城堡 ${left}`;
    },
  }))
  .register(objectiveHandler('holdAllVillages', {
    outcome: ({ state, owner, content }) => {
      const sites = state.map.tiles
        .map((terrain, index) => ({ terrain: content.terrains.get(terrain), index }))
        .filter((entry) => entry.terrain.capturable);
      return sites.length > 0 && sites.every((entry) => state.map.owners[entry.index] === owner)
        ? 'success' : 'pending';
    },
    describe: () => '控制全部据点',
    progress: ({ state, owner, content }) => {
      const sites = state.map.tiles.map((terrain, index) => ({ terrain, index })).filter(
        (entry) => content.terrains.get(entry.terrain).capturable,
      );
      return `${sites.filter((entry) => state.map.owners[entry.index] === owner).length}/${sites.length} 据点`;
    },
  }))
  .register(objectiveHandler('surviveTurns', {
    outcome: ({ state }, objective) => state.turn > objective.turns ? 'success' : 'pending',
    describe: (objective) => `坚守 ${objective.turns} 回合`,
    progress: ({ state }, objective) => `${Math.min(state.turn, objective.turns)}/${objective.turns} 回合`,
  }))
  .register(objectiveHandler('eliminate', {
    outcome: ({ state, content }, objective) => selectUnits(state, objective.selector, content).length === 0 ? 'success' : 'pending',
    describe: () => '消灭指定目标',
    progress: ({ state, content }, objective) => `剩余 ${selectUnits(state, objective.selector, content).length}`,
  }))
  .register(objectiveHandler('destroy', {
    outcome: ({ state }, objective) => objective.structures.every((id) => {
      const structure = state.structures.find((candidate) => candidate.id === id);
      return !structure || structure.hp <= 0;
    }) ? 'success' : 'pending',
    describe: () => '摧毁指定结构',
    progress: ({ state }, objective) => {
      const left = objective.structures.filter((id) => {
        const structure = state.structures.find((candidate) => candidate.id === id);
        return structure && structure.hp > 0;
      }).length;
      return `剩余结构 ${left}`;
    },
  }))
  .register(objectiveHandler('neutralizeComposite', {
    outcome: ({ state }, objective) => {
      const composite = requireComposite(state, objective.composite);
      const threshold = objective.minimumNeutralized ?? composite.minimumNeutralized;
      return compositeStatus(state, objective.composite).neutralized >= threshold ? 'success' : 'pending';
    },
    describe: () => '瘫痪复合战场目标',
    progress: ({ state }, objective) => {
      const composite = requireComposite(state, objective.composite);
      const status = compositeStatus(state, objective.composite);
      return `${status.neutralized}/${objective.minimumNeutralized ?? composite.minimumNeutralized} 部件`;
    },
  }))
  .register(objectiveHandler('protect', {
    outcome: ({ state, content }, objective) => {
      if (selectUnits(state, objective.selector, content).length < objective.minimumAlive) return 'failure';
      return state.turn > objective.untilTurn ? 'success' : 'pending';
    },
    describe: (objective) => `保护目标至第 ${objective.untilTurn} 回合`,
    progress: ({ state, content }, objective) =>
      `${selectUnits(state, objective.selector, content).length}/${objective.minimumAlive} 存活`,
  }))
  .register(objectiveHandler('escort', {
    outcome: ({ state, content }, objective) => {
      const cells = zoneCells(state, objective.zone);
      const arrived = selectUnits(state, objective.selector, content).filter((unit) =>
        cells.some((cell) => cell.x === unit.x && cell.y === unit.y)).length;
      return arrived >= objective.count ? 'success' : 'pending';
    },
    describe: () => '护送目标抵达区域',
    progress: ({ state, content }, objective) => {
      const arrived = selectUnits(state, objective.selector, content).filter((unit) =>
        zoneCells(state, objective.zone).some((cell) => cell.x === unit.x && cell.y === unit.y)).length;
      return `${arrived}/${objective.count} 抵达`;
    },
  }))
  .register(objectiveHandler('control', {
    outcome: ({ state, owner }, objective) => {
      const cells = zoneCells(state, objective.zone);
      return cells.length > 0 && cells.every(
        (cell) => state.map.owners[idx(state.map, cell.x, cell.y)] === owner,
      ) ? 'success' : 'pending';
    },
    describe: () => '控制指定区域',
    progress: ({ state, owner }, objective) => {
      const cells = zoneCells(state, objective.zone);
      const mine = cells.filter(
        (cell) => state.map.owners[idx(state.map, cell.x, cell.y)] === owner,
      ).length;
      return `${mine}/${cells.length} 区域格`;
    },
  }))
  .register(objectiveHandler('score', {
    outcome: ({ state }, objective) => {
      const value = state.scenario.variables[objective.variable];
      return typeof value === 'number' && value >= objective.atLeast ? 'success' : 'pending';
    },
    describe: (objective) => `达成计数 ${objective.atLeast}`,
    progress: ({ state }, objective) =>
      `${Number(state.scenario.variables[objective.variable] ?? 0)}/${objective.atLeast}`,
  }))
  .register(objectiveHandler('interact', {
    outcome: ({ state }, objective) =>
      state.scenario.variables[objective.variable] === objective.equals ? 'success' : 'pending',
    describe: () => '完成战场交互',
    progress: pendingProgress,
  }))
  .register(objectiveHandler('all', {
    refresh: 'children',
    children: (objective) => objective.objectives,
    outcome: everyChildSucceeds,
    describe: () => '完成全部目标',
    progress: stageProgress,
  }))
  .register(objectiveHandler('any', {
    refresh: 'children',
    children: (objective) => objective.objectives,
    outcome: someChildSucceeds,
    describe: () => '完成任一目标',
    progress: stageProgress,
  }))
  // The same fold as `all`; the difference is that a sequence reopens its
  // stages one at a time, which is `refresh`, not arithmetic.
  .register(objectiveHandler('sequence', {
    refresh: 'sequence',
    children: (objective) => objective.objectives,
    outcome: everyChildSucceeds,
    describe: () => '完成阶段目标',
    progress: stageProgress,
  }))
  .register(objectiveHandler('optional', {
    role: 'optional',
    refresh: 'children',
    children: (objective) => [objective.objective],
    outcome: (context, objective) => context.outcome(objective.objective),
    describe: (objective, handlers) => `额外：${handlers.describe(objective.objective)}`,
    progress: (context, objective) => context.handlers.progress(context, context.state, context.owner, objective.objective),
  }))
  .register(objectiveHandler('failOn', {
    role: 'critical',
    refresh: 'children',
    children: (objective) => [objective.objective],
    outcome: (context, objective) =>
      conditionMet(context, context.state, objective.condition)
        ? 'failure' : context.outcome(objective.objective),
    describe: (objective, handlers) => handlers.describe(objective.objective),
    progress: (context, objective) => context.handlers.progress(context, context.state, context.owner, objective.objective),
  }));
