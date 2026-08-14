import { CoreActionHandlers } from '../actions';
import { DefaultAiObjectiveAdvisors } from '../ai-objectives';
import { DefaultAbilityAiEvaluators, DefaultAiIntents } from '../ai';
import { CombatModifierPipeline, CombatModifierProviders } from '../combat-modifiers';
import { WeaponHitEffectHandlers } from '../hit-effects';
import { SrpgMicrokernel, type EnginePlugin } from '../kernel';
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

/** Fresh engine per session; plugin registries never leak mutations across games. */
export const createDefaultBattleEngine = (content: ContentCatalog) =>
  createDefaultMicrokernel(content).buildBattleEngine();
