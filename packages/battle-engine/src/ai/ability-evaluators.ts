import { type CommandOption } from '../actions';
import { forecastCombatPlan, type CombatPlan } from '../combat-plan';
import { unitWeapons } from '../combat';
import { idx } from '../grid';
import type { Board } from '../domain/board';
import { KeyedRegistry } from '../registry';
import { areEnemies, unitAt } from '../state';
import { structureAt } from '../structures';
import type { ContentCatalog } from '../content-pack';
import type { Coord, GameState, Unit } from '../types';
import type { AiMissionIntent } from '../ai-objectives';
import { unitWorth } from './measures';
import type { AiRules } from './rules';

export interface AbilityAiEvaluationContext {
  state: GameState;
  unit: Unit;
  at: Coord;
  option: CommandOption;
  target: Coord | null;
  /** What standing on `at` is worth before this ability is considered at all. */
  tileValue: number;
  mission: AiMissionIntent;
  /** Ruleset used for legality and prediction; identical to execution's. */
  readonly rules: AiRules;
  /** The board under its tiling, so an evaluator measures what the rules measure. */
  readonly board: Board;
}

export interface AbilityAiEvaluator {
  readonly ability: string;
  score(context: AbilityAiEvaluationContext): number | null;
}

/** Ability plugins register tactical utility here alongside legality/execution. */
export class AbilityAiEvaluatorRegistry extends KeyedRegistry<string, AbilityAiEvaluator> {
  constructor() {
    super('AI ability evaluator');
  }

  protected keyOf(evaluator: AbilityAiEvaluator): string {
    return evaluator.ability;
  }

  clone(): AbilityAiEvaluatorRegistry {
    return this.copyInto(new AbilityAiEvaluatorRegistry());
  }
}

/** What an area strike is worth beyond its primary target. */
function collateralValue(state: GameState, plan: CombatPlan, content: ContentCatalog): number {
  let score = 0;
  for (const hit of plan.unitHits.filter((candidate) => !candidate.primary)) {
    const target = state.units.find((unit) => unit.id === hit.target);
    if (!target) continue;
    const dealt = Math.min(hit.damage.damage, target.hp) / content.units.get(target.type).maxHp;
    score += dealt * content.units.get(target.type).value;
    if (hit.killed) score += unitWorth(target, content) * 0.5;
  }
  for (const hit of plan.structureHits.filter((candidate) => !candidate.primary)) {
    score += hit.forecast.damage * 7 + (hit.forecast.destroyed ? 700 : 0);
  }
  const support = plan.supportAttack;
  if (support) {
    const target = state.units.find((unit) => unit.id === support.target);
    if (target) {
      score += Math.min(support.damage.damage, support.hpBefore) /
        content.units.get(target.type).maxHp * content.units.get(target.type).value;
      if (support.killed) score += unitWorth(target, content) * 0.5;
    }
  }
  return score;
}

const evaluator = (
  ability: string,
  score: AbilityAiEvaluator['score'],
): AbilityAiEvaluator => ({ ability, score });

export const DefaultAbilityAiEvaluators = new AbilityAiEvaluatorRegistry()
  .register(evaluator('wait', (context) => context.tileValue))
  .register(evaluator('capture', (context) => {
    const tile = context.rules.content.terrains.get(context.state.map.tiles[idx(context.state.map, context.at.x, context.at.y)]);
    const prize = tile.hq ? 4000 : tile.produces.length > 0 ? 900 : 600;
    return context.tileValue * 0.3 + prize;
  }))
  .register(evaluator('heal', (context) => {
    if (!context.target) return null;
    const ally = unitAt(context.state, { x: context.target.x, y: context.target.y });
    if (!ally) return null;
    const def = context.rules.content.units.get(ally.type);
    const healed = Math.min(30, def.maxHp - ally.hp);
    return context.tileValue * 0.3 + (healed / def.maxHp) * def.value * 0.9;
  }))
  .register(evaluator('attack', (context) => {
    if (!context.target || !context.option.weapon) return null;
    // Charge time is not modelled by the planner yet: a delayed strike would be
    // scored as if it landed now, and the caster would lock itself for a strike
    // its target can simply walk out of. Declining to have an opinion is the
    // honest answer — the option stays available to a human.
    if (context.rules.content.weapons.get(context.option.weapon).castTurns > 0) return null;
    const foe = unitAt(context.state, { x: context.target.x, y: context.target.y });
    // Scenario effects and morale resolution may change ownership between
    // option enumeration and scoring.  Treat that option as stale instead of
    // asking combat prediction to plan an illegal friendly-fire strike.
    if (foe && !areEnemies(context.state, foe.owner, context.unit.owner)) return null;
    const plan = forecastCombatPlan(context.rules, context.state, context.unit, context.target, {
      from: context.at,
      weapon: context.option.weapon,
    });
    if (!foe) {
      const structure = structureAt(context.state, context.target.x, context.target.y);
      const forecast = plan.primaryStructure;
      if (!structure || !forecast) return null;
      return context.tileValue * 0.25 +
        forecast.damage * 8 +
        (forecast.destroyed ? 900 : 0) +
        (context.mission.priorityStructures.get(structure.id) ?? 0) * 300 +
        collateralValue(context.state, plan, context.rules.content);
    }
    const forecast = plan.primaryUnit;
    if (!forecast) return null;
    const foeDef = context.rules.content.units.get(foe.type);
    const unitDefinition = context.rules.content.units.get(context.unit.type);
    const dealt = Math.min(forecast.strike.damage, foe.hp) / foeDef.maxHp;
    let score = dealt * foeDef.value * 1.15;
    if (forecast.defenderDies) score += unitWorth(foe, context.rules.content) * 0.65;
    if (forecast.counter) {
      const taken = Math.min(forecast.counter.damage, context.unit.hp) / unitDefinition.maxHp;
      score -= taken * unitDefinition.value;
      if (forecast.attackerDies) score -= unitWorth(context.unit, context.rules.content) * 0.9;
    }
    if (foeDef.tags.includes('support') || unitWeapons(context.rules.content, foe).every((weapon) => weapon.minRange > 1)) score *= 1.1;
    score += (context.mission.priorityUnits.get(foe.id) ?? 0) * 260;
    for (const [protectedId, weight] of context.mission.protectedUnits) {
      const protectedUnit = context.state.units.find((candidate) => candidate.id === protectedId);
      if (protectedUnit) score += weight * 80 / (1 + context.board.distance(foe, protectedUnit));
    }
    return context.tileValue * 0.25 + score + collateralValue(context.state, plan, context.rules.content);
  }));
