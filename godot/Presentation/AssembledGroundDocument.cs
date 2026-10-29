#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Godot;

namespace Empire.Presentation;

/// <summary>Clean registered ground plus independently placed transparent art. Never a terrain or movement owner.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AssembledGroundDocument : RegisteredGroundDocument
{
    public required string ObjectCatalogPath
    {
        get; init;
    }
    public required SceneObjectPlacement[] Objects
    {
        get; init;
    }

    public BoardGroundFactory Factory(JsonObject registeredMap) => (rules, scene) =>
    {
        if (scene.LevelId != LevelId)
            throw new InvalidOperationException("Assembled ground and scene level identities differ.");
        var catalog = PresentationJson.Load<SceneObjectCatalog>(ObjectCatalogPath);
        var ground = ResourceLoader.Load<Texture2D>(ResourcePath)
            ?? throw new MissingArtResourceException(ResourcePath, "Missing assembled ground texture.");
        var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        foreach (var (id, asset) in catalog.Objects)
            textures.Add(id, ResourceLoader.Load<Texture2D>(asset.ResourcePath)
                ?? throw new MissingArtResourceException(asset.ResourcePath, $"Missing independent scene object: {id}."));
        return AssembledGroundView.Create(this, ground, catalog, textures, registeredMap, rules.Content, rules.Shadows);
    };
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SceneObjectCatalog
{
    public required int Schema
    {
        get; init;
    }
    public required Dictionary<string, SceneObjectAsset> Objects
    {
        get; init;
    }
}

/// <summary>Physical source geometry belongs to the resource, not each instance.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SceneObjectAsset
{
    public required string ResourcePath
    {
        get; init;
    }
    public required int[] PixelSize
    {
        get; init;
    }
    public required double[] AnchorPixels
    {
        get; init;
    }
    public required double[] GroundFrontPixels
    {
        get; init;
    }
    public required double[] ContactShadowRadiiPixels
    {
        get; init;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SceneObjectPlacement
{
    public required string Id
    {
        get; init;
    }
    public required string AssetId
    {
        get; init;
    }
    public required double[] Contact
    {
        get; init;
    }
    public required float Scale
    {
        get; init;
    }
    public required int[]? SelectionCell
    {
        get; init;
    }
    public required bool OwnsMapStructureBody
    {
        get; init;
    }
    public required string Pointer
    {
        get; init;
    }
}
