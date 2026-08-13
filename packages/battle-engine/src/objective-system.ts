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
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';
import { compositeStatus, requireComposite } from './composites';

export type { ObjectiveOutcome } from './types';
export type ObjectiveRole = 'primary' | 'optional' | 'critical';
export type ObjectiveRefreshMode = 'self' | 'children' | 'sequence';

type ObjectiveOf<K extends keyof ObjectiveKindMap> = ObjectiveMeta & ObjectiveKindMap[K];

export interface ObjectiveEvaluationContext {
  readonly state: GameState;
  readonly owner: PlayerId;
  readonly handlers: ObjectiveHandlerRegistry;
  readonly content: ContentCatalog;
  status(objective: Objective): ObjectiveStatus;
  outcome(objective: Objective): ObjectiveOutcome;
}

/** One cohesive strategy owns calculation, presentation, and traversal metadata. */
export interface ObjectiveHandler<K extends keyof ObjectiveKindMap = keyof ObjectiveKindMap> {
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

export class ObjectiveHandlerRegistry {
  private readonly handlers = new Map<keyof ObjectiveKindMap, ObjectiveHandler>();

  register<K extends keyof ObjectiveKindMap>(handler: ObjectiveHandler<K>): this {
    if (this.handlers.has(handler.kind)) throw new Error(`目标处理器已注册：${String(handler.kind)}`);
    this.handlers.set(handler.kind, handler as ObjectiveHandler);
    return this;
  }

  replace<K extends keyof ObjectiveKindMap>(handler: ObjectiveHandler<K>): this {
    this.handlers.set(handler.kind, handler as ObjectiveHandler);
    return this;
  }

  handler<K extends keyof ObjectiveKindMap>(kind: K): ObjectiveHandler<K> {
    const handler = this.handlers.get(kind);
    if (!handler) throw new Error(`未注册目标处理器：${String(kind)}`);
    return handler as ObjectiveHandler<K>;
  }

  kinds(): Array<keyof ObjectiveKindMap> {
    return [...this.handlers.keys()];
  }

  clone(): ObjectiveHandlerRegistry {
    const copy = new ObjectiveHandlerRegistry();
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }

  role(objective: Objective): ObjectiveRole {
    return this.handler(objective.type).role ?? 'primary';
  }

  refreshMode(objective: Objective): ObjectiveRefreshMode {
    return this.handler(objective.type).refresh ?? 'self';
  }

  children(objective: Objective): Objective[] {
    const handler = this.handler(objective.type);
    return handler.children?.(objective as never) ?? [];
  }

  evaluate(
    state: GameState,
    owner: PlayerId,
    objective: Objective,
    content: ContentCatalog = GlobalContentCatalog,
  ): ObjectiveOutcome {
    const status = player(state, owner).objectiveStates[objective.id!]?.status ?? 'active';
    const terminal = terminalOutcome(status);
    if (terminal) return terminal;
    const context = this.context(state, owner, content);
    return this.handler(objective.type).outcome(context, objective as never);
  }

  describe(objective: Objective): string {
    if (objective.label) return objective.label;
    return this.handler(objective.type).describe(objective as never, this);
  }

  progress(state: GameState, owner: PlayerId, objective: Objective, content: ContentCatalog = GlobalContentCatalog): string {
    const context = this.context(state, owner, content);
    const terminal = terminalProgress(context.status(objective));
    if (terminal) return terminal;
    return this.handler(objective.type).progress(context, objective as never);
  }

  private context(state: GameState, owner: PlayerId, content: ContentCatalog): ObjectiveEvaluationContext {
    return {
      state,
      owner,
      handlers: this,
      content,
      status: (objective) => player(state, owner).objectiveStates[objective.id!]?.status ?? 'active',
      outcome: (objective) => this.evaluate(state, owner, objective, content),
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
    outcome: (context, objective) => {
      const children = activeChildren(context, objective);
      if (children.length === 0) return 'pending';
      const outcomes = children.map(context.outcome);
      if (outcomes.includes('failure')) return 'failure';
      return outcomes.every((outcome) => outcome === 'success') ? 'success' : 'pending';
    },
    describe: () => '完成全部目标',
    progress: (context, objective) => {
      const done = objective.objectives.filter((child) => context.status(child) === 'completed').length;
      return `${done}/${objective.objectives.length} 阶段`;
    },
  }))
  .register(objectiveHandler('any', {
    refresh: 'children',
    children: (objective) => objective.objectives,
    outcome: (context, objective) => {
      const children = activeChildren(context, objective);
      if (children.length === 0) return 'pending';
      const outcomes = children.map(context.outcome);
      if (outcomes.includes('success')) return 'success';
      return outcomes.every((outcome) => outcome === 'failure') ? 'failure' : 'pending';
    },
    describe: () => '完成任一目标',
    progress: (context, objective) => {
      const done = objective.objectives.filter((child) => context.status(child) === 'completed').length;
      return `${done}/${objective.objectives.length} 阶段`;
    },
  }))
  .register(objectiveHandler('sequence', {
    refresh: 'sequence',
    children: (objective) => objective.objectives,
    outcome: (context, objective) => {
      const children = activeChildren(context, objective);
      if (children.length === 0) return 'pending';
      const outcomes = children.map(context.outcome);
      if (outcomes.includes('failure')) return 'failure';
      return outcomes.every((outcome) => outcome === 'success') ? 'success' : 'pending';
    },
    describe: () => '完成阶段目标',
    progress: (context, objective) => {
      const done = objective.objectives.filter((child) => context.status(child) === 'completed').length;
      return `${done}/${objective.objectives.length} 阶段`;
    },
  }))
  .register(objectiveHandler('optional', {
    role: 'optional',
    refresh: 'children',
    children: (objective) => [objective.objective],
    outcome: (context, objective) => context.outcome(objective.objective),
    describe: (objective, handlers) => `额外：${handlers.describe(objective.objective)}`,
    progress: (context, objective) => context.handlers.progress(context.state, context.owner, objective.objective, context.content),
  }))
  .register(objectiveHandler('failOn', {
    role: 'critical',
    refresh: 'children',
    children: (objective) => [objective.objective],
    outcome: (context, objective) => conditionMet(context.state, objective.condition, undefined, context.content)
      ? 'failure' : context.outcome(objective.objective),
    describe: (objective, handlers) => handlers.describe(objective.objective),
    progress: (context, objective) => context.handlers.progress(context.state, context.owner, objective.objective, context.content),
  }));
