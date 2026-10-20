#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

internal sealed record CrossingRun(bool Horizontal, BoardCell Start, int Length, Vector2 Pivot);
internal sealed record CrossingComponent(IReadOnlyList<(BoardCell Cell, int Mask)> Cells, IReadOnlyList<CrossingRun> Runs);

/// <summary>Projects source bridge topology without inventing gameplay crossings or resizing decks by run length.</summary>
internal static class CrossingRenderer
{
    private static readonly BoardCell[] Directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];
    private const string CutoutShader = """
        shader_type canvas_item;
        uniform vec2 logical_size;
        uniform vec2 logical_anchor;
        uniform vec4 first_cut;
        uniform vec4 second_cut;
        bool inside(vec2 p, vec4 r) { return p.x >= r.x && p.y >= r.y && p.x <= r.x+r.z && p.y <= r.y+r.w; }
        void fragment() {
            COLOR = texture(TEXTURE, UV);
            vec2 p = UV * logical_size - logical_anchor;
            if (inside(p, first_cut) || inside(p, second_cut)) COLOR.a = 0.0;
        }
        """;

    internal static IReadOnlyList<CrossingComponent> Topology(int width, int height,
        Func<int, int, bool> bridge, Func<int, int, bool> route)
    {
        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && bridge(x, y);
        var visited = new HashSet<BoardCell>();
        var components = new List<CrossingComponent>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var start = new BoardCell(x, y);
            if (!Inside(x, y) || !visited.Add(start)) continue;
            var queue = new List<BoardCell> { start };
            for (var index = 0; index < queue.Count; index++)
            foreach (var delta in Directions)
            {
                var next = new BoardCell(queue[index].X + delta.X, queue[index].Y + delta.Y);
                if (Inside(next.X, next.Y) && visited.Add(next)) queue.Add(next);
            }
            queue.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            var cells = queue.Select(cell => (cell, Directions.Select((delta, bit) =>
                Inside(cell.X + delta.X, cell.Y + delta.Y) ? 1 << bit : 0).Aggregate(0, (a, b) => a | b))).ToArray();
            var runs = new List<CrossingRun>();
            foreach (var cell in queue)
            {
                var horizontal = route(cell.X - 1, cell.Y) || route(cell.X + 1, cell.Y)
                    || !(route(cell.X, cell.Y - 1) || route(cell.X, cell.Y + 1))
                        && (Inside(cell.X - 1, cell.Y) || Inside(cell.X + 1, cell.Y));
                if (horizontal ? Inside(cell.X - 1, cell.Y) : Inside(cell.X, cell.Y - 1)) continue;
                var length = 1;
                while (horizontal ? Inside(cell.X + length, cell.Y) : Inside(cell.X, cell.Y + length)) length++;
                runs.Add(new CrossingRun(horizontal, cell, length, horizontal
                    ? new Vector2((cell.X + length / 2f) * 32, (cell.Y + 0.5f) * 32)
                    : new Vector2((cell.X + 0.5f) * 32, (cell.Y + length / 2f) * 32)));
            }
            components.Add(new CrossingComponent(cells, runs));
        }
        return components;
    }

    internal static Vector2? Junction(CrossingComponent component)
    {
        if (component.Cells.Count != 5 || component.Runs.Count != 2) return null;
        var centers = component.Cells.Where(cell => cell.Mask == 15).ToArray();
        if (centers.Length != 1) return null;
        var center = centers[0].Cell;
        var expected = new[] { (center, 15), (new BoardCell(center.X, center.Y - 1), 4),
            (new BoardCell(center.X + 1, center.Y), 8), (new BoardCell(center.X, center.Y + 1), 1),
            (new BoardCell(center.X - 1, center.Y), 2) };
        if (!expected.All(cell => component.Cells.Contains(cell))) return null;
        var pivot = new Vector2((center.X + 0.5f) * 32, (center.Y + 0.5f) * 32);
        foreach (var horizontal in new[] { true, false })
        {
            var run = component.Runs.SingleOrDefault(run => run.Horizontal == horizontal);
            if (run is null || run.Length != 3 || run.Pivot != pivot
                || run.Start != new BoardCell(center.X - (horizontal ? 1 : 0), center.Y - (horizontal ? 0 : 1))) return null;
        }
        return pivot;
    }

    internal static void Add(Node2D parent, CrossingManifest manifest, EnvironmentCatalog environment,
        Func<string, Texture2D> texture, IReadOnlyList<CrossingComponent> components, List<string> diagnostics)
    {
        if (manifest.VerticalLegacyIds.Length == 0)
            throw new InvalidOperationException("Vertical crossing source choices are not exported.");
        string Vertical(CrossingRun run) => manifest.VerticalLegacyIds[(int)Math.Floor(
            TileArtVariation.Hash(run.Start.X, run.Start.Y, 1131) * manifest.VerticalLegacyIds.Length)];
        foreach (var component in components)
        {
            var vertical = component.Runs.FirstOrDefault(run => !run.Horizontal);
            if (Junction(component) is { } pivot && vertical is not null && Vertical(vertical) == "stone-bridge-vertical")
            {
                try
                {
                    ValidateJunction(manifest);
                    var node = new Node2D { Name = $"Junction_{vertical.Start.X}_{vertical.Start.Y}", Position = pivot };
                    try
                    {
                        node.AddChild(Registered(manifest.Horizontal, "horizontal", texture, manifest.Junction.Horizontal.Cutouts));
                        node.AddChild(Registered(manifest.Vertical, "vertical", texture, manifest.Junction.Vertical.Cutouts));
                        parent.AddChild(node);
                    }
                    catch { node.Free(); throw; }
                }
                catch (MissingArtResourceException error) { diagnostics.Add($"Bridge junction: {error.Message}"); }
                continue;
            }
            foreach (var run in component.Runs)
            {
                try
                {
                    Sprite2D sprite;
                    if (run.Horizontal) sprite = Registered(manifest.Horizontal, "horizontal", texture);
                    else
                    {
                        var id = Vertical(run);
                        var cell = environment.ResolveCell(id);
                        sprite = environment.CreateSprite(id);
                        // Legacy vertical decks lie about their canvas center, not their standing anchor.
                        sprite.Offset = new Vector2(-cell.CellWidth / 2f, -cell.CellHeight / 2f) * cell.PixelDensity;
                    }
                    sprite.Name = $"Bridge_{run.Start.X}_{run.Start.Y}";
                    sprite.Position = run.Pivot;
                    sprite.SetMeta("run_length", run.Length);
                    sprite.SetMeta("axis", run.Horizontal ? "horizontal" : "vertical");
                    parent.AddChild(sprite);
                }
                catch (MissingArtResourceException error) { diagnostics.Add($"Bridge ({run.Start.X},{run.Start.Y}): {error.Message}"); }
            }
        }
    }

    private static Sprite2D Registered(RegisteredCrossing source, string orientation,
        Func<string, Texture2D> texture, double[][]? cuts = null)
    {
        var size = RasterResources.Pair(source.SourceSize, $"crossing canvas {source.Id}");
        var foot = RasterResources.Pair(source.SourceFoot, $"crossing registration {source.Id}");
        RasterResources.Positive(source.LogicalPerSourcePixel, $"crossing scale {source.Id}");
        if (source.SchemaVersion != 1 || source.Orientation != orientation || size.X <= 0 || size.Y <= 0
            || foot.X < 0 || foot.Y < 0 || foot.X > size.X || foot.Y > size.Y)
            throw new InvalidOperationException($"Invalid registered {orientation} crossing: {source.Id}");
        var image = texture(source.ResourcePath);
        var logicalSize = size * (float)source.LogicalPerSourcePixel;
        var anchor = foot * (float)source.LogicalPerSourcePixel;
        var scale = logicalSize / image.GetSize();
        var sprite = new Sprite2D { Texture = image, Centered = false, Scale = scale,
            Offset = -anchor / scale, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        if (cuts is not null)
        {
            if (cuts.Length != 2 || cuts.Any(cut => cut.Length != 4)) throw new InvalidOperationException("Junction requires two registered openings per axis.");
            var shader = new ShaderMaterial { Shader = new Shader { Code = CutoutShader } };
            shader.SetShaderParameter("logical_size", logicalSize);
            shader.SetShaderParameter("logical_anchor", anchor);
            shader.SetShaderParameter("first_cut", new Vector4((float)cuts[0][0], (float)cuts[0][1], (float)cuts[0][2], (float)cuts[0][3]));
            shader.SetShaderParameter("second_cut", new Vector4((float)cuts[1][0], (float)cuts[1][1], (float)cuts[1][2], (float)cuts[1][3]));
            sprite.Material = shader;
        }
        return sprite;
    }

    private static void ValidateJunction(CrossingManifest manifest)
    {
        var junction = manifest.Junction;
        if (junction.SchemaVersion != 1 || junction.CenterMask != 15 || junction.AxisLength != 3
            || junction.Horizontal.Id != manifest.Horizontal.Id || junction.Vertical.Id != manifest.Vertical.Id)
            throw new InvalidOperationException("Junction requires both registered sources and all four ports.");
        foreach (var (source, windows) in new[] { (manifest.Horizontal, junction.Horizontal.Cutouts), (manifest.Vertical, junction.Vertical.Cutouts) })
        {
            var size = RasterResources.Pair(source.SourceSize, "junction canvas") * (float)source.LogicalPerSourcePixel;
            var anchor = RasterResources.Pair(source.SourceFoot, "junction anchor") * (float)source.LogicalPerSourcePixel;
            var ports = new[] { Vector2.Zero, new Vector2(0, -48), new Vector2(48, 0), new Vector2(0, 48), new Vector2(-48, 0) };
            if (windows.Length != 2) throw new InvalidOperationException("Junction requires two rail windows per axis.");
            foreach (var window in windows)
            {
                if (window.Length != 4 || window.Any(value => !double.IsFinite(value))) throw new InvalidOperationException("Invalid junction window.");
                var rect = new Rect2((float)window[0], (float)window[1], (float)window[2], (float)window[3]);
                if (rect.Size.X <= 0 || rect.Size.Y <= 0 || rect.Position.X < -anchor.X || rect.Position.Y < -anchor.Y
                    || rect.End.X > size.X - anchor.X || rect.End.Y > size.Y - anchor.Y
                    || ports.Any(port => port.X >= rect.Position.X && port.Y >= rect.Position.Y && port.X <= rect.End.X && port.Y <= rect.End.Y))
                    throw new InvalidOperationException("Junction windows must preserve source canvas, center and outer ports.");
            }
        }
        var north = junction.Horizontal.Cutouts[0]; var south = junction.Horizontal.Cutouts[1];
        var west = junction.Vertical.Cutouts[0]; var east = junction.Vertical.Cutouts[1];
        if (north[0] != south[0] || north[2] != south[2] || north[0] >= 0 || north[0] + north[2] <= 0
            || north[1] + north[3] >= 0 || south[1] <= 0 || west[0] + west[2] >= 0 || east[0] <= 0
            || west[1] != east[1] || west[3] != east[3] || west[1] != north[1] + north[3]
            || west[1] + west[3] != south[1] || north[0] > west[0] + west[2] || north[0] + north[2] < east[0]
            || north[0] < -32 || north[0] + north[2] > 32 || west[0] < -32 || east[0] + east[2] > 32 || west[3] >= 32)
            throw new InvalidOperationException("Junction windows must be complementary local parapet openings.");
    }
}
