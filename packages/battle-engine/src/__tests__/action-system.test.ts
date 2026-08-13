import { describe, expect, it } from 'vitest';
import {
  ActionExecutionContext,
  ActionHandlerRegistry,
  createDefaultBattleRuleServices,
  IllegalActionError,
  type ActionHandler,
} from '../action-system';

import { CoreActionHandlers } from '../actions';
import type { ActionKindMap } from '../types';
import { makeLevel, testApplyWith, testState, u } from './fixtures';
import { createTestCatalog } from '@empire/test-content';

class TestReactionHandler implements ActionHandler<'reaction'> {
  readonly kind = 'reaction' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['reaction']): void {
    context.state.scenario.variables.lastStance = action.stance;
    context.emit({ type: 'scenarioSignal', signal: `test.${action.stance}` });
  }
}

describe('action strategy registry', () => {
  it('gives every ruleset its own registries, with no shared fallback', () => {
    const first = createDefaultBattleRuleServices({ content: createTestCatalog() });
    const second = createDefaultBattleRuleServices({ content: createTestCatalog() });

    expect(first.content).not.toBe(second.content);
    expect(first.abilities).not.toBe(second.abilities);
    expect(first.scenarioConditions).not.toBe(second.scenarioConditions);

    // Extending one ruleset must be invisible to the other.
    first.content.units.override('soldier', { value: 999 });
    expect(second.content.units.get('soldier').value).not.toBe(999);
  });

  it('registers one cohesive strategy for every built-in action kind', () => {
    expect(CoreActionHandlers.kinds().sort()).toEqual(
      [
        'changeCareer', 'changeFormation', 'command', 'deployUnit', 'disembark', 'embark',
        'endTurn', 'face', 'finishDeployment', 'reaction', 'recruit', 'tactic',
      ].sort(),
    );
  });

  it('supports an injected strategy set without changing the reducer', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const handlers = new ActionHandlerRegistry().register(new TestReactionHandler());
    const events = testApplyWith(
      state,
      { kind: 'reaction', unit: state.units[0].id, stance: 'guard' },
      handlers,
    );
    expect(state.scenario.variables.lastStance).toBe('guard');
    expect(events).toContainEqual({ type: 'scenarioSignal', signal: 'test.guard' });
  });

  it('fails clearly when a strategy is absent', () => {
    const state = testState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    expect(() =>
      testApplyWith(state, { kind: 'endTurn' }, new ActionHandlerRegistry()),
    ).toThrow(IllegalActionError);
  });
});
