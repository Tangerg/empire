import { DomainInvariantError } from './domain/errors';
import { UnitEntity } from './domain/unit-entity';
import { PlayerEntity } from './domain/player-entity';
import { inBounds } from './grid';
import { addTerrainOverlay, removeTerrainOverlay } from './overlays';
import { player, removeUnit, spawnUnit } from './state';
import { addStatus, removeStatus } from './statuses';
import { damageStructure, repairStructure } from './structures';
import { changeUnitResource } from './progression';
import { BattleAggregate } from './domain/battle-aggregate';
import { Battlefield } from './domain/battlefield';
import { MapLayers } from './domain/map-layers';
import { ScenarioTriggerEntity, TriggerOccurrence, type ScenarioTiming } from './domain/scenario-trigger';
import { announceUnitDeparture, type UnitDepartureRules } from './unit-departure';
import { returnUnitToField } from './unit-return';
import { forceMoveUnit, teleportUnit } from './forced-movement';
import {
  type BattleResourceSystem,
  } from './resources';
import type {
  Coord,
  GameEvent,
  GameState,
  MovementClass,
  ScenarioCondition,
  ScenarioConditionKindMap,
  ScenarioEffect,
  ScenarioEffectKindMap,
  ScenarioValue,
  Unit,
  UnitSelector,
  BattlefieldMarker,
  MarkerSelector,
} from './types';
import { type ContentCatalog } from './content-pack';
import { PayloadReferences } from './payload-references';
import { SplitMixRandom, type RandomSource } from './random';
import { KeyedRegistry } from './registry';
import { changeMorale, surrenderUnit } from './morale';
import { addEngagementRule, removeEngagementRule } from './engagement';
import { compositeStatus, moveComposite } from './composites';
import { withdrawTransportPassengers } from './transports';

export type { ScenarioTiming } from './domain/scenario-trigger';
type ConditionKind = Extract<keyof ScenarioConditionKindMap, string>;
type EffectKind = Extract<keyof ScenarioEffectKindMap, string>;

/** What a payload points at, built by the handler that runs it. */
const points = () => new PayloadReferences();

function zone(state: GameState, id: string) {
  const cells = state.scenario.zones[id];
  if (!cells) throw new DomainInvariantError(`unknown scenario zone "${id}"`);
  return cells;
}

function inZone(state: GameState, unit: Unit, zoneId: string): boolean {
  return zone(state, zoneId).some((cell) => cell.x === unit.x && cell.y === unit.y);
}

export function selectUnits(content: ContentCatalog, state: GameState, selector: UnitSelector): Unit[] {
  return state.units.filter((unit) => {
    if (selector.ids && !selector.ids.includes(unit.id)) return false;
    if (selector.keys && (!unit.key || !selector.keys.includes(unit.key))) return false;
    if (selector.owner !== undefined && unit.owner !== selector.owner) return false;
    if (selector.zone !== undefined && !inZone(state, unit, selector.zone)) return false;
    if (selector.anyTags?.length) {
      const tags = content.units.get(unit.type).tags;
      if (!selector.anyTags.some((tag) => tags.includes(tag))) return false;
    }
    return true;
  });
}

export function selectMarkers(state: GameState, selector: MarkerSelector): BattlefieldMarker[] {
  return state.markers.filter((marker) => {
    if (selector.ids && !selector.ids.includes(marker.id)) return false;
    if (selector.kind !== undefined && marker.kind !== selector.kind) return false;
    if (selector.owner !== undefined && marker.owner !== selector.owner) return false;
    if (selector.zone !== undefined && !zone(state, selector.zone).some((cell) =>
      cell.x === marker.at.x && cell.y === marker.at.y)) return false;
    return true;
  });
}

function compare(
  left: ScenarioValue | undefined,
  operation: 'eq' | 'neq' | 'gte' | 'lte',
  right: ScenarioValue,
): boolean {
  switch (operation) {
    case 'eq': return left === right;
    case 'neq': return left !== right;
    case 'gte': return typeof left === 'number' && typeof right === 'number' && left >= right;
    case 'lte': return typeof left === 'number' && typeof right === 'number' && left <= right;
  }
}

function numberVariable(state: GameState, key: string): number {
  const value = state.scenario.variables[key] ?? 0;
  if (typeof value !== 'number') throw new DomainInvariantError(`scenario variable "${key}" is not numeric`);
  return value;
}

/* -------------------------------------------------------- condition strategies */

export class ScenarioConditionContext {
  constructor(
    readonly state: GameState,
    private readonly registry: ScenarioConditionHandlerRegistry,
    readonly content: ContentCatalog,
    /** Seeded stream owned by the ruleset; reproducible across replays. */
    readonly random: RandomSource,
  ) {}

  evaluate(condition: ScenarioCondition): boolean {
    return this.registry.evaluate(this.state, condition, this.content);
  }
}

export interface ScenarioConditionHandler<K extends ConditionKind = ConditionKind> {
  kind: K;
  /**
   * Conditions this one is composed of, for anything that has to walk the tree.
   *
   * Declared like an objective's children rather than inferred: two traversals
   * used to hardcode `all | any | not`, so a rule pack's own compound condition
   * had its children silently unvisited by both.
   */
  children?(condition: ScenarioConditionKindMap[K]): ScenarioCondition[];
  /** The names this condition writes down, for whoever has to resolve them. */
  references?(condition: ScenarioConditionKindMap[K]): PayloadReferences;
  evaluate(context: ScenarioConditionContext, condition: ScenarioConditionKindMap[K]): boolean;
}

export class ScenarioConditionHandlerRegistry extends KeyedRegistry<ConditionKind, ScenarioConditionHandler> {
  /**
   * The random source is injected once per ruleset instead of threaded through
   * every `evaluate` call: the registry is already per-engine and cloneable, so
   * this keeps randomness swappable without widening the condition signature.
   */
  constructor(readonly random: RandomSource) {
    super('scenario condition handler');
  }

  protected keyOf(handler: ScenarioConditionHandler): ConditionKind {
    return handler.kind;
  }

  override register<K extends ConditionKind>(handler: ScenarioConditionHandler<K>): this {
    return super.register(handler as ScenarioConditionHandler);
  }

  override replace<K extends ConditionKind>(handler: ScenarioConditionHandler<K>): this {
    return super.replace(handler as ScenarioConditionHandler);
  }

  evaluate(state: GameState, condition: ScenarioCondition, content: ContentCatalog): boolean {
    return this.get(condition.type)
      .evaluate(new ScenarioConditionContext(state, this, content, this.random), condition as never);
  }

  children(condition: ScenarioCondition): ScenarioCondition[] {
    return this.get(condition.type).children?.(condition as never) ?? [];
  }

  /**
   * What this condition points at, or nothing for a kind nobody registered: an
   * unknown shape has no knowable references, and the missing kind is the
   * finding worth reporting.
   */
  references(condition: ScenarioCondition): PayloadReferences {
    return this.tryGet(condition.type)?.references?.(condition as never) ?? points();
  }

  clone(random: RandomSource = this.random): ScenarioConditionHandlerRegistry {
    return this.copyInto(new ScenarioConditionHandlerRegistry(random));
  }
}

const conditionHandler = <K extends ConditionKind>(
  kind: K,
  evaluate: ScenarioConditionHandler<K>['evaluate'],
  declares: Omit<ScenarioConditionHandler<K>, 'kind' | 'evaluate'> = {},
): ScenarioConditionHandler<K> => ({ kind, evaluate, ...declares });

export const ScenarioConditionHandlers = new ScenarioConditionHandlerRegistry(SplitMixRandom)
  .register(conditionHandler('turnAtLeast', ({ state }, condition) => state.turn >= condition.turn))
  .register(conditionHandler('turnCycle', ({ state }, condition) => {
    if (!Number.isInteger(condition.every) || condition.every < 1) return false;
    const offset = Math.round(condition.offset ?? 1);
    return state.turn >= offset && (state.turn - offset) % condition.every === 0;
  }, {
    references: (condition) => Number.isInteger(condition.every) && condition.every >= 1
      ? points()
      : points().fault('循环回合条件的间隔必须是正整数'),
  }))
  .register(conditionHandler('chance', ({ state, random }, condition) =>
    random.chance(state, condition.stream ?? 'scenario.chance', condition.percent)))
  .register(conditionHandler('currentPlayer', ({ state }, condition) => state.currentPlayer === condition.player, {
    references: (condition) => points().player(condition.player),
  }))
  .register(
    conditionHandler('variable', ({ state }, condition) =>
      compare(state.scenario.variables[condition.key], condition.op, condition.value),
    ),
  )
  .register(
    conditionHandler('unitInZone', ({ state, content }, condition) =>
      selectUnits(content, state, {
        owner: condition.owner,
        zone: condition.zone,
        anyTags: condition.anyTags,
      }).length > 0,
      { references: (condition) => points().zone(condition.zone) },
    ),
  )
  .register(conditionHandler('unitCount', ({ state, content }, condition) =>
    compare(selectUnits(content, state, condition.selector).length, condition.op, condition.value), {
    references: (condition) => points().selector(condition.selector),
  }))
  .register(conditionHandler('unitHealth', ({ state, content }, condition) => {
    const ratios = selectUnits(content, state, condition.selector)
      .map((unit) => unit.hp / content.units.get(unit.type).maxHp);
    if (ratios.length === 0) return false;
    if (condition.aggregate === 'any') return ratios.some((value) => compare(value, condition.op, condition.value));
    if (condition.aggregate === 'all') return ratios.every((value) => compare(value, condition.op, condition.value));
    return compare(ratios.reduce((sum, value) => sum + value, 0) / ratios.length, condition.op, condition.value);
  }, {
    references: (condition) => points().selector(condition.selector),
  }))
  .register(conditionHandler('markerCount', ({ state }, condition) =>
    compare(selectMarkers(state, condition.selector).length, condition.op, condition.value), {
    references: (condition) => points().selector(condition.selector),
  }))
  .register(conditionHandler('eventCount', ({ state }, condition) =>
    compare(state.scenario.eventCounts[condition.event] ?? 0, condition.op, condition.value)))
  .register(
    conditionHandler('structure', ({ state }, condition) => {
      const structure = state.structures.find((candidate) => candidate.id === condition.id);
      if (!structure) return condition.state === 'destroyed';
      if (condition.state === 'destroyed') return structure.hp <= 0;
      if (condition.state === 'disabled') return structure.hp > 0 && structure.disabled;
      return structure.hp > 0 && !structure.disabled;
    }, { references: (condition) => points().structure(condition.id) }),
  )
  .register(conditionHandler('composite', ({ state }, condition) =>
    compositeStatus(state, condition.id).state === condition.state, {
    references: (condition) => points().composite(condition.id),
  }))
  .register(
    conditionHandler('objective', ({ state }, condition) =>
      player(state, condition.player).objectiveStates[condition.id]?.status === condition.status,
      {
        references: (condition) => points()
          .player(condition.player)
          .objective(condition.player, condition.id),
      },
    ),
  )
  .register(conditionHandler(
    'all',
    (context, condition) => condition.conditions.every((child) => context.evaluate(child)),
    { children: (condition) => condition.conditions },
  ))
  .register(conditionHandler(
    'any',
    (context, condition) => condition.conditions.some((child) => context.evaluate(child)),
    { children: (condition) => condition.conditions },
  ))
  .register(conditionHandler(
    'not',
    (context, condition) => !context.evaluate(condition.condition),
    { children: (condition) => [condition.condition] },
  ));
ScenarioConditionHandlers.seal();

/**
 * Ports declared by this module. The composition-level `BattleRuleServices`
 * satisfies both structurally, so neither side needs to import the other.
 */
export interface ScenarioConditionRules {
  readonly content: ContentCatalog;
  readonly scenarioConditions: ScenarioConditionHandlerRegistry;
}

export interface ScenarioRules extends ScenarioConditionRules, UnitDepartureRules {
  readonly resources: BattleResourceSystem;
  readonly scenarioEffects: ScenarioEffectHandlerRegistry;
}

export function conditionMet(
  rules: ScenarioConditionRules,
  state: GameState,
  condition: ScenarioCondition,
): boolean {
  return rules.scenarioConditions.evaluate(state, condition, rules.content);
}

/* ----------------------------------------------------------- effect strategies */

export class ScenarioEffectContext {
  private battlefieldCache: Battlefield | null = null;
  private layerCache: MapLayers | null = null;

  constructor(
    /** The whole ruleset: an effect may reach any rule the engine composes. */
    readonly rules: ScenarioRules,
    readonly state: GameState,
    readonly emit: (event: GameEvent) => void,
  ) {}

  get content(): ContentCatalog {
    return this.rules.content;
  }

  get resources(): BattleResourceSystem {
    return this.rules.resources;
  }

  /**
   * Map projection shared by everything this one effect does.
   *
   * Two effects built a fresh one inside a `find` predicate — twice per
   * candidate tile — which rebuilt every spatial index of the battlefield to
   * answer one question about one cell.
   */
  get battlefield(): Battlefield {
    return this.battlefieldCache ??= new Battlefield(this.state, this.content);
  }

  /** The writable side of the same map: ground, height, edges and cover. */
  get layers(): MapLayers {
    return this.layerCache ??= new MapLayers(this.state.map);
  }

  zone(id: string) {
    return zone(this.state, id);
  }

  select(selector: UnitSelector): Unit[] {
    return selectUnits(this.content, this.state, selector);
  }

  /**
   * The selected units, each still on the field when its turn to be acted on
   * comes round.
   *
   * An effect that displaces, demoralises or breaks units changes the very
   * selection it is walking: one unit's rout can take another with it. Three
   * effects copied the same re-check by hand, and any effect that forgot it
   * would act on a unit that had already left.
   */
  *standing(selector: UnitSelector): Generator<Unit> {
    for (const unit of this.select(selector)) {
      if (this.state.units.some((candidate) => candidate.id === unit.id)) yield unit;
    }
  }

  /**
   * A tile in this zone that a unit of `movementClass` can be dropped onto, or
   * null when the zone is full. Deterministic: the same zone always fills in
   * reading order, so reinforcements arrive in a replayable place.
   */
  openCellIn(zoneId: string, movementClass: MovementClass): Coord | null {
    return this.zone(zoneId)
      .slice()
      .sort((left, right) => left.y - right.y || left.x - right.x)
      .find((cell) => this.battlefield.cell(cell).canReceive(movementClass)) ?? null;
  }

  selectMarkers(selector: MarkerSelector): BattlefieldMarker[] {
    return selectMarkers(this.state, selector);
  }

  changeObjective(
    ownerId: number,
    id: string,
    patch: { status?: 'active' | 'completed' | 'cancelled'; hidden?: boolean },
  ): void {
    const runtime = new PlayerEntity(player(this.state, ownerId)).objective(id);
    if (patch.status) runtime.changeStatus(patch.status);
    if (patch.hidden !== undefined) runtime.changeVisibility(patch.hidden);
    this.emit({
      type: 'objectiveChanged',
      player: ownerId,
      objective: id,
      status: runtime.status,
      hidden: runtime.hidden,
    });
  }
}

export interface ScenarioEffectHandler<K extends EffectKind = EffectKind> {
  kind: K;
  /**
   * The names this effect writes down, for whoever has to resolve them.
   *
   * Applying the effect refuses an unknown name at the moment of commitment;
   * this is the same knowledge stated where it can be *asked* instead, so a
   * level document can be linted before anybody plays it.
   */
  references?(effect: ScenarioEffectKindMap[K]): PayloadReferences;
  apply(context: ScenarioEffectContext, effect: ScenarioEffectKindMap[K]): void;
}

export class ScenarioEffectHandlerRegistry extends KeyedRegistry<EffectKind, ScenarioEffectHandler> {
  constructor() {
    super('scenario effect handler');
  }

  protected keyOf(handler: ScenarioEffectHandler): EffectKind {
    return handler.kind;
  }

  override register<K extends EffectKind>(handler: ScenarioEffectHandler<K>): this {
    return super.register(handler as ScenarioEffectHandler);
  }

  override replace<K extends EffectKind>(handler: ScenarioEffectHandler<K>): this {
    return super.replace(handler as ScenarioEffectHandler);
  }

  apply(rules: ScenarioRules, state: GameState, effect: ScenarioEffect, emit: (event: GameEvent) => void): void {
    this.get(effect.type).apply(new ScenarioEffectContext(rules, state, emit), effect as never);
  }

  /** What this effect points at, or nothing for a kind nobody registered. */
  references(effect: ScenarioEffect): PayloadReferences {
    return this.tryGet(effect.type)?.references?.(effect as never) ?? points();
  }

  clone(): ScenarioEffectHandlerRegistry {
    return this.copyInto(new ScenarioEffectHandlerRegistry());
  }
}

const effectHandler = <K extends EffectKind>(
  kind: K,
  apply: ScenarioEffectHandler<K>['apply'],
  declares: Omit<ScenarioEffectHandler<K>, 'kind' | 'apply'> = {},
): ScenarioEffectHandler<K> => ({ kind, apply, ...declares });

/** The four effects that steer an objective differ only in the change they make. */
const steersObjective = (effect: { player: number; id: string }): PayloadReferences =>
  points().objective(effect.player, effect.id);

/** Height is a whole number of steps, whether an effect sets it or shifts it. */
const wholeElevation = (cited: PayloadReferences, height: number): PayloadReferences =>
  Number.isInteger(height) ? cited : cited.fault('动态海拔必须使用整数');

export const ScenarioEffectHandlers = new ScenarioEffectHandlerRegistry()
  .register(effectHandler('setVariable', ({ state }, effect) => {
    state.scenario.variables[effect.key] = effect.value;
  }))
  .register(effectHandler('addVariable', ({ state }, effect) => {
    state.scenario.variables[effect.key] = numberVariable(state, effect.key) + effect.amount;
  }))
  .register(effectHandler('addStatus', (context, effect) => {
    for (const unit of context.select(effect.selector)) {
      addStatus(context.content, unit, { id: effect.status, remaining: effect.duration }, context.emit);
    }
  }, {
    references: (effect) => points().status(effect.status).selector(effect.selector),
  }))
  .register(effectHandler('removeStatus', (context, effect) => {
    for (const unit of context.select(effect.selector)) removeStatus(unit, effect.status, context.emit);
  }, {
    references: (effect) => points().status(effect.status).selector(effect.selector),
  }))
  .register(effectHandler('changeUnitOwner', (context, effect) => {
    for (const unit of context.select(effect.selector)) {
      const entity = new UnitEntity(unit);
      const from = entity.owner;
      if (from === effect.owner) continue;
      entity.changeOwner(effect.owner);
      context.emit({ type: 'unitOwnerChanged', unit: unit.id, from, to: effect.owner });
    }
  }, {
    references: (effect) => points().selector(effect.selector),
  }))
  .register(effectHandler('spawnUnits', (context, effect) => {
    for (const source of effect.units) {
      player(context.state, source.owner);
      if (!inBounds(context.state.map, source.x, source.y)) {
        throw new DomainInvariantError(`spawn cell out of bounds: ${source.x},${source.y}`);
      }
      if (source.key && context.state.units.some((unit) => unit.key === source.key)) {
        throw new DomainInvariantError(`unit key already exists: "${source.key}"`);
      }
      if (source.key && context.state.markers.some((marker) => marker.fallenUnit?.key === source.key)) {
        throw new DomainInvariantError(`unit key is reserved by a fallen unit: "${source.key}"`);
      }
      const definition = context.content.units.get(source.unit);
      const cell = context.battlefield.cell(source);
      // Ground this unit could never stand on is the level's mistake, and it is
      // the same on turn one as on turn twenty.
      if (!cell.admits(definition.movementClass)) {
        throw new DomainInvariantError(`unit "${source.unit}" cannot spawn at ${source.x},${source.y}`);
      }
      // Someone standing on the arrival tile is not a mistake at all — it is
      // ordinary play, and a player parked on a reinforcement cell used to end
      // the battle with an unclassified throw that the shell could only rethrow.
      // Deliberate boundary: that reinforcement does not arrive, and the ones
      // behind it still do. Its siblings already answer this way — a rescue
      // stops at a full rally zone, a revival returns null for a taken tile.
      if (cell.occupant) continue;
      const unit = spawnUnit(context.content, context.state, source.unit, source.owner, source, {
        done: !(effect.ready ?? false),
        source,
      });
      context.emit({
        type: 'unitSpawned',
        unit: unit.id,
        at: { x: unit.x, y: unit.y },
        reason: effect.reason ?? 'reinforcement',
      });
    }
  }, {
    references: (effect) => effect.units.reduce(
      (cited, source) => cited.unitType(source.unit).player(source.owner).cell(source),
      points(),
    ),
  }))
  .register(effectHandler('withdrawUnits', (context, effect) => {
    for (const unit of [...context.select(effect.selector)]) {
      const at = { x: unit.x, y: unit.y };
      const aggregate = new BattleAggregate(context.state, context.content);
      const marker = effect.leaveCorpse
        ? aggregate.createCorpse(unit)
        : aggregate.createUnitMarker(unit, 'withdrawn');
      context.emit({ type: 'markerAdded', marker: marker.id, kind: marker.kind, at: marker.at });
      removeUnit(context.state, unit.id);
      withdrawTransportPassengers(context.state, unit.id, { at, kind: 'withdrawn' }, context.emit);
      announceUnitDeparture(context.rules, context.state, unit, context.emit);
      context.emit({ type: 'unitWithdrawn', unit: unit.id, at });
    }
  }, {
    references: (effect) => points().selector(effect.selector),
  }))
  .register(effectHandler('reviveMarkers', (context, effect) => {
    const ratio = Math.max(0.01, Math.min(1, effect.hpPercent ?? 0.5));
    for (const marker of [...context.selectMarkers(effect.selector)]) {
      const fallen = marker.fallenUnit;
      if (!fallen) continue;
      returnUnitToField(context.state, marker, {
        at: marker.at,
        owner: effect.owner,
        hp: Math.max(1, Math.round(context.content.units.get(fallen.type).maxHp * ratio)),
      }, context.emit);
    }
  }, {
    references: (effect) => points().selector(effect.selector),
  }))
  .register(effectHandler('removeMarkers', (context, effect) => {
    for (const marker of [...context.selectMarkers(effect.selector)]) {
      context.state.markers.splice(context.state.markers.indexOf(marker), 1);
      context.emit({ type: 'markerRemoved', marker: marker.id, kind: marker.kind, at: marker.at });
    }
  }, {
    references: (effect) => points().selector(effect.selector),
  }))
  .register(effectHandler('setPlayerTeam', (context, effect) => {
    const target = player(context.state, effect.player);
    const from = target.team;
    if (from === effect.team) return;
    target.team = effect.team;
    context.emit({ type: 'playerTeamChanged', player: target.id, from, to: effect.team });
  }, {
    references: (effect) => points().player(effect.player),
  }))
  .register(effectHandler('forceMove', (context, effect) => {
    for (const unit of context.standing(effect.selector)) {
      forceMoveUnit(context.rules, context.state, {
        unit: unit.id,
        source: effect.source,
        mode: effect.mode,
        distance: effect.distance,
        collisionDamage: effect.collisionDamage,
      }, context.emit);
    }
  }, {
    references: (effect) => {
      const cited = points().selector(effect.selector).cell(effect.source);
      return Number.isInteger(effect.distance) && effect.distance >= 0
        ? cited
        : cited.fault('强制位移距离必须是非负整数');
    },
  }))
  .register(effectHandler('teleportUnits', (context, effect) => {
    for (const unit of context.select(effect.selector)) {
      const destination = context.openCellIn(effect.zone, context.content.units.get(unit.type).movementClass);
      if (destination) teleportUnit(context.content, context.state, unit.id, destination, context.emit);
    }
  }, {
    references: (effect) => points().zone(effect.zone).selector(effect.selector),
  }))
  .register(effectHandler('addOverlay', (context, effect) => {
    addTerrainOverlay(
      context.content,
      context.state,
      {
        id: effect.id,
        type: effect.overlay,
        cells: context.zone(effect.zone),
        remainingRounds: effect.rounds ?? null,
      },
      context.emit,
    );
  }, {
    references: (effect) => points().overlay(effect.overlay).zone(effect.zone),
  }))
  .register(effectHandler('removeOverlay', ({ state, emit }, effect) => {
    removeTerrainOverlay(state, effect.id, emit);
  }))
  .register(effectHandler('activateObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { status: 'active' });
  }, { references: steersObjective }))
  .register(effectHandler('cancelObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { status: 'cancelled' });
  }, { references: steersObjective }))
  .register(effectHandler('completeObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { status: 'completed' });
  }, { references: steersObjective }))
  .register(effectHandler('revealObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { hidden: false });
  }, { references: steersObjective }))
  .register(effectHandler('changeUnitResource', (context, effect) => {
    for (const unit of context.select(effect.selector)) {
      changeUnitResource(context.resources, unit, effect.resource, effect.amount, context.emit);
    }
  }, {
    references: (effect) => points().selector(effect.selector),
  }))
  .register(effectHandler('changeMorale', (context, effect) => {
    for (const unit of context.standing(effect.selector)) {
      changeMorale(context.rules, context.state, unit.id, effect.amount, effect.reason ?? 'scenario', context.emit);
    }
  }, {
    references: (effect) => points().selector(effect.selector),
  }))
  .register(effectHandler('surrenderUnits', (context, effect) => {
    for (const unit of context.standing(effect.selector)) {
      surrenderUnit(context.rules, context.state, unit.id, effect.to, context.emit);
    }
  }, {
    references: (effect) => points().selector(effect.selector),
  }))
  .register(effectHandler('restoreWithdrawnUnits', (context, effect) => {
    for (const marker of [...context.selectMarkers(effect.selector)]) {
      const fallen = marker.fallenUnit;
      if (!fallen) continue;
      const destination = context.openCellIn(effect.zone, context.content.units.get(fallen.type).movementClass);
      // A full zone stops the rescue rather than skipping past it: the ones
      // still in the ground stay there until there is room.
      if (!destination) break;
      returnUnitToField(context.state, marker, { at: destination, owner: effect.owner }, context.emit);
    }
  }, {
    references: (effect) => points().zone(effect.zone).selector(effect.selector),
  }))
  .register(effectHandler('setUnitDirective', (context, effect) => {
    if (effect.directive.zone && !context.state.scenario.zones[effect.directive.zone]) {
      throw new DomainInvariantError(`unknown scenario zone "${effect.directive.zone}"`);
    }
    for (const unit of context.select(effect.selector)) {
      new UnitEntity(unit).changeDirective({
        mode: effect.directive.mode,
        zone: effect.directive.zone,
        waypoints: effect.directive.waypoints?.map((point) => ({ ...point })) ?? [],
        cursor: Math.max(0, Math.round(effect.directive.cursor ?? 0)),
      });
      context.emit({ type: 'directiveChanged', unit: unit.id, mode: unit.directive.mode });
    }
  }, {
    references: (effect) => (effect.directive.waypoints ?? []).reduce(
      (cited, waypoint) => cited.cell(waypoint),
      points().selector(effect.selector).zone(effect.directive.zone).directive(effect.directive.mode),
    ),
  }))
  .register(effectHandler('addEngagementRule', ({ state }, effect) => addEngagementRule(state, effect.rule), {
    references: (effect) => (effect.rule.players ?? []).reduce(
      (cited, owner) => cited.player(owner),
      effect.rule.id.trim()
        ? points().zone(effect.rule.zone)
        : points().zone(effect.rule.zone).fault('交战规则缺少 id'),
    ),
  }))
  .register(effectHandler('removeEngagementRule', ({ state }, effect) => removeEngagementRule(state, effect.id)))
  .register(effectHandler('replaceTerrain', (context, effect) => {
    if (!context.content.terrains.has(effect.terrain)) throw new DomainInvariantError(`unknown terrain "${effect.terrain}"`);
    for (const cell of context.zone(effect.zone)) {
      const from = context.layers.changeTerrain(cell, effect.terrain);
      if (from === null) continue;
      context.emit({ type: 'terrainChanged', at: { ...cell }, from, to: effect.terrain });
    }
  }, {
    references: (effect) => points().zone(effect.zone).terrain(effect.terrain),
  }))
  .register(effectHandler('setElevation', (context, effect) => {
    for (const cell of context.zone(effect.zone)) {
      const step = context.layers.changeElevation(cell, effect.value);
      if (step) context.emit({ type: 'elevationChanged', at: { ...cell }, ...step });
    }
  }, {
    references: (effect) => wholeElevation(points().zone(effect.zone), effect.value),
  }))
  .register(effectHandler('addElevation', (context, effect) => {
    for (const cell of context.zone(effect.zone)) {
      const step = context.layers.raiseElevation(cell, effect.amount);
      if (step) context.emit({ type: 'elevationChanged', at: { ...cell }, ...step });
    }
  }, {
    references: (effect) => wholeElevation(points().zone(effect.zone), effect.amount),
  }))
  .register(effectHandler('setCliffs', (context, effect) => {
    for (const edge of effect.edges) {
      if (!context.layers.isEdge(edge.from, edge.to)) {
        throw new DomainInvariantError(`invalid cliff edge ${edge.from.x},${edge.from.y} -> ${edge.to.x},${edge.to.y}`);
      }
      if (context.layers.blockEdge(edge.from, edge.to, effect.blocked)) {
        context.emit({ type: 'cliffChanged', from: { ...edge.from }, to: { ...edge.to }, blocked: effect.blocked });
      }
    }
  }, {
    references: (effect) => effect.edges.reduce((cited, edge) => cited.edge(edge), points()),
  }))
  .register(effectHandler('setDirectionalCover', (context, effect) => {
    for (const cover of effect.covers) {
      if (!context.layers.contains(cover.at)) {
        throw new DomainInvariantError(`directional cover out of bounds: ${cover.at.x},${cover.at.y}`);
      }
      context.layers.changeCover(cover.at, cover.sides);
      context.emit({ type: 'directionalCoverChanged', at: { ...cover.at }, sides: { ...cover.sides } });
    }
  }, {
    references: (effect) => effect.covers.reduce((cited, cover) => cited.cell(cover.at), points()),
  }))
  .register(effectHandler('damageStructure', (context, effect) => {
    damageStructure(context.content, context.state, effect.id, effect.amount, context.emit);
  }, {
    references: (effect) => points().structure(effect.id),
  }))
  .register(effectHandler('repairStructure', (context, effect) => {
    repairStructure(context.content, context.state, effect.id, effect.amount, context.emit);
  }, {
    references: (effect) => points().structure(effect.id),
  }))
  .register(effectHandler('moveComposite', (context, effect) => {
    moveComposite(context.content, context.state, effect.id, { x: effect.dx, y: effect.dy }, context.emit);
  }, {
    references: (effect) => points().composite(effect.id),
  }))
  .register(effectHandler('emitSignal', ({ emit }, effect) => {
    emit({ type: 'scenarioSignal', signal: effect.signal });
  }));
ScenarioEffectHandlers.seal();

/**
 * Signals a battle raised, in the order it raised them.
 *
 * Here because this module emits them. The campaign bridge, the campaign shell
 * and a balance probe each wrote this filter out by hand — three copies of one
 * question, and the shell's copy was the one that would silently keep answering
 * the old way if the event were ever renamed.
 */
export function scenarioSignalsOf(events: readonly GameEvent[]): string[] {
  return events.flatMap((event) => (event.type === 'scenarioSignal' ? [event.signal] : []));
}

export function applyScenarioEffect(
  rules: ScenarioRules,
  state: GameState,
  effect: ScenarioEffect,
  emit: (event: GameEvent) => void,
): void {
  rules.scenarioEffects.apply(rules, state, effect, emit);
}

/**
 * Runs the triggers of one occurrence until no newly-enabled trigger remains.
 *
 * The sweep repeats because one trigger's effects can satisfy another's
 * condition, and an author expects the chain to land in the same occurrence
 * rather than a round later. Each trigger answers for itself whether it is due;
 * this loop only decides how many times to ask.
 */
export function runScenarioTriggers(
  rules: ScenarioRules,
  state: GameState,
  timing: ScenarioTiming,
  emit: (event: GameEvent) => void,
): void {
  const occurrence = TriggerOccurrence.of(state, timing);
  const firedThisOccurrence = new Set<string>();
  const limit = state.scenario.triggers.length + 1;
  for (let pass = 0; pass < limit; pass++) {
    let changed = false;
    for (const declaration of state.scenario.triggers) {
      const trigger = new ScenarioTriggerEntity(state.scenario, declaration);
      if (firedThisOccurrence.has(trigger.id) || !trigger.dueAt(occurrence)) continue;
      if (!conditionMet(rules, state, declaration.condition)) continue;
      firedThisOccurrence.add(trigger.id);
      trigger.recordFiring(occurrence);
      for (const effect of declaration.effects) applyScenarioEffect(rules, state, effect, emit);
      changed = true;
    }
    if (!changed) return;
  }
  throw new DomainInvariantError(`scenario trigger loop did not stabilise at ${timing}`);
}
