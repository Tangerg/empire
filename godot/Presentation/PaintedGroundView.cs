#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
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
    private readonly List<(Polygon2D Node, Coord Cell, Vector2[] Pixels, Vector2 Front, Coord? Selection)> _standing = new();
    private readonly List<TerrainDepthEntry> _depthEntries = new();
    private readonly HashSet<Coord> _mapStructureBodies = new();
    public override IReadOnlyList<TerrainDepthEntry> DepthEntries => _depthEntries;
    public override bool OwnsMapStructureBody(Coord cell) => _mapStructureBodies.Contains(cell);

    public static PaintedGroundView Create(PaintedGroundDocument document, Texture2D borrowedTexture, JsonObject registeredMap)
    {
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
            var width = registeredMap["width"]!.GetValue<int>();
            var height = registeredMap["height"]!.GetValue<int>();
            Coord Cell(int[] value)
            {
                if (value is not { Length: 2 } || value[0] < 0 || value[1] < 0 || value[0] >= width || value[1] >= height)
                    throw new InvalidOperationException("Painted occluders require a canonical cell reference.");
                return new(value[0], value[1]);
            }
            foreach (var layer in document.Occluders)
            {
                if (string.IsNullOrWhiteSpace(layer.Id) || !identities.Add(layer.Id))
                    throw new InvalidOperationException("Painted occluders require unique identities.");
                var cell = Cell(layer.ContactCell);
                Coord? selection = layer.SelectionCell is null ? null : Cell(layer.SelectionCell);
                if (layer.OwnsMapStructureBody && (selection is not { } structureCell || !view._mapStructureBodies.Add(structureCell)))
                    throw new InvalidOperationException($"Painted map-structure bodies require unique selection cells: {layer.Id}.");
                var front = SpriteGroundContact.Read(layer.GroundFrontPixels, document.PixelSize[0], document.PixelSize[1], layer.Id);
                if (layer.PolygonPixels is null || layer.PolygonPixels.Length < 3)
                    throw new InvalidOperationException($"Painted occluder polygon is absent: {layer.Id}.");
                var pixels = layer.PolygonPixels.Select(point => SpriteGroundContact.Read(point,
                    document.PixelSize[0], document.PixelSize[1], layer.Id)).ToArray();
                ValidatePolygon(pixels, layer.Id);
                var polygon = new Polygon2D
                {
                    Name = layer.Id,
                    Texture = borrowedTexture,
                    UV = pixels,
                    Polygon = pixels,
                    Scale = Vector2.One * document.WorldScale
                };
                view.AddChild(polygon);
                view._standing.Add((polygon, cell, pixels, front, selection));
            }
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
        for (var first = 0; first < points.Length; first++)
            for (var second = first + 1; second < points.Length; second++)
            {
                var firstEnd = (first + 1) % points.Length;
                var secondEnd = (second + 1) % points.Length;
                if (firstEnd == second || secondEnd == first)
                    continue;
                if (Geometry2D.SegmentIntersectsSegment(points[first], points[firstEnd], points[second], points[secondEnd]).VariantType != Variant.Type.Nil)
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
        foreach (var (node, cell, pixels, front, selection) in _standing)
        {
            var contact = projection.CellContact(cell);
            var anchor = contact / _document.WorldScale;
            node.Position = contact;
            node.Polygon = pixels.Select(point => point - anchor).ToArray();
            _depthEntries.Add(new(node, projection.LogicalCellContact(cell), 2,
                new PolygonStandingVisual(node, front - anchor), selection));
        }
    }
}
