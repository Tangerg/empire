import { byId } from './id-order';
import {
  abilityDef,
  abilityTargets,
  canUseAbility,
  type AbilityQuery,
  type AbilityRules,
} from './abilities';
import { idx, sameCoord } from './grid';
import { player, spawnUnit, unitsOf } from './state';
import { runScenarioTriggers } from './scenario';
import { executeTactic } from './commanders';
import { UnitEntity } from './domain/index';
import { changeCareer, unitAbilityIds } from './careers';
import { validateFormationChange } from './formations';
import { confirmDeployment, deployUnit } from './deployment';
import { disembarkUnit, embarkUnit } from './transports';
import { playerResource } from './resources';
import { resolvePartingShots } from './zone-of-control';
import { directiveOf } from './unit-directive';
import {
  ActionExecutionContext,
  ActionHandlerRegistry,
  type ActionHandler,
  type BattleRuleServices,
} from './action-system';
import type { Action, ActionKindMap, Coord, GameEvent, GameState, Unit, WeaponId } from './types';

/* --------------------------------------------------------------- enumeration */

export interface CommandOption {
  /** Stable identity in the command menu; unique even for multiple weapons. */
  key: string;
  ability: string;
  weapon?: WeaponId | undefined;
  name: string;
  hint: string;
  selfTargeted: boolean;
  targets: Coord[];
}

/** Command menu for a unit that has moved to `at`. */
export function commandOptions(
  rules: AbilityRules,
  state: GameState,
  unit: Unit,
  at: Coord,
): CommandOption[] {
  const content = rules.content;
  const moved = !(sameCoord(unit, at));
  const q: AbilityQuery = { state: state, unit, at, moved };
  const out: CommandOption[] = [];
  for (const abilityId of unitAbilityIds(content, unit)) {
    const ability = abilityDef(rules, abilityId);
    // An ability that fires nothing is one order; an ability that fires a
    // weapon is one order per weapon, and it says which weapons those are.
    const choices = ability.weaponChoices(rules, q);
    const orders = choices.length > 0
      ? choices.map((weapon) => ({ weapon, query: { ...q, weaponId: weapon.id } }))
      : [{ weapon: null, query: q }];
    for (const { weapon, query } of orders) {
      if (!canUseAbility(rules, ability, query)) continue;
      const targets = abilityTargets(rules, ability, query);
      if (!ability.selfTargeted && targets.length === 0) continue;
      out.push({
        key: weapon ? `${ability.id}:${weapon.id}` : ability.id,
        ability: ability.id,
        weapon: weapon?.id,
        name: weapon ? `${ability.name} · ${weapon.name}` : ability.name,
        hint: weapon ? `${ability.hint} 射程 ${weapon.minRange}-${weapon.maxRange}` : ability.hint,
        selfTargeted: ability.selfTargeted,
        targets,
      });
    }
  }
  return out.sort((a, b) => {
    const priority = (id: string) => abilityDef(rules, id).priority;
    return priority(a.ability) - priority(b.ability) || byId(a.key, b.key);
  });
}

/* ------------------------------------------------------------------- helpers */

interface ValidatedMovement {
  destination: Coord;
  path: Coord[];
}

function validatePath(
  context: ActionExecutionContext,
  unit: Unit,
  requestedPath: Coord[],
): ValidatedMovement {
  const state = context.state;
  if (requestedPath.length === 0) context.fail('路径为空');
  if (!sameCoord(requestedPath[0], { x: unit.x, y: unit.y })) context.fail('路径起点与单位位置不符');
  const destination = requestedPath[requestedPath.length - 1];
  if (requestedPath.length === 1) return { destination, path: [{ x: unit.x, y: unit.y }] };

  const space = context.rules.space;
  const field = space.moveField(state, unit);
  const canonical = space.pathTo(field, state, destination);
  if (!canonical) context.fail(`目标格 ${destination.x},${destination.y} 不在移动范围内`);
  if (!field.stops.has(idx(state.map, destination.x, destination.y))) context.fail('目标格已被占据');
  // Trust the engine's own cheapest path rather than the client's route.
  return { destination, path: canonical };
}

function moveUnit(
  context: ActionExecutionContext,
  unit: UnitEntity,
  movement: ValidatedMovement,
): void {
  const { destination, path } = movement;
  if (sameCoord(unit.position, destination)) return;
  const from = unit.position;
  context.battle.moveUnit(unit.id, destination);
  directiveOf(context.rules, unit.state).arriveAt?.(unit.state, destination);
  if (path.length > 1) {
    context.turnToFace(unit, context.board.grid.toward(path[path.length - 2], destination));
  }
  context.emit({ type: 'move', unit: unit.id, path });
  // Only a chosen move provokes. A unit thrown out of a zone did not disengage.
  resolvePartingShots(context.rules, context.state, unit.state, from, destination, context.emit);
}

/* ---------------------------------------------------------- action strategies */

class DeployUnitActionHandler implements ActionHandler<'deployUnit'> {
  readonly kind = 'deployUnit' as const;
  readonly duringDeployment = true;

  execute(context: ActionExecutionContext, action: ActionKindMap['deployUnit']): void {
    // Deployment predates any actor turn, so ownership is the whole rule here;
    // everything else is `deployment.ts`, which the board asks the same way.
    const unit = context.ownUnit(action.unit).state;
    deployUnit(context.rules, context.state, unit, action.at, context.emit);
  }
}

class FinishDeploymentActionHandler implements ActionHandler<'finishDeployment'> {
  readonly kind = 'finishDeployment' as const;
  readonly handsOffTurn = true;
  readonly duringDeployment = true;

  execute(context: ActionExecutionContext): void {
    confirmDeployment(context.lifecycle, context.state, context.emit);
  }
}

class CommandActionHandler implements ActionHandler<'command'> {
  readonly kind = 'command' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['command']): void {
    const actor = context.commandableUnit(action.unit, '行动');
    const unit = actor.state;
    const movement = validatePath(context, unit, action.path);
    const destination = movement.destination;
    const content = context.rules.content;
    const ability = abilityDef(context.rules, action.command.ability);
    const query: AbilityQuery = {
      state: context.state,
      unit,
      at: destination,
      moved: !sameCoord(actor.position, destination),
      weaponId: action.command.weapon,
    };
    if (!unitAbilityIds(content, unit).includes(ability.id)) {
      context.fail(`${content.units.get(unit.type).name} 没有「${ability.name}」`);
    }
    // The weapon on an order is one the ability offered. It used to be kept only
    // for the ability literally named `attack` and dropped everywhere else, so a
    // pack's second weapon-using ability fired the first weapon in the rack and
    // said nothing about it.
    const chosen = action.command.weapon;
    if (chosen && !ability.weaponChoices(context.rules, query).some((weapon) => weapon.id === chosen)) {
      context.fail(`「${ability.name}」无法使用「${chosen}」`);
    }
    if (!canUseAbility(context.rules, ability, query)) context.fail(`此处无法使用「${ability.name}」`);

    const target = action.command.target ?? null;
    if (!ability.selfTargeted) {
      if (!target) context.fail(`「${ability.name}」需要指定目标`);
      if (!abilityTargets(context.rules, ability, query).some((candidate) => sameCoord(candidate, target))) {
        context.fail('目标不合法');
      }
    }

    moveUnit(context, actor, movement);
    // The march may not have survived contact: a parting shot resolves mid-order.
    if (!context.battle.findUnit(actor.id)) return;
    if (target) context.turnToFace(actor, context.board.grid.toward(destination, target));
    ability.execute(context.rules, query, target, context.emit);
    // The ability may have killed its own user (a sacrifice, a counter-kill).
    if (context.battle.findUnit(actor.id)) actor.finishAction();
  }
}

class TacticActionHandler implements ActionHandler<'tactic'> {
  readonly kind = 'tactic' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['tactic']): void {
    executeTactic(context.rules, context.state, action.commander, action.tactic, action.target, context.emit);
  }
}

class ReactionActionHandler implements ActionHandler<'reaction'> {
  readonly kind = 'reaction' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['reaction']): void {
    const unit = context.commandableUnit(action.unit, '调整反应姿态');
    unit.changeReaction(action.stance);
    context.emit({ type: 'reactionChanged', unit: unit.id, stance: action.stance });
  }
}

class FaceActionHandler implements ActionHandler<'face'> {
  readonly kind = 'face' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['face']): void {
    context.turnToFace(context.commandableUnit(action.unit, '调整朝向'), action.facing);
  }
}

class ChangeCareerActionHandler implements ActionHandler<'changeCareer'> {
  readonly kind = 'changeCareer' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['changeCareer']): void {
    const unit = context.commandableUnit(action.unit, '转职');
    changeCareer(context.rules, context.state, unit.state, action.career, context.emit);
  }
}

class ChangeFormationActionHandler implements ActionHandler<'changeFormation'> {
  readonly kind = 'changeFormation' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['changeFormation']): void {
    const unit = context.commandableUnit(action.unit, '调整阵形');
    validateFormationChange(context.rules, context.state, unit.state, action.formation);
    if (unit.state.formation === action.formation) return;
    const from = unit.changeFormation(action.formation);
    context.emit({ type: 'formationChanged', unit: unit.id, from, to: action.formation });
  }
}

class EmbarkActionHandler implements ActionHandler<'embark'> {
  readonly kind = 'embark' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['embark']): void {
    // The passenger spends the action; the carrier only has to be yours.
    const passenger = context.commandableUnit(action.unit, '登载');
    const carrier = context.ownUnit(action.carrier);
    embarkUnit(context.rules, context.state, passenger.id, carrier.id, context.emit);
  }
}

class DisembarkActionHandler implements ActionHandler<'disembark'> {
  readonly kind = 'disembark' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['disembark']): void {
    const carrier = context.commandableUnit(action.carrier, '卸载');
    disembarkUnit(context.rules, context.state, carrier.id, action.unit, action.at, context.emit);
    carrier.finishAction();
  }
}

class RecruitActionHandler implements ActionHandler<'recruit'> {
  readonly kind = 'recruit' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['recruit']): void {
    const state = context.state;
    // Through the context, so a coordinate off the board is refused rather than
    // read out of the tile array as `undefined`.
    const cell = context.cell(action.at);
    const terrain = cell.terrain;
    if (!terrain.produces.includes(action.unit)) context.fail(`${terrain.name} 无法生产该兵种`);
    if (state.map.owners[cell.index] !== state.currentPlayer) context.fail('该建筑不属于你');
    if (cell.occupant) context.fail('建筑上已有单位');

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

    for (const cost of context.rules.resources.spendAll(definition.recruitCosts, resourceOwner)) {
      context.rules.resources.announce(resourceOwner, cost.resource, -cost.amount, context.emit);
    }
    const unit = spawnUnit(context.rules, state, action.unit, owner.id, action.at, {
      done: !state.rules.recruitsActImmediately,
    });
    context.emit({ type: 'recruit', unit: unit.id, at: action.at });
  }
}

class EndTurnActionHandler implements ActionHandler<'endTurn'> {
  readonly kind = 'endTurn' as const;
  readonly handsOffTurn = true;

  execute(context: ActionExecutionContext): void {
    context.lifecycle.advanceTurn();
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
  .register(new EndTurnActionHandler())
  .seal();

/**
 * Port declared here: the strategies that execute an action, and the rules they
 * run under. `BattleEngine` satisfies it structurally, because those are exactly
 * the two things it composes.
 */
export interface ActionDispatch {
  readonly actionHandlers: ActionHandlerRegistry;
  readonly rules: BattleRuleServices;
}

/** Mutates `state` by dispatching one action through the composed strategies. */
export function applyAction(dispatch: ActionDispatch, state: GameState, action: Action): GameEvent[] {
  const { actionHandlers: handlers, rules } = dispatch;
  const context = new ActionExecutionContext(state, rules);
  if (state.phase === 'over') context.fail('对局已结束');
  const deploymentAction = handlers.duringDeployment(action);
  if (state.phase === 'deployment' && !deploymentAction) context.fail('请先完成战前部署');
  if (state.phase === 'playing' && deploymentAction) context.fail('战斗开始后不能重新部署');
  handlers.dispatch(context, action);
  if (state.phase !== 'playing' || deploymentAction) return context.events;
  runScenarioTriggers(rules, state, 'afterAction', context.emit);
  context.lifecycle.concludeIfDecided();
  return context.events;
}
