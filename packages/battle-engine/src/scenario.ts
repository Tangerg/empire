import { UnitEntity } from './domain/unit-entity';
import { PlayerEntity } from './domain/player-entity';
import { idx } from './grid';
import { inBounds } from './grid';
import { addTerrainOverlay, removeTerrainOverlay } from './overlays';
import { player, removeUnit, spawnUnit, unitAtCoord } from './state';
import { addStatus, removeStatus } from './statuses';
import { damageStructure, repairStructure } from './structures';
import { changeUnitResource } from './progression';
import { BattleAggregate } from './domain/battle-aggregate';
import { Battlefield } from './domain/battlefield';
import { handleCommanderDefeat } from './commanders';
import { edgeKey } from './spatial';
import { forceMoveUnit, teleportUnit } from './forced-movement';
import {
  type BattleResourceSystem,
  } from './resources';
import type {
  GameEvent,
  GameState,
  ScenarioCondition,
  ScenarioConditionKindMap,
  ScenarioEffect,
  ScenarioEffectKindMap,
  ScenarioTrigger,
  ScenarioValue,
  Unit,
  UnitSelector,
  BattlefieldMarker,
  MarkerSelector,
} from './types';
import { type ContentCatalog } from './content-pack';
import { SplitMixRandom, type RandomSource } from './random';
import { cloneUnitState } from './unit-state';
import { changeMorale, surrenderUnit } from './morale';
import { addEngagementRule, removeEngagementRule } from './engagement';
import { compositeStatus, moveComposite } from './composites';
import { withdrawTransportPassengers } from './transports';

export type ScenarioTiming = ScenarioTrigger['timing'];
type ConditionKind = Extract<keyof ScenarioConditionKindMap, string>;
type EffectKind = Extract<keyof ScenarioEffectKindMap, string>;

function zone(state: GameState, id: string) {
  const cells = state.scenario.zones[id];
  if (!cells) throw new Error(`unknown scenario zone "${id}"`);
  return cells;
}

function inZone(state: GameState, unit: Unit, zoneId: string): boolean {
  return zone(state, zoneId).some((cell) => cell.x === unit.x && cell.y === unit.y);
}

export function selectUnits(
  state: GameState,
  selector: UnitSelector,
  content: ContentCatalog,
): Unit[] {
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
  if (typeof value !== 'number') throw new Error(`scenario variable "${key}" is not numeric`);
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
  evaluate(context: ScenarioConditionContext, condition: ScenarioConditionKindMap[K]): boolean;
}

export class ScenarioConditionHandlerRegistry {
  private readonly handlers = new Map<string, ScenarioConditionHandler>();

  /**
   * The random source is injected once per ruleset instead of threaded through
   * every `evaluate` call: the registry is already per-engine and cloneable, so
   * this keeps randomness swappable without widening the condition signature.
   */
  constructor(readonly random: RandomSource) {}

  register<K extends ConditionKind>(handler: ScenarioConditionHandler<K>): this {
    if (this.handlers.has(handler.kind)) throw new Error(`duplicate scenario condition handler "${handler.kind}"`);
    this.handlers.set(handler.kind, handler as ScenarioConditionHandler);
    return this;
  }

  replace<K extends ConditionKind>(handler: ScenarioConditionHandler<K>): this {
    this.handlers.set(handler.kind, handler as ScenarioConditionHandler);
    return this;
  }

  evaluate(state: GameState, condition: ScenarioCondition, content: ContentCatalog): boolean {
    const handler = this.handlers.get(condition.type);
    if (!handler) throw new Error(`no scenario condition handler for "${condition.type}"`);
    return handler.evaluate(new ScenarioConditionContext(state, this, content, this.random), condition as never);
  }

  clone(random: RandomSource = this.random): ScenarioConditionHandlerRegistry {
    const copy = new ScenarioConditionHandlerRegistry(random);
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }

  kinds(): string[] {
    return [...this.handlers.keys()];
  }
}

const conditionHandler = <K extends ConditionKind>(
  kind: K,
  evaluate: ScenarioConditionHandler<K>['evaluate'],
): ScenarioConditionHandler<K> => ({ kind, evaluate });

export const ScenarioConditionHandlers = new ScenarioConditionHandlerRegistry(SplitMixRandom)
  .register(conditionHandler('turnAtLeast', ({ state }, condition) => state.turn >= condition.turn))
  .register(conditionHandler('turnCycle', ({ state }, condition) => {
    if (!Number.isInteger(condition.every) || condition.every < 1) return false;
    const offset = Math.round(condition.offset ?? 1);
    return state.turn >= offset && (state.turn - offset) % condition.every === 0;
  }))
  .register(conditionHandler('chance', ({ state, random }, condition) =>
    random.chance(state, condition.stream ?? 'scenario.chance', condition.percent)))
  .register(conditionHandler('currentPlayer', ({ state }, condition) => state.currentPlayer === condition.player))
  .register(
    conditionHandler('variable', ({ state }, condition) =>
      compare(state.scenario.variables[condition.key], condition.op, condition.value),
    ),
  )
  .register(
    conditionHandler('unitInZone', ({ state, content }, condition) =>
      selectUnits(state, {
        owner: condition.owner,
        zone: condition.zone,
        anyTags: condition.anyTags,
      }, content).length > 0,
    ),
  )
  .register(conditionHandler('unitCount', ({ state, content }, condition) =>
    compare(selectUnits(state, condition.selector, content).length, condition.op, condition.value)))
  .register(conditionHandler('unitHealth', ({ state, content }, condition) => {
    const ratios = selectUnits(state, condition.selector, content)
      .map((unit) => unit.hp / content.units.get(unit.type).maxHp);
    if (ratios.length === 0) return false;
    if (condition.aggregate === 'any') return ratios.some((value) => compare(value, condition.op, condition.value));
    if (condition.aggregate === 'all') return ratios.every((value) => compare(value, condition.op, condition.value));
    return compare(ratios.reduce((sum, value) => sum + value, 0) / ratios.length, condition.op, condition.value);
  }))
  .register(conditionHandler('markerCount', ({ state }, condition) =>
    compare(selectMarkers(state, condition.selector).length, condition.op, condition.value)))
  .register(conditionHandler('eventCount', ({ state }, condition) =>
    compare(state.scenario.eventCounts[condition.event] ?? 0, condition.op, condition.value)))
  .register(
    conditionHandler('structure', ({ state }, condition) => {
      const structure = state.structures.find((candidate) => candidate.id === condition.id);
      if (!structure) return condition.state === 'destroyed';
      if (condition.state === 'destroyed') return structure.hp <= 0;
      if (condition.state === 'disabled') return structure.hp > 0 && structure.disabled;
      return structure.hp > 0 && !structure.disabled;
    }),
  )
  .register(conditionHandler('composite', ({ state }, condition) =>
    compositeStatus(state, condition.id).state === condition.state))
  .register(
    conditionHandler('objective', ({ state }, condition) =>
      player(state, condition.player).objectiveStates[condition.id]?.status === condition.status,
    ),
  )
  .register(conditionHandler('all', (context, condition) => condition.conditions.every((child) => context.evaluate(child))))
  .register(conditionHandler('any', (context, condition) => condition.conditions.some((child) => context.evaluate(child))))
  .register(conditionHandler('not', (context, condition) => !context.evaluate(condition.condition)));

/**
 * Ports declared by this module. The composition-level `BattleRuleServices`
 * satisfies both structurally, so neither side needs to import the other.
 */
export interface ScenarioConditionRules {
  readonly content: ContentCatalog;
  readonly scenarioConditions: ScenarioConditionHandlerRegistry;
}

export interface ScenarioRules extends ScenarioConditionRules {
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
  constructor(
    readonly state: GameState,
    readonly emit: (event: GameEvent) => void,
    readonly resources: BattleResourceSystem,
    readonly content: ContentCatalog,
  ) {}

  zone(id: string) {
    return zone(this.state, id);
  }

  select(selector: UnitSelector): Unit[] {
    return selectUnits(this.state, selector, this.content);
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
  apply(context: ScenarioEffectContext, effect: ScenarioEffectKindMap[K]): void;
}

export class ScenarioEffectHandlerRegistry {
  private readonly handlers = new Map<string, ScenarioEffectHandler>();

  register<K extends EffectKind>(handler: ScenarioEffectHandler<K>): this {
    if (this.handlers.has(handler.kind)) throw new Error(`duplicate scenario effect handler "${handler.kind}"`);
    this.handlers.set(handler.kind, handler as ScenarioEffectHandler);
    return this;
  }

  replace<K extends EffectKind>(handler: ScenarioEffectHandler<K>): this {
    this.handlers.set(handler.kind, handler as ScenarioEffectHandler);
    return this;
  }

  apply(
    state: GameState,
    effect: ScenarioEffect,
    emit: (event: GameEvent) => void,
    resources: BattleResourceSystem,
    content: ContentCatalog,
  ): void {
    const handler = this.handlers.get(effect.type);
    if (!handler) throw new Error(`no scenario effect handler for "${effect.type}"`);
    handler.apply(new ScenarioEffectContext(state, emit, resources, content), effect as never);
  }

  clone(): ScenarioEffectHandlerRegistry {
    const copy = new ScenarioEffectHandlerRegistry();
    for (const handler of this.handlers.values()) copy.register(handler);
    return copy;
  }

  kinds(): string[] {
    return [...this.handlers.keys()];
  }
}

const effectHandler = <K extends EffectKind>(
  kind: K,
  apply: ScenarioEffectHandler<K>['apply'],
): ScenarioEffectHandler<K> => ({ kind, apply });

export const ScenarioEffectHandlers = new ScenarioEffectHandlerRegistry()
  .register(effectHandler('setVariable', ({ state }, effect) => {
    state.scenario.variables[effect.key] = effect.value;
  }))
  .register(effectHandler('addVariable', ({ state }, effect) => {
    state.scenario.variables[effect.key] = numberVariable(state, effect.key) + effect.amount;
  }))
  .register(effectHandler('addStatus', (context, effect) => {
    for (const unit of context.select(effect.selector)) {
      addStatus(unit, effect.status, effect.duration, context.content, context.emit);
    }
  }))
  .register(effectHandler('removeStatus', (context, effect) => {
    for (const unit of context.select(effect.selector)) removeStatus(unit, effect.status, context.emit);
  }))
  .register(effectHandler('changeUnitOwner', (context, effect) => {
    for (const unit of context.select(effect.selector)) {
      const entity = new UnitEntity(unit);
      const from = entity.owner;
      if (from === effect.owner) continue;
      entity.changeOwner(effect.owner);
      context.emit({ type: 'unitOwnerChanged', unit: unit.id, from, to: effect.owner });
    }
  }))
  .register(effectHandler('spawnUnits', (context, effect) => {
    for (const source of effect.units) {
      player(context.state, source.owner);
      if (!inBounds(context.state.map, source.x, source.y)) {
        throw new Error(`spawn cell out of bounds: ${source.x},${source.y}`);
      }
      if (unitAtCoord(context.state, source)) throw new Error(`spawn cell is occupied: ${source.x},${source.y}`);
      if (source.key && context.state.units.some((unit) => unit.key === source.key)) {
        throw new Error(`unit key already exists: "${source.key}"`);
      }
      if (source.key && context.state.markers.some((marker) => marker.fallenUnit?.key === source.key)) {
        throw new Error(`unit key is reserved by a fallen unit: "${source.key}"`);
      }
      const battlefield = new Battlefield(context.state, context.content);
      const definition = context.content.units.get(source.unit);
      const cell = battlefield.cell(source);
      if (cell.blocksMovement || cell.movementCost(definition.movementClass) === null) {
        throw new Error(`unit "${source.unit}" cannot spawn at ${source.x},${source.y}`);
      }
      const unit = spawnUnit(context.state, source.unit, source.owner, source, {
        done: !(effect.ready ?? false),
        source,
      }, context.content);
      context.emit({
        type: 'unitSpawned',
        unit: unit.id,
        at: { x: unit.x, y: unit.y },
        reason: effect.reason ?? 'reinforcement',
      });
    }
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
      withdrawTransportPassengers(context.state, unit.id, at, 'withdrawn', context.emit);
      handleCommanderDefeat(context.state, unit.id, context.emit, context.content);
      context.emit({ type: 'unitWithdrawn', unit: unit.id, at });
    }
  }))
  .register(effectHandler('reviveMarkers', (context, effect) => {
    const ratio = Math.max(0.01, Math.min(1, effect.hpPercent ?? 0.5));
    for (const marker of [...context.selectMarkers(effect.selector)]) {
      const fallen = marker.fallenUnit;
      if (!fallen || unitAtCoord(context.state, marker.at)) continue;
      if (context.state.units.some((unit) => unit.id === fallen.id || (fallen.key && unit.key === fallen.key))) continue;
      const restored: Unit = {
        ...cloneUnitState(fallen),
        owner: effect.owner ?? fallen.owner,
        x: marker.at.x,
        y: marker.at.y,
        hp: Math.max(1, Math.round(context.content.units.get(fallen.type).maxHp * ratio)),
        done: true,
        capture: 0,
        statuses: [],
      };
      context.state.units.push(restored);
      const commander = context.state.commanders.find((candidate) => candidate.unitId === restored.id);
      if (commander) commander.owner = restored.owner;
      context.state.nextUnitId = Math.max(context.state.nextUnitId, restored.id + 1);
      context.state.markers.splice(context.state.markers.indexOf(marker), 1);
      context.emit({ type: 'markerRemoved', marker: marker.id, kind: marker.kind, at: marker.at });
      context.emit({ type: 'unitRevived', unit: restored.id, marker: marker.id, at: marker.at, hp: restored.hp });
    }
  }))
  .register(effectHandler('removeMarkers', (context, effect) => {
    for (const marker of [...context.selectMarkers(effect.selector)]) {
      context.state.markers.splice(context.state.markers.indexOf(marker), 1);
      context.emit({ type: 'markerRemoved', marker: marker.id, kind: marker.kind, at: marker.at });
    }
  }))
  .register(effectHandler('setPlayerTeam', (context, effect) => {
    const target = player(context.state, effect.player);
    const from = target.team;
    if (from === effect.team) return;
    target.team = effect.team;
    context.emit({ type: 'playerTeamChanged', player: target.id, from, to: effect.team });
  }))
  .register(effectHandler('forceMove', (context, effect) => {
    for (const unit of [...context.select(effect.selector)]) {
      if (!context.state.units.some((candidate) => candidate.id === unit.id)) continue;
      forceMoveUnit(context.state, {
        unit: unit.id,
        source: effect.source,
        mode: effect.mode,
        distance: effect.distance,
        collisionDamage: effect.collisionDamage,
      }, context.emit, context.content);
    }
  }))
  .register(effectHandler('teleportUnits', (context, effect) => {
    const destinations = context.zone(effect.zone)
      .slice()
      .sort((a, b) => a.y - b.y || a.x - b.x);
    for (const unit of context.select(effect.selector)) {
      const destination = destinations.find((cell) => unitAtCoord(context.state, cell) === undefined &&
        new Battlefield(context.state, context.content).cell(cell).movementCost(context.content.units.get(unit.type).movementClass) !== null &&
        !new Battlefield(context.state, context.content).cell(cell).blocksMovement);
      if (destination) teleportUnit(context.state, unit.id, destination, context.emit, context.content);
    }
  }))
  .register(effectHandler('addOverlay', (context, effect) => {
    addTerrainOverlay(
      context.state,
      {
        id: effect.id,
        type: effect.overlay,
        cells: context.zone(effect.zone),
        remainingRounds: effect.rounds ?? null,
      },
      context.emit,
      context.content,
    );
  }))
  .register(effectHandler('removeOverlay', ({ state, emit }, effect) => {
    removeTerrainOverlay(state, effect.id, emit);
  }))
  .register(effectHandler('activateObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { status: 'active' });
  }))
  .register(effectHandler('cancelObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { status: 'cancelled' });
  }))
  .register(effectHandler('completeObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { status: 'completed' });
  }))
  .register(effectHandler('revealObjective', (context, effect) => {
    context.changeObjective(effect.player, effect.id, { hidden: false });
  }))
  .register(effectHandler('changeUnitResource', (context, effect) => {
    for (const unit of context.select(effect.selector)) {
      changeUnitResource(unit, effect.resource, effect.amount, context.emit, context.resources);
    }
  }))
  .register(effectHandler('changeMorale', (context, effect) => {
    for (const unit of [...context.select(effect.selector)]) {
      if (context.state.units.some((candidate) => candidate.id === unit.id)) {
        changeMorale(context.state, unit.id, effect.amount, effect.reason ?? 'scenario', context.emit, context.content);
      }
    }
  }))
  .register(effectHandler('surrenderUnits', (context, effect) => {
    for (const unit of [...context.select(effect.selector)]) {
      if (context.state.units.some((candidate) => candidate.id === unit.id)) {
        surrenderUnit(context.state, unit.id, effect.to, context.emit, context.content);
        handleCommanderDefeat(context.state, unit.id, context.emit, context.content);
      }
    }
  }))
  .register(effectHandler('restoreWithdrawnUnits', (context, effect) => {
    const destinations = context.zone(effect.zone).slice().sort((left, right) => left.y - right.y || left.x - right.x);
    for (const marker of [...context.selectMarkers(effect.selector)]) {
      const fallen = marker.fallenUnit;
      if (!fallen) continue;
      if (context.state.units.some((unit) => unit.id === fallen.id || (fallen.key && unit.key === fallen.key))) continue;
      const destination = destinations.find((cell) => {
        if (unitAtCoord(context.state, cell)) return false;
        const battlefieldCell = new Battlefield(context.state, context.content).cell(cell);
        const movement = context.content.units.get(fallen.type).movementClass;
        return !battlefieldCell.blocksMovement && battlefieldCell.movementCost(movement) !== null;
      });
      if (!destination) break;
      const restored = cloneUnitState(fallen);
      restored.owner = effect.owner ?? restored.owner;
      restored.x = destination.x;
      restored.y = destination.y;
      restored.done = true;
      restored.capture = 0;
      restored.morale.current = Math.max(1, restored.morale.current);
      context.state.units.push(restored);
      context.state.markers.splice(context.state.markers.indexOf(marker), 1);
      context.state.nextUnitId = Math.max(context.state.nextUnitId, restored.id + 1);
      const commander = context.state.commanders.find((candidate) => candidate.unitId === restored.id);
      if (commander) commander.owner = restored.owner;
      context.emit({ type: 'markerRemoved', marker: marker.id, kind: marker.kind, at: marker.at });
      context.emit({ type: 'unitRevived', unit: restored.id, marker: marker.id, at: { ...destination }, hp: restored.hp });
    }
  }))
  .register(effectHandler('setUnitDirective', (context, effect) => {
    if (effect.directive.zone && !context.state.scenario.zones[effect.directive.zone]) {
      throw new Error(`unknown scenario zone "${effect.directive.zone}"`);
    }
    for (const unit of context.select(effect.selector)) {
      unit.directive = {
        mode: effect.directive.mode,
        zone: effect.directive.zone,
        waypoints: effect.directive.waypoints?.map((point) => ({ ...point })) ?? [],
        cursor: Math.max(0, Math.round(effect.directive.cursor ?? 0)),
      };
      context.emit({ type: 'directiveChanged', unit: unit.id, mode: unit.directive.mode });
    }
  }))
  .register(effectHandler('addEngagementRule', ({ state }, effect) => addEngagementRule(state, effect.rule)))
  .register(effectHandler('removeEngagementRule', ({ state }, effect) => removeEngagementRule(state, effect.id)))
  .register(effectHandler('replaceTerrain', (context, effect) => {
    if (!context.content.terrains.has(effect.terrain)) throw new Error(`unknown terrain "${effect.terrain}"`);
    for (const cell of context.zone(effect.zone)) {
      const index = idx(context.state.map, cell.x, cell.y);
      const from = context.state.map.tiles[index];
      if (from === effect.terrain) continue;
      context.state.map.tiles[index] = effect.terrain;
      context.emit({ type: 'terrainChanged', at: { ...cell }, from, to: effect.terrain });
    }
  }))
  .register(effectHandler('setElevation', (context, effect) => {
    const value = Math.round(effect.value);
    for (const cell of context.zone(effect.zone)) {
      const index = idx(context.state.map, cell.x, cell.y);
      const from = context.state.map.elevation[index] ?? 0;
      if (from === value) continue;
      context.state.map.elevation[index] = value;
      context.emit({ type: 'elevationChanged', at: { ...cell }, from, to: value });
    }
  }))
  .register(effectHandler('addElevation', (context, effect) => {
    const amount = Math.round(effect.amount);
    for (const cell of context.zone(effect.zone)) {
      const index = idx(context.state.map, cell.x, cell.y);
      const from = context.state.map.elevation[index] ?? 0;
      const to = from + amount;
      if (from === to) continue;
      context.state.map.elevation[index] = to;
      context.emit({ type: 'elevationChanged', at: { ...cell }, from, to });
    }
  }))
  .register(effectHandler('setCliffs', (context, effect) => {
    for (const edge of effect.edges) {
      if (!inBounds(context.state.map, edge.from.x, edge.from.y) ||
        !inBounds(context.state.map, edge.to.x, edge.to.y) ||
        Math.abs(edge.from.x - edge.to.x) + Math.abs(edge.from.y - edge.to.y) !== 1) {
        throw new Error(`invalid cliff edge ${edge.from.x},${edge.from.y} -> ${edge.to.x},${edge.to.y}`);
      }
      const key = edgeKey(edge.from, edge.to);
      const index = context.state.map.cliffs.findIndex((candidate) => edgeKey(candidate.from, candidate.to) === key);
      if (effect.blocked && index < 0) context.state.map.cliffs.push({ from: { ...edge.from }, to: { ...edge.to } });
      if (!effect.blocked && index >= 0) context.state.map.cliffs.splice(index, 1);
      if ((effect.blocked && index < 0) || (!effect.blocked && index >= 0)) {
        context.emit({ type: 'cliffChanged', from: { ...edge.from }, to: { ...edge.to }, blocked: effect.blocked });
      }
    }
  }))
  .register(effectHandler('setDirectionalCover', (context, effect) => {
    for (const cover of effect.covers) {
      if (!inBounds(context.state.map, cover.at.x, cover.at.y)) {
        throw new Error(`directional cover out of bounds: ${cover.at.x},${cover.at.y}`);
      }
      const existing = context.state.map.directionalCover.find((entry) =>
        entry.at.x === cover.at.x && entry.at.y === cover.at.y);
      if (existing) existing.sides = { ...cover.sides };
      else context.state.map.directionalCover.push({ at: { ...cover.at }, sides: { ...cover.sides } });
      context.emit({ type: 'directionalCoverChanged', at: { ...cover.at }, sides: { ...cover.sides } });
    }
  }))
  .register(effectHandler('damageStructure', (context, effect) => {
    damageStructure(context.state, effect.id, effect.amount, context.content, context.emit);
  }))
  .register(effectHandler('repairStructure', (context, effect) => {
    repairStructure(context.state, effect.id, effect.amount, context.content, context.emit);
  }))
  .register(effectHandler('moveComposite', (context, effect) => {
    moveComposite(context.state, effect.id, { x: effect.dx, y: effect.dy }, context.emit);
  }))
  .register(effectHandler('emitSignal', ({ emit }, effect) => {
    emit({ type: 'scenarioSignal', signal: effect.signal });
  }));

export function applyScenarioEffect(
  rules: ScenarioRules,
  state: GameState,
  effect: ScenarioEffect,
  emit: (event: GameEvent) => void,
): void {
  rules.scenarioEffects.apply(state, effect, emit, rules.resources, rules.content);
}

/** Runs one-shot or bounded repeating data triggers until no newly-enabled trigger remains. */
export function runScenarioTriggers(
  rules: ScenarioRules,
  state: GameState,
  timing: ScenarioTiming,
  emit: (event: GameEvent) => void,
): void {
  const fired = new Set(state.scenario.firedTriggerIds);
  const firedThisOccurrence = new Set<string>();
  const occurrence = `${state.turn}:${state.currentPlayer}:${timing}`;
  const limit = state.scenario.triggers.length + 1;
  for (let pass = 0; pass < limit; pass++) {
    let changed = false;
    for (const trigger of state.scenario.triggers) {
      if (trigger.timing !== timing || firedThisOccurrence.has(trigger.id)) continue;
      const repeat = trigger.repeat;
      if (!repeat && fired.has(trigger.id)) continue;
      if (repeat) {
        const runtime = state.scenario.triggerRuntime[trigger.id] ?? { count: 0, lastOccurrence: '' };
        const start = repeat.startTurn ?? 1;
        if (!Number.isInteger(repeat.everyRounds) || repeat.everyRounds < 1 || state.turn < start) continue;
        if (repeat.endTurn !== undefined && state.turn > repeat.endTurn) continue;
        if ((state.turn - start) % repeat.everyRounds !== 0) continue;
        if (repeat.maxFirings !== undefined && runtime.count >= repeat.maxFirings) continue;
        if (runtime.lastOccurrence === occurrence) continue;
      }
      if (!conditionMet(rules, state, trigger.condition)) continue;
      firedThisOccurrence.add(trigger.id);
      if (repeat) {
        const runtime = state.scenario.triggerRuntime[trigger.id] ?? { count: 0, lastOccurrence: '' };
        state.scenario.triggerRuntime[trigger.id] = { count: runtime.count + 1, lastOccurrence: occurrence };
      } else {
        fired.add(trigger.id);
        state.scenario.firedTriggerIds.push(trigger.id);
      }
      for (const effect of trigger.effects) applyScenarioEffect(rules, state, effect, emit);
      changed = true;
    }
    if (!changed) return;
  }
  throw new Error(`scenario trigger loop did not stabilise at ${timing}`);
}
