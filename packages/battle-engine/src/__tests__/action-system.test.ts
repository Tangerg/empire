import { describe, expect, it } from 'vitest';
import { ActionExecutionContext, ActionHandlerRegistry, IllegalActionError, type ActionHandler } from '../action-system';
import { createBattleRules } from '../plugins/default';

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

declare module '../types' {
  interface ActionKindMap {
    rally: { kind: 'rally' };
  }
}

/** A pack's own pre-battle order: legal while deploying, illegal once playing. */
class PreBattleRallyHandler implements ActionHandler<'rally'> {
  readonly kind = 'rally' as const;
  readonly duringDeployment = true;

  execute(context: ActionExecutionContext): void {
    const variables = context.state.scenario.variables;
    variables.rallied = Number(variables.rallied ?? 0) + 1;
  }
}

describe('action strategy registry', () => {
  it('gives every ruleset its own registries, with no shared fallback', () => {
    const first = createBattleRules({ content: createTestCatalog() });
    const second = createBattleRules({ content: createTestCatalog() });

    expect(first.content).not.toBe(second.content);
    expect(first.abilities).not.toBe(second.abilities);
    expect(first.scenarioConditions).not.toBe(second.scenarioConditions);

    // Extending one ruleset must be invisible to the other.
    first.content.units.override('soldier', { value: 999 });
    expect(second.content.units.get('soldier').value).not.toBe(999);
  });

  it('registers one cohesive strategy for every built-in action kind', () => {
    expect(CoreActionHandlers.keys().sort()).toEqual(
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

  it('lets a pack add its own order to the deployment phase', () => {
    // The dispatcher used to name `deployUnit` and `finishDeployment` in a
    // literal pair — the very closed-pair comparison that `handsOffTurn` was
    // introduced to remove, three lines under the comment saying so. A pack's
    // own pre-battle order was refused as "finish deploying first", which is
    // exactly what it was doing.
    const rules = createBattleRules({ content: createTestCatalog() });
    const handlers = CoreActionHandlers.clone().register(new PreBattleRallyHandler());
    const state = testState(makeLevel(['....'], {
      units: [u(0, 0, 'soldier', 1), u(3, 0, 'soldier', 2)],
      scenario: { zones: [{ id: 'front', cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }] }] },
      deployment: { order: [1], zones: [{ player: 1, zone: 'front' }] },
    }));
    expect(state.phase).toBe('deployment');

    testApplyWith(state, { kind: 'rally' } as never, handlers, rules);
    expect(state.scenario.variables.rallied).toBe(1);
    // And an ordinary order is still refused until the arrangement is confirmed.
    expect(() => testApplyWith(state, { kind: 'endTurn' }, handlers, rules)).toThrow(IllegalActionError);
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
