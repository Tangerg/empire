import { commandOptions, type CommandOption } from './actions';
import { Abilities, type AbilityDef } from './abilities';
import {
  buildAiMissionIntent,
  DefaultAiObjectiveAdvisors,
  type AiMissionIntent,
  type AiObjectiveAdvisorRegistry,
} from './ai-objectives';
import { tacticOptions } from './commanders';
import { unitWeapons } from './combat';
import { DefaultCombatModifierPipeline, type CombatModifierPipeline } from './combat-modifiers';
import { forecastCombatPlan, type CombatPlan } from './combat-plan';
import { Battlefield } from './domain/battlefield';
import { dist, idx } from './grid';
import type { MoveField } from './movement';
import { ObjectiveHandlers, type ObjectiveHandlerRegistry } from './objective-system';
import {
  type BattleResourceSystem,
  DefaultBattleResources,
  playerResource,
} from './resources';
import { hasStatus } from './statuses';
import type { Registry } from './registry';
import { CoreTacticalSpace, DefaultTacticalSpace, type TacticalSpace } from './tactical-space';
import { structureAt } from './structures';
import {
  areEnemies,
  areAllies,
  enemyUnitsOf,
  player,
  productionTilesOf,
  unitAt,
  unitsOf,
} from './state';
import type { Action, Coord, GameState, Unit, UnitTypeId } from './types';
import { GlobalContentCatalog, type ContentCatalog } from './content-pack';

/**
 * One-ply utility AI. It scores every (destination, ability, target) triple a
 * unit can legally produce this turn and commits to the single best action in
 * the whole army, one action per call — which lets the UI animate each step and
 * keeps the search shallow enough to stay instant.
 *
 * Fog of war is intentionally ignored by the AI (documented, not accidental).
 */

export interface AiOptions {
  /** 0 = turtle on defensible ground, 1 = charge the enemy keep. */
  aggression: number;
}

const weaponsForType = (type: UnitTypeId, content: ContentCatalog) =>
  content.units.get(type).weapons.map((id) => content.weapons.get(id));
const maximumWeaponPower = (type: UnitTypeId, content: ContentCatalog) =>
  Math.max(0, ...weaponsForType(type, content).map((weapon) => weapon.power));
const maximumWeaponRange = (type: UnitTypeId, content: ContentCatalog) =>
  Math.max(0, ...weaponsForType(type, content).map((weapon) => weapon.maxRange));
const preferredEngagementRange = (unit: Unit, content: ContentCatalog): number => {
  const weapons = unitWeapons(unit, content);
  if (weapons.some((weapon) => weapon.minRange <= 1)) return 1;
  return Math.max(1, ...weapons.map((weapon) => weapon.maxRange));
};

const hpRatio = (u: Unit, content: ContentCatalog) => u.hp / content.units.get(u.type).maxHp;
const unitValue = (u: Unit, content: ContentCatalog) =>
  content.units.get(u.type).value * (0.4 + 0.6 * hpRatio(u, content)) * (1 + u.rank * 0.08);

/** Total damage enemies could land on each tile if they all attacked it. */
function dangerMap(s: GameState, me: number, space: TacticalSpace, content: ContentCatalog): Map<number, number> {
  const danger = new Map<number, number>();
  for (const foe of enemyUnitsOf(s, me)) {
    const weight = maximumWeaponPower(foe.type, content) * (0.5 + 0.5 * hpRatio(foe, content));
    for (const i of space.threatOf(s, foe)) {
      danger.set(i, (danger.get(i) ?? 0) + weight);
    }
  }
  return danger;
}

interface Objectives {
  /** Tiles worth walking to: enemy/neutral capturables, weighted. */
  captureTargets: { at: Coord; weight: number }[];
  enemyHqs: Coord[];
  myHqs: Coord[];
  mission: AiMissionIntent;
}

function objectivesFor(
  s: GameState,
  me: number,
  advisors: AiObjectiveAdvisorRegistry,
  handlers: ObjectiveHandlerRegistry,
  content: ContentCatalog,
): Objectives {
  const captureTargets: { at: Coord; weight: number }[] = [];
  const enemyHqs: Coord[] = [];
  const myHqs: Coord[] = [];
  for (let i = 0; i < s.map.tiles.length; i++) {
    const t = content.terrains.get(s.map.tiles[i]);
    if (!t.capturable) continue;
    const at = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
    const owner = s.map.owners[i];
    if (t.hq) {
      if (owner === me) myHqs.push(at);
      else if (areEnemies(s, owner, me)) enemyHqs.push(at);
    }
    if (owner !== me) {
      const weight = t.hq ? 6 : t.produces.length > 0 ? 3 : 2;
      captureTargets.push({ at, weight });
    }
  }
  return {
    captureTargets,
    enemyHqs,
    myHqs,
    mission: buildAiMissionIntent(s, me, advisors, handlers, content),
  };
}

function nearest(from: Coord, list: Coord[]): number {
  let best = Infinity;
  for (const c of list) best = Math.min(best, dist(from, c));
  return best;
}

function directivePositionScore(state: GameState, unit: Unit, at: Coord): number {
  const directive = unit.directive;
  if (directive.mode === 'assault') return 0;
  if (directive.mode === 'patrol' && directive.waypoints.length > 0) {
    const destination = directive.waypoints[directive.cursor % directive.waypoints.length];
    return 320 / (1 + dist(at, destination));
  }
  const cells = directive.zone ? state.scenario.zones[directive.zone] ?? [] : [];
  if (cells.length === 0) return directive.mode === 'retreat' ? -100 : 0;
  const distance = nearest(at, cells);
  if (directive.mode === 'guard') return distance === 0 ? 260 : -distance * 55;
  return distance === 0 ? 1_200 : -distance * 180;
}

/** Static desirability of standing on `at`, ignoring what we do from there. */
function positionScore(
  s: GameState,
  unit: Unit,
  at: Coord,
  obj: Objectives,
  danger: Map<number, number>,
  opts: AiOptions,
  battlefield: Battlefield,
  content: ContentCatalog,
): number {
  const def = content.units.get(unit.type);
  const terrain = content.terrains.get(s.map.tiles[idx(s.map, at.x, at.y)]);
  const battlefieldCell = battlefield.cell(at);
  let score = 0;

  score += directivePositionScore(s, unit, at);

  score += terrain.defense * 90;
  score += battlefieldCell.elevation * 3;

  const canCapture = def.abilities.includes('capture');
  if (canCapture && obj.captureTargets.length > 0) {
    let best = 0;
    for (const t of obj.captureTargets) {
      const d = dist(at, t.at);
      best = Math.max(best, (t.weight * 120) / (1 + d));
    }
    score += best * (0.6 + 0.4 * opts.aggression);
  }

  let missionDestination = 0;
  for (const destination of obj.mission.destinations) {
    if (destination.unitIds && !destination.unitIds.has(unit.id)) continue;
    if (destination.captureOnly && !canCapture) continue;
    missionDestination = Math.max(
      missionDestination,
      destination.weight * 180 / (1 + dist(at, destination.at)),
    );
  }
  score += missionDestination;

  const protectedWeight = obj.mission.protectedUnits.get(unit.id) ?? 0;
  if (protectedWeight === 0 && obj.mission.protectedUnits.size > 0) {
    let protectionPull = 0;
    for (const [protectedId, weight] of obj.mission.protectedUnits) {
      const protectedUnit = s.units.find((candidate) => candidate.id === protectedId);
      if (!protectedUnit) continue;
      protectionPull = Math.max(protectionPull, weight * 100 / (1 + dist(at, protectedUnit)));
    }
    score += protectionPull;
  }

  const foes = enemyUnitsOf(s, unit.owner);
  if (foes.length > 0) {
    const dFoe = nearest(at, foes.map((f) => ({ x: f.x, y: f.y })));
    // Ranged units want to be near-but-not-adjacent; melee wants contact.
    const ideal = preferredEngagementRange(unit, content);
    const engagementWeight = unit.directive.mode === 'retreat' ? 0.15 : 1;
    score -= Math.abs(dFoe - ideal) * 16 * (0.5 + opts.aggression) * engagementWeight;
    const nearestFoe = foes.slice().sort((left, right) => dist(at, left) - dist(at, right))[0];
    const cover = battlefieldCell.directionalCoverFrom(nearestFoe);
    score += cover === 'full' ? 36 : cover === 'half' ? 18 : battlefieldCell.cover === 'full' ? 28 : battlefieldCell.cover === 'half' ? 14 : 0;
  }

  if (obj.enemyHqs.length > 0) {
    score -= nearest(at, obj.enemyHqs) * 8 * opts.aggression;
  }
  if (obj.myHqs.length > 0) {
    // Defensive pull: stay useful to our own keep when it is threatened.
    const threatToHome = obj.myHqs.some((h) =>
      foes.some((f) => dist({ x: f.x, y: f.y }, h) <= 5),
    );
    if (threatToHome) score -= nearest(at, obj.myHqs) * 14 * (1 - opts.aggression * 0.5);
  }

  const risk = danger.get(idx(s.map, at.x, at.y)) ?? 0;
  score -= risk * (0.55 - 0.25 * opts.aggression) * (1 - def.defense) *
    (1 + protectedWeight * 0.15);

  const healRate =
    s.map.owners[idx(s.map, at.x, at.y)] === unit.owner ? terrain.heal : 0;
  if (healRate > 0 && hpRatio(unit, content) < 0.6) score += healRate * 4;

  if (unit.commanderId) {
    const commander = s.commanders.find((candidate) => candidate.id === unit.commanderId);
    const leader = commander && s.units.find((candidate) => candidate.id === commander.unitId);
    if (commander && leader) {
      const commandDistance = dist(at, leader);
      score += commandDistance <= commander.radius
        ? 45
        : -Math.max(0, commandDistance - commander.radius) * 18;
    }
  }

  return score;
}

interface Candidate {
  action: Action;
  score: number;
}

export interface AbilityAiEvaluationContext {
  state: GameState;
  unit: Unit;
  at: Coord;
  option: CommandOption;
  target: Coord | null;
  positionScore: number;
  mission: AiMissionIntent;
  combatModifiers: CombatModifierPipeline;
  resources: BattleResourceSystem;
  content: ContentCatalog;
}

export interface AbilityAiEvaluator {
  readonly ability: string;
  score(context: AbilityAiEvaluationContext): number | null;
}

/** Ability plugins register tactical utility here alongside legality/execution. */
export class AbilityAiEvaluatorRegistry {
  private readonly evaluators = new Map<string, AbilityAiEvaluator>();

  register(evaluator: AbilityAiEvaluator): this {
    if (this.evaluators.has(evaluator.ability)) {
      throw new Error(`AI evaluator already registered for ability "${evaluator.ability}"`);
    }
    this.evaluators.set(evaluator.ability, evaluator);
    return this;
  }

  replace(evaluator: AbilityAiEvaluator): this {
    this.evaluators.set(evaluator.ability, evaluator);
    return this;
  }

  evaluator(ability: string): AbilityAiEvaluator | undefined {
    return this.evaluators.get(ability);
  }

  kinds(): string[] {
    return [...this.evaluators.keys()];
  }

  clone(): AbilityAiEvaluatorRegistry {
    const copy = new AbilityAiEvaluatorRegistry();
    for (const evaluator of this.evaluators.values()) copy.register(evaluator);
    return copy;
  }
}

function collateralValue(state: GameState, plan: CombatPlan, content: ContentCatalog): number {
  let score = 0;
  for (const hit of plan.unitHits.filter((candidate) => !candidate.primary)) {
    const target = state.units.find((unit) => unit.id === hit.target);
    if (!target) continue;
    const dealt = Math.min(hit.damage.damage, target.hp) / content.units.get(target.type).maxHp;
    score += dealt * content.units.get(target.type).value;
    if (hit.killed) score += unitValue(target, content) * 0.5;
  }
  for (const hit of plan.structureHits.filter((candidate) => !candidate.primary)) {
    score += hit.forecast.damage * 7 + (hit.forecast.destroyed ? 700 : 0);
  }
  if (plan.supportAttack) {
    const target = state.units.find((unit) => unit.id === plan.supportAttack!.target);
    if (target) {
      score += Math.min(plan.supportAttack.damage.damage, plan.supportAttack.hpBefore) /
        content.units.get(target.type).maxHp * content.units.get(target.type).value;
      if (plan.supportAttack.killed) score += unitValue(target, content) * 0.5;
    }
  }
  return score;
}

const evaluator = (
  ability: string,
  score: AbilityAiEvaluator['score'],
): AbilityAiEvaluator => ({ ability, score });

export const DefaultAbilityAiEvaluators = new AbilityAiEvaluatorRegistry()
  .register(evaluator('wait', (context) => context.positionScore))
  .register(evaluator('capture', (context) => {
    const tile = context.content.terrains.get(context.state.map.tiles[idx(context.state.map, context.at.x, context.at.y)]);
    const prize = tile.hq ? 4000 : tile.produces.length > 0 ? 900 : 600;
    return context.positionScore * 0.3 + prize;
  }))
  .register(evaluator('heal', (context) => {
    if (!context.target) return null;
    const ally = unitAt(context.state, context.target.x, context.target.y);
    if (!ally) return null;
    const def = context.content.units.get(ally.type);
    const healed = Math.min(30, def.maxHp - ally.hp);
    return context.positionScore * 0.3 + (healed / def.maxHp) * def.value * 0.9;
  }))
  .register(evaluator('attack', (context) => {
    if (!context.target || !context.option.weapon) return null;
    const foe = unitAt(context.state, context.target.x, context.target.y);
    const plan = forecastCombatPlan(
      context.state,
      context.unit,
      context.target,
      context.at,
      context.option.weapon,
      context.combatModifiers,
      context.resources,
      context.content,
    );
    if (!foe) {
      const structure = structureAt(context.state, context.target.x, context.target.y);
      const forecast = plan.primaryStructure;
      if (!structure || !forecast) return null;
      return context.positionScore * 0.25 +
        forecast.damage * 8 +
        (forecast.destroyed ? 900 : 0) +
        (context.mission.priorityStructures.get(structure.id) ?? 0) * 300 +
        collateralValue(context.state, plan, context.content);
    }
    const forecast = plan.primaryUnit;
    if (!forecast) return null;
    const foeDef = context.content.units.get(foe.type);
    const unitDefinition = context.content.units.get(context.unit.type);
    const dealt = Math.min(forecast.strike.damage, foe.hp) / foeDef.maxHp;
    let score = dealt * foeDef.value * 1.15;
    if (forecast.defenderDies) score += unitValue(foe, context.content) * 0.65;
    if (forecast.counter) {
      const taken = Math.min(forecast.counter.damage, context.unit.hp) / unitDefinition.maxHp;
      score -= taken * unitDefinition.value;
      if (forecast.attackerDies) score -= unitValue(context.unit, context.content) * 0.9;
    }
    if (foeDef.tags.includes('support') || unitWeapons(foe, context.content).every((weapon) => weapon.minRange > 1)) score *= 1.1;
    score += (context.mission.priorityUnits.get(foe.id) ?? 0) * 260;
    for (const [protectedId, weight] of context.mission.protectedUnits) {
      const protectedUnit = context.state.units.find((candidate) => candidate.id === protectedId);
      if (protectedUnit) score += weight * 80 / (1 + dist(foe, protectedUnit));
    }
    return context.positionScore * 0.25 + score + collateralValue(context.state, plan, context.content);
  }));

function tacticAction(
  state: GameState,
  owner: number,
  resources: BattleResourceSystem,
  content: ContentCatalog,
): Action | null {
  let best: { action: Action; score: number } | null = null;
  for (const commander of state.commanders.filter((entry) => entry.owner === owner)) {
    for (const option of tacticOptions(state, commander.id, resources, content)) {
      const tactic = content.tactics.get(option.id);
      for (const target of option.targets) {
        const affected = unitsOf(state, owner).filter((unit) => dist(unit, target) <= tactic.radius);
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

function evaluateUnit(
  s: GameState,
  unit: Unit,
  obj: Objectives,
  danger: Map<number, number>,
  opts: AiOptions,
  combatModifiers: CombatModifierPipeline,
  resources: BattleResourceSystem,
  abilities: Registry<AbilityDef>,
  abilityEvaluators: AbilityAiEvaluatorRegistry,
  space: TacticalSpace,
  battlefield: Battlefield,
  content: ContentCatalog,
): Candidate | null {
  const field = space.moveField(s, unit);
  let best: Candidate | null = null;

  const consider = (c: Candidate) => {
    if (!best || c.score > best.score) best = c;
  };

  for (const i of field.stops) {
    const at = { x: i % s.map.width, y: Math.floor(i / s.map.width) };
    const pos = positionScore(s, unit, at, obj, danger, opts, battlefield, content);
    const path = pathFrom(field, s, at);
    if (!path) continue;

    for (const opt of commandOptions(s, unit, at, resources, abilities, space, content)) {
      const aiEvaluator = abilityEvaluators.evaluator(opt.ability);
      if (!aiEvaluator) continue;
      const targets: Array<Coord | null> = opt.selfTargeted ? [null] : opt.targets;
      for (const target of targets) {
        const score = aiEvaluator.score({
          state: s,
          unit,
          at,
          option: opt,
          target,
          positionScore: pos,
          mission: obj.mission,
          combatModifiers,
          resources,
          content,
        });
        if (score === null || !Number.isFinite(score)) continue;
        const directiveAdjustment = unit.directive.mode === 'retreat' && opt.ability === 'attack' ? -5_000 : 0;
        consider({
          action: {
            kind: 'command',
            unit: unit.id,
            path,
            command: target
              ? { ability: opt.ability, weapon: opt.weapon, target }
              : { ability: opt.ability, weapon: opt.weapon },
          },
          score: score + directiveAdjustment,
        });
      }
    }
  }

  return best;
}

function desiredReaction(s: GameState, unit: Unit, obj: Objectives, content: ContentCatalog): Unit['reaction'] {
  const def = content.units.get(unit.type);
  const protectedWeight = obj.mission.protectedUnits.get(unit.id) ?? 0;
  const enemyNear = enemyUnitsOf(s, unit.owner).some((enemy) =>
    dist(unit, enemy) <= content.units.get(enemy.type).movement + maximumWeaponRange(enemy.type, content));
  if (hpRatio(unit, content) < 0.35 || (protectedWeight >= 4 && enemyNear)) return 'guard';

  const adjacentAlly = s.units.some((candidate) =>
    candidate.id !== unit.id &&
    areAllies(s, candidate.owner, unit.owner) &&
    dist(candidate, unit) === 1);
  if (adjacentAlly && (def.tags.includes('support') || def.tags.includes('monster'))) return 'support';

  const hasScarceRangedWeapon = def.tags.includes('ranged') && unitWeapons(unit, content).some((weapon) =>
    weapon.tags.includes('ranged') && (weapon.cooldown > 0 || weapon.resourceCosts.length > 0));
  if (hasScarceRangedWeapon) return 'conserve';
  return def.defaultReaction;
}

function reactionAction(s: GameState, owner: number, obj: Objectives, content: ContentCatalog): Action | null {
  for (const unit of unitsOf(s, owner).filter((candidate) => !candidate.done)) {
    const stance = desiredReaction(s, unit, obj, content);
    if (stance !== unit.reaction) return { kind: 'reaction', unit: unit.id, stance };
  }
  return null;
}

function pathFrom(
  field: MoveField,
  s: GameState,
  to: Coord,
): Coord[] | null {
  const target = idx(s.map, to.x, to.y);
  const out: Coord[] = [];
  let cur = target;
  for (let guard = 0; guard <= field.tiles.size + 1; guard++) {
    const node = field.tiles.get(cur);
    if (!node) return null;
    out.push({ x: node.x, y: node.y });
    if (node.from === -1) {
      out.reverse();
      return out;
    }
    cur = node.from;
  }
  return null;
}

/* --------------------------------------------------------------- recruitment */

function scoreRecruit(
  s: GameState,
  me: number,
  type: UnitTypeId,
  resources: BattleResourceSystem,
  content: ContentCatalog,
): number {
  const def = content.units.get(type);
  const foes = enemyUnitsOf(s, me);
  const mine = unitsOf(s, me);

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
  let score = (maximumWeaponPower(type, content) * matchup + def.maxHp * 0.35 + def.movement * 12) /
    Math.max(0.1, resources.planningValue(def.recruitCosts) / 100);

  const infantry = mine.filter((u) => content.units.get(u.type).abilities.includes('capture')).length;
  if (def.abilities.includes('capture') && infantry < 2) score *= 1.6;

  const composition = mine.filter((u) => u.type === type).length;
  score *= 1 - Math.min(0.4, composition * 0.08); // discourage one-note armies

  return score;
}

function recruitAction(s: GameState, me: number, resources: BattleResourceSystem, content: ContentCatalog): Action | null {
  const resourceOwner = playerResource(player(s, me));
  const cap = s.rules.maxUnitsPerPlayer;
  if (cap !== null && unitsOf(s, me).length >= cap) return null;

  for (const at of productionTilesOf(s, me, content)) {
    if (unitAt(s, at.x, at.y)) continue;
    const terrain = content.terrains.get(s.map.tiles[idx(s.map, at.x, at.y)]);
    const affordable = terrain.produces.filter((id) =>
      resources.canAfford(content.units.get(id).recruitCosts, resourceOwner));
    if (affordable.length === 0) continue;

    const ranked = affordable
      .map((id) => ({ id, score: scoreRecruit(s, me, id, resources, content) }))
      .sort((a, b) => b.score - a.score);
    const pick = ranked[0];

    return { kind: 'recruit', at, unit: pick.id };
  }
  return null;
}

/* -------------------------------------------------------------------- driver */

/**
 * The next single action for the current (AI) player. Returns `endTurn` when
 * there is nothing left worth doing, so callers can simply loop.
 */
export interface AiPlanningDependencies {
  objectiveAdvisors: AiObjectiveAdvisorRegistry;
  objectives: ObjectiveHandlerRegistry;
  combatModifiers: CombatModifierPipeline;
  resources: BattleResourceSystem;
  abilities: Registry<AbilityDef>;
  abilityEvaluators: AbilityAiEvaluatorRegistry;
  space: TacticalSpace;
  content: ContentCatalog;
}

export function chooseAction(
  s: GameState,
  opts?: Partial<AiOptions>,
  dependencies: Partial<AiPlanningDependencies> = {},
): Action {
  if (s.phase === 'deployment') return { kind: 'finishDeployment' };
  const me = s.currentPlayer;
  const aggression = opts?.aggression ?? player(s, me).ai.aggression;
  const options: AiOptions = { aggression };

  const resources = dependencies.resources ?? DefaultBattleResources;
  const abilities = dependencies.abilities ?? Abilities;
  const content = dependencies.content ?? GlobalContentCatalog;
  const space = dependencies.space ?? (dependencies.content ? new DefaultTacticalSpace(content) : CoreTacticalSpace);
  const tactic = tacticAction(s, me, resources, content);
  if (tactic) return tactic;

  const recruit = recruitAction(s, me, resources, content);
  if (recruit) return recruit;

  const obj = objectivesFor(
    s,
    me,
    dependencies.objectiveAdvisors ?? DefaultAiObjectiveAdvisors,
    dependencies.objectives ?? ObjectiveHandlers,
    content,
  );
  const danger = dangerMap(s, me, space, content);

  const reaction = reactionAction(s, me, obj, content);
  if (reaction) return reaction;

  let best: Candidate | null = null;
  const battlefield = new Battlefield(s, content);
  for (const u of unitsOf(s, me)) {
    if (u.done) continue;
    const c = evaluateUnit(
      s,
      u,
      obj,
      danger,
      options,
      dependencies.combatModifiers ?? DefaultCombatModifierPipeline,
      resources,
      abilities,
      dependencies.abilityEvaluators ?? DefaultAbilityAiEvaluators,
      space,
      battlefield,
      content,
    );
    if (c && (!best || c.score > best.score)) best = c;
  }

  return best ? best.action : { kind: 'endTurn' };
}

/** Convenience: the whole turn as a list of actions (used in tests). */
export function planTurn(s: GameState, apply: (a: Action) => void, maxActions = 200): void {
  for (let n = 0; n < maxActions; n++) {
    const action = chooseAction(s);
    apply(action);
    if (action.kind === 'endTurn') return;
    if (s.phase !== 'playing') return;
  }
}
