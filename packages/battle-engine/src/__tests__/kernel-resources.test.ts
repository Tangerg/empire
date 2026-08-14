import { describe, expect, it } from 'vitest';
import { SrpgMicrokernel, type EnginePlugin } from '../kernel';
import {
  AiPlanningPlugin,
  contentPluginFor,
  buildBattleEngine,
  createBattleEngine,
  createDefaultMicrokernel,
  overridePlugin,
  DEFAULT_RULE_PLUGINS,
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
import { Reactions } from '../reactions';
import { DeterministicOnlyRandom, SplitMixRandom } from '../random';
import { ScenarioConditionHandlers } from '../scenario';
import { TEST_CONTENT, makeLevel, testState, u } from './fixtures';

describe('cohesive microkernel modules', () => {
  it('ships four self-contained capability modules rather than component-sized plugins', () => {
    expect(DEFAULT_RULE_PLUGINS).toEqual([
      TacticalRulesPlugin,
      MissionRulesPlugin,
      ResourceEconomyPlugin,
      AiPlanningPlugin,
    ]);
    // Content is supplied by the composition root, not by a rule plugin.
    expect(DEFAULT_RULE_PLUGINS.some((plugin) => plugin.provides?.includes('content'))).toBe(false);
    expect(createDefaultMicrokernel(TEST_CONTENT).compose().providerOf('content'))
      .toBe('engine.content');

    const context = createDefaultMicrokernel(TEST_CONTENT).compose();
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
    expect(() => new SrpgMicrokernel().use(contentPluginFor(TEST_CONTENT)).use(AiPlanningPlugin).compose()).toThrow(/missing capability/);

    const left: EnginePlugin = {
      id: 'test.left', version: 1, requires: ['test.right'], install: () => {},
    };
    const right: EnginePlugin = {
      id: 'test.right', version: 1, requires: ['test.left'], install: () => {},
    };
    expect(() => new SrpgMicrokernel().use(contentPluginFor(TEST_CONTENT)).useAll([left, right]).compose()).toThrow(/cyclic/);

    const duplicate: EnginePlugin = {
      id: 'test.duplicate',
      version: 1,
      install: (context) => context.provide('resources', DefaultBattleResources.clone()),
    };
    expect(() => new SrpgMicrokernel().use(contentPluginFor(TEST_CONTENT))
      .use(ResourceEconomyPlugin)
      .use(duplicate)
      .compose()).toThrow(/already provided/);
  });

  it('orders and accepts substitute providers by capability instead of fixed plugin id', () => {
    const substituteTactical: EnginePlugin = {
      ...TacticalRulesPlugin,
      id: 'test.substitute-tactical-rules',
    };
    const context = new SrpgMicrokernel().use(contentPluginFor(TEST_CONTENT))
      .use(AiPlanningPlugin)
      .use(ResourceEconomyPlugin)
      .use(MissionRulesPlugin)
      .use(substituteTactical)
      .compose();

    // Capabilities resolve by manifest, not by plugin id, so a substitute is
    // accepted transparently. Content now comes from the composition root.
    expect(context.providerOf('abilities')).toBe(substituteTactical.id);
    expect(context.providerOf('space')).toBe(substituteTactical.id);
    expect(context.providerOf('content')).toBe('engine.content');
    expect(context.providerOf('abilityAiEvaluators')).toBe(AiPlanningPlugin.id);
  });
});

describe('capability substitution', () => {
  const loudReactions = () => Reactions.clone().register({
    id: 'test.parry',
    name: '格挡',
    hint: '',
    intercepts: false,
    retaliates: true,
    conservesResources: false,
    incomingMultiplier: 0.5,
  });

  it('lets a later plugin replace a rule an earlier plugin introduced', () => {
    const reactions = loudReactions();
    const context = createDefaultMicrokernel(TEST_CONTENT)
      .use(overridePlugin('reactions', reactions))
      .compose();

    expect(context.require('reactions')).toBe(reactions);
    // The introducer still owns the slot; the override is a separate claim.
    expect(context.providerOf('reactions')).toBe(TacticalRulesPlugin.id);
  });

  it('refuses to replace a capability nobody introduced', () => {
    expect(() => new SrpgMicrokernel()
      .use(contentPluginFor(TEST_CONTENT))
      .use(overridePlugin('reactions', loudReactions()))
      .compose()).toThrow(/requires missing capability "reactions"/);
  });

  it('holds an override to its manifest, in both directions', () => {
    const sneaky: EnginePlugin = {
      id: 'test.sneaky',
      version: 1,
      requiresCapabilities: ['reactions'],
      install: (context) => context.replace('reactions', loudReactions()),
    };
    expect(() => createDefaultMicrokernel(TEST_CONTENT).use(sneaky).compose())
      .toThrow(/did override undeclared capability "reactions"/);

    const idle: EnginePlugin = {
      id: 'test.idle',
      version: 1,
      overrides: ['reactions'],
      install: () => {},
    };
    expect(() => createDefaultMicrokernel(TEST_CONTENT).use(idle).compose())
      .toThrow(/declared but did not override capability "reactions"/);
  });

  it('lands an override before everyone who reads the capability', () => {
    // The mission rules read `random` at install time to seed the scenario
    // condition registry. An override that arrived after them would leave the
    // seeded copy holding the source it was supposed to replace.
    const random = DeterministicOnlyRandom;
    const engine = buildBattleEngine(
      createDefaultMicrokernel(TEST_CONTENT).use(overridePlugin('random', random)).compose(),
    );

    expect(engine.rules.random).toBe(random);
    expect(engine.rules.scenarioConditions.random).toBe(random);
  });

  it('splits one factory override per capability so the order stays acyclic', () => {
    // `scenarioConditions` is introduced by the mission rules and consumes
    // `random` from the tactical rules. One plugin overriding both would have to
    // run before and after the mission rules at once.
    const random = DeterministicOnlyRandom;
    const scenarioConditions = ScenarioConditionHandlers.clone(SplitMixRandom);
    const engine = createBattleEngine({ content: TEST_CONTENT, random, scenarioConditions });

    expect(engine.rules.random).toBe(random);
    expect(engine.rules.scenarioConditions).toBe(scenarioConditions);
  });
});

describe('entity-owned resource accounts', () => {
  it('stores state on the aggregate and keeps cloned engines policy-isolated', () => {
    const state = testState(makeLevel(['..'], {
      units: [u(0, 0, 'soldier', 1), u(1, 0, 'soldier', 2)],
      funds: [100, 0],
    }));
    const subject = playerResource(state.players[0]);
    const engineA = buildBattleEngine(createDefaultMicrokernel(TEST_CONTENT).compose());
    const engineB = buildBattleEngine(createDefaultMicrokernel(TEST_CONTENT).compose());

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
