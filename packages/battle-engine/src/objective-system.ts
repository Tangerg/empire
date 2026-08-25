import { sameCoord } from './grid';
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
import { PayloadReferences } from './payload-references';
import { KeyedRegistry } from './registry';

export type { ObjectiveOutcome } from './types';
export type ObjectiveRole = 'primary' | 'optional' | 'critical';
export type ObjectiveRefreshMode = 'self' | 'children' | 'sequence';

/** The discriminant every objective is keyed by, named once for every consumer. */
export type ObjectiveKind = Extract<keyof ObjectiveKindMap, string>;

/** One objective of a known kind: the shared meta, plus that kind's own payload. */
export type ObjectiveOf<K extends ObjectiveKind> = ObjectiveMeta & ObjectiveKindMap[K];

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
  /** The names this objective writes down, for whoever has to resolve them. */
  references?(objective: ObjectiveOf<K>): PayloadReferences;
  outcome(context: ObjectiveEvaluationContext, objective: ObjectiveOf<K>): ObjectiveOutcome;
  /**
   * What this *kind* of objective is called, with no instance to look at.
   *
   * Required, because someone always needs it before an objective exists: the
   * editor's picker offered nine hand-written labels of its own, so an objective
   * kind a content pack registered could not be authored at all — the same defect
   * the facing buttons in that panel had, cured there and left standing here.
   */
  readonly label: string;
  /**
   * This particular objective in words, defaulting to `label`.
   *
   * Twelve of the seventeen shipped kinds returned a constant here, which is the
   * label written a second time in the same object. Only the five that read the
   * instance — a turn count, a threshold, a nested objective — say anything the
   * label cannot.
   */
  describe?(objective: ObjectiveOf<K>, handlers: ObjectiveHandlerRegistry): string;
  progress(context: ObjectiveEvaluationContext, objective: ObjectiveOf<K>): string;
}

/**
 * The status a player's record gives one objective, defaulting to active.
 *
 * Written out three times — twice here and once in `victory.ts` — each time as
 * `objectiveStates[objective.id!]?.status ?? 'active'`, where the assertion was
 * covering for the one case the `?.` already handled: an objective with no id is
 * one nobody assigned an id to, so there is no record of it to consult.
 */
export function objectiveStatusOf(
  state: GameState,
  owner: PlayerId,
  objective: Objective,
): ObjectiveStatus {
  const records = player(state, owner).objectiveStates;
  return (objective.id === undefined ? undefined : records[objective.id])?.status ?? 'active';
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

  /** What this objective points at, or nothing for a kind nobody registered. */
  references(objective: Objective): PayloadReferences {
    return this.tryGet(objective.type)?.references?.(objective as never) ?? new PayloadReferences();
  }

  evaluate(
    rules: ObjectiveRules,
    state: GameState,
    owner: PlayerId,
    objective: Objective,
  ): ObjectiveOutcome {
    const status = objectiveStatusOf(state, owner, objective);
    const terminal = terminalOutcome(status);
    if (terminal) return terminal;
    const context = this.context(rules, state, owner);
    return this.get(objective.type).outcome(context, objective as never);
  }

  describe(objective: Objective): string {
    if (objective.label) return objective.label;
    const handler = this.get(objective.type);
    return handler.describe?.(objective as never, this) ?? handler.label;
  }

  /** Every kind installed in this engine, with the name each goes by. */
  kinds(): { type: ObjectiveKind; label: string }[] {
    return this.keys().map((type) => ({ type, label: this.get(type).label }));
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
      status: (objective) => objectiveStatusOf(state, owner, objective),
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

function lostHQ(content: ContentCatalog, state: GameState, id: PlayerId): boolean {
  const owner = state.players.find((candidate) => candidate.id === id);
  return Boolean(owner?.startedWithHQ) && hqTilesOf(content, state, id).length === 0;
}

function activeChildren(context: ObjectiveEvaluationContext, objective: Objective): Objective[] {
  return context.handlers.children(objective).filter((child) => {
    const status = context.status(child);
    return context.handlers.role(child) !== 'optional' && status !== 'inactive' && status !== 'cancelled';
  });
}

const pendingProgress = () => '进行中';

/** What a payload points at, built by the handler that scores it. */
const points = () => new PayloadReferences();

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
    label: '歼灭所有敌军',
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
      return contenders.length > 0 && contenders.every((candidate) => lostHQ(content, state, candidate.id))
        ? 'success' : 'pending';
    },
    label: '攻占敌方城堡',
    progress: ({ state, owner, content }) => {
      const left = state.players
        .filter((candidate) => areEnemies(state, candidate.id, owner))
        .reduce((sum, candidate) => sum + hqTilesOf(content, state, candidate.id).length, 0);
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
    label: '控制全部据点',
    progress: ({ state, owner, content }) => {
      const sites = state.map.tiles.map((terrain, index) => ({ terrain, index })).filter(
        (entry) => content.terrains.get(entry.terrain).capturable,
      );
      return `${sites.filter((entry) => state.map.owners[entry.index] === owner).length}/${sites.length} 据点`;
    },
  }))
  .register(objectiveHandler('surviveTurns', {
    outcome: ({ state }, objective) => state.turn > objective.turns ? 'success' : 'pending',
    label: '坚守回合',
    describe: (objective) => `坚守 ${objective.turns} 回合`,
    progress: ({ state }, objective) => `${Math.min(state.turn, objective.turns)}/${objective.turns} 回合`,
  }))
  .register(objectiveHandler('eliminate', {
    references: (objective) => points().selector(objective.selector),
    outcome: ({ state, content }, objective) => selectUnits(content, state, objective.selector).length === 0 ? 'success' : 'pending',
    label: '消灭指定目标',
    progress: ({ state, content }, objective) => `剩余 ${selectUnits(content, state, objective.selector).length}`,
  }))
  .register(objectiveHandler('destroy', {
    references: (objective) => objective.structures.reduce(
      (cited, id) => cited.structure(id),
      points(),
    ),
    outcome: ({ state }, objective) => objective.structures.every((id) => {
      const structure = state.structures.find((candidate) => candidate.id === id);
      return !structure || structure.hp <= 0;
    }) ? 'success' : 'pending',
    label: '摧毁指定结构',
    progress: ({ state }, objective) => {
      const left = objective.structures.filter((id) => {
        const structure = state.structures.find((candidate) => candidate.id === id);
        return structure && structure.hp > 0;
      }).length;
      return `剩余结构 ${left}`;
    },
  }))
  .register(objectiveHandler('neutralizeComposite', {
    references: (objective) => {
      const cited = points().composite(objective.composite);
      const threshold = objective.minimumNeutralized;
      return threshold === undefined || (Number.isInteger(threshold) && threshold >= 1)
        ? cited
        : cited.fault('瘫痪数量必须 >= 1');
    },
    outcome: ({ state }, objective) => {
      const composite = requireComposite(state, objective.composite);
      const threshold = objective.minimumNeutralized ?? composite.minimumNeutralized;
      return compositeStatus(state, objective.composite).neutralized >= threshold ? 'success' : 'pending';
    },
    label: '瘫痪复合战场目标',
    progress: ({ state }, objective) => {
      const composite = requireComposite(state, objective.composite);
      const status = compositeStatus(state, objective.composite);
      return `${status.neutralized}/${objective.minimumNeutralized ?? composite.minimumNeutralized} 部件`;
    },
  }))
  .register(objectiveHandler('protect', {
    references: (objective) => {
      const cited = points().selector(objective.selector);
      return objective.minimumAlive >= 1 && objective.untilTurn >= 1
        ? cited
        : cited.fault('保护目标的人数和截止回合必须 >= 1');
    },
    outcome: ({ state, content }, objective) => {
      if (selectUnits(content, state, objective.selector).length < objective.minimumAlive) return 'failure';
      return state.turn > objective.untilTurn ? 'success' : 'pending';
    },
    label: '保护目标',
    describe: (objective) => `保护目标至第 ${objective.untilTurn} 回合`,
    progress: ({ state, content }, objective) =>
      `${selectUnits(content, state, objective.selector).length}/${objective.minimumAlive} 存活`,
  }))
  .register(objectiveHandler('escort', {
    references: (objective) => {
      const cited = points().zone(objective.zone).selector(objective.selector);
      return objective.count >= 1 ? cited : cited.fault('护送目标的抵达人数必须 >= 1');
    },
    outcome: ({ state, content }, objective) => {
      const cells = zoneCells(state, objective.zone);
      const arrived = selectUnits(content, state, objective.selector).filter((unit) =>
        cells.some((cell) => sameCoord(cell, unit))).length;
      return arrived >= objective.count ? 'success' : 'pending';
    },
    label: '护送目标抵达区域',
    progress: ({ state, content }, objective) => {
      const arrived = selectUnits(content, state, objective.selector).filter((unit) =>
        zoneCells(state, objective.zone).some((cell) => sameCoord(cell, unit))).length;
      return `${arrived}/${objective.count} 抵达`;
    },
  }))
  .register(objectiveHandler('control', {
    references: (objective) => points().zone(objective.zone),
    outcome: ({ state, owner }, objective) => {
      const cells = zoneCells(state, objective.zone);
      return cells.length > 0 && cells.every(
        (cell) => state.map.owners[idx(state.map, cell.x, cell.y)] === owner,
      ) ? 'success' : 'pending';
    },
    label: '控制指定区域',
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
    label: '达成计数',
    describe: (objective) => `达成计数 ${objective.atLeast}`,
    progress: ({ state }, objective) =>
      `${Number(state.scenario.variables[objective.variable] ?? 0)}/${objective.atLeast}`,
  }))
  .register(objectiveHandler('interact', {
    outcome: ({ state }, objective) =>
      state.scenario.variables[objective.variable] === objective.equals ? 'success' : 'pending',
    label: '完成战场交互',
    progress: pendingProgress,
  }))
  .register(objectiveHandler('all', {
    refresh: 'children',
    children: (objective) => objective.objectives,
    outcome: everyChildSucceeds,
    label: '完成全部目标',
    progress: stageProgress,
  }))
  .register(objectiveHandler('any', {
    refresh: 'children',
    children: (objective) => objective.objectives,
    outcome: someChildSucceeds,
    label: '完成任一目标',
    progress: stageProgress,
  }))
  // The same fold as `all`; the difference is that a sequence reopens its
  // stages one at a time, which is `refresh`, not arithmetic.
  .register(objectiveHandler('sequence', {
    refresh: 'sequence',
    children: (objective) => objective.objectives,
    outcome: everyChildSucceeds,
    label: '完成阶段目标',
    progress: stageProgress,
  }))
  .register(objectiveHandler('optional', {
    role: 'optional',
    refresh: 'children',
    children: (objective) => [objective.objective],
    outcome: (context, objective) => context.outcome(objective.objective),
    label: '可选目标',
    describe: (objective, handlers) => `额外：${handlers.describe(objective.objective)}`,
    progress: (context, objective) => context.handlers.progress(context, context.state, context.owner, objective.objective),
  }))
  .register(objectiveHandler('failOn', {
    role: 'critical',
    refresh: 'children',
    children: (objective) => [objective.objective],
    // The embedded condition was invisible to every walker in the engine: the
    // objective tree stops at children, and the condition tree starts at
    // triggers, so a losing condition naming an unknown zone reached the battle.
    references: (objective) => points().condition(objective.condition),
    outcome: (context, objective) =>
      conditionMet(context, context.state, objective.condition)
        ? 'failure' : context.outcome(objective.objective),
    label: '失败条件',
    describe: (objective, handlers) => handlers.describe(objective.objective),
    progress: (context, objective) => context.handlers.progress(context, context.state, context.owner, objective.objective),
  }))
  .seal();
