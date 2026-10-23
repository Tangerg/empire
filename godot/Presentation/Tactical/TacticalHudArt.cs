#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation.Tactical;

public sealed class TacticalHudArtDocument
{
    public int Schema
    {
        get; init;
    }
    public Dictionary<string, TacticalPortraitBinding> Portraits { get; init; } = new(StringComparer.Ordinal);
    public string[] SourceIconKeys { get; init; } = Array.Empty<string>();
    public Dictionary<string, NarrativeArt> Icons { get; init; } = new(StringComparer.Ordinal);
}
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class TacticalPortraitBinding
{
    public string UnitType { get; init; } = "";
    public string? ActorKey
    {
        get; init;
    }
    [System.Text.Json.Serialization.JsonRequired]
    public string? PortraitId
    {
        get; init;
    }
}

/// <summary>Explicit portrait ID or null for the unit idle frame; unknown IDs never fall back.</summary>
public sealed class TacticalHudArt
{
    private readonly TacticalHudArtDocument document;
    private readonly AssetCatalog assets;
    private readonly PortraitCatalog portraits;
    public TacticalHudArt(AssetCatalog assets, PortraitCatalog portraits, TacticalHudArtDocument document)
    {
        if (document.Schema != 2)
            throw new InvalidOperationException("Unknown tactical HUD art schema.");
        foreach (var binding in document.Portraits.Values)
            if (binding.PortraitId is { } id)
                portraits.Resolve(id);
        this.portraits = portraits;
        this.document = document;
        this.assets = assets;
    }
    public static TacticalHudArt Load(AssetCatalog assets, PortraitCatalog portraits) => new(assets, portraits, PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json"));
    private static Texture2D Image(NarrativeArt image) => image.ResourcePath is { } path && ResourceLoader.Exists(path, "Texture2D")
        ? ResourceLoader.Load<Texture2D>(path) : throw new MissingArtResourceException(image.Source, "Missing source HUD art: " + image.Source);
    public Texture2D? Icon(string key)
    {
        if (document.Icons.TryGetValue(key, out var image))
            return Image(image);
        if (Array.IndexOf(document.SourceIconKeys, key) >= 0)
            throw new MissingArtResourceException(key, "Missing declared source HUD icon: " + key);
        return null; // The original provider does not contribute an icon for this key.
    }
    public Texture2D UnitIcon(TacticalHudPortrait unit)
    {
        var asset = assets.ResolveUnit(unit.UnitType, unit.ActorKey);
        return assets.CreateFrameTexture(asset, AssetCatalog.FrameIndex(asset, "idle"));
    }
    public Texture2D Portrait(TacticalHudPortrait portrait)
    {
        var key = portrait.ActorKey is { } actor && document.Portraits.ContainsKey(actor) ? actor : portrait.UnitType;
        if (!document.Portraits.TryGetValue(key, out var binding))
            throw new MissingArtResourceException(key, "Missing source HUD portrait binding: " + key);
        if (binding.UnitType != portrait.UnitType)
            throw new InvalidOperationException("HUD actor portrait type mismatch: " + key);
        if (binding.PortraitId is { } portraitId)
            return Image(portraits.Resolve(portraitId));
        var unit = assets.ResolveUnit(portrait.UnitType, portrait.ActorKey);
        return assets.CreateFrameTexture(unit, AssetCatalog.FrameIndex(unit, "idle"));
    }
}
