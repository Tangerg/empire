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
    /// <summary>HUD-only physical-pixel square: named-image-local or generic idle-frame-local; null preserves the whole generic frame.</summary>
    [System.Text.Json.Serialization.JsonRequired]
    public int[]? PortraitRegion
    {
        get; init;
    }
}

/// <summary>Explicit portrait ID or null for the unit idle frame; unknown IDs never fall back.</summary>
public sealed class TacticalHudArt
{
    private readonly TacticalHudArtDocument document;
    private readonly AssetCatalog assets;
    private readonly Dictionary<(string Binding, string Asset), AtlasTexture> genericPortraitTextures = new();
    private readonly Dictionary<string, AtlasTexture> portraitTextures = new(StringComparer.Ordinal);
    public TacticalHudArt(AssetCatalog assets, PortraitCatalog portraits, TacticalHudArtDocument document)
    {
        if (document.Schema != 2)
            throw new InvalidOperationException("Unknown tactical HUD art schema.");
        foreach (var (key, binding) in document.Portraits)
        {
            if (binding.PortraitId is not { } id)
            {
                if (binding.PortraitRegion is { } region)
                {
                    var unit = assets.ResolveUnit(binding.UnitType, binding.ActorKey);
                    genericPortraitTextures.Add((key, unit.Identity), Crop(
                        assets.CreateFrameTexture(unit, AssetCatalog.FrameIndex(unit, "idle")), region, key));
                }
                continue;
            }
            var image = Image(portraits.Resolve(id));
            portraitTextures.Add(key, Crop(image, binding.PortraitRegion, key));
        }
        this.document = document;
        this.assets = assets;
    }
    private static AtlasTexture Crop(Texture2D image, int[]? crop, string key)
    {
        if (crop is not { Length: 4 } || crop[0] < 0 || crop[1] < 0 || crop[2] <= 0 || crop[2] != crop[3]
            || (long)crop[0] + crop[2] > image.GetWidth() || (long)crop[1] + crop[3] > image.GetHeight())
            throw new InvalidOperationException("HUD portrait requires an in-bounds square physical-pixel crop: " + key);
        var origin = image is AtlasTexture frame ? frame.Region.Position : Vector2.Zero;
        return new AtlasTexture
        {
            Atlas = image is AtlasTexture atlas ? atlas.Atlas : image,
            Region = new Rect2(origin + new Vector2(crop[0], crop[1]), new Vector2(crop[2], crop[3])),
            FilterClip = true
        };
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
        if (binding.PortraitId is not null)
            return portraitTextures[key];
        var unit = assets.ResolveUnit(portrait.UnitType, portrait.ActorKey);
        var idle = assets.CreateFrameTexture(unit, AssetCatalog.FrameIndex(unit, "idle"));
        if (binding.PortraitRegion is not { } region)
            return idle;
        var cacheKey = (key, unit.Identity);
        if (!genericPortraitTextures.TryGetValue(cacheKey, out var texture))
            genericPortraitTextures.Add(cacheKey, texture = Crop(idle, region, key));
        return texture;
    }
}
