#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

public delegate BoardPlane BoardPlaneFactory(Vector2 logicalSize, int minimumHeight, int maximumHeight);

/// <summary>A scene's explicit affine ground registration, independent of tactical legality.</summary>
public sealed class BoardPlane
{
    public Vector2 BasisX
    {
        get;
    }
    public Vector2 BasisY
    {
        get;
    }
    public Vector2 Origin
    {
        get;
    }
    public Vector2 CanvasSize
    {
        get;
    }

    public BoardPlane(Vector2 basisX, Vector2 basisY, Vector2 origin, Vector2 canvasSize)
    {
        if (!basisX.IsFinite() || !basisY.IsFinite() || !origin.IsFinite() || !canvasSize.IsFinite()
            || basisX.Cross(basisY) <= 0.0001f || basisX.Y < 0 || basisY.Y <= 0
            || canvasSize.X <= 0 || canvasSize.Y <= 0)
            throw new ArgumentException("Ground registration requires finite, forward, nondegenerate axes and a positive canvas.");
        BasisX = basisX;
        BasisY = basisY;
        Origin = origin;
        CanvasSize = canvasSize;
    }

    public Vector2 Ground(Vector2 logicalPixel) => BasisX * logicalPixel.X + BasisY * logicalPixel.Y;

    public static BoardPlane Dimetric(Vector2 logicalSize, int minimumHeight, int maximumHeight) => new(
        new(.8f, .4f), new(-.8f, .4f),
        new(logicalSize.Y * .8f, Math.Max(0, maximumHeight) * BoardProjection.HeightStep),
        new((logicalSize.X + logicalSize.Y) * .8f,
            (logicalSize.X + logicalSize.Y) * .4f
            + (Math.Max(0, maximumHeight) - Math.Min(0, minimumHeight)) * BoardProjection.HeightStep));
}
