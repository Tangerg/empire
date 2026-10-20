using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Persistence;

public static class RulesetManifest
{
    public static JsonObject Create(JsonArray packs, IReadOnlyDictionary<string, int> plugins)
    {
        _ = new StateContent(packs);
        var pluginVersions = new JsonObject();
        foreach (var pair in plugins.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1)
                throw new DomainInvariantException("plugin identity requires a nonempty id and positive integer version");
            pluginVersions[pair.Key] = pair.Value;
        }
        var packVersions = new JsonObject();
        foreach (var pack in StateJson.Objects(packs, "content packs").OrderBy(pack => StateJson.Text(pack, "id"), StringComparer.Ordinal))
            packVersions[StateJson.Text(pack, "id")] = StateJson.Required(pack, "version").DeepClone();
        return new JsonObject { ["plugins"] = pluginVersions, ["contentPacks"] = packVersions };
    }

    public static void RequirePersistent(JsonObject manifest)
    {
        var packs = StateJson.Object(manifest["contentPacks"], "contentPacks");
        var plugins = StateJson.Object(manifest["plugins"], "plugins");
        if (packs.Count == 0) throw new DomainInvariantException(
            "ruleset has no versioned content pack; install authored content through ContentPackInstaller before persisting");
        var anonymous = plugins.Select(pair => pair.Key).FirstOrDefault(id => id.StartsWith("engine.override.", StringComparison.Ordinal));
        if (anonymous is not null) throw new DomainInvariantException(
            $"ruleset contains unversioned override \"{anonymous}\"; persist it through a named, versioned plugin");
    }

    public static IReadOnlyList<string> Differences(JsonObject expected, JsonObject actual)
    {
        var result = new List<string>();
        void Compare(string family, string key)
        {
            var current = StateJson.Object(expected[key], key);
            var saved = StateJson.Object(actual[key], key);
            foreach (var id in current.Select(pair => pair.Key).Concat(saved.Select(pair => pair.Key))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!current.ContainsKey(id)) result.Add($"{family} \"{id}\" is not part of the current ruleset");
                else if (!saved.ContainsKey(id)) result.Add($"{family} \"{id}\" is missing");
                else if (StateJson.Number(current[id], id) != StateJson.Number(saved[id], id))
                    result.Add($"{family} \"{id}\" version is {saved[id]!.ToJsonString()}, expected {current[id]!.ToJsonString()}");
            }
        }
        Compare("plugin", "plugins");
        Compare("content pack", "contentPacks");
        return result;
    }

    internal static void ValidateStored(JsonNode? value)
    {
        if (value is not JsonObject manifest || manifest["plugins"] is not JsonObject plugins ||
            manifest["contentPacks"] is not JsonObject packs)
            StoredSchema.Reject("规则集版本信息缺失或损坏");
        else foreach (var pair in plugins.Concat(packs))
            if (string.IsNullOrWhiteSpace(pair.Key) || !StoredSchema.Matches(pair.Value, StoredKind.Integer) ||
                StateJson.Number(pair.Value, "version") < 1)
                StoredSchema.Reject($"规则集版本「{pair.Key}」损坏");
    }
}
