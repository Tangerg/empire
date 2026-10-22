#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

public sealed class PaintedGroundDocument
{
    public required string LevelId
    {
        get; init;
    }
    public required string LevelSourceSha256
    {
        get; init;
    }
    public required string ResourcePath
    {
        get; init;
    }
    public required int[] PixelSize
    {
        get; init;
    }
    public required float WorldScale
    {
        get; init;
    }
    public required float[] OriginPixels
    {
        get; init;
    }
    public required float[] CellXPixels
    {
        get; init;
    }
    public required float[] CellYPixels
    {
        get; init;
    }

    public required PaintedOccluder[] Occluders
    {
        get; init;
    }

    public void ValidateLevel(JsonObject level)
    {
        if (EcmaJson.ReadString(level["id"]) != LevelId)
            throw new InvalidOperationException("Painted ground and canonical level identities differ.");
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(EcmaJson.Stringify(level)))).ToLowerInvariant();
        if (sourceHash != LevelSourceSha256)
            throw new InvalidOperationException("Painted scene registration does not match its reviewed canonical level source.");
    }

    public BoardPlane CreatePlane(Vector2 logicalSize, int minimumHeight, int maximumHeight)
    {
        if (minimumHeight > maximumHeight)
            throw new InvalidOperationException("Painted ground height bounds must be ordered.");
        if (Occluders is null || PixelSize.Length != 2 || PixelSize[0] <= 0 || PixelSize[1] <= 0
            || OriginPixels.Length != 2 || CellXPixels.Length != 2 || CellYPixels.Length != 2
            || !float.IsFinite(WorldScale) || WorldScale <= 0)
            throw new InvalidOperationException("Painted ground dimensions and registration must be explicit and positive.");
        var plane = new BoardPlane(
            new Vector2(CellXPixels[0], CellXPixels[1]) * (WorldScale / BoardProjection.TileSize),
            new Vector2(CellYPixels[0], CellYPixels[1]) * (WorldScale / BoardProjection.TileSize),
            new Vector2(OriginPixels[0], OriginPixels[1]) * WorldScale,
            new Vector2(PixelSize[0], PixelSize[1]) * WorldScale);
        foreach (var corner in new[] { Vector2.Zero, new Vector2(logicalSize.X, 0), logicalSize, new Vector2(0, logicalSize.Y) })
        {
            foreach (var height in new[] { minimumHeight, maximumHeight })
            {
                var point = plane.Ground(corner) + plane.Origin - Vector2.Down * height * BoardProjection.HeightStep;
                if (point.X < 0 || point.Y < 0 || point.X > plane.CanvasSize.X || point.Y > plane.CanvasSize.Y)
                    throw new InvalidOperationException("Registered tactical surface lies outside the painted canvas.");
            }
        }
        return plane;
    }

    public BoardGroundFactory Factory(JsonObject registeredMap) => (_, scene) =>
    {
        if (scene.LevelId != LevelId)
            throw new InvalidOperationException("Painted ground and scene level identities differ.");
        var texture = ResourceLoader.Load<Texture2D>(ResourcePath)
            ?? throw new MissingArtResourceException(ResourcePath, $"Cannot load painted ground {ResourcePath}.");
        if (PixelSize.Length != 2 || texture.GetWidth() != PixelSize[0] || texture.GetHeight() != PixelSize[1])
            throw new InvalidOperationException("Painted ground texture dimensions differ from its registration.");
        return PaintedGroundView.Create(this, texture, registeredMap);
    };
}

public sealed class PaintedOccluder
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
    public int[]? SelectionCell
    {
        get; init;
    }
    public bool OwnsMapStructureBody
    {
        get; init;
    }
}
