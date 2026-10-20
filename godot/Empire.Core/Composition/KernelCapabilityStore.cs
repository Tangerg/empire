namespace Empire.Core.Composition;

internal sealed class KernelCapabilityStore
{
    private readonly Dictionary<string, Type> types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> capabilities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> providers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> overriders = new(StringComparer.Ordinal);

    internal void CheckKey(CapabilityKey key)
    {
        if (types.TryGetValue(key.Id, out var type) && type != key.ValueType)
            throw new DomainInvariantException(
                $"kernel capability \"{key.Id}\" has type \"{type.FullName}\", not \"{key.ValueType.FullName}\"");
        types.TryAdd(key.Id, key.ValueType);
    }

    internal void Provide<T>(string providerId, CapabilityKey<T> key, T capability)
    {
        CheckKey(key);
        if (providers.TryGetValue(key.Id, out var provider))
            throw new DomainInvariantException($"kernel capability \"{key.Id}\" already provided by \"{provider}\"");
        capabilities.Add(key.Id, capability);
        providers.Add(key.Id, providerId);
    }

    internal void Replace<T>(string providerId, CapabilityKey<T> key, T capability)
    {
        CheckKey(key);
        if (!providers.ContainsKey(key.Id))
            throw new DomainInvariantException($"kernel capability \"{key.Id}\" cannot be replaced before it is provided");
        capabilities[key.Id] = capability;
        overriders[key.Id] = providerId;
    }

    internal bool Has(CapabilityKey key)
    {
        CheckKey(key);
        return capabilities.ContainsKey(key.Id);
    }

    internal T Require<T>(CapabilityKey<T> key)
    {
        CheckKey(key);
        if (!capabilities.TryGetValue(key.Id, out var value))
            throw new DomainInvariantException($"missing kernel capability: \"{key.Id}\"");
        return (T)value!;
    }

    internal string? ProviderOf(CapabilityKey key)
    {
        CheckKey(key);
        return providers.GetValueOrDefault(key.Id);
    }

    internal IReadOnlyList<string> ProvidedBy(string providerId) => AttributedTo(providers, providerId);
    internal IReadOnlyList<string> OverriddenBy(string providerId) => AttributedTo(overriders, providerId);

    private static IReadOnlyList<string> AttributedTo(Dictionary<string, string> attribution, string providerId) =>
        attribution.Where(pair => pair.Value == providerId).Select(pair => pair.Key).ToArray();
}
