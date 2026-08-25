import { IllegalActionError } from './domain/errors';
import { CommanderEntity } from './domain/commander-entity';
import type { UnitDepartureHandler } from './unit-departure';
import type { MoraleRules } from './morale';
import { boardOf } from './domain/board';
import type { GridRules } from './tactical-grid';
import { addStatus, removeStatus } from './statuses';
import { areAllies, player } from './state';
import {
  BattleResourceSystem,
  playerResource,
} from './resources';
import type {
  CommanderAura,
  CommanderState,
  Coord,
  GameEvent,
  GameState,
  TacticId,
  TacticDef,
  Unit,
} from './types';
import { type ContentCatalog } from './content-pack';
import { changeMorale } from './morale';

const NO_COMMAND_AURA: CommanderAura = {
  attackMultiplier: 1,
  defenseDelta: 0,
  movementDelta: 0,
};

export function commanderUnit(state: GameState, commander: CommanderState): Unit | undefined {
  return state.units.find((unit) => unit.id === commander.unitId);
}

export function activeCommanderFor(rules: GridRules, state: GameState, unit: Unit): CommanderState | null {
  if (!unit.commanderId) return null;
  const commander = state.commanders.find((candidate) => candidate.id === unit.commanderId);
  if (!commander || commander.owner !== unit.owner) return null;
  const leader = commanderUnit(state, commander);
  if (!leader || leader.owner !== unit.owner) return null;
  return boardOf(rules, state).distance(unit, leader) <= commander.radius ? commander : null;
}

export function commanderAuraFor(rules: GridRules, state: GameState, unit: Unit): CommanderAura {
  return activeCommanderFor(rules, state, unit)?.aura ?? NO_COMMAND_AURA;
}

export interface TacticOption {
  id: TacticId;
  name: string;
  costs: TacticDef['costs'];
  targets: Coord[];
}

/**
 * Port declared by this module. The composition-level `BattleRuleServices`
 * satisfies it structurally, so neither side needs to import the other.
 */
export interface CommanderRules extends GridRules {
  readonly content: ContentCatalog;
  readonly resources: BattleResourceSystem;
}

export function tacticOptions(
  rules: CommanderRules,
  state: GameState,
  commanderId: string,
): TacticOption[] {
  const { content, resources } = rules;
  const commander = state.commanders.find((candidate) => candidate.id === commanderId);
  if (!commander || commander.owner !== state.currentPlayer) return [];
  const leader = commanderUnit(state, commander);
  if (!leader) return [];
  const owner = player(state, commander.owner);
  const options: TacticOption[] = [];
  for (const id of commander.tactics) {
    if (commander.usedTactics.includes(id)) continue;
    const tactic = content.tactics.get(id);
    if (!resources.canAfford(tactic.costs, playerResource(owner))) continue;
    const targets: Coord[] = [];
    if (tactic.target === 'self') {
      targets.push({ x: leader.x, y: leader.y });
    } else {
      for (let y = 0; y < state.map.height; y++) {
        for (let x = 0; x < state.map.width; x++) {
          if (boardOf(rules, state).distance(leader, { x, y }) <= tactic.range) targets.push({ x, y });
        }
      }
    }
    options.push({ id, name: tactic.name, costs: tactic.costs.map((cost) => ({ ...cost })), targets });
  }
  return options;
}

export function executeTactic(
  rules: CommanderRules,
  state: GameState,
  commanderId: string,
  tacticId: string,
  requestedTarget: Coord | undefined,
  emit: (event: GameEvent) => void,
): void {
  const { content, resources } = rules;
  const commander = state.commanders.find((candidate) => candidate.id === commanderId);
  if (!commander) throw new IllegalActionError(`unknown commander "${commanderId}"`);
  if (commander.owner !== state.currentPlayer) throw new IllegalActionError('commander does not belong to current player');
  const option = tacticOptions(rules, state, commanderId).find((candidate) => candidate.id === tacticId);
  if (!option) throw new IllegalActionError(`tactic "${tacticId}" is not available`);
  const leader = commanderUnit(state, commander);
  if (!leader) throw new IllegalActionError(`commander "${commanderId}" has no unit on the field`);
  const tactic = content.tactics.get(tacticId);
  const target = tactic.target === 'self' ? { x: leader.x, y: leader.y } : requestedTarget;
  if (!target || !option.targets.some((cell) => cell.x === target.x && cell.y === target.y)) {
    throw new IllegalActionError('illegal tactic target');
  }

  const owner = player(state, commander.owner);
  const resourceOwner = playerResource(owner);
  const spent = resources.spendAll(tactic.costs, resourceOwner);
  commander.usedTactics.push(tactic.id);
  for (const cost of spent) resources.announce(resourceOwner, cost.resource, -cost.amount, emit);
  emit({ type: 'tacticUsed', commander: commander.id, tactic: tactic.id, target });

  const affected = state.units.filter(
    (unit) => areAllies(state, unit.owner, commander.owner) &&
      boardOf(rules, state).distance(unit, target) <= tactic.radius,
  );
  for (const effect of tactic.effects) {
    for (const unit of affected) {
      if (effect.type === 'addStatus') addStatus(content, unit, { id: effect.status, remaining: effect.duration, sourceUnitId: leader.id }, emit);
      else removeStatus(unit, effect.status, emit);
    }
  }
}

export function refreshCommanderTurn(
  resources: BattleResourceSystem,
  state: GameState,
  ownerId: number,
  emit: (event: GameEvent) => void,
): void {
  const owner = player(state, ownerId);
  const commanders = state.commanders.filter(
    (commander) => commander.owner === ownerId && commanderUnit(state, commander),
  );
  for (const commander of commanders) new CommanderEntity(commander).refreshTactics();
  const grants = new Map<string, number>();
  for (const commander of commanders) {
    for (const grant of commander.turnGrants) {
      grants.set(grant.resource, (grants.get(grant.resource) ?? 0) + grant.amount);
    }
  }
  const resourceOwner = playerResource(owner);
  for (const [resource, requested] of grants) {
    if (!resources.hasAccount(resource, resourceOwner)) continue;
    resources.announce(resourceOwner, resource, resources.credit(resource, resourceOwner, requested), emit);
  }
}

/** Linked troops lose the aura immediately and receive a short morale shock. */
function handleCommanderDefeat(
  rules: MoraleRules,
  state: GameState,
  unitId: number,
  emit: (event: GameEvent) => void,
): void {
  const commander = state.commanders.find((candidate) => candidate.unitId === unitId);
  if (!commander) return;
  const content = rules.content;
  emit({ type: 'commanderDefeated', commander: commander.id, unit: unitId });
  for (const unit of state.units.filter(
    (candidate) => candidate.commanderId === commander.id && candidate.owner === commander.owner,
  )) {
    if (content.statuses.has('shaken')) addStatus(content, unit, { id: 'shaken', remaining: 2, sourceUnitId: unitId }, emit);
    if (state.rules.moraleEnabled && state.units.some((candidate) => candidate.id === unit.id)) {
      changeMorale(rules, state, unit.id, -state.rules.moraleCommanderDefeatLoss, 'commander-defeated', emit);
    }
  }
}

/**
 * Losing the commander is a consequence of departure, not of *how* it departed,
 * so it is registered once here instead of being called from every death site.
 * Nothing else may call it: a consequence with a second entry point is a
 * consequence that will eventually fire twice, or not at all.
 */
export const CommanderDefeatDepartureHandler: UnitDepartureHandler = {
  id: 'commander.defeat',
  handle: ({ rules, state, unit, emit }) => handleCommanderDefeat(rules, state, unit.id, emit),
};
