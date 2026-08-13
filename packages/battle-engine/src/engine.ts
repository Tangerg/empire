import { CoreActionHandlers, applyActionWith, commandOptions } from './actions';
import { BattleLifecycle } from './turn-cycle';
import {
  chooseAction,
  DefaultAbilityAiEvaluators,
  DefaultAiIntents,
  type AbilityAiEvaluatorRegistry,
  type AiIntentRegistry,
  type AiOptions,
} from './ai';
import {
  DefaultAiObjectiveAdvisors,
  type AiObjectiveAdvisorRegistry,
} from './ai-objectives';
import {
  ActionHandlerRegistry,
  createDefaultBattleRuleServices,
  type BattleRuleServices,
} from './action-system';
import {
  forecast,
  type CombatForecast,
} from './combat';
import {
  type CombatModifierPipeline,
} from './combat-modifiers';
import { forecastCombatPlan } from './combat-plan';
import { validateLevel } from './mapio';
import type { MoveField } from './movement';
import { cloneState, createState, restoreState, type CreateStateOptions } from './state';
import type { ContentCatalog } from './content-pack';
import { careerOptions } from './careers';
import { activeTurnOrder, mayAct, type TurnOrderPolicy } from './turn-order';
import type { Action, Coord, GameEvent, GameState, LevelData, PlayerId, Unit, WeaponId } from './types';
import type { Objective, ScenarioCondition } from './types';

export interface BattleEngineDependencies extends BattleRuleServices {
  readonly actionHandlers: ActionHandlerRegistry;
  readonly aiObjectiveAdvisors: AiObjectiveAdvisorRegistry;
  readonly abilityAiEvaluators: AbilityAiEvaluatorRegistry;
  readonly aiIntents: AiIntentRegistry;
}

export interface BattleDispatchReceipt {
  readonly events: GameEvent[];
  /** Snapshot immediately before the action, suitable for an undo stack. */
  readonly before: GameState;
}

export class BattleEngineConfigurationError extends Error {
  constructor(readonly issues: readonly string[]) {
    super(`invalid battle engine configuration:\n${issues.map((issue) => `- ${issue}`).join('\n')}`);
    this.name = 'BattleEngineConfigurationError';
  }
}

export class BattleLevelError extends Error {
  constructor(readonly levelId: string, readonly issues: readonly string[]) {
    super(`invalid battle level "${levelId}":\n${issues.map((issue) => `- ${issue}`).join('\n')}`);
    this.name = 'BattleLevelError';
  }
}

/**
 * Application-facing façade and dependency-injection boundary.
 *
 * It composes domain strategies but owns no UI, story, or singleton state. A
 * server simulation, mod sandbox, or alternate ruleset can create an isolated
 * instance with different handlers/providers.
 */
export class BattleEngine {
  readonly actionHandlers: ActionHandlerRegistry;
  readonly combatModifiers: CombatModifierPipeline;
  readonly aiObjectiveAdvisors: AiObjectiveAdvisorRegistry;
  readonly abilityAiEvaluators: AbilityAiEvaluatorRegistry;
  readonly aiIntents: AiIntentRegistry;
  readonly rules: BattleRuleServices;

  constructor(dependencies: BattleEngineDependencies) {
    this.actionHandlers = dependencies.actionHandlers;
    this.combatModifiers = dependencies.combatModifiers;
    this.aiObjectiveAdvisors = dependencies.aiObjectiveAdvisors;
    this.abilityAiEvaluators = dependencies.abilityAiEvaluators;
    this.aiIntents = dependencies.aiIntents;
    this.rules = {
      content: dependencies.content,
      abilities: dependencies.abilities,
      space: dependencies.space,
      combatModifiers: this.combatModifiers,
      hitEffects: dependencies.hitEffects,
      statusBehaviors: dependencies.statusBehaviors,
      scenarioConditions: dependencies.scenarioConditions,
      scenarioEffects: dependencies.scenarioEffects,
      objectives: dependencies.objectives,
      progression: dependencies.progression,
      resources: dependencies.resources,
      turnOrders: dependencies.turnOrders,
      reactions: dependencies.reactions,
      unitDepartures: dependencies.unitDepartures,
      random: dependencies.random,
    };
    this.assertConfiguration();
  }

  /** The ruleset's content catalog. Presentation must read from this, never a global. */
  get content() {
    return this.rules.content;
  }

  createState(level: LevelData, options: CreateStateOptions = {}): GameState {
    const issues = validateLevel(level, this.rules.content)
      .filter((issue) => issue.severity === 'error')
      .map((issue) => issue.message);
    issues.push(...this.levelStrategyIssues(level));
    if (issues.length > 0) throw new BattleLevelError(level.id, [...new Set(issues)]);
    const state = createState(level, this.rules.content, options);
    // A level without a deployment phase is already playing, so it needs its
    // first actor turn now; deployment levels get theirs on finishDeployment.
    if (state.phase === 'playing') this.lifecycle(state).start();
    return state;
  }

  /** Phase and round transitions of one battle running under this ruleset. */
  lifecycle(state: GameState, emit?: (event: GameEvent) => void): BattleLifecycle {
    return new BattleLifecycle(state, this.rules, emit);
  }

  /** Turn-order policy this battle runs under. */
  turnOrder(state: GameState): TurnOrderPolicy {
    return activeTurnOrder(this.rules, state);
  }

  /** May this unit act right now, under the battle's ordering policy? */
  canAct(state: GameState, unit: Unit): boolean {
    return mayAct(this.rules, state, unit);
  }

  /** Units entitled to act in the current actor turn. */
  actors(state: GameState): Unit[] {
    return state.phase === 'playing' ? this.turnOrder(state).actors(state) : [];
  }

  /** Deterministic look-ahead for an initiative strip; empty for side turns. */
  turnOrderPreview(state: GameState, count = 8): Unit[] {
    const ids = this.turnOrder(state).preview(state, this.rules.content, count);
    return ids.flatMap((id) => {
      const unit = state.units.find((candidate) => candidate.id === id);
      return unit ? [unit] : [];
    });
  }

  cloneState(state: GameState): GameState {
    return cloneState(state);
  }

  dispatch(state: GameState, action: Action): GameEvent[] {
    return this.dispatchWithReceipt(state, action).events;
  }

  /**
   * Authoritative transactional command boundary.
   *
   * Handlers may use convenient in-place domain mutations; callers still get
   * all-or-nothing semantics, and stateful shells can reuse the same snapshot
   * for undo instead of cloning the aggregate a second time.
   */
  dispatchWithReceipt(state: GameState, action: Action): BattleDispatchReceipt {
    const before = cloneState(state);
    try {
      const events = applyActionWith(state, action, this.actionHandlers, this.rules);
      return { events, before };
    } catch (error) {
      restoreState(state, before);
      throw error;
    }
  }

  commandsAt(state: GameState, unit: Unit, at: Coord) {
    return commandOptions(this.rules, state, unit, at);
  }

  moveField(state: GameState, unit: Unit): MoveField {
    return this.rules.space.moveField(state, unit);
  }

  careerOptions(state: GameState, unit: Unit) {
    return careerOptions(this.rules, state, unit);
  }

  pathTo(field: MoveField, state: GameState, destination: Coord): Coord[] | null {
    return this.rules.space.pathTo(field, state, destination);
  }

  threatOf(state: GameState, unit: Unit, field?: MoveField): Set<number> {
    return this.rules.space.threatOf(state, unit, field);
  }

  /** Ground this unit's enemies hold; entering a tile of it ends the move. */
  controlZoneAgainst(state: GameState, unit: Unit): Set<number> {
    return this.rules.space.controlZoneAgainst(state, unit);
  }

  visibleTiles(state: GameState, viewer: PlayerId): Set<number> {
    return this.rules.space.visibleTiles(state, viewer);
  }

  isUnitVisible(state: GameState, viewer: PlayerId, unit: Unit, seen?: Set<number>): boolean {
    return this.rules.space.isUnitVisible(state, viewer, unit, seen);
  }

  visibleUnits(state: GameState, viewer: PlayerId): Unit[] {
    return this.rules.space.visibleUnits(state, viewer);
  }

  forecast(
    state: GameState,
    attacker: Unit,
    defender: Unit,
    attackFrom?: Coord,
    weapon?: WeaponId,
  ): CombatForecast {
    return forecast(this.rules, state, attacker, defender, { attackFrom, weapon });
  }

  attackPlan(state: GameState, attacker: Unit, aimedAt: Coord, attackFrom?: Coord, weapon?: WeaponId) {
    return forecastCombatPlan(this.rules, state, attacker, aimedAt, { from: attackFrom, weapon });
  }

  chooseAiAction(state: GameState, options?: Partial<AiOptions>): Action {
    return chooseAction({
      rules: this.rules,
      objectiveAdvisors: this.aiObjectiveAdvisors,
      abilityEvaluators: this.abilityAiEvaluators,
      intents: this.aiIntents,
    }, state, options);
  }

  private assertConfiguration(): void {
    const issues: string[] = [];
    for (const unit of this.rules.content.units.all()) {
      for (const ability of unit.abilities) {
        if (!this.rules.abilities.has(ability)) issues.push(`unit "${unit.id}" requires missing ability "${ability}"`);
      }
    }
    for (const career of this.rules.content.careers.all()) {
      for (const ability of career.masteryAbilities) {
        if (!this.rules.abilities.has(ability)) issues.push(`career "${career.id}" requires missing ability "${ability}"`);
      }
    }
    const hitEffects = new Set(this.rules.hitEffects.keys());
    for (const weapon of this.rules.content.weapons.all()) {
      for (const effect of weapon.hitEffects) {
        if (!hitEffects.has(effect.type)) issues.push(`weapon "${weapon.id}" requires missing hit-effect handler "${effect.type}"`);
      }
    }
    if (issues.length > 0) throw new BattleEngineConfigurationError([...new Set(issues)]);
  }

  private levelStrategyIssues(level: LevelData): string[] {
    const issues: string[] = [];
    const objectiveKinds = new Set(this.rules.objectives.keys());
    const visitObjective = (objective: Objective): void => {
      if (!objectiveKinds.has(objective.type)) {
        issues.push(`objective "${objective.type}" has no registered handler`);
        return;
      }
      for (const child of this.rules.objectives.children(objective)) visitObjective(child);
    };
    for (const player of level.players) {
      for (const objective of player.objectives?.length ? player.objectives : (level.victory ?? [])) {
        visitObjective(objective);
      }
    }

    const conditionKinds = new Set(this.rules.scenarioConditions.keys());
    const visitCondition = (condition: ScenarioCondition): void => {
      if (!conditionKinds.has(condition.type)) {
        issues.push(`scenario condition "${condition.type}" has no registered handler`);
        return;
      }
      if (condition.type === 'all' || condition.type === 'any') condition.conditions.forEach(visitCondition);
      else if (condition.type === 'not') visitCondition(condition.condition);
    };
    const effectKinds = new Set(this.rules.scenarioEffects.keys());
    for (const trigger of level.scenario?.triggers ?? []) {
      visitCondition(trigger.condition);
      for (const effect of trigger.effects) {
        if (!effectKinds.has(effect.type)) issues.push(`scenario effect "${effect.type}" has no registered handler`);
      }
    }
    return issues;
  }
}

/**
 * Convenience factory for focused rule overrides. Unlike the old optional
 * constructor, every omitted mutable strategy is cloned for this engine.
 */
export interface BattleEngineOverrides extends Partial<BattleEngineDependencies> {
  /** The catalog this engine plays on; never defaulted to ambient state. */
  readonly content: ContentCatalog;
}

export function createBattleEngine(overrides: BattleEngineOverrides): BattleEngine {
  const rules = createDefaultBattleRuleServices(overrides);
  return new BattleEngine({
    ...rules,
    actionHandlers: overrides.actionHandlers ?? CoreActionHandlers.clone(),
    aiObjectiveAdvisors: overrides.aiObjectiveAdvisors ?? DefaultAiObjectiveAdvisors.clone(),
    abilityAiEvaluators: overrides.abilityAiEvaluators ?? DefaultAbilityAiEvaluators.clone(),
    aiIntents: overrides.aiIntents ?? DefaultAiIntents.clone(),
  });
}
