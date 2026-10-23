using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Empire.Presentation;

public sealed record PortraitAsset(string ResourcePath, string Sha256)
{
    [JsonIgnore]
    public string Source => "/godot/" + ResourcePath[6..];
}

/// <summary>The only native portrait resource declarations. Consumers own IDs, never copies of asset facts.</summary>
public sealed class PortraitCatalog
{
    public IReadOnlyDictionary<string, PortraitAsset> Portraits
    {
        get;
    }

    public PortraitCatalog(IEnumerable<KeyValuePair<string, PortraitAsset>> portraits)
    {
        var entries = new Dictionary<string, PortraitAsset>(StringComparer.Ordinal);
        var resources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, asset) in portraits)
        {
            if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[a-z][a-z0-9-]*$") || asset is null
                || string.IsNullOrWhiteSpace(asset.ResourcePath)
                || !Regex.IsMatch(asset.ResourcePath, @"^res://assets/runtime/narrative-static/char-[a-z0-9-]+\.png$")
                || string.IsNullOrWhiteSpace(asset.Sha256) || !Regex.IsMatch(asset.Sha256, "^[a-f0-9]{64}$")
                || !resources.Add(asset.ResourcePath))
                throw new InvalidOperationException($"Invalid native portrait declaration: {id}.");
            if (!entries.TryAdd(id, asset))
                throw new InvalidOperationException($"Duplicate native portrait ID: {id}.");
        }
        if (entries.Count == 0)
            throw new InvalidOperationException("Native portrait catalog is empty.");
        Portraits = new ReadOnlyDictionary<string, PortraitAsset>(entries);
    }

    public NarrativeArt Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Portraits.TryGetValue(id, out var asset))
            throw new InvalidOperationException($"Unknown portrait ID: {id}.");
        return new NarrativeArt { Source = asset.Source, ResourcePath = asset.ResourcePath, Sha256 = asset.Sha256 };
    }

    public static PortraitCatalog Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.EnumerateObject().Select(value => value.Name).Order().SequenceEqual(new[] { "portraits", "schema" }) == false
            || root.GetProperty("schema").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported portrait catalog schema.");
        var entries = new List<KeyValuePair<string, PortraitAsset>>();
        foreach (var entry in root.GetProperty("portraits").EnumerateObject())
        {
            if (!entry.Value.EnumerateObject().Select(value => value.Name).Order().SequenceEqual(new[] { "resourcePath", "sha256" }))
                throw new InvalidOperationException($"Unexpected portrait asset fields: {entry.Name}.");
            entries.Add(new(entry.Name, new(entry.Value.GetProperty("resourcePath").GetString()!, entry.Value.GetProperty("sha256").GetString()!)));
        }
        return new PortraitCatalog(entries);
    }
}
