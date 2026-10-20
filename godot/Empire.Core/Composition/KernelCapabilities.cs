using System.Collections.ObjectModel;

namespace Empire.Core.Composition;

public class KernelCapabilities
{
    private protected readonly KernelCapabilityStore Store;

    internal KernelCapabilities(KernelCapabilityStore store, IReadOnlyDictionary<string, double>? pluginManifest = null)
    {
        Store = store;
        PluginManifest = new ReadOnlyDictionary<string, double>(pluginManifest is null
            ? new Dictionary<string, double>(StringComparer.Ordinal)
            : new Dictionary<string, double>(pluginManifest, StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, double> PluginManifest { get; }
    public bool Has(CapabilityKey key) => Store.Has(key);
    public virtual T Require<T>(CapabilityKey<T> key) => Store.Require(key);
    public string? ProviderOf(CapabilityKey key) => Store.ProviderOf(key);
}

/// <summary>Attributes installation to one plugin. As in the source, a retained context stays live.</summary>
public sealed class KernelPluginContext : KernelCapabilities
{
    private readonly string providerId;
    private readonly List<string> consumed = [];

    internal KernelPluginContext(KernelCapabilityStore store, string providerId) : base(store)
    {
        this.providerId = providerId;
    }

    internal IReadOnlyList<string> Consumed => consumed;

    public override T Require<T>(CapabilityKey<T> key)
    {
        if (!consumed.Contains(key.Id, StringComparer.Ordinal)) consumed.Add(key.Id);
        return base.Require(key);
    }

    public void Provide<T>(CapabilityKey<T> key, T capability) => Store.Provide(providerId, key, capability);
    public void Replace<T>(CapabilityKey<T> key, T capability) => Store.Replace(providerId, key, capability);
}
