#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.State;
using Godot;

namespace Empire.Presentation;

public partial class PaintedGroundView : BoardGroundView
{
    private Node2D? _authored;
    private PaintedGroundDocument _document = null!;
    private JsonObject _registeredMap = null!;
    private Texture2D _texture = null!;
    private PaintedSurface? _surface;
    private BoardProjection? _projection;
    public override IReadOnlyList<string> Diagnostics => Array.Empty<string>();
    private readonly List<(Polygon2D Node, Coord Cell, Vector2[] Pixels, Vector2 Front, Vector2[]? Pointer, Coord? Selection)> _standing = new();
    private readonly List<TerrainDepthEntry> _depthEntries = new();
    private IReadOnlyList<GroundSite> _sites = Array.Empty<GroundSite>();
    public override IReadOnlyList<TerrainDepthEntry> DepthEntries => _depthEntries;
    public override IReadOnlyList<GroundSite> Sites => _sites;

    public static PaintedGroundView Create(PaintedGroundDocument document, Texture2D borrowedTexture,
        JsonObject registeredMap, ReadOnlyContent content)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Occluders is null)
            throw new InvalidOperationException("Painted ground requires explicit occluders.");
        ArgumentNullException.ThrowIfNull(borrowedTexture);
        ArgumentNullException.ThrowIfNull(registeredMap);
        ArgumentNullException.ThrowIfNull(content);
        var width = registeredMap["width"]!.GetValue<int>();
        var height = registeredMap["height"]!.GetValue<int>();
        var tiles = registeredMap["tiles"]!.AsArray();
        var elevation = registeredMap["elevation"]!.AsArray().Select(value => value!.GetValue<int>()).ToArray();
        if (width <= 0 || height <= 0 || tiles.Count != checked(width * height) || elevation.Length != tiles.Count)
            throw new InvalidOperationException("Painted ground requires canonical map dimensions, tiles and elevation.");
        document.CreatePlane(new Vector2(width, height) * BoardProjection.TileSize, elevation.Min(), elevation.Max());
        if (borrowedTexture.GetWidth() != document.PixelSize[0] || borrowedTexture.GetHeight() != document.PixelSize[1])
            throw new InvalidOperationException("Painted ground texture dimensions differ from its registration.");
        var view = new PaintedGroundView
        {
            Name = "PaintedGround",
            _document = document,
            _registeredMap = registeredMap.DeepClone().AsObject(),
            _texture = borrowedTexture
        };
        try
        {
            view.AddChild(new Sprite2D
            {
                Name = "EnvironmentPainting",
                Texture = borrowedTexture,
                Centered = false,
                Scale = Vector2.One * document.WorldScale,
                ZIndex = -4096
            });
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var siteCells = new HashSet<Coord>();
            var sites = new List<GroundSite>();
            Coord Cell(int[] value, string identity)
            {
                if (value is not { Length: 2 } || value[0] < 0 || value[1] < 0 || value[0] >= width || value[1] >= height)
                    throw new InvalidOperationException($"Painted object requires a canonical cell reference: {identity}.");
                return new(value[0], value[1]);
            }
            Vector2[] Polygon(double[][]? values, string identity)
            {
                if (values is not { Length: >= 3 })
                    throw new InvalidOperationException($"Painted polygon is absent: {identity}.");
                var points = values.Select(point => SpriteGroundContact.Read(point,
                    document.PixelSize[0], document.PixelSize[1], identity)).ToArray();
                ValidatePolygon(points, identity);
                return points;
            }
            foreach (var item in document.Occluders)
            {
                if (item is null || string.IsNullOrWhiteSpace(item.Id) || !identities.Add(item.Id))
                    throw new InvalidOperationException("Painted objects require unique identities.");
                if (item.Parts is not { Length: > 0 })
                    throw new InvalidOperationException($"Painted object requires explicit material parts: {item.Id}.");
                Coord? selection = item.SelectionCell is null ? null : Cell(item.SelectionCell, item.Id);
                if (item.OwnsMapStructureBody && selection is null)
                    throw new InvalidOperationException($"Painted body ownership requires a canonical site: {item.Id}.");
                if (selection is { } siteCell)
                {
                    var terrainId = tiles[siteCell.Y * width + siteCell.X]!.GetValue<string>();
                    var terrain = content.Definition("terrains", terrainId)
                        ?? throw new InvalidOperationException($"Missing canonical terrain for painted site: {item.Id}.");
                    if (!siteCells.Add(siteCell) || !terrain.GetProperty("capturable").GetBoolean()
                        || terrain.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "outside"))
                        throw new InvalidOperationException($"Painted site must reference a unique canonical capturable cell: {item.Id}.");
                    sites.Add(new(item.Id, siteCell, item.OwnsMapStructureBody));
                }
                var partIds = new HashSet<string>(StringComparer.Ordinal);
                var hasTarget = false;
                foreach (var part in item.Parts)
                {
                    if (part is null || string.IsNullOrWhiteSpace(part.Id) || !partIds.Add(part.Id))
                        throw new InvalidOperationException($"Painted parts require unique identities within object: {item.Id}.");
                    var identity = $"{item.Id}/{part.Id}";
                    var cell = Cell(part.ContactCell, identity);
                    var front = SpriteGroundContact.Read(part.GroundFrontPixels, document.PixelSize[0], document.PixelSize[1], identity);
                    var pixels = Polygon(part.PolygonPixels, identity);
                    if (part.Pointer is null)
                        throw new InvalidOperationException($"Painted part requires an explicit pointer contract: {identity}.");
                    Vector2[]? pointer = null;
                    Coord? target = null;
                    switch (part.Pointer.Kind)
                    {
                        case "target":
                        case "block":
                            pointer = Polygon(part.Pointer.PolygonPixels, $"{identity} pointer");
                            if (Geometry2D.ClipPolygons(pointer, pixels).Count != 0)
                                throw new InvalidOperationException($"Painted pointer contour extends beyond its material part: {identity}.");
                            if (part.Pointer.Kind == "target")
                            {
                                target = selection ?? throw new InvalidOperationException($"Painted target requires an object site: {identity}.");
                                hasTarget = true;
                            }
                            break;
                        case "pass-through":
                            if (part.Pointer.PolygonPixels is not null)
                                throw new InvalidOperationException($"Pass-through painted part must not declare a pointer polygon: {identity}.");
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown painted pointer kind: {identity}/{part.Pointer.Kind}.");
                    }
                    var polygon = new Polygon2D
                    {
                        Name = $"{item.Id}_{part.Id}",
                        Texture = borrowedTexture,
                        UV = pixels,
                        Polygon = pixels,
                        Scale = Vector2.One * document.WorldScale
                    };
                    view.AddChild(polygon);
                    view._standing.Add((polygon, cell, pixels, front, pointer, target));
                }
                if (selection is not null && !hasTarget)
                    throw new InvalidOperationException($"Painted site requires at least one explicit target part: {item.Id}.");
            }
            view._sites = sites.AsReadOnly();
            return view;
        }
        catch
        {
            view.Free();
            throw;
        }
    }

    private static void ValidatePolygon(Vector2[] points, string identity)
    {
        if (points.Distinct().Count() != points.Length || Geometry2D.TriangulatePolygon(points).Length == 0)
            throw new InvalidOperationException($"Painted occluder polygon is degenerate: {identity}.");
        static double Cross(Vector2 a, Vector2 b, Vector2 c) =>
            ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
        static bool OnSegment(Vector2 point, Vector2 a, Vector2 b) => Cross(a, b, point) == 0
            && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X)
            && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);
        for (var vertex = 0; vertex < points.Length; vertex++)
        {
            var previous = points[(vertex + points.Length - 1) % points.Length];
            var next = points[(vertex + 1) % points.Length];
            if (OnSegment(previous, points[vertex], next) || OnSegment(next, previous, points[vertex]))
                throw new InvalidOperationException($"Painted occluder polygon doubles back: {identity}.");
        }
        for (var first = 0; first < points.Length; first++)
            for (var second = first + 1; second < points.Length; second++)
            {
                var firstEnd = (first + 1) % points.Length;
                var secondEnd = (second + 1) % points.Length;
                if (firstEnd == second || secondEnd == first)
                    continue;
                if (Geometry2D.SegmentIntersectsSegment(points[first], points[firstEnd], points[second], points[secondEnd]).VariantType != Variant.Type.Nil
                    || OnSegment(points[first], points[second], points[secondEnd])
                    || OnSegment(points[firstEnd], points[second], points[secondEnd])
                    || OnSegment(points[second], points[first], points[firstEnd])
                    || OnSegment(points[secondEnd], points[first], points[firstEnd]))
                    throw new InvalidOperationException($"Painted occluder polygon crosses itself: {identity}.");
            }
    }

    public override void ConfigureAuthoredGround(Node2D authoredGround)
    {
        if (_authored is not null || authoredGround.GetParent() is not null || authoredGround.GetChildCount() != 0)
            throw new InvalidOperationException("Painted ground cannot silently absorb material-ground decorations.");
        AddChild(authoredGround);
        _authored = authoredGround;
    }

    public override Node2D DetachAuthoredGround()
    {
        var authored = _authored ?? throw new InvalidOperationException("No authored ground is attached.");
        RemoveChild(authored);
        _authored = null;
        return authored;
    }

    public override void Render(BoardProjection projection, string[] tiles, int[] elevation, Vector2I size)
    {
        if (_registeredMap["width"]!.GetValue<int>() != size.X || _registeredMap["height"]!.GetValue<int>() != size.Y
            || !_registeredMap["tiles"]!.AsArray().Select(EcmaJson.ReadString).SequenceEqual(tiles)
            || !_registeredMap["elevation"]!.AsArray().Select(value => value!.GetValue<int>()).SequenceEqual(elevation))
            throw new InvalidOperationException("Canonical terrain changed without a matching authored painted surface.");
        var plane = _document.CreatePlane(new Vector2(size.X, size.Y) * BoardProjection.TileSize,
            elevation.Min(), elevation.Max());
        if (!projection.MatchesPlane(plane) || !projection.MatchesTerrainGeometry(_registeredMap))
            throw new InvalidOperationException("Painted surface and tactical projection use different registration.");
        if (_projection != projection)
        {
            var surface = PaintedSurface.Create(projection, _texture, _document.WorldScale);
            if (_surface is not null)
            {
                RemoveChild(_surface);
                _surface.Free();
            }
            AddChild(surface);
            _surface = surface;
            _projection = projection;
        }
        _depthEntries.Clear();
        foreach (var (node, cell, pixels, front, pointer, selection) in _standing)
        {
            var contact = projection.CellContact(cell);
            var anchor = contact / _document.WorldScale;
            node.Position = contact;
            node.Polygon = pixels.Select(point => point - anchor).ToArray();
            _depthEntries.Add(new(node, projection.LogicalCellContact(cell), 2,
                new PolygonStandingVisual(node, front - anchor, pointer?.Select(point => point - anchor).ToArray()), selection));
        }
    }
}
