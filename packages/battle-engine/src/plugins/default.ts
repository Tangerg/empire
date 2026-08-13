import { CoreActionHandlers } from '../actions';
import { DefaultAiObjectiveAdvisors } from '../ai-objectives';
import { DefaultAbilityAiEvaluators } from '../ai';
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
import { SplitMixRandom } from '../random';
import { cloneContentCatalog, GlobalContentCatalog } from '../content-pack';

/** Cohesive tactical rules: resolution, effects, statuses and battle-local growth. */
export const TacticalRulesPlugin: EnginePlugin = {
  id: 'engine.tactical-rules',
  version: 1,
  provides: [
    'content',
    'abilities',
    'space',
    'combatModifiers',
    'hitEffects',
    'statusBehaviors',
    'progression',
    'turnOrders',
    'random',
  ],
  install: (context) => {
    const content = cloneContentCatalog(GlobalContentCatalog);
    context.provide('content', content);
    context.provide('abilities', Abilities.clone());
    context.provide('space', new DefaultTacticalSpace(content));
    context.provide('combatModifiers', new CombatModifierPipeline(CombatModifierProviders.clone()));
    context.provide('hitEffects', WeaponHitEffectHandlers.clone());
    context.provide('statusBehaviors', StatusBehaviors.clone());
    context.provide('progression', DefaultRankProgression);
    context.provide('random', SplitMixRandom);
    context.provide('turnOrders', TurnOrders.clone());
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
  provides: ['aiObjectiveAdvisors', 'abilityAiEvaluators'],
  requiresCapabilities: ['content', 'abilities', 'space', 'objectives', 'resources'],
  install: (context) => {
    context.provide('aiObjectiveAdvisors', DefaultAiObjectiveAdvisors.clone());
    context.provide('abilityAiEvaluators', DefaultAbilityAiEvaluators.clone());
  },
};

export const DEFAULT_ENGINE_PLUGINS: readonly EnginePlugin[] = [
  TacticalRulesPlugin,
  MissionRulesPlugin,
  ResourceEconomyPlugin,
  AiPlanningPlugin,
];

export function createDefaultMicrokernel(): SrpgMicrokernel {
  return new SrpgMicrokernel().useAll(DEFAULT_ENGINE_PLUGINS);
}

/** Fresh engine per session; plugin registries never leak mutations across games. */
export const createDefaultBattleEngine = () => createDefaultMicrokernel().buildBattleEngine();
