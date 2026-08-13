import type { ActionHandlerRegistry, BattleRuleServices } from './action-system';
import type { AiObjectiveAdvisorRegistry } from './ai-objectives';
import type { AbilityAiEvaluatorRegistry, AiIntentRegistry } from './ai';
import type { CombatModifierPipeline } from './combat-modifiers';
import { orderByDependencies } from './dependency-order';
import { BattleEngine } from './engine';

/**
 * Open capability map. Third-party plugins may declaration-merge capabilities
 * used by their own plugins even when BattleEngine itself does not consume them.
 */
export interface KernelCapabilityMap {
  content: BattleRuleServices['content'];
  abilities: BattleRuleServices['abilities'];
  space: BattleRuleServices['space'];
  actionHandlers: ActionHandlerRegistry;
  combatModifiers: CombatModifierPipeline;
  hitEffects: BattleRuleServices['hitEffects'];
  statusBehaviors: BattleRuleServices['statusBehaviors'];
  scenarioConditions: BattleRuleServices['scenarioConditions'];
  scenarioEffects: BattleRuleServices['scenarioEffects'];
  objectives: BattleRuleServices['objectives'];
  progression: BattleRuleServices['progression'];
  resources: BattleRuleServices['resources'];
  turnOrders: BattleRuleServices['turnOrders'];
  reactions: BattleRuleServices['reactions'];
  unitDepartures: BattleRuleServices['unitDepartures'];
  random: BattleRuleServices['random'];
  aiObjectiveAdvisors: AiObjectiveAdvisorRegistry;
  abilityAiEvaluators: AbilityAiEvaluatorRegistry;
  aiIntents: AiIntentRegistry;
}

export type KernelCapabilityId = Extract<keyof KernelCapabilityMap, string>;

export interface EnginePlugin {
  readonly id: string;
  readonly version: number;
  /** Capability manifest enables substitution without depending on provider ids. */
  readonly provides?: readonly KernelCapabilityId[];
  readonly requiresCapabilities?: readonly KernelCapabilityId[];
  /** Explicit ordering/dependency edge for non-capability relationships. */
  readonly requires?: readonly string[];
  install(context: KernelPluginContext): void;
}

/** Read-only result of kernel composition. */
export interface KernelCapabilities {
  has(id: KernelCapabilityId): boolean;
  require<K extends KernelCapabilityId>(id: K): KernelCapabilityMap[K];
  providerOf(id: KernelCapabilityId): string | undefined;
}

/** Capability-segregated port handed to one plugin during installation. */
export interface KernelPluginContext extends KernelCapabilities {
  provide<K extends KernelCapabilityId>(id: K, capability: KernelCapabilityMap[K]): void;
}

class KernelCapabilityStore {
  private readonly capabilities = new Map<string, unknown>();
  private readonly providers = new Map<string, string>();

  provide<K extends KernelCapabilityId>(
    providerId: string,
    id: K,
    capability: KernelCapabilityMap[K],
  ): void {
    const provider = this.providers.get(id);
    if (provider) {
      throw new Error(`kernel capability "${id}" already provided by "${provider}"`);
    }
    this.capabilities.set(id, capability);
    this.providers.set(id, providerId);
  }

  has(id: KernelCapabilityId): boolean {
    return this.capabilities.has(id);
  }

  require<K extends KernelCapabilityId>(id: K): KernelCapabilityMap[K] {
    if (!this.capabilities.has(id)) throw new Error(`missing kernel capability: "${id}"`);
    return this.capabilities.get(id) as KernelCapabilityMap[K];
  }

  providerOf(id: KernelCapabilityId): string | undefined {
    return this.providers.get(id);
  }
}

class ReadonlyKernelCapabilities implements KernelCapabilities {
  constructor(protected readonly store: KernelCapabilityStore) {}

  has(id: KernelCapabilityId): boolean {
    return this.store.has(id);
  }

  require<K extends KernelCapabilityId>(id: K): KernelCapabilityMap[K] {
    return this.store.require(id);
  }

  providerOf(id: KernelCapabilityId): string | undefined {
    return this.store.providerOf(id);
  }
}

class ScopedKernelPluginContext extends ReadonlyKernelCapabilities implements KernelPluginContext {
  constructor(
    store: KernelCapabilityStore,
    private readonly providerId: string,
  ) {
    super(store);
  }

  provide<K extends KernelCapabilityId>(id: K, capability: KernelCapabilityMap[K]): void {
    this.store.provide(this.providerId, id, capability);
  }
}

const ENGINE_CAPABILITIES = [
  'content',
  'abilities',
  'space',
  'actionHandlers',
  'combatModifiers',
  'hitEffects',
  'statusBehaviors',
  'scenarioConditions',
  'scenarioEffects',
  'objectives',
  'progression',
  'resources',
  'turnOrders',
  'reactions',
  'unitDepartures',
  'random',
  'aiObjectiveAdvisors',
  'abilityAiEvaluators',
  'aiIntents',
] as const satisfies readonly KernelCapabilityId[];

/** Tiny plugin host: dependency graph, deterministic install order, capability wiring. */
export class SrpgMicrokernel {
  private readonly plugins = new Map<string, EnginePlugin>();

  use(plugin: EnginePlugin): this {
    if (!plugin.id.trim()) throw new Error('engine plugin id cannot be empty');
    if (!Number.isInteger(plugin.version) || plugin.version < 1) {
      throw new Error(`engine plugin "${plugin.id}" version must be a positive integer`);
    }
    const installed = this.plugins.get(plugin.id);
    if (installed) {
      if (installed.version === plugin.version) return this;
      throw new Error(
        `engine plugin "${plugin.id}" already registered at version ${installed.version}, requested ${plugin.version}`,
      );
    }
    this.plugins.set(plugin.id, plugin);
    return this;
  }

  useAll(plugins: readonly EnginePlugin[]): this {
    for (const plugin of plugins) this.use(plugin);
    return this;
  }

  pluginManifest(): ReadonlyMap<string, number> {
    return new Map([...this.plugins.values()].map((plugin) => [plugin.id, plugin.version]));
  }

  compose(): KernelCapabilities {
    const store = new KernelCapabilityStore();
    for (const plugin of this.orderedPlugins()) {
      plugin.install(new ScopedKernelPluginContext(store, plugin.id));
      for (const capability of plugin.provides ?? []) {
        if (store.providerOf(capability) !== plugin.id) {
          throw new Error(`engine plugin "${plugin.id}" declared but did not provide capability "${capability}"`);
        }
      }
    }
    return new ReadonlyKernelCapabilities(store);
  }

  buildBattleEngine(): BattleEngine {
    const context = this.compose();
    for (const capability of ENGINE_CAPABILITIES) context.require(capability);
    return new BattleEngine({
      content: context.require('content'),
      abilities: context.require('abilities'),
      space: context.require('space'),
      actionHandlers: context.require('actionHandlers'),
      combatModifiers: context.require('combatModifiers'),
      hitEffects: context.require('hitEffects'),
      statusBehaviors: context.require('statusBehaviors'),
      scenarioConditions: context.require('scenarioConditions'),
      scenarioEffects: context.require('scenarioEffects'),
      objectives: context.require('objectives'),
      progression: context.require('progression'),
      resources: context.require('resources'),
      turnOrders: context.require('turnOrders'),
      reactions: context.require('reactions'),
      unitDepartures: context.require('unitDepartures'),
      random: context.require('random'),
      aiObjectiveAdvisors: context.require('aiObjectiveAdvisors'),
      abilityAiEvaluators: context.require('abilityAiEvaluators'),
      aiIntents: context.require('aiIntents'),
    });
  }

  private orderedPlugins(): EnginePlugin[] {
    const plugins = [...this.plugins.values()];
    const capabilityProviders = new Map<KernelCapabilityId, string>();
    for (const plugin of plugins) {
      for (const capability of plugin.provides ?? []) {
        const provider = capabilityProviders.get(capability);
        if (provider && provider !== plugin.id) {
          throw new Error(`kernel capability "${capability}" declared by both "${provider}" and "${plugin.id}"`);
        }
        capabilityProviders.set(capability, plugin.id);
      }
    }
    return orderByDependencies(plugins, {
      idOf: (plugin) => plugin.id,
      dependenciesOf: (plugin) => [
        ...(plugin.requires ?? []),
        ...(plugin.requiresCapabilities ?? []).map((capability) => {
          const provider = capabilityProviders.get(capability);
          if (!provider) {
            throw new Error(`engine plugin "${plugin.id}" requires missing capability "${capability}"`);
          }
          return provider;
        }),
      ].filter((dependency) => dependency !== plugin.id),
      missing: (plugin, dependency) =>
        new Error(`engine plugin "${plugin.id}" requires "${dependency}"`),
      cycle: (path) => new Error(`cyclic engine plugin dependency: ${path.join(' -> ')}`),
    });
  }
}
