#nullable enable
using System;
using System.Collections.Generic;

namespace Empire.Presentation;

public sealed class WorldMaterialsManifest
{
    public int Schema { get; init; }
    public Dictionary<string, WorldMaterialRecord> Materials { get; init; } = new(StringComparer.Ordinal);
    public ForestMaskRecord ForestMask { get; init; } = new();
    public Dictionary<string, TerrainMaterialBinding> TerrainBindings { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string[]> TerrainTags { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string[]> GroundTagFamilies { get; init; } = new(StringComparer.Ordinal);
    public ScenerySpecifications Scenery { get; init; } = new();
    public static WorldMaterialsManifest Load(string path = "res://data/world-materials.json") =>
        PresentationJson.Load<WorldMaterialsManifest>(path);
}

public sealed class WorldMaterialRecord
{
    public int SchemaVersion { get; init; }
    public string Id { get; init; } = "";
    public string ResourcePath { get; init; } = "";
    public int[] LogicalSize { get; init; } = Array.Empty<int>();
    public int[] RasterSize { get; init; } = Array.Empty<int>();
    public double[] WorldOrigin { get; init; } = Array.Empty<double>();
    public MaterialFit Fit { get; init; } = new();
    public MaterialReplacement Replaces { get; init; } = new();
}
public sealed class MaterialFit { public double Inset { get; init; } public double Bleed { get; init; } }
public sealed class MaterialReplacement
{
    public string Atlas { get; init; } = "";
    public string? Mask { get; init; }
    public int? Tone { get; init; }
}
public sealed class ForestMaskRecord
{
    public string Id { get; init; } = "";
    public string ResourcePath { get; init; } = "";
    public int Columns { get; init; }
    public int Rows { get; init; }
    public int CellWidth { get; init; }
    public int CellHeight { get; init; }
    public int CellWidth2x { get; init; }
    public int CellHeight2x { get; init; }
    public int[] MaskOrder { get; init; } = Array.Empty<int>();
}
public sealed class TerrainMaterialBinding
{
    public string? Surface { get; init; }
    public string? Blend { get; init; }
    public int? Tone { get; init; }
    public MaterialReplacement? Connected { get; init; }
    public ScenerySpecification? Scenery { get; init; }
    public ScenerySpecification? Decals { get; init; }
}
public sealed class ScenerySpecifications
{
    public ScenerySpecification? ForestFloor { get; init; }
    public ScenerySpecification? SettlementLife { get; init; }
}
public sealed class ScenerySpecification
{
    public string[] Ids { get; init; } = Array.Empty<string>();
    public double Scale { get; init; }
    public double? Chance { get; init; }
    public double Jitter { get; init; }
}
public sealed class RoadCorridorProfile
{
    public double Width { get; init; }
    public double MaxTangentCut { get; init; }
    public double MaxPlainOverhang { get; init; }
    public int[][] ProtectedCells { get; init; } = Array.Empty<int[]>();
}
