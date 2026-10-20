using System.Text.Json.Nodes;

namespace Empire.Core.Persistence;

/// <summary>Runtime capabilities declared by the composition root, not guessed from manifest names.</summary>
public sealed class BattleSaveFeatures
{
    private readonly Dictionary<string, HashSet<string>> known = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<JsonObject, IEnumerable<string>>> guards = new(StringComparer.Ordinal);

    public BattleSaveFeatures Register(string family, IEnumerable<string> ids)
    {
        if (!known.TryAdd(family, new HashSet<string>(ids, StringComparer.Ordinal)))
            throw new DomainInvariantException($"save feature family already registered: \"{family}\"");
        return this;
    }

    public BattleSaveFeatures Guard(string id, Func<JsonObject, IEnumerable<string>> inspect)
    {
        if (!guards.TryAdd(id, inspect)) throw new DomainInvariantException($"save feature guard already registered: \"{id}\"");
        return this;
    }

    public bool Has(string family, string id) => known.TryGetValue(family, out var ids) && ids.Contains(id);

    internal BattleSaveFeatures Snapshot()
    {
        var result = new BattleSaveFeatures();
        foreach (var pair in known) result.Register(pair.Key, pair.Value);
        foreach (var pair in guards) result.Guard(pair.Key, pair.Value);
        return result;
    }

    internal IEnumerable<string> StateIssues(JsonObject state)
    {
        foreach (var guard in guards)
            foreach (var issue in guard.Value(state)) yield return $"{guard.Key}: {issue}";
    }
}
