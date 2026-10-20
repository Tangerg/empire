#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

public sealed class EdgeDecorationManifest
{
    public int SchemaVersion { get; init; }
    public int MeaningfulAlphaMin { get; init; }
    public EdgeDecorationAsset[] Assets { get; init; } = Array.Empty<EdgeDecorationAsset>();
    public EdgeDecorationScene[] Scenes { get; init; } = Array.Empty<EdgeDecorationScene>();
}
public sealed class EdgeDecorationAsset
{
    public string Id { get; init; } = "";
    public string ResourcePath { get; init; } = "";
    public double[] SourceSize { get; init; } = Array.Empty<double>();
    public double[] SourceAnchor { get; init; } = Array.Empty<double>();
    public double[] MeaningfulSourceBounds { get; init; } = Array.Empty<double>();
    public double LogicalPerSourcePixel { get; init; }
    public int SourcePadding { get; init; }
}
public sealed class EdgeDecorationScene
{
    public string LevelId { get; init; } = "";
    public int[] MapSize { get; init; } = Array.Empty<int>();
    public EdgeDecorationPlacement[] Placements { get; init; } = Array.Empty<EdgeDecorationPlacement>();
}
public sealed class EdgeDecorationPlacement
{
    public string Id { get; init; } = "";
    public string AssetId { get; init; } = "";
    public double[] Center { get; init; } = Array.Empty<double>();
    public double Scale { get; init; }
    public bool FlipX { get; init; }
    public bool FlipY { get; init; }
    public EdgeTerrainSupport TerrainSupport { get; init; } = new();
}
public sealed class EdgeTerrainSupport
{
    public int[] Origin { get; init; } = Array.Empty<int>();
    public string[][] Rows { get; init; } = Array.Empty<string[]>();
}

/// <summary>Source-registered sparse overlays, guarded by exact local canonical terrain.</summary>
internal static class EdgeDecorationRenderer
{
    internal static void Validate(EdgeDecorationManifest manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.MeaningfulAlphaMin != 2)
            throw new InvalidOperationException("Unsupported authored edge decoration contract.");
        var assets = new Dictionary<string, EdgeDecorationAsset>(StringComparer.Ordinal);
        foreach (var asset in manifest.Assets)
        {
            var size = RasterResources.Pair(asset.SourceSize, $"edge canvas {asset.Id}");
            var anchor = RasterResources.Pair(asset.SourceAnchor, $"edge anchor {asset.Id}");
            var bounds = asset.MeaningfulSourceBounds;
            var scale = asset.LogicalPerSourcePixel;
            if (!assets.TryAdd(asset.Id, asset) || string.IsNullOrWhiteSpace(asset.Id)
                || !double.IsFinite(scale) || scale <= 0 || asset.SourcePadding < 0 || asset.SourcePadding * scale > 4
                || asset.SourceSize.Any(value => value <= 0 || value != Math.Truncate(value))
                || anchor.X < 0 || anchor.Y < 0 || anchor.X > size.X || anchor.Y > size.Y
                || bounds.Length != 4 || bounds.Any(value => !double.IsFinite(value))
                || bounds[0] < 0 || bounds[1] < 0 || bounds[2] > size.X || bounds[3] > size.Y
                || bounds[0] >= bounds[2] || bounds[1] >= bounds[3])
                throw new InvalidOperationException($"Invalid edge registration: {asset.Id}");
        }
        var scenes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scene in manifest.Scenes)
        {
            if (scene.MapSize.Length != 2 || scene.MapSize.Any(value => value <= 0)
                || !scenes.Add($"{scene.LevelId}:{string.Join('x', scene.MapSize)}"))
                throw new InvalidOperationException("Invalid or duplicate edge scene extent.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in scene.Placements)
            {
                if (!assets.TryGetValue(placement.AssetId, out var asset))
                    throw new InvalidOperationException($"Unknown edge asset: {placement.AssetId}");
                RasterResources.Pair(placement.Center, $"edge center {placement.Id}");
                var origin = placement.TerrainSupport.Origin;
                var rows = placement.TerrainSupport.Rows;
                var width = rows.FirstOrDefault()?.Length ?? 0;
                if (!ids.Add(placement.Id) || !double.IsFinite(placement.Scale) || placement.Scale <= 0 || placement.Scale > 1
                    || origin.Length != 2 || origin.Any(value => value < 0) || width == 0
                    || rows.Any(row => row.Length != width || row.Any(string.IsNullOrEmpty))
                    || origin[0] + width > scene.MapSize[0] || origin[1] + rows.Length > scene.MapSize[1])
                    throw new InvalidOperationException($"Invalid edge placement: {placement.Id}");
                for (var axis = 0; axis < 2; axis++)
                {
                    var size = (asset.SourceSize[axis] + asset.SourcePadding * 2) * asset.LogicalPerSourcePixel;
                    var anchor = (asset.SourceAnchor[axis] + asset.SourcePadding) * asset.LogicalPerSourcePixel;
                    var sign = (axis == 0 ? placement.FlipX : placement.FlipY) ? -1 : 1;
                    var ends = new[] { 0.0, size }.Select(value => placement.Center[axis] + (value - anchor) * placement.Scale * sign).ToArray();
                    var minimum = ends.Min(); var maximum = ends.Max();
                    var supportEnd = origin[axis] + (axis == 0 ? width : rows.Length);
                    if (minimum < 0 || maximum > scene.MapSize[axis] * 32
                        || origin[axis] > Math.Max(0, Math.Floor(minimum / 32) - 1)
                        || supportEnd < Math.Min(scene.MapSize[axis], Math.Ceiling(maximum / 32) + 1))
                        throw new InvalidOperationException($"Incomplete edge terrain support: {placement.Id}");
                }
            }
        }
    }

    internal static int Add(Node2D parent, EdgeDecorationManifest manifest, string levelId, Vector2I size,
        string[] tiles, Func<string, Texture2D> texture, List<string> diagnostics)
    {
        var scene = manifest.Scenes.SingleOrDefault(scene => scene.LevelId == levelId
            && scene.MapSize.SequenceEqual(new[] { size.X, size.Y }));
        if (scene is null) return 0;
        var count = 0;
        foreach (var placement in scene.Placements)
        {
            var support = placement.TerrainSupport;
            if (!support.Rows.SelectMany((row, y) => row.Select((tile, x) =>
                tiles[(support.Origin[1] + y) * size.X + support.Origin[0] + x] == tile)).All(value => value)) continue;
            var asset = manifest.Assets.Single(asset => asset.Id == placement.AssetId);
            try
            {
                var image = texture(asset.ResourcePath);
                var logicalSize = (RasterResources.Pair(asset.SourceSize, asset.Id) + Vector2.One * asset.SourcePadding * 2)
                    * (float)asset.LogicalPerSourcePixel;
                var logicalAnchor = (RasterResources.Pair(asset.SourceAnchor, asset.Id) + Vector2.One * asset.SourcePadding)
                    * (float)asset.LogicalPerSourcePixel;
                var rasterScale = logicalSize / image.GetSize();
                var sprite = new Sprite2D { Name = placement.Id, Texture = image, Centered = false,
                    Position = RasterResources.Pair(placement.Center, placement.Id), Offset = -logicalAnchor / rasterScale,
                    Scale = rasterScale * (float)placement.Scale * new Vector2(placement.FlipX ? -1 : 1, placement.FlipY ? -1 : 1),
                    TextureFilter = CanvasItem.TextureFilterEnum.Linear };
                sprite.SetMeta("edge_asset", asset.Id);
                parent.AddChild(sprite);
                count++;
            }
            catch (MissingArtResourceException error) { diagnostics.Add($"Edge overlay {placement.Id}: {error.Message}"); }
        }
        return count;
    }
}
