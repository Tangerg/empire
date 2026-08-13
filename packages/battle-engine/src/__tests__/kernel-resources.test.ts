import { describe, expect, it } from 'vitest';
import { SrpgMicrokernel, type EnginePlugin } from '../kernel';
import {
  AiPlanningPlugin,
  createDefaultMicrokernel,
  DEFAULT_ENGINE_PLUGINS,
  MissionRulesPlugin,
  ResourceEconomyPlugin,
  TacticalRulesPlugin,
} from '../plugins/default';
import {
  COMMAND_POINTS_RESOURCE,
  DefaultBattleResources,
  FUNDS_RESOURCE,
  playerResource,
} from '../resources';
import { makeLevel, testState, u } from './fixtures';

describe('cohesive microkernel modules', () => {
  it('ships four self-contained capability modules rather than component-sized plugins', () => {
    expect(DEFAULT_ENGINE_PLUGINS).toEqual([
      TacticalRulesPlugin,
      MissionRulesPlugin,
      ResourceEconomyPlugin,
      AiPlanningPlugin,
    ]);

    const context = createDefaultMicrokernel().compose();
    expect(context.providerOf('abilities')).toBe(TacticalRulesPlugin.id);
    expect(context.providerOf('space')).toBe(TacticalRulesPlugin.id);
    expect(context.providerOf('combatModifiers')).toBe(TacticalRulesPlugin.id);
    expect(context.providerOf('statusBehaviors')).toBe(TacticalRulesPlugin.id);
    expect(context.providerOf('actionHandlers')).toBe(MissionRulesPlugin.id);
    expect(context.providerOf('objectives')).toBe(MissionRulesPlugin.id);
    expect(context.providerOf('resources')).toBe(ResourceEconomyPlugin.id);
    expect(context.providerOf('aiObjectiveAdvisors')).toBe(AiPlanningPlugin.id);
  });

  it('rejects missing dependencies, cycles, and competing capability providers', () => {
    expect(() => new SrpgMicrokernel().use(AiPlanningPlugin).compose()).toThrow(/missing capability/);

    const left: EnginePlugin = {
      id: 'test.left', version: 1, requires: ['test.right'], install: () => {},
    };
    const right: EnginePlugin = {
      id: 'test.right', version: 1, requires: ['test.left'], install: () => {},
    };
    expect(() => new SrpgMicrokernel().useAll([left, right]).compose()).toThrow(/cyclic/);

    const duplicate: EnginePlugin = {
      id: 'test.duplicate',
      version: 1,
      install: (context) => context.provide('resources', DefaultBattleResources.clone()),
    };
    expect(() => new SrpgMicrokernel()
      .use(ResourceEconomyPlugin)
      .use(duplicate)
      .compose()).toThrow(/already provided/);
  });

  it('orders and accepts substitute providers by capability instead of fixed plugin id', () => {
    const substituteTactical: EnginePlugin = {
      ...TacticalRulesPlugin,
      id: 'test.substitute-tactical-rules',
    };
    const context = new SrpgMicrokernel()
      .use(AiPlanningPlugin)
      .use(ResourceEconomyPlugin)
      .use(MissionRulesPlugin)
      .use(substituteTactical)
      .compose();

    expect(context.providerOf('content')).toBe(substituteTactical.id);
    expect(context.providerOf('abilityAiEvaluators')).toBe(AiPlanningPlugin.id);
  });
});

describe('entity-owned resource accounts', () => {
  it('stores state on the aggregate and keeps cloned engines policy-isolated', () => {
    const state = testState(makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      funds: [100, 0],
    }));
    const subject = playerResource(state.players[0]);
    const engineA = createDefaultMicrokernel().buildBattleEngine();
    const engineB = createDefaultMicrokernel().buildBattleEngine();

    engineA.rules.resources.spend(FUNDS_RESOURCE, subject, 30);

    expect(state.players[0].resources[FUNDS_RESOURCE].current).toBe(70);
    expect(engineB.rules.resources.adapters).not.toBe(engineA.rules.resources.adapters);
  });

  it('checks repeated costs atomically before changing an entity account', () => {
    const state = testState(makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
    }));
    const subject = playerResource(state.players[0]);
    state.players[0].resources[COMMAND_POINTS_RESOURCE] = { current: 3, capacity: 5 };
    const costs = [
      { resource: COMMAND_POINTS_RESOURCE, amount: 2 },
      { resource: COMMAND_POINTS_RESOURCE, amount: 2 },
    ];

    expect(DefaultBattleResources.canAfford(costs, subject)).toBe(false);
    expect(() => DefaultBattleResources.spendAll(costs, subject)).toThrow(/insufficient resource/);
    expect(state.players[0].resources[COMMAND_POINTS_RESOURCE].current).toBe(3);
  });
});
