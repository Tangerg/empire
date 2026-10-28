#nullable enable
using System;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

/// <summary>One affine registration shared by authored ground renderers; no tactical rules.</summary>
public abstract class RegisteredGroundDocument
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

    public void ValidateLevel(JsonObject level)
    {
        if (EcmaJson.ReadString(level["id"]) != LevelId)
            throw new InvalidOperationException("Registered ground and canonical level identities differ.");
        var sourceHash = EcmaJson.Sha256(level);
        if (sourceHash != LevelSourceSha256)
            throw new InvalidOperationException("Scene registration does not match its reviewed canonical level source.");
    }

    public BoardPlane CreatePlane(Vector2 logicalSize, int minimumHeight, int maximumHeight)
    {
        if (minimumHeight > maximumHeight)
            throw new InvalidOperationException("Registered ground height bounds must be ordered.");
        if (PixelSize is not { Length: 2 } || PixelSize[0] <= 0 || PixelSize[1] <= 0
            || OriginPixels is not { Length: 2 } || CellXPixels is not { Length: 2 } || CellYPixels is not { Length: 2 }
            || !float.IsFinite(WorldScale) || WorldScale <= 0)
            throw new InvalidOperationException("Registered ground dimensions and registration must be explicit and positive.");
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
                    throw new InvalidOperationException("Registered tactical surface lies outside the registered canvas.");
            }
        }
        return plane;
    }

}
