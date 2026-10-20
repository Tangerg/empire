#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation.Tactical;

public sealed class TacticalHudArtDocument
{
    public int Schema { get; init; }
    public Dictionary<string,TacticalPortraitBinding> Portraits { get; init; } = new(StringComparer.Ordinal);
    public string[] SourceIconKeys { get; init; } = Array.Empty<string>();
    public Dictionary<string,NarrativeArt> Icons { get; init; } = new(StringComparer.Ordinal);
}
public sealed class TacticalPortraitBinding
{
    public string UnitType { get; init; } = "";
    public string? ActorKey { get; init; }
    public NarrativeArt? Card { get; init; }
}

/// <summary>Source provider's explicit card-versus-frame choice, never a sprite fallback for a missing card.</summary>
public sealed class TacticalHudArt
{
    private readonly TacticalHudArtDocument document;
    private readonly AssetCatalog assets;
    private readonly Dictionary<string,AtlasTexture> cards = new(StringComparer.Ordinal);
    public int OwnedTextureCount => cards.Count;
    public void ReleaseOwnedTextures() { foreach (var texture in cards.Values) texture.Dispose(); cards.Clear(); }
    public TacticalHudArt(TacticalHudArtDocument document, AssetCatalog assets)
    {
        if(document.Schema != 1) throw new InvalidOperationException("Unknown tactical HUD art schema.");
        this.document = document; this.assets = assets;
    }
    public static TacticalHudArt Load(AssetCatalog assets) => new(PresentationJson.Load<TacticalHudArtDocument>("res://data/tactical-hud-art.json"),assets);
    private static Texture2D Image(NarrativeArt image) => image.ResourcePath is { } path && ResourceLoader.Exists(path,"Texture2D")
        ? ResourceLoader.Load<Texture2D>(path) : throw new MissingArtResourceException(image.Source,"Missing source HUD art: " + image.Source);
    public Texture2D? Icon(string key)
    {
        if (document.Icons.TryGetValue(key,out var image)) return Image(image);
        if (Array.IndexOf(document.SourceIconKeys,key) >= 0) throw new MissingArtResourceException(key,"Missing declared source HUD icon: " + key);
        return null; // The original provider does not contribute an icon for this key.
    }
    public Texture2D UnitIcon(TacticalHudPortrait unit)
    {
        var asset = assets.ResolveUnit(unit.UnitType,unit.ActorKey);
        return assets.CreateFrameTexture(asset,AssetCatalog.FrameIndex(asset,"idle"));
    }
    public Texture2D Portrait(TacticalHudPortrait portrait)
    {
        var key = portrait.ActorKey is { } actor && document.Portraits.ContainsKey(actor) ? actor : portrait.UnitType;
        if(!document.Portraits.TryGetValue(key,out var binding)) throw new MissingArtResourceException(key,"Missing source HUD portrait binding: " + key);
        if(binding.UnitType != portrait.UnitType) throw new InvalidOperationException("HUD actor portrait type mismatch: " + key);
        if(binding.Card is { } card)
        {
            if (cards.TryGetValue(key,out var cached)) return cached;
            var texture = Image(card);
            // The source draws its 112-square card at x=-8 in a 96x112 clipped plate.
            var cropped = new AtlasTexture { Atlas = texture, Region = new Rect2(texture.GetWidth()/14f,0,texture.GetWidth()*6f/7f,texture.GetHeight()) };
            cards.Add(key,cropped);
            return cropped;
        }
        var unit = assets.ResolveUnit(portrait.UnitType,portrait.ActorKey);
        return assets.CreateFrameTexture(unit,AssetCatalog.FrameIndex(unit,"idle"));
    }
}
