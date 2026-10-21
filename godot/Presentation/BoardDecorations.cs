#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

public sealed class BoardDecorationDocument
{
    public int Schema
    {
        get; init;
    }
    public int TileSize
    {
        get; init;
    }
    public Dictionary<string, string> Spots { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Rings { get; init; } = new(StringComparer.Ordinal);
    public DecorationDrop ActionDrop { get; init; } = new();
    public DecorationDrop RouteDrop { get; init; } = new();
    public float MarkerOpacity
    {
        get; init;
    }
    public string PathTemplate { get; init; } = "";
    public string ArrowTemplate { get; init; } = "";
    public static BoardDecorationDocument Load() => PresentationJson.Load<BoardDecorationDocument>("res://data/board-decorations.json");
}
public sealed class DecorationDrop
{
    public float X
    {
        get; init;
    }
    public float Y
    {
        get; init;
    }
    public float Sigma
    {
        get; init;
    }
    public string Color { get; init; } = "";
    public float Opacity
    {
        get; init;
    }
    internal SpriteShadows.Drop ToDrop() => new(new(X, Y), Sigma, new Color(Color, Opacity));
}

/// <summary>Exact source markup plus bounded native resources. No gameplay facts are derived here.</summary>
internal sealed class BoardDecorations : IDisposable
{
    internal sealed record Drawing(ImageTexture Texture, Rect2 Bounds, float Opacity);
    private readonly BoardDecorationDocument document;
    private readonly Dictionary<string, Drawing> staticDrawings = new(StringComparer.Ordinal);
    private Drawing? pathDrawing;
    private string? pathKey;
    private bool disposed;
    internal int TextureCount => staticDrawings.Count + (pathDrawing is null ? 0 : 1);
    internal BoardDecorations(BoardDecorationDocument document)
    {
        if (document.Schema != 1 || document.TileSize != 32)
            throw new InvalidOperationException("Unsupported source board decoration contract.");
        this.document = document;
    }

    private readonly record struct SurfaceVertex(Vector2 Logical, Vector2 Projected);

    internal static void DrawProjected(Node2D canvas, Drawing drawing, Vector2 offset, IReadOnlyList<BoardSurfacePatch> patches)
    {
        var bounds = new Rect2(drawing.Bounds.Position + offset, drawing.Bounds.Size);
        foreach (var patch in patches)
        {
            var logicalBounds = new Rect2(patch.Vertices[0].Logical, Vector2.Zero);
            foreach (var vertex in patch.Vertices)
                logicalBounds = logicalBounds.Expand(vertex.Logical);
            if (!logicalBounds.Intersects(bounds))
                continue;
            var polygon = patch.Vertices.Select(vertex => new SurfaceVertex(vertex.Logical, vertex.Projected)).ToList();
            polygon = Clip(polygon, point => point.X - bounds.Position.X);
            polygon = Clip(polygon, point => bounds.End.X - point.X);
            polygon = Clip(polygon, point => point.Y - bounds.Position.Y);
            polygon = Clip(polygon, point => bounds.End.Y - point.Y);
            polygon = polygon.Distinct().ToList();
            if (polygon.Count < 3)
                continue;
            var area = 0f;
            for (var index = 0; index < polygon.Count; index++)
            {
                var a = polygon[index].Projected;
                var b = polygon[(index + 1) % polygon.Count].Projected;
                area += a.X * b.Y - a.Y * b.X;
            }
            if (Math.Abs(area) <= 0.0001f)
                continue;
            // DrawPolygon takes normalized UVs (unlike Polygon2D's pixel UVs).
            // Clipping both positions together keeps cliff seams on their own side.
            canvas.DrawPolygon(polygon.Select(vertex => vertex.Projected).ToArray(),
                new[] { new Color(1, 1, 1, drawing.Opacity) },
                polygon.Select(vertex => (vertex.Logical - bounds.Position) / bounds.Size).ToArray(), drawing.Texture);
        }
    }

    private static List<SurfaceVertex> Clip(List<SurfaceVertex> polygon, Func<Vector2, float> signedDistance)
    {
        var output = new List<SurfaceVertex>();
        if (polygon.Count == 0)
            return output;
        var previous = polygon[^1];
        var previousDistance = signedDistance(previous.Logical);
        foreach (var current in polygon)
        {
            var distance = signedDistance(current.Logical);
            if ((distance > 0 && previousDistance < 0) || (distance < 0 && previousDistance > 0))
            {
                var fraction = previousDistance / (previousDistance - distance);
                output.Add(new SurfaceVertex(previous.Logical.Lerp(current.Logical, fraction),
                    previous.Projected.Lerp(current.Projected, fraction)));
            }
            if (distance >= 0)
                output.Add(current);
            previous = current;
            previousDistance = distance;
        }
        return output;
    }

    internal Drawing Spot(string role) => Static("spot:" + role, document.Spots[role]);
    internal Drawing Ring(string role) => Static("ring:" + role, document.Rings[role]);
    private Drawing Static(string key, string markup)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (staticDrawings.TryGetValue(key, out var cached))
            return cached;
        // Element opacity is a post-filter group operation; fill/stroke opacity is not.
        var xml = new XmlDocument { XmlResolver = null };
        xml.LoadXml(markup);
        var element = xml.DocumentElement!;
        var opacity = element.HasAttribute("opacity") ? float.Parse(element.GetAttribute("opacity"), CultureInfo.InvariantCulture) : 1f;
        element.RemoveAttribute("opacity");
        var value = Raster(element.OuterXml, new(-8, -8, 48, 48), document.ActionDrop, opacity);
        staticDrawings.Add(key, value);
        return value;
    }

    internal Drawing? Path(IReadOnlyList<BoardCell> points)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var key = string.Join(";", points.Select(point => $"{point.X},{point.Y}"));
        if (key == pathKey)
            return pathDrawing;
        pathDrawing?.Texture.Dispose();
        pathDrawing = null;
        pathKey = key;
        if (points.Count < 2)
            return null;
        var path = PathData(points);
        var end = points[^1];
        var previous = points[^2];
        var angle = Math.Atan2(end.Y - previous.Y, end.X - previous.X) * 180 / Math.PI;
        var arrow = document.ArrowTemplate.Replace("{angle}", Number(angle), StringComparison.Ordinal);
        var markup = document.PathTemplate.Replace("{path}", path, StringComparison.Ordinal)
            + $"<g transform=\"translate({end.X * 32} {end.Y * 32})\">{arrow}</g>";
        // Cubic controls can extend beyond the node bounds. Include each control,
        // then the source's widest stroke and terminal arrow.
        var positions = ControlPoints(points).ToArray();
        var min = new Vector2(positions.Min(point => point.X), positions.Min(point => point.Y)) - Vector2.One * 8;
        var max = new Vector2(positions.Max(point => point.X), positions.Max(point => point.Y)) + Vector2.One * 8;
        pathDrawing = Raster(markup, new(min, max - min), null, 1);
        return pathDrawing;
    }

    internal static string PathData(IReadOnlyList<BoardCell> points)
    {
        if (points.Count == 0)
            throw new ArgumentException("A source path needs an origin.");
        var spots = points.Select(point => new Vector2(point.X * 32 + 16, point.Y * 32 + 16)).ToArray();
        var value = new StringBuilder($"M{Number(spots[0].X)} {Number(spots[0].Y)}");
        for (var index = 0; index < spots.Length - 1; index++)
        {
            var p0 = spots[Math.Max(0, index - 1)];
            var p1 = spots[index];
            var p2 = spots[index + 1];
            var p3 = spots[Math.Min(spots.Length - 1, index + 2)];
            value.Append($" C{Fixed(p1.X + (p2.X - p0.X) / 6d)} {Fixed(p1.Y + (p2.Y - p0.Y) / 6d)} {Fixed(p2.X - (p3.X - p1.X) / 6d)} {Fixed(p2.Y - (p3.Y - p1.Y) / 6d)} {Number(p2.X)} {Number(p2.Y)}");
        }
        return value.ToString();
    }
    private static IEnumerable<Vector2> ControlPoints(IReadOnlyList<BoardCell> points)
    {
        var spots = points.Select(point => new Vector2(point.X * 32 + 16, point.Y * 32 + 16)).ToArray();
        foreach (var point in spots)
            yield return point;
        for (var index = 0; index < spots.Length - 1; index++)
        {
            yield return spots[index] + (spots[index + 1] - spots[Math.Max(0, index - 1)]) / 6;
            yield return spots[index + 1] - (spots[Math.Min(spots.Length - 1, index + 2)] - spots[index]) / 6;
        }
    }
    private static string Number(double number) => EcmaJson.Stringify(JsonValue.Create(number));
    private static string Fixed(double number) => number.ToString("F1", CultureInfo.InvariantCulture);

    private static Drawing Raster(string markup, Rect2 bounds, DecorationDrop? drop, float opacity)
    {
        const float density = 2;
        using var source = new Image();
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Number(bounds.Size.X)}\" height=\"{Number(bounds.Size.Y)}\" viewBox=\"{Number(bounds.Position.X)} {Number(bounds.Position.Y)} {Number(bounds.Size.X)} {Number(bounds.Size.Y)}\">{markup}</svg>";
        var error = source.LoadSvgFromString(svg, density);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Source decoration SVG failed: {error}");
        if (drop is null)
            return new(ImageTexture.CreateFromImage(source), bounds, opacity);
        var filtered = ShadowRaster.Filter(source, new[] { drop.ToDrop() }, density);
        using (filtered.Image)
        {
            var padding = filtered.Padding / density;
            return new(ImageTexture.CreateFromImage(filtered.Image), bounds.Grow(padding), opacity);
        }
    }
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        foreach (var value in staticDrawings.Values)
            value.Texture.Dispose();
        staticDrawings.Clear();
        pathDrawing?.Texture.Dispose();
        pathDrawing = null;
        pathKey = null;
    }
}
