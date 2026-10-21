using System.Text.Json.Nodes;

namespace Empire.Checks;

/// <summary>
/// Declares the intentional native assembly identity change for source-derived
/// test documents. Gameplay state, events and digests are never rewritten.
/// This file is test-only and must not be compiled into the game.
/// </summary>
internal static class FixtureRuleset
{
    private static readonly string[] SourcePlugins =
    [
        "engine.ai-planning",
        "engine.content",
        "engine.mission-rules",
        "engine.resource-economy",
        "engine.tactical-rules",
    ];

    internal static JsonObject ExpectedNative(JsonObject sourceManifest)
    {
        if (sourceManifest["plugins"] is not JsonObject sourcePlugins)
            throw new InvalidOperationException("The source fixture has no plugin identity.");

        foreach (var name in SourcePlugins)
        {
            if (sourcePlugins[name]?.GetValue<double>() != 1)
                throw new InvalidOperationException($"Unexpected source fixture plugin identity: {name}.");
        }

        if (sourcePlugins.ContainsKey("engine.tactical-space"))
            throw new InvalidOperationException("The source fixture already declares the native-only space plugin.");

        var native = (JsonObject)sourceManifest.DeepClone();
        var plugins = native["plugins"]!.AsObject();
        plugins["engine.tactical-rules"] = 2;
        plugins["engine.tactical-space"] = 1;
        native["plugins"] = new JsonObject(plugins.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value?.DeepClone())));
        return native;
    }

    internal static JsonObject NativeDocument(JsonObject sourceDocument)
    {
        if (sourceDocument["ruleset"] is not JsonObject sourceManifest)
            throw new InvalidOperationException("The source fixture document has no ruleset identity.");

        var native = (JsonObject)sourceDocument.DeepClone();
        native["ruleset"] = ExpectedNative(sourceManifest);
        return native;
    }
}
