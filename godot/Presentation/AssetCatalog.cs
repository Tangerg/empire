#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

namespace Empire.Presentation;

public sealed class MissingArtResourceException : InvalidOperationException
{
    public string Resource
    {
        get;
    }

    public MissingArtResourceException(string resource, string message) : base(message) =>
        Resource = resource;
}

/// <summary>Logical geometry and canonical identity are independent of the physical PNG density.</summary>
public sealed record RuntimeAsset(
    string Identity,
    string TopicId,
    string Category,
    string ResourcePath,
    float FrameWidth,
    float FrameHeight,
    int FrameCount,
    IReadOnlyList<string> FrameOrder,
    Vector2 Anchor,
    Vector2 GroundFront,
    Vector2I? Footprint,
    float PixelDensity,
    int PhysicalWidth,
    int PhysicalHeight,
    bool PreserveAuthoredPalette,
    float PresentationScale,
    double? FramesPerSecond,
    bool Loop,
    string BlendMode)
{
    public CanvasItem.TextureFilterEnum TextureFilter { get; init; } = CanvasItem.TextureFilterEnum.Nearest;
    public IReadOnlyList<Rect2>? FrameRegions
    {
        get; init;
    }
    public Rect2 RegionAt(int index) => FrameRegions is { } regions ? regions[index]
        : new Rect2(index * FrameWidth * PixelDensity, 0, FrameWidth * PixelDensity, FrameHeight * PixelDensity);
    public IReadOnlyList<Vector2>? FrameAnchors
    {
        get; init;
    }
    public Vector2 AnchorAt(int index) => FrameAnchors is { } anchors ? anchors[index] : Anchor;
    public Vector2 GroundFrontAt(int index) => FrameAnchors is { } anchors ? anchors[index] : GroundFront;
}

public sealed class AssetCatalog
{
    private readonly Dictionary<string, RuntimeAsset> _topics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RuntimeAsset> _variants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UnitArtBinding> _units;
    private readonly Dictionary<string, ActorArtBinding> _actors;
    private readonly Dictionary<string, MapStructureArtBinding> _mapStructures;
    private readonly Dictionary<string, StructureArtBinding> _structures;
    private readonly Dictionary<string, string> _weaponEffects;
    private readonly string? _healEffect;
    private readonly RasterResources _rasters = new();

    public int LogicalTileSize
    {
        get;
    }

    public AssetCatalog(AssetManifest manifest, ArtBindings bindings)
    {
        if (manifest.Schema != 2 || manifest.LogicalTileSize <= 0)
            throw new InvalidOperationException("Unsupported selected-assets schema or logical tile size.");
        LogicalTileSize = manifest.LogicalTileSize;
        _units = new(bindings.Units, StringComparer.Ordinal);
        _actors = new(bindings.Actors, StringComparer.Ordinal);
        _mapStructures = new(bindings.MapStructures, StringComparer.Ordinal);
        _structures = new(bindings.Structures, StringComparer.Ordinal);
        _weaponEffects = new(bindings.WeaponEffects, StringComparer.Ordinal);
        _healEffect = bindings.HealEffect;

        foreach (var record in manifest.Assets.Where(item => item.Id is null))
        {
            var asset = ReadAsset(record, null);
            if (!_topics.TryAdd(asset.TopicId, asset))
                throw new InvalidOperationException($"Duplicate selected topic: {asset.TopicId}");
        }
        foreach (var record in manifest.Assets.Where(item => item.Id is not null))
        {
            if (!_topics.TryGetValue(record.TopicId, out var basis))
                throw new InvalidOperationException($"Selected variant {record.Id} has no canonical base {record.TopicId}.");
            var asset = ReadAsset(record, basis);
            if (!_variants.TryAdd(record.Id!, asset))
                throw new InvalidOperationException($"Duplicate selected variant: {record.Id}");
        }
        foreach (var (id, binding) in _structures)
        {
            if (string.IsNullOrWhiteSpace(id) || binding is null)
                throw new InvalidOperationException("Structure art requires an explicit identity and lifecycle binding.");
            _ = binding.Depth;
            foreach (var state in Enum.GetValues<StructureVisualState>())
            {
                var frame = binding.FrameFor(state);
                if (frame is null || string.IsNullOrWhiteSpace(frame.TopicId) || string.IsNullOrWhiteSpace(frame.Frame))
                    throw new InvalidOperationException($"Missing structure lifecycle art: {id}/{state}.");
                _ = FrameIndex(ResolveTopic(frame.TopicId), frame.Frame);
            }
        }
        foreach (var (key, actor) in _actors)
        {
            if (string.IsNullOrWhiteSpace(key) || key != key.Trim()
                || !_units.TryGetValue(actor.ExpectedUnitType, out var generic)
                || generic.TopicId != actor.UnitArt.TopicId || actor.UnitArt.AppearanceId is null)
                throw new InvalidOperationException($"Invalid actor-key art binding: {key}");
        }
    }

    public static AssetCatalog Load(
        string manifestPath = "res://data/selected-assets.json",
        string bindingsPath = "res://data/art-bindings.json") =>
        new(PresentationJson.Load<AssetManifest>(manifestPath), PresentationJson.Load<ArtBindings>(bindingsPath));

    public RuntimeAsset ResolveTopic(string topicId) =>
        _topics.TryGetValue(topicId, out var asset) ? asset
            : throw new MissingArtResourceException(topicId, $"Topic not included in selected-assets.json: {topicId}");

    public RuntimeAsset ResolveUnit(string unitType, string? actorKey = null)
    {
        UnitArtBinding binding;
        if (actorKey is not null && _actors.TryGetValue(actorKey, out var actor))
        {
            if (actor.ExpectedUnitType != unitType)
                throw new InvalidOperationException($"Actor {actorKey} expected {actor.ExpectedUnitType}, received {unitType}.");
            binding = actor.UnitArt;
        }
        else if (!_units.TryGetValue(unitType, out binding!))
            throw new MissingArtResourceException(unitType, $"Unit art binding is absent: {unitType}");
        return ResolveBinding(binding.TopicId, binding.AppearanceId);
    }

    public bool HasMapStructureBinding(string terrainId) => _mapStructures.ContainsKey(terrainId);

    public (RuntimeAsset Asset, string Frame, BoardDepthLayer Depth)? ResolveStructure(string structureType, StructureVisualState state)
    {
        if (!_structures.TryGetValue(structureType, out var binding))
            return null;
        var frame = binding.FrameFor(state);
        var asset = ResolveTopic(frame.TopicId);
        _ = FrameIndex(asset, frame.Frame);
        return (asset, frame.Frame, binding.Depth);
    }

    public RuntimeAsset? ResolveWeaponEffect(string weapon) =>
        _weaponEffects.TryGetValue(weapon, out var topic) ? ResolveEffect(topic) : null;

    public RuntimeAsset? ResolveHealEffect() => _healEffect is null ? null : ResolveEffect(_healEffect);

    public RuntimeAsset ResolveEffect(string topic)
    {
        var asset = ResolveTopic(topic);
        return asset.Category == "fx" ? asset
            : throw new InvalidOperationException($"Effect binding does not reference an FX strip: {topic}");
    }

    public RuntimeAsset ResolveMapStructure(string terrainId)
    {
        if (!_mapStructures.TryGetValue(terrainId, out var binding))
            throw new InvalidOperationException($"No map structure binding: {terrainId}");
        return ResolveBinding(binding.TopicId, binding.PresentationId);
    }

    public AtlasTexture CreateFrameTexture(RuntimeAsset asset, int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= asset.FrameCount)
            throw new ArgumentOutOfRangeException(nameof(frameIndex), $"Frame outside {asset.Identity}.");
        return _rasters.Frame(asset.ResourcePath, asset.PhysicalWidth, asset.PhysicalHeight,
            asset.RegionAt(frameIndex));
    }

    public Sprite2D CreateSprite(RuntimeAsset asset, string frameRole = "idle") =>
        CreateSpriteAtFrame(asset, FrameIndex(asset, frameRole));

    public Sprite2D CreateSpriteAtFrame(RuntimeAsset asset, int frameIndex)
    {
        var sprite = RasterResources.Sprite(CreateFrameTexture(asset, frameIndex),
            asset.AnchorAt(frameIndex), asset.PixelDensity, asset.PresentationScale);
        sprite.TextureFilter = asset.TextureFilter;
        SpriteGroundContact.Attach(sprite, asset.GroundFrontAt(frameIndex) * asset.PixelDensity);
        sprite.SetMeta("art_identity", asset.Identity);
        sprite.SetMeta("topic_id", asset.TopicId);
        if (asset.Footprint is { } footprint)
            sprite.SetMeta("visual_footprint", footprint);
        return sprite;
    }

    public void ApplyFrame(Sprite2D sprite, RuntimeAsset asset, int frameIndex)
    {
        sprite.Texture = CreateFrameTexture(asset, frameIndex);
        sprite.Offset = -asset.AnchorAt(frameIndex) * asset.PixelDensity;
        SpriteGroundContact.Attach(sprite, asset.GroundFrontAt(frameIndex) * asset.PixelDensity);
    }

    public static int FrameIndex(RuntimeAsset asset, string frameRole)
    {
        for (var index = 0; index < asset.FrameOrder.Count; index++)
            if (asset.FrameOrder[index] == frameRole)
                return index;
        throw new InvalidOperationException($"Asset {asset.Identity} has no declared '{frameRole}' frame.");
    }

    private RuntimeAsset ResolveBinding(string topicId, string? variantId)
    {
        if (variantId is null)
            return ResolveTopic(topicId);
        if (!_variants.TryGetValue(variantId, out var asset))
            throw new MissingArtResourceException(variantId,
                $"Named art variant not included in selected-assets.json: {variantId} ({topicId})");
        if (asset.TopicId != topicId)
            throw new InvalidOperationException($"Art variant {variantId} does not belong to {topicId}.");
        return asset;
    }

    private static RuntimeAsset ReadAsset(AssetRecord record, RuntimeAsset? basis)
    {
        var identity = record.Id ?? record.AssetId ?? record.TopicId;
        float width = record.FrameWidth ?? record.Width;
        float height = record.FrameHeight ?? record.Height;
        var frames = record.Frames ?? 1;
        var density = record.PixelDensity;
        var category = basis?.Category ?? record.Category ?? "";
        var effect = category == "fx";
        var order = record.FrameOrder ?? record.StateOrder ?? Array.Empty<string>();
        var filter = record.TextureFilter switch
        {
            "nearest" => CanvasItem.TextureFilterEnum.Nearest,
            "linear" => CanvasItem.TextureFilterEnum.Linear,
            _ => throw new InvalidOperationException($"Unsupported authored texture filter: {identity}/{record.TextureFilter}.")
        };
        Rect2[]? regions = null;
        if (record.FrameRegions is { } authoredRegions)
        {
            if (category is not ("combat-unit" or "mission-unit") || authoredRegions.Length != frames
                || record.FrameAnchors is null || record.FrameAnchors.Length != frames
                || density != 1 || record.FrameWidth is not null || record.FrameHeight is not null
                || record.Width != 0 || record.Height != 0 || record.Anchor is not null || record.GroundFront is not null)
                throw new InvalidOperationException($"Explicit actor regions require paired contacts, density one and no competing uniform geometry: {identity}.");
            regions = authoredRegions.Select(region =>
            {
                if (region is null || region.Length != 4 || region[0] < 0 || region[1] < 0
                    || region[2] <= 0 || region[3] <= 0
                    || (long)region[0] + region[2] > record.PhysicalWidth
                    || (long)region[1] + region[3] > record.PhysicalHeight)
                    throw new InvalidOperationException($"Explicit actor region outside physical sheet: {identity}.");
                return new Rect2(region[0], region[1], region[2], region[3]);
            }).ToArray();
            if (regions.Length > 0)
            {
                width = regions[0].Size.X;
                height = regions[0].Size.Y;
            }
        }
        else if (record.FrameAnchors is not null)
            throw new InvalidOperationException($"Per-frame contacts require explicit actor regions: {identity}.");
        if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(record.TopicId)
            || !record.ResourcePath.StartsWith("res://assets/", StringComparison.Ordinal)
            || record.ResourcePath.Contains("..", StringComparison.Ordinal)
            || !float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0 || frames <= 0
            || !double.IsFinite(density) || density <= 0 || density > float.MaxValue
            || record.PhysicalWidth <= 0 || record.PhysicalHeight <= 0
            || (regions is null && (Math.Abs(record.PhysicalWidth - width * (double)frames * density) > 0.001
                || Math.Abs(record.PhysicalHeight - height * density) > 0.001)))
            throw new InvalidOperationException($"Invalid asset identity, resource path, or logical/physical geometry: {identity}");
        // Actors and structures name their semantic roles. Source FX instead play
        // every numeric strip index; inventing actor roles would change that contract.
        if (((frames > 1 && !effect) || order.Length > 0)
            && (order.Length != frames || order.Distinct(StringComparer.Ordinal).Count() != frames
                || order.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidOperationException($"Invalid authored frame order: {identity}");
        Vector2[]? frameAnchors = null;
        if (record.FrameAnchors is { } contacts)
            frameAnchors = contacts.Select((point, index) => SpriteGroundContact.Read(point,
                regions![index].Size.X, regions[index].Size.Y, $"{identity}/{index}")).ToArray();
        var anchor = frameAnchors?[0] ?? RasterResources.Pair(record.Anchor
            ?? new[] { width / 2.0, height - 1.0 }, $"anchor of {identity}");
        if (anchor.X < 0 || anchor.X > width || anchor.Y < 0 || anchor.Y > height)
            throw new InvalidOperationException($"Anchor outside frame: {identity}");
        var groundFront = frameAnchors?[0] ?? SpriteGroundContact.Read(record.GroundFront, width, height, identity);
        var fps = effect ? record.Fps ?? 12 : record.Fps;
        var blend = record.BlendMode ?? "normal";
        if (fps is { } speed && (!double.IsFinite(speed) || speed <= 0))
            throw new InvalidOperationException($"Invalid strip frame rate: {identity}");
        if (effect && (record.Anchor is null || anchor.X >= width || anchor.Y >= height || blend != "normal"))
            throw new InvalidOperationException($"Invalid or unsupported effect pivot/blend: {identity}");
        RasterResources.Positive(record.PresentationScale, $"presentation scale of {identity}");

        // Named actors change their sheet, never the base actor's canonical footprint.
        var isHero = basis?.Category == "combat-unit" || basis?.Category == "mission-unit";
        var footprint = isHero ? basis!.Footprint : RasterResources.Footprint(record.Footprint, identity);
        if (isHero && record.Footprint is not null
            && RasterResources.Footprint(record.Footprint, identity) != basis!.Footprint)
            throw new InvalidOperationException($"Actor appearance changes canonical footprint: {identity}");
        return new RuntimeAsset(identity, record.TopicId, category,
            record.ResourcePath, width, height, frames,
            new ReadOnlyCollection<string>((string[])order.Clone()), anchor, groundFront, footprint,
            (float)density, record.PhysicalWidth, record.PhysicalHeight,
            isHero || record.PreserveAuthoredPalette, (float)record.PresentationScale,
            fps, record.Loop ?? false, blend)
        {
            TextureFilter = filter,
            FrameRegions = regions is null ? null : Array.AsReadOnly(regions),
            FrameAnchors = frameAnchors is null ? null : Array.AsReadOnly(frameAnchors)
        };
    }
}

internal sealed class RasterResources
{
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Path, Rect2 Region), AtlasTexture> _frames = new();

    public AtlasTexture Frame(string path, int physicalWidth, int physicalHeight,
        float width, float height, int columns, int index, float density)
        => Frame(path, physicalWidth, physicalHeight, new Rect2(index % columns * width * density,
            index / columns * height * density, width * density, height * density));

    public AtlasTexture Frame(string path, int physicalWidth, int physicalHeight, Rect2 region)
    {
        if (!_textures.TryGetValue(path, out var texture))
        {
            if (!ResourceLoader.Exists(path, "Texture2D"))
                throw new MissingArtResourceException(path, $"Missing imported texture: {path}");
            texture = ResourceLoader.Load<Texture2D>(path)
                ?? throw new InvalidOperationException($"Texture failed to load: {path}");
            _textures.Add(path, texture);
        }
        if (texture.GetWidth() != physicalWidth || texture.GetHeight() != physicalHeight)
            throw new InvalidOperationException($"Texture dimensions differ from manifest: {path}; expected {physicalWidth}x{physicalHeight}, got {texture.GetWidth()}x{texture.GetHeight()}.");
        var key = (path, region);
        if (_frames.TryGetValue(key, out var cached))
            return cached;
        var frame = new AtlasTexture
        {
            Atlas = texture,
            Region = region,
            FilterClip = true,
        };
        _frames.Add(key, frame);
        return frame;
    }

    public static Sprite2D Sprite(AtlasTexture texture, Vector2 anchor, float density, float scale) =>
        new()
        {
            Texture = texture,
            Centered = false,
            Offset = -anchor * density,
            Scale = Vector2.One * (scale / density),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };

    public static Vector2 Pair(double[] values, string field)
    {
        if (values.Length != 2 || values.Any(value => !double.IsFinite(value)
            || Math.Abs(value) > float.MaxValue))
            throw new InvalidOperationException($"Expected two finite coordinates for {field}.");
        return new Vector2((float)values[0], (float)values[1]);
    }

    public static Vector2I? Footprint(int[]? values, string identity)
    {
        if (values is null)
            return null;
        if (values.Length != 2 || values.Any(value => value <= 0))
            throw new InvalidOperationException($"Invalid visual footprint: {identity}");
        return new Vector2I(values[0], values[1]);
    }

    public static void Positive(double value, string field)
    {
        if (!double.IsFinite(value) || value <= 0 || value > float.MaxValue)
            throw new InvalidOperationException($"Expected positive finite {field}.");
    }
}

/// <summary>Asset-owned foreground contact, independent of placement and gameplay footprint.</summary>
public static class SpriteGroundContact
{
    private const string Metadata = "authored_ground_front";

    internal static Vector2 Read(double[]? value, float width, float height, string identity)
    {
        if (value is null)
            throw new InvalidOperationException($"Missing authored ground-front contact: {identity}");
        var point = RasterResources.Pair(value, $"ground-front contact of {identity}");
        if (point.X < 0 || point.Y < 0 || point.X > width || point.Y > height)
            throw new InvalidOperationException($"Ground-front contact outside frame: {identity}");
        return point;
    }

    internal static void Attach(Sprite2D sprite, Vector2 physicalPoint) =>
        sprite.SetMeta(Metadata, physicalPoint);

    /// <summary>Returns the contact displacement in the sprite parent's logical coordinates.</summary>
    public static Vector2 Offset(Sprite2D sprite)
    {
        if (!sprite.HasMeta(Metadata) || sprite.Texture is null)
            throw new InvalidOperationException("Sprite lacks an authored ground-front contact or frame texture.");
        var point = sprite.GetMeta(Metadata).AsVector2();
        var size = sprite.Texture.GetSize();
        if (sprite.FlipH)
            point.X = size.X - point.X;
        if (sprite.FlipV)
            point.Y = size.Y - point.Y;
        point += sprite.Offset;
        if (sprite.Centered)
            point -= size / 2;
        return sprite.Transform * point - sprite.Position;
    }
}
