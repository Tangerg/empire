#nullable enable
using System;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PaintedGroundDocument : RegisteredGroundDocument
{
    public required PaintedOccluder[] Occluders
    {
        get; init;
    }

    public BoardGroundFactory Factory(JsonObject registeredMap) => (rules, scene) =>
    {
        if (scene.LevelId != LevelId)
            throw new InvalidOperationException("Painted ground and scene level identities differ.");
        var texture = ResourceLoader.Load<Texture2D>(ResourcePath)
            ?? throw new MissingArtResourceException(ResourcePath, $"Cannot load painted ground {ResourcePath}.");
        return PaintedGroundView.Create(this, texture, registeredMap, rules.Content);
    };
}

/// <summary>A logical object owns its canonical site once, independently of its material drawing parts.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PaintedOccluder
{
    public required string Id
    {
        get; init;
    }
    public int[]? SelectionCell
    {
        get; init;
    }
    public bool OwnsMapStructureBody
    {
        get; init;
    }
    public required PaintedOccluderPart[] Parts
    {
        get; init;
    }
}

/// <summary>One opaque material patch, with its own drawing-depth front and explicit pointer coverage.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PaintedOccluderPart
{
    public required string Id
    {
        get; init;
    }
    public required int[] ContactCell
    {
        get; init;
    }
    public required double[] GroundFrontPixels
    {
        get; init;
    }
    public required double[][] PolygonPixels
    {
        get; init;
    }
    public required PaintedPointer Pointer
    {
        get; init;
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PaintedPointer
{
    public required string Kind
    {
        get; init;
    }
    public double[][]? PolygonPixels
    {
        get; init;
    }
}
