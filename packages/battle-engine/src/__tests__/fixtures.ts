import { type ActionHandlerRegistry, type BattleRuleServices } from '../action-system';
import { createBattleRules } from '../plugins/default';
import { applyActionWith, commandOptions } from '../actions';
import { addStatus } from '../statuses';
import { chooseAction, type AiPlanningDependencies, DefaultAbilityAiEvaluators, DefaultAiIntents } from '../ai';
import { buildAiMissionIntent, DefaultAiObjectiveAdvisors } from '../ai-objectives';
import { computeDamage, forecast, forecastStructure } from '../combat';
import { forecastCombatPlan } from '../combat-plan';
import { computeMoveField, hasDirectLineOfSight, threatTiles, type MoveField } from '../movement';
import { applyScenarioEffect, conditionMet, runScenarioTriggers } from '../scenario';
import { boardOf } from '../domain/board';
import { createState } from '../state';
import {
  evaluateVictory,
  objectiveOutcome,
  objectiveProgress,
  refreshObjectiveStates,
} from '../victory';
import type { AbilityQuery } from '../abilities';
import { mapFromLevel, normaliseLevel, validateLevel } from '../level/index';
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
  LevelCommander,
  LevelScenario,
  LevelStructure,
  LevelUnit,
  LevelDeployment,
  LevelCliffEdge,
  LevelDirectionalCover,
  LevelComposite,
  Objective,
  PlayerId,
  RuleSet,
} from '../types';
import { COMMAND_POINTS_RESOURCE, FUNDS_RESOURCE } from '../resources';
import { createTestCatalog } from '@empire/test-content';
import type { ContentCatalog } from '../content-pack';

/** Build a level from an ASCII sketch — keeps the tests readable. */
export function makeLevel(
  terrain: string[],
  opts: {
    units?: LevelUnit[];
    commanders?: LevelCommander[];
    owners?: { x: number; y: number; owner: PlayerId }[];
    rules?: Partial<RuleSet>;
    victory?: Objective[];
    funds?: [number, number];
    structures?: LevelStructure[];
    composites?: LevelComposite[];
    scenario?: LevelScenario;
    deployment?: LevelDeployment;
    elevation?: number[];
    cliffs?: LevelCliffEdge[];
    directionalCover?: LevelDirectionalCover[];
  } = {},
): LevelData {
  return normaliseLevel({
    schema: 2,
    id: 'test',
    name: 'test',
    width: terrain[0].length,
    height: terrain.length,
    terrain,
    owners: opts.owners ?? [],
    units: opts.units ?? [],
    commanders: opts.commanders ?? [],
    structures: opts.structures ?? [],
    composites: opts.composites ?? [],
    players: [
      {
        id: 1,
        name: 'P1',
        team: 1,
        color: '#3f7fd8',
        controller: 'human',
        resources: {
          [FUNDS_RESOURCE]: { current: opts.funds?.[0] ?? 0, capacity: null },
          [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 },
        },
      },
      {
        id: 2,
        name: 'P2',
        team: 2,
        color: '#d8483f',
        controller: 'ai',
        resources: {
          [FUNDS_RESOURCE]: { current: opts.funds?.[1] ?? 0, capacity: null },
          [COMMAND_POINTS_RESOURCE]: { current: 0, capacity: 5 },
        },
        ai: { aggression: 0.5 },
      },
    ],
    rules: opts.rules ?? {},
    victory: opts.victory ?? [{ type: 'routEnemies' }, { type: 'captureHQ' }],
    scenario: opts.scenario,
    deployment: opts.deployment,
    elevation: opts.elevation,
    cliffs: opts.cliffs,
    directionalCover: opts.directionalCover,
  } satisfies Partial<LevelData>);
}

export const u = (x: number, y: number, unit: string, owner: PlayerId, hp?: number): LevelUnit => ({
  x,
  y,
  unit,
  owner,
  ...(hp === undefined ? {} : { hp }),
});

/* ------------------------------------------------------------------ harness */

/**
 * One explicitly composed ruleset for the whole suite.
 *
 * Tests exercise the same injection path production uses: nothing here reaches
 * for ambient state, so a signature that forgets a dependency cannot compile.
 */
export const TEST_CONTENT: ContentCatalog = createTestCatalog();
export const TEST_RULES: BattleRuleServices = createBattleRules({ content: TEST_CONTENT });

export const testState = (level: LevelData): GameState => createState(level, TEST_CONTENT);

export const testMap = (level: LevelData) => mapFromLevel(level, TEST_CONTENT);
export const testValidate = (level: LevelData) => validateLevel(level, TEST_CONTENT);

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
  defenderAt?: Coord,
  weapon?: WeaponId,
  attackerAt?: Coord,
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

export const testApplyWith = (
  state: GameState,
  action: Action,
  handlers: ActionHandlerRegistry,
  rules: BattleRuleServices = TEST_RULES,
) => applyActionWith(state, action, handlers, rules);

export const testAddStatus = (
  unit: Unit,
  id: string,
  remaining: number,
  emit?: (event: GameEvent) => void,
  sourceUnitId?: number,
) => addStatus(TEST_CONTENT, unit, id, remaining, emit, sourceUnitId);

export const testVictory = (state: GameState, emit?: (event: GameEvent) => void) =>
  evaluateVictory(TEST_RULES, state, emit);
export const testObjectiveOutcome = (state: GameState, owner: PlayerId, objective: Objective) =>
  objectiveOutcome(TEST_RULES, state, owner, objective);
export const testObjectiveProgress = (state: GameState, owner: PlayerId, objective: Objective) =>
  objectiveProgress(TEST_RULES, state, owner, objective);
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

export const testAiDependencies = (): AiPlanningDependencies => ({
  rules: TEST_RULES,
  objectiveAdvisors: DefaultAiObjectiveAdvisors,
  abilityEvaluators: DefaultAbilityAiEvaluators,
  intents: DefaultAiIntents,
});
export const testChooseAction = (state: GameState, options?: { aggression: number }) =>
  chooseAction(testAiDependencies(), state, options);
export const testMissionIntent = (state: GameState, owner: PlayerId) =>
  buildAiMissionIntent(state, owner, DefaultAiObjectiveAdvisors, TEST_RULES.objectives, TEST_CONTENT);

export const testAbilityQuery = (state: GameState, unit: Unit, at: Coord, moved = false): AbilityQuery => ({
  state,
  unit,
  at,
  moved,
});
