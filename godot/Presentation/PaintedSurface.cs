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
    private float _worldScale;
    private readonly List<Polygon2D> _polygons = new();

    internal static PaintedSurface Create(BoardProjection projection, Texture2D borrowedTexture, float worldScale)
    {
        var surface = new PaintedSurface
        {
            Name = "RegisteredSurface",
            _projection = projection,
            _texture = borrowedTexture,
            _worldScale = worldScale
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
            foreach (var group in _projection.Faces.SelectMany(_projection.FacePatches).GroupBy(patch => patch.Depth))
                Add(group.Key, group.Select(patch => patch.Vertices));

            void Add(int depth, IEnumerable<IReadOnlyList<BoardSurfaceVertex>> patches)
            {
                var points = new List<Vector2>();
                var indices = new Godot.Collections.Array();
                foreach (var patch in patches)
                {
                    indices.Add(Enumerable.Range(points.Count, patch.Count).ToArray());
                    points.AddRange(patch.Select(vertex => vertex.Projected));
                }
                var polygon = new Polygon2D
                {
                    Name = $"PaintedStrip_{replacements.Count}",
                    ZAsRelative = false,
                    Texture = _texture,
                    Polygon = points.ToArray(),
                    Polygons = indices,
                    UV = points.Select(point => point / _worldScale).ToArray(),
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
        Clear();
    }
}
