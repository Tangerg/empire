#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

public sealed record RuntimeEnvironmentCell(
    string Id,
    string AtlasId,
    string ResourcePath,
    int Index,
    int Columns,
    int Rows,
    int CellWidth,
    int CellHeight,
    float PixelDensity,
    Vector2 Anchor,
    Vector2I? Footprint,
    float PresentationScale);

/// <summary>Resolves authored atlas cells only; it does not decide terrain composition or passability.</summary>
public sealed class EnvironmentCatalog
{
    private readonly Dictionary<string, RuntimeEnvironmentCell> _cells = new(StringComparer.Ordinal);
    private readonly RasterResources _rasters = new();

    public int LogicalTileSize { get; }
    public bool RuntimeReady { get; }

    public EnvironmentCatalog(EnvironmentManifest manifest,
        string resourceRoot = "res://assets/environment-builder-v1/")
    {
        if (manifest.SchemaVersion != "environment-builder-v1.1" || manifest.LogicalTileSize <= 0)
            throw new InvalidOperationException("Unsupported environment catalog schema or tile size.");
        LogicalTileSize = manifest.LogicalTileSize;
        RuntimeReady = manifest.RuntimeReady;
        var atlasIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var atlas in manifest.Atlases)
        {
            if (!atlasIds.Add(atlas.Id) || string.IsNullOrWhiteSpace(atlas.Id)
                || atlas.Columns <= 0 || atlas.Rows <= 0 || atlas.CellWidth <= 0 || atlas.CellHeight <= 0
                || atlas.ComponentCount <= 0 || atlas.ComponentCount > atlas.Columns * atlas.Rows)
                throw new InvalidOperationException($"Invalid environment atlas geometry or identity: {atlas.Id}");
            var density = atlas.PixelDensity ?? (atlas.Png2x is not null ? 2 : 1);
            RasterResources.Positive(density, $"density of {atlas.Id}");
            var path = atlas.ResourcePath ?? resourceRoot + (atlas.Png2x ?? atlas.Png);
            if (!path.StartsWith("res://assets/", StringComparison.Ordinal)
                || path.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException($"Environment resource path escaped assets: {path}");
            for (var declaredIndex = 0; declaredIndex < atlas.Cells.Count; declaredIndex++)
            {
                var cell = atlas.Cells[declaredIndex];
                var index = cell.Index ?? declaredIndex;
                if (string.IsNullOrWhiteSpace(cell.Id) || index < 0 || index >= atlas.ComponentCount)
                    throw new InvalidOperationException($"Invalid authored environment cell: {cell.Id}");
                var anchor = RasterResources.Pair(cell.Anchor ?? atlas.Anchor
                    ?? new[] { atlas.CellWidth / 2.0, (double)atlas.CellHeight }, $"anchor of {cell.Id}");
                if (anchor.X < 0 || anchor.X > atlas.CellWidth || anchor.Y < 0 || anchor.Y > atlas.CellHeight)
                    throw new InvalidOperationException($"Environment anchor outside cell: {cell.Id}");
                RasterResources.Positive(cell.PresentationScale, $"presentation scale of {cell.Id}");
                var resolved = new RuntimeEnvironmentCell(cell.Id, atlas.Id, path, index,
                    atlas.Columns, atlas.Rows, atlas.CellWidth, atlas.CellHeight, (float)density,
                    anchor, RasterResources.Footprint(cell.Footprint, cell.Id), (float)cell.PresentationScale);
                if (!_cells.TryAdd(cell.Id, resolved))
                    throw new InvalidOperationException($"Duplicate authored environment cell: {cell.Id}");
            }
        }
    }

    public static EnvironmentCatalog Load(string path = "res://data/environment-catalog.json",
        string resourceRoot = "res://assets/environment-builder-v1/") =>
        new(PresentationJson.Load<EnvironmentManifest>(path), resourceRoot);

    public RuntimeEnvironmentCell ResolveCell(string id) =>
        _cells.TryGetValue(id, out var cell) ? cell
            : throw new MissingArtResourceException(id, $"Environment cell not declared in catalog: {id}");

    public Sprite2D CreateSprite(string id, float placementScale = 1, bool flip = false, float opacity = 1)
    {
        RasterResources.Positive(placementScale, $"placement scale of {id}");
        if (!float.IsFinite(opacity) || opacity < 0 || opacity > 1)
            throw new InvalidOperationException($"Opacity outside [0,1]: {id}");
        var cell = ResolveCell(id);
        var texture = _rasters.Frame(cell.ResourcePath,
            checked((int)(cell.CellWidth * cell.Columns * cell.PixelDensity)),
            checked((int)(cell.CellHeight * cell.Rows * cell.PixelDensity)),
            cell.CellWidth, cell.CellHeight, cell.Columns, cell.Index, cell.PixelDensity);
        var sprite = RasterResources.Sprite(texture, cell.Anchor, cell.PixelDensity,
            cell.PresentationScale * placementScale);
        // Authored environment flips mirror inside the original frame, not around the foot.
        sprite.FlipH = flip;
        sprite.Modulate = new Color(1, 1, 1, opacity);
        sprite.SetMeta("environment_cell", cell.Id);
        sprite.SetMeta("atlas_id", cell.AtlasId);
        if (cell.Footprint is { } footprint) sprite.SetMeta("visual_footprint", footprint);
        return sprite;
    }
}
