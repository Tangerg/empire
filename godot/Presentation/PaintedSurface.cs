#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Depth-composites registered source pixels without changing their screen positions.</summary>
internal partial class PaintedSurface : Node2D
{
    private BoardProjection _projection = null!;
    private Texture2D _texture = null!;
    private Transform2D _textureToBoard;
    private Vector2[]? _bounds;
    private Image? _hitImage;
    private readonly List<Polygon2D> _polygons = new();

    internal static PaintedSurface Create(BoardProjection projection, Texture2D borrowedTexture, float worldScale)
    {
        return Create(projection, borrowedTexture, new Transform2D(0, Vector2.One * worldScale, 0, Vector2.Zero), null);
    }

    internal static PaintedSurface Create(BoardProjection projection, Sprite2D source, Vector2 contact)
    {
        if (source.Texture is null || source.RegionEnabled || source.Hframes != 1 || source.Vframes != 1
            || source.FlipH || source.FlipV
            || source.Texture is AtlasTexture atlas && (atlas.Atlas is null || atlas.Atlas is AtlasTexture
                || atlas.Region != new Rect2(Vector2.Zero, atlas.Atlas.GetSize()) || atlas.Margin != default))
            throw new InvalidOperationException("Bounded surfaces require a complete, unflipped single-frame texture; sprite regions and cropped atlas frames are unsupported.");
        var transform = new Transform2D(source.Rotation, source.Scale, source.Skew, contact + source.Position)
            * new Transform2D(0, source.GetRect().Position);
        var size = source.Texture.GetSize();
        var bounds = new[] { Vector2.Zero, new Vector2(size.X, 0), size, new Vector2(0, size.Y) }
            .Select(point => transform * point).ToArray();
        var surface = Create(projection, source.Texture, transform, bounds);
        surface.Position = -contact;
        surface.TextureFilter = source.TextureFilter;
        surface.Modulate = source.Modulate;
        surface.SelfModulate = source.SelfModulate;
        return surface;
    }

    private static PaintedSurface Create(BoardProjection projection, Texture2D borrowedTexture,
        Transform2D textureToBoard, Vector2[]? bounds)
    {
        var surface = new PaintedSurface
        {
            Name = "RegisteredSurface",
            _projection = projection,
            _texture = borrowedTexture,
            _textureToBoard = textureToBoard,
            _bounds = bounds
        };
        try
        {
            projection.DepthPartitionsChanged += surface.Rebuild;
            surface.Rebuild();
            return surface;
        }
        catch
        {
            surface.Free();
            throw;
        }
    }

    private void Rebuild()
    {
        var replacements = new List<Polygon2D>();
        try
        {
            foreach (var strip in _projection.DepthPartitions)
                Add(strip.StartDepth, strip.Patches.Select(patch => patch.Vertices));
            // Local floor decals cover registered ground tops, not vertical cliff faces.
            if (_bounds is null)
                foreach (var group in _projection.Faces.SelectMany(_projection.FacePatches).GroupBy(patch => patch.Depth))
                    Add(group.Key, group.Select(patch => patch.Vertices));

            void Add(int depth, IEnumerable<IReadOnlyList<BoardSurfaceVertex>> patches)
            {
                var points = new List<Vector2>();
                var indices = new Godot.Collections.Array();
                foreach (var patch in patches)
                {
                    var projected = patch.Select(vertex => vertex.Projected).ToArray();
                    var clipped = _bounds is null ? new[] { projected }
                        : Geometry2D.IntersectPolygons(projected, _bounds).ToArray();
                    foreach (var piece in clipped)
                    {
                        indices.Add(Enumerable.Range(points.Count, piece.Length).ToArray());
                        points.AddRange(piece);
                    }
                }
                if (points.Count == 0)
                    return;
                var boardToTexture = _textureToBoard.AffineInverse();
                var polygon = new Polygon2D
                {
                    Name = $"PaintedStrip_{replacements.Count}",
                    ZAsRelative = false,
                    Texture = _texture,
                    Polygon = points.ToArray(),
                    Polygons = indices,
                    UV = points.Select(point => boardToTexture * point).ToArray(),
                    ZIndex = _projection.LayerAtDepth(depth, BoardDepthLayer.Surface)
                };
                replacements.Add(polygon);
            }
        }
        catch
        {
            foreach (var polygon in replacements)
                polygon.Free();
            throw;
        }
        Clear();
        foreach (var polygon in replacements)
        {
            AddChild(polygon);
            _polygons.Add(polygon);
        }
    }

    internal Polygon2D? HitPolygon(Vector2 globalPoint)
    {
        if (!IsVisibleInTree() || Modulate.A <= 0 || SelfModulate.A <= 0)
            return null;
        var local = ToLocal(globalPoint);
        var uv = _textureToBoard.AffineInverse() * local;
        if (uv.X < 0 || uv.Y < 0 || uv.X >= _texture.GetWidth() || uv.Y >= _texture.GetHeight())
            return null;
        if (_hitImage is null)
        {
            _hitImage = _texture.GetImage();
            if (_hitImage.IsCompressed() && _hitImage.Decompress() != Error.Ok)
                throw new InvalidOperationException("Surface texture alpha could not be decoded for picking.");
        }
        if (_hitImage.GetPixel((int)uv.X, (int)uv.Y).A <= .1f)
            return null;
        Polygon2D? front = null;
        foreach (var polygon in _polygons)
            if (polygon.IsVisibleInTree() && (front is null || polygon.ZIndex >= front.ZIndex)
                && polygon.Polygons.Any(piece => Geometry2D.IsPointInPolygon(local,
                    piece.AsInt32Array().Select(index => polygon.Polygon[index]).ToArray())))
                front = polygon;
        return front;
    }

    private void Clear()
    {
        foreach (var polygon in _polygons)
        {
            RemoveChild(polygon);
            polygon.Free();
        }
        _polygons.Clear();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete)
            return;
        _projection.DepthPartitionsChanged -= Rebuild;
        _hitImage?.Dispose();
        Clear();
    }
}
