#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Empire.Presentation;

public sealed class AssetManifest
{
    public int Schema { get; init; }
    public int LogicalTileSize { get; init; }
    public List<AssetRecord> Assets { get; init; } = new();
}

public sealed class AssetRecord
{
    public string? AssetId { get; init; }
    public string? Id { get; init; }
    public string TopicId { get; init; } = "";
    public string? Category { get; init; }
    public string ResourcePath { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public int? FrameWidth { get; init; }
    public int? FrameHeight { get; init; }
    public int? Frames { get; init; }
    public string[]? FrameOrder { get; init; }
    public string[]? StateOrder { get; init; }
    public double[]? Anchor { get; init; }
    public int[]? Footprint { get; init; }
    public double PixelDensity { get; init; }
    public int PhysicalWidth { get; init; }
    public int PhysicalHeight { get; init; }
    public bool PreserveAuthoredPalette { get; init; }
    public double PresentationScale { get; init; } = 1;
}

public sealed class UnitArtBinding
{
    public string TopicId { get; init; } = "";
    public string? AppearanceId { get; init; }
}

public sealed class ActorArtBinding
{
    public string ExpectedUnitType { get; init; } = "";
    public UnitArtBinding UnitArt { get; init; } = new();
}

public sealed class MapStructureArtBinding
{
    public string TopicId { get; init; } = "";
    public string? PresentationId { get; init; }
}

public sealed class ArtBindings
{
    [JsonPropertyName("CANDIDATE_01_UNIT_ART")]
    public Dictionary<string, UnitArtBinding> Units { get; init; } = new();
    [JsonPropertyName("CANDIDATE_01_ACTOR_ART")]
    public Dictionary<string, ActorArtBinding> Actors { get; init; } = new();
    [JsonPropertyName("CANDIDATE_01_MAP_STRUCTURE_ART")]
    public Dictionary<string, MapStructureArtBinding> MapStructures { get; init; } = new();
}

public sealed class EnvironmentManifest
{
    public string SchemaVersion { get; init; } = "";
    public int LogicalTileSize { get; init; }
    public bool RuntimeReady { get; init; }
    public List<EnvironmentAtlasRecord> Atlases { get; init; } = new();
}

public sealed class EnvironmentAtlasRecord
{
    public string Id { get; init; } = "";
    public string Png { get; init; } = "";
    public string? Png2x { get; init; }
    public string? ResourcePath { get; init; }
    public double? PixelDensity { get; init; }
    public int Columns { get; init; }
    public int Rows { get; init; }
    public int CellWidth { get; init; }
    public int CellHeight { get; init; }
    public int ComponentCount { get; init; }
    public double[]? Anchor { get; init; }
    public List<EnvironmentCellRecord> Cells { get; init; } = new();
}

public sealed class EnvironmentCellRecord
{
    public string Id { get; init; } = "";
    public int? Index { get; init; }
    public double[]? Anchor { get; init; }
    public int[]? Footprint { get; init; }
    public double PresentationScale { get; init; } = 1;
}

public sealed class AuthoredScene
{
    public string SchemaVersion { get; init; } = "";
    public string LevelId { get; init; } = "";
    public string Title { get; init; } = "";
    public bool RuntimeReady { get; init; }
    public int[] MapSize { get; init; } = Array.Empty<int>();
    public List<AuthoredPlacement> Placements { get; init; } = new();
    public List<FramePlacement> FramePlacements { get; init; } = new();

    public static AuthoredScene Load(string path = "res://data/grayflag-authored-scene.json") =>
        PresentationJson.Load<AuthoredScene>(path);
}

public sealed class AuthoredPlacement
{
    public string? Id { get; init; }
    public string? TopicId { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public string Layer { get; init; } = "under-units";
    public double Scale { get; init; } = 1;
    public bool Flip { get; init; }
    public double Opacity { get; init; } = 1;
}

public sealed class FramePlacement
{
    public string Id { get; init; } = "";
    public double[] Contact { get; init; } = Array.Empty<double>();
    public double Scale { get; init; } = 1;
    public bool Flip { get; init; }
}

public static class PresentationJson
{
    public static T Load<T>(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            throw new InvalidOperationException($"Cannot read presentation document {path}: {Godot.FileAccess.GetOpenError()}");
        return JsonSerializer.Deserialize<T>(file.GetAsText(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException($"Empty presentation document: {path}");
    }
}
