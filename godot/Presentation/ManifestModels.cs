#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Empire.Presentation;

public sealed class AssetManifest
{
    public int Schema
    {
        get; init;
    }
    public int LogicalTileSize
    {
        get; init;
    }
    public List<AssetRecord> Assets { get; init; } = new();
}

public sealed class AssetRecord
{
    public string? AssetId
    {
        get; init;
    }
    public string? Id
    {
        get; init;
    }
    public string TopicId { get; init; } = "";
    public string? Category
    {
        get; init;
    }
    public string ResourcePath { get; init; } = "";
    public int Width
    {
        get; init;
    }
    public int Height
    {
        get; init;
    }
    public int? FrameWidth
    {
        get; init;
    }
    public int? FrameHeight
    {
        get; init;
    }
    public int? Frames
    {
        get; init;
    }
    public string[]? FrameOrder
    {
        get; init;
    }
    public string[]? StateOrder
    {
        get; init;
    }
    public double? Fps
    {
        get; init;
    }
    public bool? Loop
    {
        get; init;
    }
    public string? BlendMode
    {
        get; init;
    }
    public double[]? Anchor
    {
        get; init;
    }
    public double[]? GroundFront
    {
        get; init;
    }
    public int[]? Footprint
    {
        get; init;
    }
    public double PixelDensity
    {
        get; init;
    }
    public int PhysicalWidth
    {
        get; init;
    }
    public int PhysicalHeight
    {
        get; init;
    }
    public bool PreserveAuthoredPalette
    {
        get; init;
    }
    public double PresentationScale { get; init; } = 1;
}

public sealed class UnitArtBinding
{
    public string TopicId { get; init; } = "";
    public string? AppearanceId
    {
        get; init;
    }
}

public sealed class ActorArtBinding
{
    public string ExpectedUnitType { get; init; } = "";
    public UnitArtBinding UnitArt { get; init; } = new();
}

public sealed class MapStructureArtBinding
{
    public string TopicId { get; init; } = "";
    public string? PresentationId
    {
        get; init;
    }
}

public sealed class ArtBindings
{
    [JsonPropertyName("CANDIDATE_01_UNIT_ART")]
    public Dictionary<string, UnitArtBinding> Units { get; init; } = new();
    [JsonPropertyName("CANDIDATE_01_ACTOR_ART")]
    public Dictionary<string, ActorArtBinding> Actors { get; init; } = new();
    [JsonPropertyName("CANDIDATE_01_MAP_STRUCTURE_ART")]
    public Dictionary<string, MapStructureArtBinding> MapStructures { get; init; } = new();
    [JsonPropertyName("CANDIDATE_01_STRUCTURE_ART")]
    public Dictionary<string, StructureArtBinding> Structures { get; init; } = new();
    [JsonPropertyName("CANDIDATE_01_WEAPON_FX")]
    public Dictionary<string, string> WeaponEffects { get; init; } = new();
    [JsonPropertyName("CANDIDATE_01_HEAL_FX")]
    public string? HealEffect
    {
        get; init;
    }
}

public sealed class EnvironmentManifest
{
    public string SchemaVersion { get; init; } = "";
    public int LogicalTileSize
    {
        get; init;
    }
    public bool RuntimeReady
    {
        get; init;
    }
    public List<EnvironmentAtlasRecord> Atlases { get; init; } = new();
}

public sealed class EnvironmentAtlasRecord
{
    public string Id { get; init; } = "";
    public string Png { get; init; } = "";
    public string? Png2x
    {
        get; init;
    }
    public string? ResourcePath
    {
        get; init;
    }
    public double? PixelDensity
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
    public int CellWidth
    {
        get; init;
    }
    public int CellHeight
    {
        get; init;
    }
    public int ComponentCount
    {
        get; init;
    }
    public string TileMode { get; init; } = "";
    public string? CellIndex
    {
        get; init;
    }
    public int VariantsPerMask { get; init; } = 1;
    public int[] MaskOrder { get; init; } = Array.Empty<int>();
    public double[]? Anchor
    {
        get; init;
    }
    public List<EnvironmentCellRecord> Cells { get; init; } = new();
}

public sealed class EnvironmentCellRecord
{
    public string Id { get; init; } = "";
    public int? Index
    {
        get; init;
    }
    public double[]? Anchor
    {
        get; init;
    }
    public double[]? GroundFront
    {
        get; init;
    }
    public int[]? Footprint
    {
        get; init;
    }
    public double PresentationScale { get; init; } = 1;
}

public enum StructureVisualState
{
    Normal, Captured, Damaged, Destroyed
}

public sealed record AssetFrameBinding
{
    public required string TopicId
    {
        get; init;
    }
    public required string Frame
    {
        get; init;
    }
}

public sealed record StructureArtBinding
{
    public required AssetFrameBinding Normal
    {
        get; init;
    }
    public required AssetFrameBinding Captured
    {
        get; init;
    }
    public required AssetFrameBinding Damaged
    {
        get; init;
    }
    public required AssetFrameBinding Destroyed
    {
        get; init;
    }

    public AssetFrameBinding FrameFor(StructureVisualState state) => state switch
    {
        StructureVisualState.Normal => Normal,
        StructureVisualState.Captured => Captured,
        StructureVisualState.Damaged => Damaged,
        StructureVisualState.Destroyed => Destroyed,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}

public sealed class AuthoredScene
{
    public string SchemaVersion { get; init; } = "";
    public string LevelId { get; init; } = "";
    public string Title { get; init; } = "";
    public bool RuntimeReady
    {
        get; init;
    }
    public int[] MapSize { get; init; } = Array.Empty<int>();
    public int[]? OpeningFocus
    {
        get; init;
    }
    public List<AuthoredPlacement> Placements { get; init; } = new();
    private List<FramePlacement> _framePlacements = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasAuthoredFramePlacements
    {
        get; private set;
    }
    public List<FramePlacement> FramePlacements
    {
        get => _framePlacements;
        init
        {
            _framePlacements = value ?? throw new InvalidOperationException("Authored frame placements cannot be null.");
            HasAuthoredFramePlacements = true;
        }
    }
    public double[][] GroundOutline { get; init; } = Array.Empty<double[]>();
    public RoadCorridorProfile? RoadCorridor
    {
        get; init;
    }
    public RiverProfile? River
    {
        get; init;
    }
    public List<AuthoredZone> Zones { get; init; } = new();

    public static AuthoredScene Load(string path) =>
        PresentationJson.Load<AuthoredScene>(path);
}

public sealed class AuthoredZone
{
    public string Id { get; init; } = "";
    public int[] Bounds { get; init; } = Array.Empty<int>();
    public string[] AuthoredTerrainScenery { get; init; } = Array.Empty<string>();
    public string[] AuthoredTerrainSurface { get; init; } = Array.Empty<string>();
}

public sealed class AuthoredPlacement
{
    public string? Id
    {
        get; init;
    }
    public string? TopicId
    {
        get; init;
    }
    public double X
    {
        get; init;
    }
    public double Y
    {
        get; init;
    }
    public string Layer { get; init; } = "under-units";
    public double Scale { get; init; } = 1;
    public bool Flip
    {
        get; init;
    }
    public double Opacity { get; init; } = 1;
    public bool StandingShadow
    {
        get; init;
    }
}

public sealed class FramePlacement
{
    public required string Layer
    {
        get; init;
    }
    public string Id { get; init; } = "";
    public double[] Contact { get; init; } = Array.Empty<double>();
    public double Scale { get; init; } = 1;
    public bool Flip
    {
        get; init;
    }
}

public static class PresentationJson
{
    public static T Load<T>(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            throw new InvalidOperationException($"Cannot read presentation document {path}: {Godot.FileAccess.GetOpenError()}");
        var text = file.GetAsText();
        if (typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(typeof(T)))
            return (T)(object)(Empire.Core.Determinism.EcmaJson.Parse(text) ?? throw new InvalidOperationException($"Empty presentation document: {path}"));
        return JsonSerializer.Deserialize<T>(text,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidOperationException($"Empty presentation document: {path}");
    }
}
