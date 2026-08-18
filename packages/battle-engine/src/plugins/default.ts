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
import { BattleEngine, type BattleEngineDependencies } from '../engine';
import { ObjectiveHandlers } from '../objective-system';
import { DefaultRankProgression } from '../progression';
import { DefaultBattleResources } from '../resources';
import { ScenarioConditionHandlers, ScenarioEffectHandlers } from '../scenario';
import { StatusBehaviors } from '../statuses';
import { Abilities } from '../abilities';
import { DefaultTacticalSpace } from '../tactical-space';
import { TacticalGrids } from '../tactical-grid';
import { TurnOrders } from '../turn-order';
import { Reactions } from '../reactions';
import { UnitDepartureHandlers } from '../unit-departure';
import { WeaponAreaShapes } from '../weapon-area';
import { UnitDirectives } from '../unit-directive';
import { DefaultRuleReferenceChecks } from '../rule-references';
import { DefaultBattleSaves } from '../battle-save';
import { SplitMixRandom } from '../random';
import type { ContentCatalog } from '../content-pack';

/**
 * Supplies the catalog this engine plays on. Content is never discovered.
 *
 * The catalog belongs to this engine alone, so two engines in one process can
 * run different themes — including themes reusing the same terrain legend
 * characters, because the namespace is per catalog.
 *
 * There used to be a second function claiming the same plugin id and the same
 * capability, differing only in that it built the catalog from packs itself. No
 * caller wanted it: every root composes its catalog explicitly, which is what
 * "there is no ambient content" asks for, and two ways to provide one capability
 * is one way too many.
 */
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
    'saves',
    'grids',
  ],
  install: (context) => {
    const content = context.require('content');
    const grids = TacticalGrids.clone();
    context.provide('grids', grids);
    context.provide('abilities', Abilities.clone());
    context.provide('space', new DefaultTacticalSpace(content, grids));
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
    context.provide('saves', DefaultBattleSaves.clone());
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
 *
 * Module-private, because a second exported function that returns a
 * `BattleEngine` is a second way to build one however carefully it is
 * documented — and the demo had already taken it.
 */
function assembleBattleEngine(capabilities: KernelCapabilities): BattleEngine {
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
    grids: capabilities.require('grids'),
    saves: capabilities.require('saves'),
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
  }, capabilities.pluginManifest);
}

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
 * What one engine is composed from.
 *
 * Here rather than beside `BattleEngine`, because this is the composition
 * root's parameter object and not part of the engine's own vocabulary: an
 * engine does not know what a plugin is, and saying otherwise made `engine.ts`
 * and `kernel.ts` import each other. `content` is required and is never
 * defaulted to ambient state; every capability field, if given, replaces the
 * default the rule plugins install.
 */
export interface BattleEngineOverrides extends Partial<BattleEngineDependencies> {
  /** The catalog this engine plays on; never defaulted to ambient state. */
  readonly content: ContentCatalog;
  /**
   * Plugins installed alongside the defaults, ordered like any other.
   *
   * The reason this exists rather than a second builder: swapping one capability
   * for a ready-made value is what the fields above are for, but a plugin that
   * *provides* something new, or that needs the value it replaces, has to take
   * part in composition. Without this the only way to install one was to run the
   * kernel by hand — which is a second way to build an engine, and there is one.
   */
  readonly plugins?: readonly EnginePlugin[];
}

/**
 * The one composition root.
 *
 * There used to be two, and only one of them ran the plugins: every app and
 * every test but one built its engine through a factory that assembled the same
 * twenty-one defaults by hand, so the plugin architecture was real code the
 * product never executed — and the defaults had to be kept in step with the
 * plugins that shadowed them.
 *
 * Then a third appeared for a smaller reason: composing by hand was the only
 * way to add a plugin or to read the manifest afterwards. Both are ordinary
 * requests, so both are parameters — `plugins` goes in, `pluginManifest` comes
 * out on the engine, and the kernel stays behind this function.
 */
export function createBattleEngine(overrides: BattleEngineOverrides): BattleEngine {
  const { content, plugins = [], ...swapped } = overrides;
  const kernel = createDefaultMicrokernel(content).useAll(plugins);
  for (const [capability, value] of Object.entries(swapped)) {
    if (value === undefined) continue;
    kernel.use(overridePlugin(capability as KernelCapabilityId, value as never));
  }
  return assembleBattleEngine(kernel.compose());
}

/** The ruleset alone, for a rule under test that needs no engine around it. */
export const createBattleRules = (overrides: BattleEngineOverrides) =>
  createBattleEngine(overrides).rules;
