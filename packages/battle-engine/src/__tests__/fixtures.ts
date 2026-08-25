import { type ActionHandlerRegistry, type BattleRuleServices } from '../action-system';
import { createBattleEngine } from '../plugins/default';
import { applyAction, commandOptions } from '../actions';
import { addStatus } from '../statuses';
import { chooseAction, type AiDriverDependencies, DefaultAbilityAiEvaluators, DefaultAiIntents } from '../ai';
import { DefaultAiObjectiveAdvisors } from '../ai-objectives';
import { computeDamage, forecast, forecastStructure } from '../combat';
import { forecastCombatPlan } from '../combat-plan';
import { computeMoveField, hasDirectLineOfSight, threatTiles, type MoveField } from '../movement';
import { applyScenarioEffect, conditionMet, runScenarioTriggers } from '../scenario';
import { boardOf } from '../domain/board';
import { createState } from '../state';
import {
  evaluateVictory,
  objectiveOutcome,
  refreshObjectiveStates,
} from '../victory';
import type { AbilityQuery } from '../abilities';
import { mapFromLevel } from '../level/index';
import { validateLevel } from '../level-validation';
import type {
  Action,
  Coord,
  GameEvent,
  GameState,
  ScenarioCondition,
  ScenarioEffect,
  StructureState,
  Unit,
  WeaponId,
  LevelData,
  Objective,
  PlayerId,
} from '../types';
import { createTestCatalog } from '@empire/test-content';
import type { ContentCatalog } from '../content-pack';

/** The level sketches live in the package two suites share them through. */
export { makeLevel, u } from '@empire/test-content';

/* ------------------------------------------------------------------ harness */

/**
 * One explicitly composed ruleset for the whole suite.
 *
 * Tests exercise the same injection path production uses: nothing here reaches
 * for ambient state, so a signature that forgets a dependency cannot compile.
 */
export const TEST_CONTENT: ContentCatalog = createTestCatalog();
/**
 * One engine for the suite, composed the way an application composes one.
 *
 * `TEST_RULES` is its ruleset rather than a second composition: dispatching an
 * action needs both halves of the engine — the strategies and the rules — and a
 * helper that paired one instance's rules with the module-global registry was a
 * path production never takes.
 */
export const TEST_ENGINE = createBattleEngine({ content: TEST_CONTENT });
export const TEST_RULES: BattleRuleServices = TEST_ENGINE.rules;

export const testState = (level: LevelData): GameState => createState(TEST_RULES, level);

export const testMap = (level: LevelData) => mapFromLevel(TEST_CONTENT, level);
export const testValidate = (level: LevelData) => validateLevel(TEST_RULES, level);

export const testMoveField = (state: GameState, unit: Unit) =>
  computeMoveField(TEST_RULES, state, unit);
export const testThreat = (state: GameState, unit: Unit, field?: MoveField) =>
  threatTiles(TEST_RULES, state, unit, field);
export const testLineOfSight = (state: GameState, from: Coord, target: Coord) =>
  hasDirectLineOfSight(TEST_RULES, state, from, target);
/** The default four-way board, for a test that needs the geometry directly. */
export const testBoard = (state: GameState) => boardOf(TEST_RULES, state);

export const testDamage = (
  state: GameState,
  attacker: Unit,
  defender: Unit,
  defenderAt?: Coord | undefined,
  weapon?: WeaponId | undefined,
  attackerAt?: Coord | undefined,
) => computeDamage(TEST_RULES, state, attacker, defender, { defenderAt, weapon, attackerAt });

export const testForecast = (
  state: GameState,
  attacker: Unit,
  defender: Unit,
  attackFrom?: Coord,
  weapon?: WeaponId,
) => forecast(TEST_RULES, state, attacker, defender, { attackFrom, weapon });

export const testForecastStructure = (
  state: GameState,
  attacker: Unit,
  structure: StructureState,
  weapon?: WeaponId,
) => forecastStructure(TEST_RULES, state, attacker, structure, { weapon });

export const testCombatPlan = (
  state: GameState,
  attacker: Unit,
  aimedAt: Coord,
  from?: Coord,
  weapon?: WeaponId,
) => forecastCombatPlan(TEST_RULES, state, attacker, aimedAt, { from, weapon });

export const testCommands = (state: GameState, unit: Unit, at: Coord) =>
  commandOptions(TEST_RULES, state, unit, at);

/** One action through the shared ruleset's own strategies. */
export const testApply = (state: GameState, action: Action) =>
  applyAction(TEST_ENGINE, state, action);

/** One action through a registry a test composed itself. */
export const testApplyWith = (
  state: GameState,
  action: Action,
  actionHandlers: ActionHandlerRegistry,
  rules: BattleRuleServices = TEST_RULES,
) => applyAction({ actionHandlers, rules }, state, action);

export const testAddStatus = (
  unit: Unit,
  id: string,
  remaining: number,
  emit?: (event: GameEvent) => void,
  sourceUnitId?: number,
) => addStatus(TEST_CONTENT, unit, { id, remaining, sourceUnitId }, emit);

export const testVictory = (state: GameState, emit?: (event: GameEvent) => void) =>
  evaluateVictory(TEST_RULES, state, emit);
export const testObjectiveOutcome = (state: GameState, owner: PlayerId, objective: Objective) =>
  objectiveOutcome(TEST_RULES, state, owner, objective);
export const testRefreshObjectives = (state: GameState, emit?: (event: GameEvent) => void) =>
  refreshObjectiveStates(TEST_RULES, state, emit);

export const testCondition = (state: GameState, condition: ScenarioCondition) =>
  conditionMet(TEST_RULES, state, condition);
export const testScenarioEffect = (
  state: GameState,
  effect: ScenarioEffect,
  emit: (event: GameEvent) => void = () => {},
) => applyScenarioEffect(TEST_RULES, state, effect, emit);
export const testScenarioTriggers = (
  state: GameState,
  timing: 'afterAction' | 'turnStart' | 'turnEnd',
  emit: (event: GameEvent) => void = () => {},
) => runScenarioTriggers(TEST_RULES, state, timing, emit);

export const testAiDependencies = (): AiDriverDependencies => ({
  rules: TEST_RULES,
  objectiveAdvisors: DefaultAiObjectiveAdvisors,
  abilityEvaluators: DefaultAbilityAiEvaluators,
  intents: DefaultAiIntents,
});
export const testChooseAction = (state: GameState, options?: { aggression: number }) =>
  chooseAction(testAiDependencies(), state, options);
export const testAbilityQuery = (state: GameState, unit: Unit, at: Coord, moved = false): AbilityQuery => ({
  state,
  unit,
  at,
  moved,
});
