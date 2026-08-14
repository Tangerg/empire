import { CoreActionHandlers } from '../actions';
import { DefaultAiObjectiveAdvisors } from '../ai-objectives';
import { DefaultAbilityAiEvaluators, DefaultAiIntents } from '../ai';
import { CombatModifierPipeline, CombatModifierProviders } from '../combat-modifiers';
import { WeaponHitEffectHandlers } from '../hit-effects';
import {
  SrpgMicrokernel,
  type EnginePlugin,
  type KernelCapabilities,
  type KernelCapabilityId,
  type KernelCapabilityMap,
} from '../kernel';
import { BattleEngine, type BattleEngineOverrides } from '../engine';
import { ObjectiveHandlers } from '../objective-system';
import { DefaultRankProgression } from '../progression';
import { DefaultBattleResources } from '../resources';
import { ScenarioConditionHandlers, ScenarioEffectHandlers } from '../scenario';
import { StatusBehaviors } from '../statuses';
import { Abilities } from '../abilities';
import { DefaultTacticalSpace } from '../tactical-space';
import { TurnOrders } from '../turn-order';
import { Reactions } from '../reactions';
import { UnitDepartureHandlers } from '../unit-departure';
import { WeaponAreaShapes } from '../weapon-area';
import { UnitDirectives } from '../unit-directive';
import { DefaultRuleReferenceChecks } from '../rule-references';
import { SplitMixRandom } from '../random';
import {
  ContentPackInstaller,
  createContentCatalog,
  type ContentCatalog,
  type ContentPack,
} from '../content-pack';

/**
 * Content is supplied by the composition root, not discovered.
 *
 * An application (or a test) declares which packs its engine plays on; the
 * plugin installs them into a catalog owned by that engine alone. Two engines
 * can therefore run different themes — including themes that reuse the same
 * terrain legend characters — because the namespace is per catalog.
 */
export function createContentPlugin(packs: readonly ContentPack[]): EnginePlugin {
  return {
    id: 'engine.content',
    version: 1,
    provides: ['content'],
    install: (context) => {
      const catalog = createContentCatalog();
      new ContentPackInstaller(catalog).install(...packs);
      context.provide('content', catalog);
    },
  };
}

/** Supplies an already-composed catalog, e.g. one shared by a test suite. */
export function contentPluginFor(catalog: ContentCatalog): EnginePlugin {
  return {
    id: 'engine.content',
    version: 1,
    provides: ['content'],
    install: (context) => context.provide('content', catalog),
  };
}

/** Cohesive tactical rules: resolution, effects, statuses and battle-local growth. */
export const TacticalRulesPlugin: EnginePlugin = {
  id: 'engine.tactical-rules',
  version: 1,
  requiresCapabilities: ['content'],
  provides: [
    'abilities',
    'space',
    'combatModifiers',
    'hitEffects',
    'statusBehaviors',
    'progression',
    'turnOrders',
    'reactions',
    'unitDepartures',
    'random',
    'areaShapes',
    'directives',
    'referenceChecks',
  ],
  install: (context) => {
    const content = context.require('content');
    context.provide('abilities', Abilities.clone());
    context.provide('space', new DefaultTacticalSpace(content));
    context.provide('combatModifiers', new CombatModifierPipeline(CombatModifierProviders.clone()));
    context.provide('hitEffects', WeaponHitEffectHandlers.clone());
    context.provide('statusBehaviors', StatusBehaviors.clone());
    context.provide('progression', DefaultRankProgression);
    context.provide('random', SplitMixRandom);
    context.provide('turnOrders', TurnOrders.clone());
    context.provide('reactions', Reactions.clone());
    context.provide('unitDepartures', UnitDepartureHandlers.clone());
    context.provide('areaShapes', WeaponAreaShapes.clone());
    context.provide('directives', UnitDirectives.clone());
    context.provide('referenceChecks', DefaultRuleReferenceChecks.clone());
  },
};

/** Cohesive mission loop: commands, scripted situations and victory objectives. */
export const MissionRulesPlugin: EnginePlugin = {
  id: 'engine.mission-rules',
  version: 1,
  provides: ['actionHandlers', 'scenarioConditions', 'scenarioEffects', 'objectives'],
  requiresCapabilities: ['random'],
  install: (context) => {
    context.provide('actionHandlers', CoreActionHandlers.clone());
    context.provide('scenarioConditions', ScenarioConditionHandlers.clone(context.require('random')));
    context.provide('scenarioEffects', ScenarioEffectHandlers.clone());
    context.provide('objectives', ObjectiveHandlers.clone());
  },
};

/** Account policies and adapters; the account state itself stays on its entity. */
export const ResourceEconomyPlugin: EnginePlugin = {
  id: 'engine.resource-economy',
  version: 1,
  provides: ['resources'],
  install: (context) => context.provide('resources', DefaultBattleResources.clone()),
};

/** Complete AI planning module; required by the default playable engine. */
export const AiPlanningPlugin: EnginePlugin = {
  id: 'engine.ai-planning',
  version: 1,
  provides: ['aiObjectiveAdvisors', 'abilityAiEvaluators', 'aiIntents'],
  requiresCapabilities: ['content', 'abilities', 'space', 'objectives', 'resources'],
  install: (context) => {
    context.provide('aiObjectiveAdvisors', DefaultAiObjectiveAdvisors.clone());
    context.provide('abilityAiEvaluators', DefaultAbilityAiEvaluators.clone());
    context.provide('aiIntents', DefaultAiIntents.clone());
  },
};

/** The rule plugins. Content is deliberately absent: the app declares it. */
export const DEFAULT_RULE_PLUGINS: readonly EnginePlugin[] = [
  TacticalRulesPlugin,
  MissionRulesPlugin,
  ResourceEconomyPlugin,
  AiPlanningPlugin,
];

export function createDefaultMicrokernel(content: ContentCatalog): SrpgMicrokernel {
  return new SrpgMicrokernel().use(contentPluginFor(content)).useAll(DEFAULT_RULE_PLUGINS);
}

/**
 * Turns a composed capability set into the application it was composed for.
 *
 * Deliberately not a method on the microkernel: a generic plugin host that
 * knows how to construct one concrete engine is a host that cannot compose
 * anything else. Each field demands its own capability by name, so `require`
 * names whichever one a plugin set forgot.
 */
export function buildBattleEngine(capabilities: KernelCapabilities): BattleEngine {
  return new BattleEngine({
    content: capabilities.require('content'),
    abilities: capabilities.require('abilities'),
    space: capabilities.require('space'),
    actionHandlers: capabilities.require('actionHandlers'),
    combatModifiers: capabilities.require('combatModifiers'),
    hitEffects: capabilities.require('hitEffects'),
    statusBehaviors: capabilities.require('statusBehaviors'),
    areaShapes: capabilities.require('areaShapes'),
    directives: capabilities.require('directives'),
    referenceChecks: capabilities.require('referenceChecks'),
    scenarioConditions: capabilities.require('scenarioConditions'),
    scenarioEffects: capabilities.require('scenarioEffects'),
    objectives: capabilities.require('objectives'),
    progression: capabilities.require('progression'),
    resources: capabilities.require('resources'),
    turnOrders: capabilities.require('turnOrders'),
    reactions: capabilities.require('reactions'),
    unitDepartures: capabilities.require('unitDepartures'),
    random: capabilities.require('random'),
    aiObjectiveAdvisors: capabilities.require('aiObjectiveAdvisors'),
    abilityAiEvaluators: capabilities.require('abilityAiEvaluators'),
    aiIntents: capabilities.require('aiIntents'),
  });
}

/** Fresh engine per session; plugin registries never leak mutations across games. */
export const createDefaultBattleEngine = (content: ContentCatalog): BattleEngine =>
  buildBattleEngine(createDefaultMicrokernel(content).compose());

/**
 * A plugin whose whole job is to swap one rule for the ruleset it is added to.
 *
 * One plugin per capability rather than one plugin for all of them, and that is
 * not cosmetic: a single plugin overriding both `random` and `scenarioConditions`
 * would have to install after the mission rules (whose `scenarioConditions` it
 * replaces) and before them (whose `random` it feeds) — a cycle. Split, each
 * override lands in exactly the right place in the order.
 */
export function overridePlugin<K extends KernelCapabilityId>(
  capability: K,
  value: KernelCapabilityMap[K],
): EnginePlugin {
  return {
    id: `engine.override.${capability}`,
    version: 1,
    overrides: [capability],
    install: (context) => context.replace(capability, value),
  };
}

/**
 * The one composition root.
 *
 * There used to be two, and only one of them ran the plugins: every app and
 * every test but one built its engine through a factory that assembled the same
 * twenty-one defaults by hand, so the plugin architecture was real code the
 * product never executed — and the defaults had to be kept in step with the
 * plugins that shadowed them.
 */
export function createBattleEngine(overrides: BattleEngineOverrides): BattleEngine {
  const { content, ...swapped } = overrides;
  const kernel = createDefaultMicrokernel(content);
  for (const [capability, value] of Object.entries(swapped)) {
    if (value === undefined) continue;
    kernel.use(overridePlugin(capability as KernelCapabilityId, value as never));
  }
  return buildBattleEngine(kernel.compose());
}

/** The ruleset alone, for a rule under test that needs no engine around it. */
export const createBattleRules = (overrides: BattleEngineOverrides) =>
  createBattleEngine(overrides).rules;
