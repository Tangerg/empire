import { describe, expect, it } from 'vitest';
import { ActionExecutionContext, ActionHandlerRegistry, type ActionHandler } from '../action-system';
import { applyActionWith, CoreActionHandlers } from '../actions';
import {
  AiObjectiveAdvisorRegistry,
  buildAiMissionIntent,
  DefaultAiObjectiveAdvisors,
} from '../ai-objectives';
import { CombatModifierProviders } from '../combat-modifiers';
import { createBattleEngine } from '../engine';
import {
  ObjectiveHandlerRegistry,
  ObjectiveHandlers,
  type ObjectiveHandler,
} from '../objective-system';
import {
  ScenarioConditionHandlerRegistry,
  ScenarioConditionHandlers,
  ScenarioEffectHandlerRegistry,
  ScenarioEffectHandlers,
  applyScenarioEffect,
  conditionMet,
} from '../scenario';
import { createState } from '../state';
import type { ActionKindMap, GameEvent, ObjectiveKindMap } from '../types';
import { objectiveOutcome, objectiveProgress, refreshObjectiveStates } from '../victory';
import { makeLevel, u } from './fixtures';

declare module '../types' {
  interface ActionKindMap {
    testSignal: { kind: 'testSignal'; signal: string };
  }

  interface ScenarioConditionKindMap {
    testFlag: { type: 'testFlag'; key: string };
  }

  interface ScenarioEffectKindMap {
    testRecord: { type: 'testRecord'; key: string; value: string };
  }

  interface ObjectiveKindMap {
    testVariable: { type: 'testVariable'; key: string; equals: string };
  }
}

class TestSignalAction implements ActionHandler<'testSignal'> {
  readonly kind = 'testSignal' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['testSignal']): void {
    context.state.scenario.variables.lastSignal = action.signal;
    context.state.scenario.variables.unlocked = true;
    context.emit({ type: 'scenarioSignal', signal: action.signal });
  }
}

const testObjective: ObjectiveHandler<'testVariable'> = {
  kind: 'testVariable',
  outcome: ({ state }, objective) =>
    state.scenario.variables[objective.key] === objective.equals ? 'success' : 'pending',
  describe: (objective) => `变量 ${objective.key} 等于 ${objective.equals}`,
  progress: ({ state }, objective) => String(state.scenario.variables[objective.key] ?? '未设置'),
};

function testState(withExtensionObjective = false) {
  return createState(
    makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      scenario: { variables: { unlocked: false } },
      victory: withExtensionObjective
        ? [{ id: 'extension-goal', type: 'testVariable', key: 'chapter', equals: 'done' }]
        : [{ type: 'routEnemies' }],
    }),
  );
}

describe('open extension contracts', () => {
  it('adds a new action kind through declaration merging and a local strategy', () => {
    const state = testState();
    const handlers = new ActionHandlerRegistry().register(new TestSignalAction());

    const events = applyActionWith(state, { kind: 'testSignal', signal: 'extension.ready' }, handlers);

    expect(state.scenario.variables.lastSignal).toBe('extension.ready');
    expect(events).toContainEqual({ type: 'scenarioSignal', signal: 'extension.ready' });
  });

  it('adds scenario conditions and effects without changing the DSL interpreter', () => {
    const state = testState();
    const conditions = new ScenarioConditionHandlerRegistry().register({
      kind: 'testFlag',
      evaluate: ({ state: current }, condition) => current.scenario.variables[condition.key] === true,
    });
    const effects = new ScenarioEffectHandlerRegistry().register({
      kind: 'testRecord',
      apply: ({ state: current }, effect) => {
        current.scenario.variables[effect.key] = effect.value;
      },
    });

    expect(conditionMet(state, { type: 'testFlag', key: 'unlocked' }, conditions)).toBe(false);
    applyScenarioEffect(state, { type: 'testRecord', key: 'result', value: 'accepted' }, () => {}, effects);
    state.scenario.variables.unlocked = true;

    expect(conditionMet(state, { type: 'testFlag', key: 'unlocked' }, conditions)).toBe(true);
    expect(state.scenario.variables.result).toBe('accepted');
  });

  it('adds a stateful objective with outcome, description, progress, and lifecycle', () => {
    const state = testState(true);
    const handlers = new ObjectiveHandlerRegistry().register(testObjective);
    const objective = state.players[0].objectives[0] as ObjectiveKindMap['testVariable'] & { id: string };

    expect(objectiveOutcome(state, 1, objective, handlers)).toBe('pending');
    expect(handlers.describe(objective)).toContain('chapter');
    expect(objectiveProgress(state, 1, objective, handlers)).toBe('未设置');

    state.scenario.variables.chapter = 'done';
    const events: GameEvent[] = [];
    refreshObjectiveStates(state, (event) => events.push(event), handlers);

    expect(state.players[0].objectiveStates['extension-goal'].status).toBe('completed');
    expect(events).toContainEqual(expect.objectContaining({
      type: 'objectiveChanged',
      objective: 'extension-goal',
      status: 'completed',
    }));
  });

  it('teaches AI how to pursue a custom objective through an isolated advisor', () => {
    const state = testState(true);
    const handlers = new ObjectiveHandlerRegistry().register(testObjective);
    const advisors = new AiObjectiveAdvisorRegistry().register({
      kind: 'testVariable',
      advise: (context) => context.destination({ x: 1, y: 0 }, 9, 'extension target'),
    });

    const intent = buildAiMissionIntent(state, 1, advisors, handlers);
    expect(intent.destinations).toContainEqual(expect.objectContaining({
      at: { x: 1, y: 0 },
      weight: 9,
      reason: 'extension target',
    }));
  });

  it('composes all custom strategies in one isolated BattleEngine', () => {
    const actionHandlers = new ActionHandlerRegistry().register(new TestSignalAction());
    const conditions = new ScenarioConditionHandlerRegistry().register({
      kind: 'testFlag',
      evaluate: ({ state }, condition) => state.scenario.variables[condition.key] === true,
    });
    const effects = new ScenarioEffectHandlerRegistry().register({
      kind: 'testRecord',
      apply: ({ state }, effect) => {
        state.scenario.variables[effect.key] = effect.value;
      },
    });
    const objectives = new ObjectiveHandlerRegistry().register(testObjective);
    const engine = createBattleEngine({
      actionHandlers,
      scenarioConditions: conditions,
      scenarioEffects: effects,
      objectives,
    });
    const state = engine.createState(makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      victory: [{ id: 'extension-goal', type: 'testVariable', key: 'result', equals: 'accepted' }],
      scenario: {
        variables: { unlocked: false },
        triggers: [{
          id: 'extension-trigger',
          timing: 'afterAction',
          condition: { type: 'testFlag', key: 'unlocked' },
          effects: [{ type: 'testRecord', key: 'result', value: 'accepted' }],
        }],
      },
    }));

    const events = engine.dispatch(state, { kind: 'testSignal', signal: 'extension.ready' });

    expect(state.scenario.variables.result).toBe('accepted');
    expect(state.players[0].objectiveStates['extension-goal'].status).toBe('completed');
    expect(state.phase).toBe('over');
    expect(events).toContainEqual(expect.objectContaining({ type: 'gameOver', team: 1 }));
  });

  it('clones default registries before extending them, without global pollution', () => {
    const actionCopy = CoreActionHandlers.clone().register(new TestSignalAction());
    const conditionCopy = ScenarioConditionHandlers.clone().register({
      kind: 'testFlag',
      evaluate: () => true,
    });
    const effectCopy = ScenarioEffectHandlers.clone().register({
      kind: 'testRecord',
      apply: () => {},
    });
    const objectiveCopy = ObjectiveHandlers.clone().register(testObjective);
    const modifierCopy = CombatModifierProviders.clone().register({
      id: 'test.extension',
      priority: 999,
      provide: () => [],
    });
    const aiCopy = DefaultAiObjectiveAdvisors.clone().register({
      kind: 'testVariable',
      advise: () => {},
    });

    expect(actionCopy.kinds()).toContain('testSignal');
    expect(CoreActionHandlers.kinds()).not.toContain('testSignal');
    expect(conditionMet(testState(), { type: 'testFlag', key: 'x' }, conditionCopy)).toBe(true);
    expect(() => conditionMet(testState(), { type: 'testFlag', key: 'x' })).toThrow();
    expect(effectCopy.kinds()).toContain('testRecord');
    expect(ScenarioEffectHandlers.kinds()).not.toContain('testRecord');
    expect(objectiveCopy.handler('testVariable')).toBe(testObjective);
    expect(() => ObjectiveHandlers.handler('testVariable')).toThrow();
    expect(modifierCopy.ordered().some((provider) => provider.id === 'test.extension')).toBe(true);
    expect(CombatModifierProviders.ordered().some((provider) => provider.id === 'test.extension')).toBe(false);
    expect(aiCopy.advisor('testVariable')).toBeDefined();
    expect(DefaultAiObjectiveAdvisors.advisor('testVariable')).toBeUndefined();
  });
});
