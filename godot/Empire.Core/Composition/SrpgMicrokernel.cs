using System.Collections.ObjectModel;
using Empire.Core.Determinism;

namespace Empire.Core.Composition;

/// <summary>Rule-independent composition with source-ordered dependency and capability validation.</summary>
public sealed class SrpgMicrokernel
{
    private readonly Dictionary<string, EnginePlugin> plugins = new(StringComparer.Ordinal);

    public SrpgMicrokernel Use(EnginePlugin plugin)
    {
        if (EcmaText.IsBlank(plugin.Id)) throw new DomainInvariantException("engine plugin id cannot be empty");
        if (!double.IsFinite(plugin.Version) || Math.Truncate(plugin.Version) != plugin.Version || plugin.Version < 1)
            throw new DomainInvariantException($"engine plugin \"{plugin.Id}\" version must be a positive integer");
        if (plugins.TryGetValue(plugin.Id, out var installed))
            throw new DomainInvariantException(
                $"engine plugin \"{plugin.Id}\" already registered at version {EcmaNumber.Format(installed.Version)}; " +
                $"a second declaration at version {EcmaNumber.Format(plugin.Version)} is ambiguous");
        plugins.Add(plugin.Id, plugin);
        return this;
    }

    public SrpgMicrokernel UseAll(IEnumerable<EnginePlugin> additions)
    {
        foreach (var plugin in additions) Use(plugin);
        return this;
    }

    public IReadOnlyDictionary<string, double> PluginManifest => new ReadOnlyDictionary<string, double>(
        plugins.ToDictionary(pair => pair.Key, pair => pair.Value.Version, StringComparer.Ordinal));

    public KernelCapabilities Compose()
    {
        var store = new KernelCapabilityStore();
        var ordered = OrderedPlugins(store);
        foreach (var plugin in ordered)
        {
            var context = new KernelPluginContext(store, plugin.Id);
            plugin.Install(context);
            AssertManifest(plugin, "provide", plugin.Provides, store.ProvidedBy(plugin.Id));
            AssertManifest(plugin, "override", plugin.Overrides, store.OverriddenBy(plugin.Id));
            AssertConsumption(plugin, context.Consumed);
        }
        return new KernelCapabilities(store, PluginManifest);
    }

    private static void AssertConsumption(EnginePlugin plugin, IReadOnlyList<string> consumed)
    {
        var declared = plugin.RequiresCapabilities.Select(key => key.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var capability in consumed)
            if (!declared.Contains(capability))
                throw new DomainInvariantException($"engine plugin \"{plugin.Id}\" required undeclared capability \"{capability}\"");
    }

    private static void AssertManifest(EnginePlugin plugin, string verb,
        IReadOnlyList<CapabilityKey> declared, IReadOnlyList<string> installed)
    {
        var done = installed.ToHashSet(StringComparer.Ordinal);
        foreach (var capability in declared)
            if (!done.Contains(capability.Id))
                throw new DomainInvariantException($"engine plugin \"{plugin.Id}\" declared but did not {verb} capability \"{capability.Id}\"");
        var promised = declared.Select(key => key.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var capability in installed)
            if (!promised.Contains(capability))
                throw new DomainInvariantException($"engine plugin \"{plugin.Id}\" did {verb} undeclared capability \"{capability}\"");
    }

    private IReadOnlyList<EnginePlugin> OrderedPlugins(KernelCapabilityStore store)
    {
        var batch = plugins.Values.ToArray();
        var introducers = new Dictionary<string, string>(StringComparer.Ordinal);
        var overriders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var plugin in batch)
        {
            foreach (var key in plugin.Provides.Concat(plugin.Overrides).Concat(plugin.RequiresCapabilities)) store.CheckKey(key);
            foreach (var capability in plugin.Provides)
            {
                if (introducers.TryGetValue(capability.Id, out var provider) && provider != plugin.Id)
                    throw new DomainInvariantException($"kernel capability \"{capability.Id}\" declared by both \"{provider}\" and \"{plugin.Id}\"");
                introducers[capability.Id] = plugin.Id;
            }
            foreach (var capability in plugin.Overrides)
            {
                if (!overriders.TryGetValue(capability.Id, out var claimants))
                    overriders.Add(capability.Id, claimants = []);
                claimants.Add(plugin.Id);
            }
        }
        foreach (var (capability, claimants) in overriders)
            if (claimants.Count > 1)
                throw new DomainInvariantException($"kernel capability \"{capability}\" overridden by multiple plugins: " +
                    string.Join(", ", claimants.Select(id => $"\"{id}\"")));

        string IntroducerOf(EnginePlugin plugin, CapabilityKey capability)
        {
            if (!introducers.TryGetValue(capability.Id, out var provider))
                throw new DomainInvariantException($"engine plugin \"{plugin.Id}\" requires missing capability \"{capability.Id}\"");
            return provider;
        }

        return DependencyOrder.Order(batch, new DependencyOrderOptions<EnginePlugin>
        {
            IdOf = plugin => plugin.Id,
            DependenciesOf = plugin => plugin.Requires
                .Concat(plugin.Overrides.Select(key => IntroducerOf(plugin, key)))
                .Concat(plugin.RequiresCapabilities.SelectMany(key =>
                    new[] { IntroducerOf(plugin, key) }.Concat(overriders.GetValueOrDefault(key.Id) ?? [])))
                .Where(dependency => dependency != plugin.Id).ToArray(),
            Duplicate = id => new DomainInvariantException($"duplicate engine plugin dependency node: \"{id}\""),
            Missing = (plugin, dependency) => new DomainInvariantException($"engine plugin \"{plugin.Id}\" requires \"{dependency}\""),
            Cycle = path => new DomainInvariantException($"cyclic engine plugin dependency: {string.Join(" -> ", path)}"),
        });
    }

}
