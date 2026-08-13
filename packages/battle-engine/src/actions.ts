import {
  abilityDef,
  abilityTargets,
  canUseAbility,
  type AbilityQuery,
  type AbilityRules,
} from './abilities';
import { advanceWeaponCooldowns, unitWeapons } from './combat';
import { idx, sameCoord } from './grid';
import { player, removeUnit, requireUnit, spawnUnit, unitAt, unitsOf } from './state';
import { evaluateVictory, healRateAt, turnResourceGrantsFor } from './victory';
import { resolveTurnStartStatuses } from './statuses';
import { runScenarioTriggers } from './scenario';
import { advanceTerrainOverlayRound, applyOverlayTurnStartEffects } from './overlays';
import { executeTactic, handleCommanderDefeat, refreshCommanderTurn } from './commanders';
import { BattleAggregate, UnitEntity } from './domain/index';
import { Battlefield } from './domain/battlefield';
import { changeCareer, unitAbilityIds } from './careers';
import { directionToward } from './spatial';
import { validateFormationChange } from './formations';
import { disembarkUnit, embarkUnit } from './transports';
import { playerResource } from './resources';
import type { ContentCatalog } from './content-pack';
import { type TacticalSpace } from './tactical-space';
import {
  ActionExecutionContext,
  ActionHandlerRegistry,
  DefaultBattleRuleServices,
  IllegalActionError,
  type ActionHandler,
  type BattleRuleServices,
} from './action-system';
import type { Action, ActionKindMap, Coord, GameEvent, GameState, PlayerId, Unit, WeaponId } from './types';

export { IllegalActionError } from './action-system';

function fail(msg: string): never {
  throw new IllegalActionError(msg);
}

/* --------------------------------------------------------------- enumeration */

export interface CommandOption {
  /** Stable identity in the command menu; unique even for multiple weapons. */
  key: string;
  ability: string;
  weapon?: WeaponId;
  name: string;
  hint: string;
  selfTargeted: boolean;
  targets: Coord[];
}

/** Command menu for a unit that has moved to `at`. */
export function commandOptions(
  rules: AbilityRules,
  s: GameState,
  unit: Unit,
  at: Coord,
): CommandOption[] {
  const content = rules.content;
  const moved = !(unit.x === at.x && unit.y === at.y);
  const q: AbilityQuery = { state: s, unit, at, moved };
  const out: CommandOption[] = [];
  for (const abilityId of unitAbilityIds(unit, content)) {
    if (abilityId === 'attack') continue;
    const ability = abilityDef(rules, abilityId);
    if (!canUseAbility(rules, ability, q)) continue;
    const targets = abilityTargets(rules, ability, q);
    if (!ability.selfTargeted && targets.length === 0) continue;
    out.push({
      key: ability.id,
      ability: ability.id,
      name: ability.name,
      hint: ability.hint,
      selfTargeted: ability.selfTargeted,
      targets,
    });
  }

  if (content.units.get(unit.type).abilities.includes('attack')) {
    const attack = abilityDef(rules, 'attack');
    for (const weapon of unitWeapons(unit, content)) {
      const weaponQuery: AbilityQuery = { ...q, weaponId: weapon.id };
      if (!canUseAbility(rules, attack, weaponQuery)) continue;
      const targets = abilityTargets(rules, attack, weaponQuery);
      if (targets.length === 0) continue;
      out.push({
        key: `attack:${weapon.id}`,
        ability: 'attack',
        weapon: weapon.id,
        name: `攻击 · ${weapon.name}`,
        hint: `${attack.hint} 射程 ${weapon.minRange}-${weapon.maxRange}`,
        selfTargeted: false,
        targets,
      });
    }
  }
  return out.sort((a, b) => {
    const priority = (id: string) => abilityDef(rules, id).priority;
    return priority(a.ability) - priority(b.ability) || a.key.localeCompare(b.key);
  });
}

export const canAct = (rules: BattleRuleServices, s: GameState, u: Unit): boolean =>
  s.phase === 'playing' && rules.turnOrders.get(s.turnOrder.policy).canAct(s, u);

/* ------------------------------------------------------------------- helpers */

interface ValidatedMovement {
  destination: Coord;
  path: Coord[];
}

function validatePath(
  s: GameState,
  unit: Unit,
  requestedPath: Coord[],
  space: TacticalSpace,
): ValidatedMovement {
  if (requestedPath.length === 0) fail('路径为空');
  if (!sameCoord(requestedPath[0], { x: unit.x, y: unit.y })) fail('路径起点与单位位置不符');
  const destination = requestedPath[requestedPath.length - 1];
  if (requestedPath.length === 1) return { destination, path: [{ x: unit.x, y: unit.y }] };

  const field = space.moveField(s, unit);
  const canonical = space.pathTo(field, s, destination);
  if (!canonical) fail(`目标格 ${destination.x},${destination.y} 不在移动范围内`);
  if (!field.stops.has(idx(s.map, destination.x, destination.y))) fail('目标格已被占据');
  // Trust the engine's own cheapest path rather than the client's route.
  return { destination, path: canonical };
}

function moveUnit(
  s: GameState,
  unit: Unit,
  movement: ValidatedMovement,
  emit: (e: GameEvent) => void,
  content: ContentCatalog,
): void {
  const { destination: dest, path } = movement;
  if (unit.x === dest.x && unit.y === dest.y) return;
  new BattleAggregate(s, content).moveUnit(unit.id, dest);
  if (unit.directive.mode === 'patrol' && unit.directive.waypoints.length > 0) {
    const waypoint = unit.directive.waypoints[unit.directive.cursor % unit.directive.waypoints.length];
    if (sameCoord(dest, waypoint)) unit.directive.cursor = (unit.directive.cursor + 1) % unit.directive.waypoints.length;
  }
  if (path.length > 1) {
    const previous = new UnitEntity(unit).changeFacing(directionToward(path[path.length - 2], dest));
    if (previous !== unit.facing) emit({ type: 'facingChanged', unit: unit.id, from: previous, to: unit.facing });
  }
  emit({ type: 'move', unit: unit.id, path });
}

/* ---------------------------------------------------------- action strategies */

class DeployUnitActionHandler implements ActionHandler<'deployUnit'> {
  readonly kind = 'deployUnit' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['deployUnit']): void {
    const state = context.state;
    const deployment = state.deployment;
    if (state.phase !== 'deployment' || !deployment) context.fail('当前不在战前部署阶段');
    const unit = requireUnit(state, action.unit);
    if (unit.owner !== state.currentPlayer) context.fail('不是你的单位');
    const assignment = deployment.assignments.find((entry) =>
      entry.player === state.currentPlayer && entry.unitIds.includes(unit.id));
    if (!assignment) context.fail('该单位不在当前部署编组中');
    const cells = state.scenario.zones[assignment.zone];
    if (!cells?.some((cell) => sameCoord(cell, action.at))) context.fail('目标格不在该单位的部署区域内');

    const occupant = unitAt(state, action.at.x, action.at.y);
    if (occupant && occupant.id !== unit.id) {
      const swappable = deployment.assignments.some((entry) =>
        entry.player === state.currentPlayer && entry.unitIds.includes(occupant.id));
      if (!swappable) context.fail('目标格已被其他单位占据');
    }
    const destination = new Battlefield(state, context.rules.content).cell(action.at);
    const movementClass = context.rules.content.units.get(unit.type).movementClass;
    if (destination.blocksMovement || destination.movementCost(movementClass) === null) {
      context.fail('该单位无法部署到目标格');
    }
    const from = { x: unit.x, y: unit.y };
    if (sameCoord(from, action.at)) return;
    if (occupant) {
      const occupantAssignment = deployment.assignments.find((entry) => entry.unitIds.includes(occupant.id));
      const occupantCells = occupantAssignment && state.scenario.zones[occupantAssignment.zone];
      if (!occupantCells?.some((cell) => sameCoord(cell, from))) context.fail('交换后会使另一单位离开部署区域');
      occupant.x = from.x;
      occupant.y = from.y;
      occupant.capture = 0;
    }
    unit.x = action.at.x;
    unit.y = action.at.y;
    unit.capture = 0;
    context.emit({ type: 'unitDeployed', unit: unit.id, from, to: { ...action.at } });
  }
}

class FinishDeploymentActionHandler implements ActionHandler<'finishDeployment'> {
  readonly kind = 'finishDeployment' as const;

  execute(context: ActionExecutionContext): void {
    const state = context.state;
    const deployment = state.deployment;
    if (state.phase !== 'deployment' || !deployment) context.fail('当前不在战前部署阶段');
    const confirmed = state.currentPlayer;
    context.emit({ type: 'deploymentConfirmed', player: confirmed });
    deployment.currentIndex++;
    if (deployment.currentIndex < deployment.order.length) {
      state.currentPlayer = deployment.order[deployment.currentIndex];
      return;
    }
    state.deployment = null;
    state.phase = 'playing';
    for (const unit of state.units) unit.done = false;
    startTurnOrder(state, context.rules, context.emit);
    context.emit({ type: 'battleStarted', player: state.currentPlayer, turn: state.turn });
  }
}

class CommandActionHandler implements ActionHandler<'command'> {
  readonly kind = 'command' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['command']): void {
    const state = context.state;
    const unit = requireUnit(state, action.unit);
    if (unit.owner !== state.currentPlayer) context.fail('不是你的单位');
    if (unit.done) context.fail('该单位本回合已行动');
    if (!context.rules.turnOrders.get(state.turnOrder.policy).canAct(state, unit)) {
      context.fail('该单位当前没有行动权');
    }

    const movement = validatePath(state, unit, action.path, context.rules.space);
    const destination = movement.destination;
    const ability = abilityDef(context.rules, action.command.ability);
    const moved = !(unit.x === destination.x && unit.y === destination.y);
    const weaponId = action.command.ability === 'attack' ? action.command.weapon : undefined;
    const query: AbilityQuery = { state, unit, at: destination, moved, weaponId };
    if (!unitAbilityIds(unit, context.rules.content).includes(ability.id)) {
      context.fail(`${context.rules.content.units.get(unit.type).name} 没有「${ability.name}」`);
    }
    if (!canUseAbility(context.rules, ability, query)) context.fail(`此处无法使用「${ability.name}」`);

    const target = 'target' in action.command ? action.command.target ?? null : null;
    if (!ability.selfTargeted) {
      if (!target) context.fail(`「${ability.name}」需要指定目标`);
      if (!abilityTargets(context.rules, ability, query).some((candidate) => sameCoord(candidate, target))) {
        context.fail('目标不合法');
      }
    }

    moveUnit(state, unit, movement, context.emit, context.rules.content);
    if (target) {
      const previous = new UnitEntity(unit).changeFacing(directionToward(destination, target));
      if (previous !== unit.facing) {
        context.emit({ type: 'facingChanged', unit: unit.id, from: previous, to: unit.facing });
      }
    }
    ability.execute(context.rules, query, target, context.emit);
    if (context.battle.findUnit(unit.id)) new UnitEntity(unit).finishAction();
  }
}

class TacticActionHandler implements ActionHandler<'tactic'> {
  readonly kind = 'tactic' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['tactic']): void {
    try {
      executeTactic(
        context.state,
        action.commander,
        action.tactic,
        action.target,
        context.emit,
        context.rules.resources,
        context.rules.content,
      );
    } catch (error) {
      context.fail((error as Error).message);
    }
  }
}

class ReactionActionHandler implements ActionHandler<'reaction'> {
  readonly kind = 'reaction' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['reaction']): void {
    const unit = context.battle.unit(action.unit);
    if (!unit.isOwnedBy(context.state.currentPlayer)) context.fail('不是你的单位');
    if (unit.state.done) context.fail('已行动单位不能再调整反应姿态');
    unit.changeReaction(action.stance);
    context.emit({ type: 'reactionChanged', unit: unit.id, stance: action.stance });
  }
}

class FaceActionHandler implements ActionHandler<'face'> {
  readonly kind = 'face' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['face']): void {
    const unit = context.battle.unit(action.unit);
    if (!unit.isOwnedBy(context.state.currentPlayer)) context.fail('不是你的单位');
    if (unit.state.done) context.fail('已行动单位不能再调整朝向');
    const previous = unit.changeFacing(action.facing);
    if (previous !== action.facing) {
      context.emit({ type: 'facingChanged', unit: unit.id, from: previous, to: action.facing });
    }
  }
}

class ChangeCareerActionHandler implements ActionHandler<'changeCareer'> {
  readonly kind = 'changeCareer' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['changeCareer']): void {
    const unit = context.battle.unit(action.unit);
    if (!unit.isOwnedBy(context.state.currentPlayer)) context.fail('不是你的单位');
    if (unit.state.done) context.fail('已行动单位不能转职');
    try {
      changeCareer(context.state, unit.state, action.career, context.emit, context.rules.resources, context.rules.content);
    } catch (error) {
      context.fail((error as Error).message);
    }
  }
}

class ChangeFormationActionHandler implements ActionHandler<'changeFormation'> {
  readonly kind = 'changeFormation' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['changeFormation']): void {
    const unit = context.battle.unit(action.unit);
    if (!unit.isOwnedBy(context.state.currentPlayer)) context.fail('不是你的单位');
    if (unit.state.done) context.fail('已行动单位不能调整阵形');
    try {
      validateFormationChange(context.state, unit.state, action.formation, context.rules.content);
    } catch (error) {
      context.fail((error as Error).message);
    }
    const from = unit.state.formation;
    if (from === action.formation) return;
    unit.state.formation = action.formation;
    context.emit({ type: 'formationChanged', unit: unit.id, from, to: action.formation });
  }
}

class EmbarkActionHandler implements ActionHandler<'embark'> {
  readonly kind = 'embark' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['embark']): void {
    const unit = requireUnit(context.state, action.unit);
    const carrier = requireUnit(context.state, action.carrier);
    if (unit.owner !== context.state.currentPlayer || carrier.owner !== context.state.currentPlayer) {
      context.fail('只能操作当前玩家的运输编组');
    }
    if (unit.done) context.fail('已行动单位不能登载');
    try {
      embarkUnit(context.state, unit.id, carrier.id, context.emit, context.rules.content);
    } catch (error) {
      context.fail((error as Error).message);
    }
  }
}

class DisembarkActionHandler implements ActionHandler<'disembark'> {
  readonly kind = 'disembark' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['disembark']): void {
    const carrier = requireUnit(context.state, action.carrier);
    if (carrier.owner !== context.state.currentPlayer) context.fail('不是你的运输单位');
    if (carrier.done) context.fail('已行动运输单位不能卸载');
    try {
      disembarkUnit(context.state, carrier.id, action.unit, action.at, context.emit, context.rules.content);
      new UnitEntity(carrier).finishAction();
    } catch (error) {
      context.fail((error as Error).message);
    }
  }
}

class RecruitActionHandler implements ActionHandler<'recruit'> {
  readonly kind = 'recruit' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['recruit']): void {
    const state = context.state;
    const index = idx(state.map, action.at.x, action.at.y);
    const terrain = context.rules.content.terrains.get(state.map.tiles[index]);
    if (!terrain.produces.includes(action.unit)) context.fail(`${terrain.name} 无法生产该兵种`);
    if (state.map.owners[index] !== state.currentPlayer) context.fail('该建筑不属于你');
    if (unitAt(state, action.at.x, action.at.y)) context.fail('建筑上已有单位');

    const owner = player(state, state.currentPlayer);
    const definition = context.rules.content.units.get(action.unit);
    const resourceOwner = playerResource(owner);
    if (!context.rules.resources.canAfford(definition.recruitCosts, resourceOwner)) {
      context.fail('招募资源不足');
    }
    const cap = state.rules.maxUnitsPerPlayer;
    if (cap !== null && unitsOf(state, owner.id).length >= cap) {
      context.fail(`单位数量已达上限 ${cap}`);
    }

    const spent = context.rules.resources.spendAll(definition.recruitCosts, resourceOwner);
    for (const cost of spent) {
      const current = context.rules.resources.balance(cost.resource, resourceOwner);
      if (current !== null) {
        context.emit({
          type: 'resourceChanged',
          resource: cost.resource,
          subject: { kind: 'player', id: owner.id },
          amount: -cost.amount,
          current,
        });
      }
    }
    const unit = spawnUnit(state, action.unit, owner.id, action.at, {
      done: !state.rules.recruitsActImmediately,
    }, context.rules.content);
    context.emit({ type: 'recruit', unit: unit.id, at: action.at });
  }
}

class EndTurnActionHandler implements ActionHandler<'endTurn'> {
  readonly kind = 'endTurn' as const;

  execute(context: ActionExecutionContext): void {
    advanceTurn(context.state, context.emit, context.rules);
  }
}

export const CoreActionHandlers = new ActionHandlerRegistry()
  .register(new DeployUnitActionHandler())
  .register(new FinishDeploymentActionHandler())
  .register(new CommandActionHandler())
  .register(new TacticActionHandler())
  .register(new ReactionActionHandler())
  .register(new FaceActionHandler())
  .register(new ChangeCareerActionHandler())
  .register(new ChangeFormationActionHandler())
  .register(new EmbarkActionHandler())
  .register(new DisembarkActionHandler())
  .register(new RecruitActionHandler())
  .register(new EndTurnActionHandler());

/** Mutates `state` using an injectable action-strategy registry. */
export function applyActionWith(
  state: GameState,
  action: Action,
  handlers: ActionHandlerRegistry,
  rules: BattleRuleServices,
): GameEvent[] {
  const context = new ActionExecutionContext(state, rules);
  if (state.phase === 'over') context.fail('对局已结束');
  const deploymentAction = action.kind === 'deployUnit' || action.kind === 'finishDeployment';
  if (state.phase === 'deployment' && !deploymentAction) context.fail('请先完成战前部署');
  if (state.phase === 'playing' && deploymentAction) context.fail('战斗开始后不能重新部署');
  handlers.dispatch(context, action);
  if (state.phase !== 'playing' || deploymentAction) return context.events;
  runScenarioTriggers(
    state,
    'afterAction',
    context.emit,
    rules.scenarioConditions,
    rules.scenarioEffects,
    rules.resources,
    rules.content,
  );
  checkGameOver(state, context.emit, rules);
  return context.events;
}

export function applyAction(state: GameState, action: Action): GameEvent[] {
  return applyActionWith(state, action, CoreActionHandlers, DefaultBattleRuleServices);
}

/* ---------------------------------------------------------------- turn cycle */

/**
 * Seeds the ordering policy and claims the first actor turn. Called once the
 * battle actually starts, i.e. after deployment or straight from construction.
 */
export function startTurnOrder(
  state: GameState,
  rules: BattleRuleServices,
  emit: (event: GameEvent) => void = () => {},
): void {
  const policy = rules.turnOrders.get(state.rules.turnOrder);
  state.turnOrder = policy.initialState(state, rules.content);
  const handoff = policy.begin(state, { content: rules.content, emit });
  state.currentPlayer = handoff.player;
  state.turnOrder.activeUnit = handoff.activeUnit;
}

/**
 * Hands the actor turn over through the battle's ordering policy.
 *
 * The engine no longer knows whether a turn belongs to a side or to a single
 * unit; it only knows the difference between a *round* (battle clock: income,
 * overlay decay, `everyRounds` triggers) and an *actor turn* (statuses, healing,
 * cooldowns, reaction budget).
 */
function advanceTurn(s: GameState, emit: (e: GameEvent) => void, rules: BattleRuleServices): void {
  emit({ type: 'turnEnd', player: s.currentPlayer });
  runScenarioTriggers(s, 'turnEnd', emit, rules.scenarioConditions, rules.scenarioEffects, rules.resources, rules.content);

  const policy = rules.turnOrders.get(s.turnOrder.policy);
  const handoff = policy.advance(s, { content: rules.content, emit });
  if (handoff.exhausted) {
    s.phase = 'over';
    return;
  }
  if (handoff.roundAdvanced) {
    s.turn++;
    advanceTerrainOverlayRound(s, emit);
    emit({ type: 'roundStart', turn: s.turn });
  }
  s.currentPlayer = handoff.player;
  s.turnOrder.activeUnit = handoff.activeUnit;
  beginTurn(s, emit, rules, handoff.roundAdvanced);
}

function grantTurnResources(
  s: GameState,
  owner: PlayerId,
  emit: (e: GameEvent) => void,
  rules: BattleRuleServices,
): void {
  const p = player(s, owner);
  const resourceOwner = playerResource(p);
  for (const grant of turnResourceGrantsFor(s, p.id, rules.content)) {
    if (!rules.resources.hasAccount(grant.resource, resourceOwner)) continue;
    const amount = rules.resources.credit(grant.resource, resourceOwner, grant.amount);
    const current = rules.resources.balance(grant.resource, resourceOwner);
    if (amount > 0 && current !== null) {
      emit({
        type: 'resourceChanged',
        resource: grant.resource,
        subject: { kind: 'player', id: p.id },
        amount,
        current,
      });
    }
  }
}

function beginTurn(
  s: GameState,
  emit: (e: GameEvent) => void,
  rules: BattleRuleServices,
  roundAdvanced: boolean,
): void {
  const p = player(s, s.currentPlayer);
  const active = s.turnOrder.activeUnit;
  // Side turns refresh a whole army; per-unit orders refresh only the actor.
  const scope = active === null
    ? unitsOf(s, p.id)
    : s.units.filter((candidate) => candidate.id === active);

  emit(active === null
    ? { type: 'turnStart', player: p.id, turn: s.turn }
    : { type: 'turnStart', player: p.id, turn: s.turn, activeUnit: active });

  applyOverlayTurnStartEffects(s, p.id, emit, rules.content, scope);
  resolveTurnStartStatuses(rules, s, emit, scope, (unitId) =>
    handleCommanderDefeat(s, unitId, emit, rules.content));

  if (active === null) {
    // One income grant per player per round; side turns already give each
    // player exactly one actor turn per round.
    refreshCommanderTurn(s, p.id, emit, rules.resources);
    grantTurnResources(s, p.id, emit, rules);
  } else if (roundAdvanced) {
    for (const candidate of s.players.filter((entry) => entry.alive)) {
      refreshCommanderTurn(s, candidate.id, emit, rules.resources);
      grantTurnResources(s, candidate.id, emit, rules);
    }
  }

  for (const u of scope) {
    advanceWeaponCooldowns(u);
    const entity = new UnitEntity(u);
    entity.readyForTurn();
    const heal = healRateAt(s, u.x, u.y, p.id, rules.content);
    if (heal > 0) {
      const max = rules.content.units.get(u.type).maxHp;
      const amount = Math.min(heal, max - u.hp);
      if (amount > 0) {
        const healed = entity.heal(amount, max);
        emit({ type: 'regen', unit: u.id, amount: healed });
      }
    }
  }


  runScenarioTriggers(s, 'turnStart', emit, rules.scenarioConditions, rules.scenarioEffects, rules.resources, rules.content);
}

function checkGameOver(s: GameState, emit: (e: GameEvent) => void, rules: BattleRuleServices): void {
  const before = s.players.filter((p) => p.alive).map((p) => p.id);
  const result = evaluateVictory(rules, s, emit);
  for (const id of before) {
    if (!player(s, id).alive) emit({ type: 'defeat', player: id });
  }
  if (result.team !== null || result.reason) {
    s.phase = 'over';
    s.winnerTeam = result.team;
    s.endReason = result.reason;
    emit({ type: 'gameOver', team: result.team, reason: result.reason });
  }
}

/** Marks every remaining unit done — used by "end turn" confirmation UI. */
/** Units still entitled to act under the battle's ordering policy. */
export function idleUnits(rules: BattleRuleServices, s: GameState): Unit[] {
  return rules.turnOrders.get(s.turnOrder.policy).actors(s);
}

/** Removes a unit outright (used by editor previews and scripted effects). */
export function killUnit(s: GameState, id: number): void {
  removeUnit(s, id);
}
