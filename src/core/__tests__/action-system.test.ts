import { describe, expect, it } from 'vitest';
import {
  ActionExecutionContext,
  ActionHandlerRegistry,
  createDefaultBattleRuleServices,
  DefaultBattleRuleServices,
  IllegalActionError,
  type ActionHandler,
} from '../action-system';
import { GlobalContentCatalog } from '../content-pack';
import { applyActionWith, CoreActionHandlers } from '../actions';
import { createState } from '../state';
import type { ActionKindMap } from '../types';
import { makeLevel, u } from './fixtures';

class TestReactionHandler implements ActionHandler<'reaction'> {
  readonly kind = 'reaction' as const;

  execute(context: ActionExecutionContext, action: ActionKindMap['reaction']): void {
    context.state.scenario.variables.lastStance = action.stance;
    context.emit({ type: 'scenarioSignal', signal: `test.${action.stance}` });
  }
}

describe('action strategy registry', () => {
  it('separates the live low-level defaults from isolated engine rule graphs', () => {
    const isolated = createDefaultBattleRuleServices();
    expect(DefaultBattleRuleServices.content).toBe(GlobalContentCatalog);
    expect(isolated.content).not.toBe(GlobalContentCatalog);
    expect(isolated.abilities).not.toBe(DefaultBattleRuleServices.abilities);
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
    const state = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    const handlers = new ActionHandlerRegistry().register(new TestReactionHandler());
    const events = applyActionWith(
      state,
      { kind: 'reaction', unit: state.units[0].id, stance: 'guard' },
      handlers,
    );
    expect(state.scenario.variables.lastStance).toBe('guard');
    expect(events).toContainEqual({ type: 'scenarioSignal', signal: 'test.guard' });
  });

  it('fails clearly when a strategy is absent', () => {
    const state = createState(
      makeLevel(['..'], { units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)] }),
    );
    expect(() =>
      applyActionWith(state, { kind: 'endTurn' }, new ActionHandlerRegistry()),
    ).toThrow(IllegalActionError);
  });
});
