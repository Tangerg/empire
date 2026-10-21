using System.Text.Json.Nodes;

namespace Empire.Core.Persistence;

/// <summary>Structural stored-state guards supplied by their aggregate owners.</summary>
public sealed class BattleSaveFeatures
{
    private readonly Dictionary<string, Func<JsonObject, IEnumerable<string>>> guards = new(StringComparer.Ordinal);

    public BattleSaveFeatures Guard(string id, Func<JsonObject, IEnumerable<string>> inspect)
    {
        if (!guards.TryAdd(id, inspect))
            throw new DomainInvariantException($"save feature guard already registered: \"{id}\"");
        return this;
    }

    internal IEnumerable<string> StateIssues(JsonObject state)
    {
        foreach (var guard in guards)
            foreach (var issue in guard.Value(state))
                yield return $"{guard.Key}: {issue}";
    }
}
