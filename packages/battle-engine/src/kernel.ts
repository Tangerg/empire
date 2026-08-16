import { DomainInvariantError } from './domain/errors';
import { orderByDependencies } from './dependency-order';
import type { BattleEngineDependencies } from './engine';

/**
 * What a composed ruleset can be asked for.
 *
 * The engine's own dependencies *are* the base capability set, so they are
 * stated once, where their documentation already lives. This used to restate
 * all twenty-one names as `BattleRuleServices['x']` — a second list whose only
 * job was to agree with the first.
 *
 * Still open: a third-party plugin may declaration-merge capabilities its own
 * plugins consume, which the engine itself never asks for.
 */
export interface KernelCapabilityMap extends BattleEngineDependencies {}

export type KernelCapabilityId = Extract<keyof KernelCapabilityMap, string>;

export interface EnginePlugin {
  readonly id: string;
  readonly version: number;
  /** Capabilities this plugin introduces. Two plugins cannot introduce the same one. */
  readonly provides?: readonly KernelCapabilityId[];
  /**
   * Capabilities this plugin *replaces* rather than introduces.
   *
   * Substitution was advertised and impossible: two plugins naming the same
   * capability in `provides` was an error, and the store refused a second
   * `provide`, so the only way to swap a rule was to bypass the kernel entirely
   * — which is exactly what the engine factory did. An overriding plugin is
   * ordered after the capability's introducer and before everyone who consumes
   * it, so a consumer never captures the value that was replaced.
   */
  readonly overrides?: readonly KernelCapabilityId[];
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
  /**
   * What composed this, as plugin id → version.
   *
   * Carried by the composition rather than left on the kernel that ran it, so
   * anything holding the result can say what it is made of without keeping the
   * builder alive beside it. That was the whole reason the demo held a kernel:
   * it wanted the manifest, and the only way to have one was to run composition
   * itself — which is how a second way to build an engine appeared.
   */
  readonly pluginManifest: ReadonlyMap<string, number>;
}

/** Capability-segregated port handed to one plugin during installation. */
export interface KernelPluginContext extends KernelCapabilities {
  /** Introduces a capability nobody has provided yet. */
  provide<K extends KernelCapabilityId>(id: K, capability: KernelCapabilityMap[K]): void;
  /** Replaces one somebody already provided. Declare it in `overrides`. */
  replace<K extends KernelCapabilityId>(id: K, capability: KernelCapabilityMap[K]): void;
}

class KernelCapabilityStore {
  private readonly capabilities = new Map<string, unknown>();
  private readonly providers = new Map<string, string>();
  private readonly overriders = new Map<string, string>();

  provide<K extends KernelCapabilityId>(
    providerId: string,
    id: K,
    capability: KernelCapabilityMap[K],
  ): void {
    const provider = this.providers.get(id);
    if (provider) {
      throw new DomainInvariantError(`kernel capability "${id}" already provided by "${provider}"`);
    }
    this.capabilities.set(id, capability);
    this.providers.set(id, providerId);
  }

  replace<K extends KernelCapabilityId>(
    providerId: string,
    id: K,
    capability: KernelCapabilityMap[K],
  ): void {
    if (!this.providers.has(id)) {
      throw new DomainInvariantError(`kernel capability "${id}" cannot be replaced before it is provided`);
    }
    this.capabilities.set(id, capability);
    this.overriders.set(id, providerId);
  }

  has(id: KernelCapabilityId): boolean {
    return this.capabilities.has(id);
  }

  require<K extends KernelCapabilityId>(id: K): KernelCapabilityMap[K] {
    if (!this.capabilities.has(id)) throw new DomainInvariantError(`missing kernel capability: "${id}"`);
    return this.capabilities.get(id) as KernelCapabilityMap[K];
  }

  providerOf(id: KernelCapabilityId): string | undefined {
    return this.providers.get(id);
  }

  providedBy(providerId: string): KernelCapabilityId[] {
    return this.attributedTo(this.providers, providerId);
  }

  overriddenBy(providerId: string): KernelCapabilityId[] {
    return this.attributedTo(this.overriders, providerId);
  }

  private attributedTo(attribution: ReadonlyMap<string, string>, providerId: string): KernelCapabilityId[] {
    return [...attribution]
      .filter(([, provider]) => provider === providerId)
      .map(([capability]) => capability as KernelCapabilityId);
  }
}

class ReadonlyKernelCapabilities implements KernelCapabilities {
  constructor(
    protected readonly store: KernelCapabilityStore,
    readonly pluginManifest: ReadonlyMap<string, number> = new Map(),
  ) {}

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
  /** Capabilities this plugin actually took a value out of, in call order. */
  readonly consumed = new Set<KernelCapabilityId>();

  constructor(
    store: KernelCapabilityStore,
    private readonly providerId: string,
  ) {
    super(store);
  }

  /**
   * Recorded, because taking the value is what creates the ordering obligation.
   *
   * `has` and `providerOf` are deliberately not: asking whether a capability
   * exists captures nothing, so a plugin that probes for an optional one has no
   * dependency on whoever might replace it.
   */
  override require<K extends KernelCapabilityId>(id: K): KernelCapabilityMap[K] {
    this.consumed.add(id);
    return super.require(id);
  }

  provide<K extends KernelCapabilityId>(id: K, capability: KernelCapabilityMap[K]): void {
    this.store.provide(this.providerId, id, capability);
  }

  replace<K extends KernelCapabilityId>(id: K, capability: KernelCapabilityMap[K]): void {
    this.store.replace(this.providerId, id, capability);
  }
}

/** Tiny plugin host: dependency graph, deterministic install order, capability wiring. */
export class SrpgMicrokernel {
  private readonly plugins = new Map<string, EnginePlugin>();

  use(plugin: EnginePlugin): this {
    if (!plugin.id.trim()) throw new DomainInvariantError('engine plugin id cannot be empty');
    if (!Number.isInteger(plugin.version) || plugin.version < 1) {
      throw new DomainInvariantError(`engine plugin "${plugin.id}" version must be a positive integer`);
    }
    const installed = this.plugins.get(plugin.id);
    if (installed) {
      if (installed.version === plugin.version) return this;
      throw new DomainInvariantError(
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

  get pluginManifest(): ReadonlyMap<string, number> {
    return new Map([...this.plugins.values()].map((plugin) => [plugin.id, plugin.version]));
  }

  /**
   * The manifest must match what installation actually did, in both directions.
   *
   * Only the first half was checked, and the second half had already drifted:
   * a capability provided but not declared has no provider as far as ordering
   * is concerned, so any plugin depending on it is told the capability is
   * missing — while the composed engine holds it the whole time.
   */
  compose(): KernelCapabilities {
    const store = new KernelCapabilityStore();
    for (const plugin of this.orderedPlugins()) {
      const context = new ScopedKernelPluginContext(store, plugin.id);
      plugin.install(context);
      this.assertManifest(plugin, 'provide', plugin.provides, store.providedBy(plugin.id));
      this.assertManifest(plugin, 'override', plugin.overrides, store.overriddenBy(plugin.id));
      this.assertConsumption(plugin, context.consumed);
    }
    return new ReadonlyKernelCapabilities(store, this.pluginManifest);
  }

  /**
   * A plugin may only take a capability it said it consumes.
   *
   * The third direction the manifest has to agree in, and the one that was
   * missing. `provides` and `overrides` were checked both ways; consumption was
   * not checked at all — so a plugin could `require('space')` without declaring
   * it, and ordering would place it *before* a plugin that replaces `space`.
   * It would then hold the value that was replaced for the life of the engine,
   * which is the exact failure `overrides` exists to prevent: "substitution that
   * only works when nobody captured the value at install time is not
   * substitution."
   *
   * Only this direction is enforced. Declaring a capability and not reading it
   * on this particular composition is a legitimate ordering statement — a
   * plugin may consume it down one content branch and not another — and failing
   * a build over an untaken branch would be a worse rule than the one it guards.
   */
  private assertConsumption(plugin: EnginePlugin, consumed: ReadonlySet<KernelCapabilityId>): void {
    const declared = new Set(plugin.requiresCapabilities ?? []);
    for (const capability of consumed) {
      if (!declared.has(capability)) {
        throw new DomainInvariantError(
          `engine plugin "${plugin.id}" required undeclared capability "${capability}"`,
        );
      }
    }
  }

  private assertManifest(
    plugin: EnginePlugin,
    verb: 'provide' | 'override',
    declared: readonly KernelCapabilityId[] = [],
    installed: readonly KernelCapabilityId[] = [],
  ): void {
    const done = new Set(installed);
    for (const capability of declared) {
      if (!done.has(capability)) {
        throw new DomainInvariantError(`engine plugin "${plugin.id}" declared but did not ${verb} capability "${capability}"`);
      }
    }
    const promised = new Set(declared);
    for (const capability of installed) {
      if (!promised.has(capability)) {
        throw new DomainInvariantError(`engine plugin "${plugin.id}" did ${verb} undeclared capability "${capability}"`);
      }
    }
  }

  /**
   * Install order, derived entirely from the manifests.
   *
   * Three kinds of edge, and the third is what makes substitution safe: an
   * overriding plugin runs after whoever introduced the capability, and every
   * consumer of that capability runs after the override — otherwise a consumer
   * that reads a capability at install time keeps the value that was replaced.
   */
  private orderedPlugins(): EnginePlugin[] {
    const plugins = [...this.plugins.values()];
    const introducers = new Map<KernelCapabilityId, string>();
    const overriders = new Map<KernelCapabilityId, string[]>();
    for (const plugin of plugins) {
      for (const capability of plugin.provides ?? []) {
        const provider = introducers.get(capability);
        if (provider && provider !== plugin.id) {
          throw new DomainInvariantError(`kernel capability "${capability}" declared by both "${provider}" and "${plugin.id}"`);
        }
        introducers.set(capability, plugin.id);
      }
      for (const capability of plugin.overrides ?? []) {
        overriders.set(capability, [...(overriders.get(capability) ?? []), plugin.id]);
      }
    }
    const introducerOf = (plugin: EnginePlugin, capability: KernelCapabilityId): string => {
      const provider = introducers.get(capability);
      if (!provider) {
        throw new DomainInvariantError(`engine plugin "${plugin.id}" requires missing capability "${capability}"`);
      }
      return provider;
    };
    return orderByDependencies(plugins, {
      idOf: (plugin) => plugin.id,
      dependenciesOf: (plugin) => [
        ...(plugin.requires ?? []),
        // An override waits for the capability to exist.
        ...(plugin.overrides ?? []).map((capability) => introducerOf(plugin, capability)),
        // A consumer waits for the capability *and* for anyone replacing it.
        ...(plugin.requiresCapabilities ?? []).flatMap((capability) => [
          introducerOf(plugin, capability),
          ...(overriders.get(capability) ?? []),
        ]),
      ].filter((dependency) => dependency !== plugin.id),
      missing: (plugin, dependency) =>
        new Error(`engine plugin "${plugin.id}" requires "${dependency}"`),
      cycle: (path) => new Error(`cyclic engine plugin dependency: ${path.join(' -> ')}`),
    });
  }
}
