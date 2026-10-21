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

    public IReadOnlyDictionary<string, double> PluginManifest
    {
        get;
    }
    public bool Has(CapabilityKey key) => Store.Has(key);
    public virtual T Require<T>(CapabilityKey<T> key) => Store.Require(key);
    public string? ProviderOf(CapabilityKey key) => Store.ProviderOf(key);
}

/// <summary>One plugin installation scope; consumption and writes end with its callback.</summary>
public sealed class KernelPluginContext : KernelCapabilities
{
    private readonly string providerId;
    private readonly List<string> consumed = [];
    private bool closed;

    internal KernelPluginContext(KernelCapabilityStore store, string providerId) : base(store)
    {
        this.providerId = providerId;
    }

    internal IReadOnlyList<string> Consumed => consumed;
    internal void Close() => closed = true;

    public override T Require<T>(CapabilityKey<T> key)
    {
        RequireOpen();
        if (!consumed.Contains(key.Id, StringComparer.Ordinal))
            consumed.Add(key.Id);
        return base.Require(key);
    }

    public void Provide<T>(CapabilityKey<T> key, T capability)
    {
        RequireOpen();
        Store.Provide(providerId, key, capability);
    }

    public void Replace<T>(CapabilityKey<T> key, T capability)
    {
        RequireOpen();
        Store.Replace(providerId, key, capability);
    }

    private void RequireOpen()
    {
        if (closed)
            throw new DomainInvariantException($"engine plugin \"{providerId}\" installation context is closed");
    }
}
