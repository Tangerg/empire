#nullable enable
using System;

namespace Empire.Presentation;

public sealed class RiverProfile
{
    public double Width
    {
        get; init;
    }
    public double BankWidth
    {
        get; init;
    }
    public double[][] Knots { get; init; } = Array.Empty<double[]>();
    public double[]? Widths
    {
        get; init;
    }
}

public sealed class OccupancyMaskRecord
{
    public int TileSize
    {
        get; init;
    }
    public int Columns
    {
        get; init;
    }
    public int Rows
    {
        get; init;
    }
    public string Geometry { get; init; } = "";
    public string[] BitOrder { get; init; } = Array.Empty<string>();
    public string ResourcePath { get; init; } = "";
}

public sealed class CrossingManifest
{
    public RegisteredCrossing Horizontal { get; init; } = new();
    public RegisteredCrossing Vertical { get; init; } = new();
    public CrossingJunction Junction { get; init; } = new();
    public string[] VerticalLegacyIds { get; init; } = Array.Empty<string>();
}

public sealed class RegisteredCrossing
{
    public int SchemaVersion
    {
        get; init;
    }
    public string Id { get; init; } = "";
    public string Orientation { get; init; } = "";
    public double[] SourceSize { get; init; } = Array.Empty<double>();
    public double[] SourceFoot { get; init; } = Array.Empty<double>();
    public double LogicalPerSourcePixel
    {
        get; init;
    }
    public string ResourcePath { get; init; } = "";
}

public sealed class CrossingJunction
{
    public int SchemaVersion
    {
        get; init;
    }
    public string Id { get; init; } = "";
    public int CenterMask
    {
        get; init;
    }
    public int AxisLength
    {
        get; init;
    }
    public CrossingCutouts Horizontal { get; init; } = new();
    public CrossingCutouts Vertical { get; init; } = new();
}

public sealed class CrossingCutouts
{
    public string Id { get; init; } = "";
    public double[][] Cutouts { get; init; } = Array.Empty<double[]>();
}
