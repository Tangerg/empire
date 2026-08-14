import { commandOptions } from '../actions';
import { tacticOptions, type CommanderRules } from '../commanders';
import { unitWeapons } from '../combat';
import { dist, idx } from '../grid';
import type { MoveField } from '../movement';
import { type BattleResourceSystem, playerResource } from '../resources';
import {
  areAllies,
  enemyUnitsOf,
  player,
  productionTilesOf,
  unitAt,
  unitsOf,
} from '../state';
import { hasStatus } from '../statuses';
import { directiveOf } from '../unit-directive';
import type { ContentCatalog } from '../content-pack';
import type { Action, Coord, GameState, PlayerId, Unit, UnitTypeId } from '../types';
import type { AiAgenda } from './agenda';
import {
  hpRatio,
  maximumWeaponPower,
  maximumWeaponRange,
  weaponsForType,
} from './measures';
import { TileAppraisal } from './tile-appraisal';
import { AiIntentRegistry, type AiTurnContext } from './turn-context';

/** An action the planner is willing to take, with what it thinks of it. */
interface ScoredAction {
  readonly action: Action;
  readonly score: number;
}

/* ------------------------------------------------------------------- commands */

/**
 * Walks a completed move field back to the start, giving the path that reaches
 * `to`. Returns null when the tile is not in the field at all.
 */
function pathTo(field: MoveField, state: GameState, to: Coord): Coord[] | null {
  const steps: Coord[] = [];
  let cursor = idx(state.map, to.x, to.y);
  for (let guard = 0; guard <= field.tiles.size + 1; guard++) {
    const node = field.tiles.get(cursor);
    if (!node) return null;
    steps.push({ x: node.x, y: node.y });
    if (node.from === -1) {
      steps.reverse();
      return steps;
    }
    cursor = node.from;
  }
  return null;
}

/**
 * The best (destination, ability, target) triple this unit can legally produce.
 *
 * One ply, deliberately: the search stays instant, and every option it scores
 * came out of the same legality gate the human player's UI uses.
 */
function bestCommandFor(context: AiTurnContext, unit: Unit): ScoredAction | null {
  const { rules, state } = context;
  const field = rules.space.moveField(state, unit);
  let best: ScoredAction | null = null;
  const consider = (candidate: ScoredAction) => {
    if (!best || candidate.score > best.score) best = candidate;
  };

  for (const stop of field.stops) {
    const at = { x: stop % state.map.width, y: Math.floor(stop / state.map.width) };
    const path = pathTo(field, state, at);
    if (!path) continue;
    const tileValue = new TileAppraisal(context, unit, at).value;

    for (const option of commandOptions(rules, state, unit, at)) {
      const evaluator = context.abilityEvaluators.tryGet(option.ability);
      if (!evaluator) continue;
      const targets: Array<Coord | null> = option.selfTargeted ? [null] : option.targets;
      for (const target of targets) {
        const score = evaluator.score({
          rules,
          state,
          unit,
          at,
          option,
          target,
          tileValue,
          mission: context.agenda.mission,
        });
        if (score === null || !Number.isFinite(score)) continue;
        // A unit under orders to withdraw does not stop to trade blows.
        const withdrawing = option.ability === 'attack' ? directiveOf(rules, unit).fightPenalty : 0;
        consider({
          action: {
            kind: 'command',
            unit: unit.id,
            path,
            command: target
              ? { ability: option.ability, weapon: option.weapon, target }
              : { ability: option.ability, weapon: option.weapon },
          },
          score: score + withdrawing,
        });
      }
    }
  }

  return best;
}

/* -------------------------------------------------------------------- tactics */

function proposeTactic(rules: CommanderRules, state: GameState, side: PlayerId): Action | null {
  const { content, resources } = rules;
  let best: ScoredAction | null = null;
  for (const commander of state.commanders.filter((entry) => entry.owner === side)) {
    for (const option of tacticOptions(rules, state, commander.id)) {
      const tactic = content.tactics.get(option.id);
      for (const target of option.targets) {
        const affected = unitsOf(state, side).filter((unit) => dist(unit, target) <= tactic.radius);
        let score = 0;
        for (const effect of tactic.effects) {
          if (effect.type === 'addStatus') {
            score += affected.filter((unit) => !hasStatus(unit, effect.status)).length * 30;
          } else {
            score += affected.filter((unit) => hasStatus(unit, effect.status)).length * 35;
          }
        }
        score -= resources.planningValue(option.costs);
        if (score > 0 && (!best || score > best.score)) {
          best = {
            action: { kind: 'tactic', commander: commander.id, tactic: option.id, target },
            score,
          };
        }
      }
    }
  }
  return best?.action ?? null;
}

/* -------------------------------------------------------------------- stances */

/** The stance this unit's situation calls for, whatever it is standing in now. */
function preferredStance(
  state: GameState,
  unit: Unit,
  agenda: AiAgenda,
  content: ContentCatalog,
): Unit['reaction'] {
  const definition = content.units.get(unit.type);
  const protectedWeight = agenda.mission.protectedUnits.get(unit.id) ?? 0;
  const enemyNear = enemyUnitsOf(state, unit.owner).some((enemy) =>
    dist(unit, enemy) <= content.units.get(enemy.type).movement + maximumWeaponRange(enemy.type, content));
  if (hpRatio(unit, content) < 0.35 || (protectedWeight >= 4 && enemyNear)) return 'guard';

  const adjacentAlly = state.units.some((candidate) =>
    candidate.id !== unit.id &&
    areAllies(state, candidate.owner, unit.owner) &&
    dist(candidate, unit) === 1);
  if (adjacentAlly && (definition.tags.includes('support') || definition.tags.includes('monster'))) return 'support';

  const hasScarceRangedWeapon = definition.tags.includes('ranged') && unitWeapons(unit, content).some((weapon) =>
    weapon.tags.includes('ranged') && (weapon.cooldown > 0 || weapon.resourceCosts.length > 0));
  if (hasScarceRangedWeapon) return 'conserve';
  return definition.defaultReaction;
}

function proposeStanceChange(
  state: GameState,
  side: PlayerId,
  agenda: AiAgenda,
  content: ContentCatalog,
): Action | null {
  for (const unit of unitsOf(state, side).filter((candidate) => !candidate.done)) {
    const stance = preferredStance(state, unit, agenda, content);
    if (stance !== unit.reaction) return { kind: 'reaction', unit: unit.id, stance };
  }
  return null;
}

/* ---------------------------------------------------------------- recruitment */

function scoreRecruit(
  state: GameState,
  side: PlayerId,
  type: UnitTypeId,
  resources: BattleResourceSystem,
  content: ContentCatalog,
): number {
  const definition = content.units.get(type);
  const foes = enemyUnitsOf(state, side);
  const mine = unitsOf(state, side);

  let matchup = 1;
  if (foes.length > 0) {
    let total = 0;
    for (const foe of foes) {
      const armor = content.units.get(foe.type).armorClass;
      total += Math.max(...weaponsForType(type, content).map((weapon) =>
        content.damageMatchups.effectiveness(weapon.damageType, armor)));
    }
    matchup = total / foes.length;
  }

  // Value per gold, nudged by how well it answers what we are facing.
  let score = (maximumWeaponPower(type, content) * matchup + definition.maxHp * 0.35 + definition.movement * 12) /
    Math.max(0.1, resources.planningValue(definition.recruitCosts) / 100);

  const infantry = mine.filter((unit) => content.units.get(unit.type).abilities.includes('capture')).length;
  if (definition.abilities.includes('capture') && infantry < 2) score *= 1.6;

  const composition = mine.filter((unit) => unit.type === type).length;
  score *= 1 - Math.min(0.4, composition * 0.08); // discourage one-note armies

  return score;
}

function proposeRecruit(
  state: GameState,
  side: PlayerId,
  resources: BattleResourceSystem,
  content: ContentCatalog,
): Action | null {
  const account = playerResource(player(state, side));
  const cap = state.rules.maxUnitsPerPlayer;
  if (cap !== null && unitsOf(state, side).length >= cap) return null;

  for (const at of productionTilesOf(state, side, content)) {
    if (unitAt(state, at.x, at.y)) continue;
    const terrain = content.terrains.get(state.map.tiles[idx(state.map, at.x, at.y)]);
    const affordable = terrain.produces.filter((id) =>
      resources.canAfford(content.units.get(id).recruitCosts, account));
    if (affordable.length === 0) continue;

    const ranked = affordable
      .map((id) => ({ id, score: scoreRecruit(state, side, id, resources, content) }))
      .sort((left, right) => right.score - left.score);

    return { kind: 'recruit', at, unit: ranked[0].id };
  }
  return null;
}

/* -------------------------------------------------------------------- the four */

export const DefaultAiIntents = new AiIntentRegistry()
  .register({
    id: 'tactic',
    priority: 10,
    propose: (context) => proposeTactic(context.rules, context.state, context.player),
  })
  .register({
    id: 'recruit',
    priority: 20,
    propose: (context) =>
      proposeRecruit(context.state, context.player, context.rules.resources, context.content),
  })
  .register({
    id: 'reaction',
    priority: 30,
    propose: (context) =>
      proposeStanceChange(context.state, context.player, context.agenda, context.content),
  })
  .register({
    id: 'command',
    priority: 40,
    propose: (context) => {
      let best: ScoredAction | null = null;
      for (const unit of context.actors()) {
        const candidate = bestCommandFor(context, unit);
        if (candidate && (!best || candidate.score > best.score)) best = candidate;
      }
      return best?.action ?? null;
    },
  });
