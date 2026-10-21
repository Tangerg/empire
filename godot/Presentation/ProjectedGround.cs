#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Empire.Core.Geometry;

namespace Empire.Presentation;

public sealed record TerrainDepthEntry(Node2D Node, Vector2 LogicalContact, int Layer, Sprite2D? GroundSprite = null);

/// <summary>Owns the logical ground viewport; projected polygons only borrow its texture.</summary>
internal sealed record CliffSurfaceMaterial(Texture2D Texture, Vector2 Period, IReadOnlySet<Coord> FieldTopCells);
internal sealed record GroundSurfaceRules(BoardProjection Projection, CliffSurfaceMaterial? Cliff);

internal partial class ProjectedGround : Node2D
{
    private SubViewport? _viewport;
    private readonly List<Polygon2D> _surfaces = new();
    private BoardProjection? _projection;
    private CliffSurfaceMaterial? _cliff;
    private readonly Dictionary<(Vector2 Logical, Vector2 Projected), Color> _shades = new();

    internal static ProjectedGround Create(GroundSurfaceRules rules, Node2D logicalGround, Vector2I size)
    {
        var projection = rules.Projection;
        var result = new ProjectedGround { Name = "ProjectedGround", _cliff = rules.Cliff, _projection = projection };
        try
        {
            const int density = 2;
            result._viewport = new SubViewport
            {
                Name = "LogicalGround",
                Disable3D = true,
                TransparentBg = true,
                Size = size * (32 * density),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
                RenderTargetClearMode = SubViewport.ClearMode.Always,
            };
            result.AddChild(result._viewport);
            logicalGround.Scale = Vector2.One * density;
            result._viewport.AddChild(logicalGround);
            var normals = new Dictionary<(Vector2 Logical, Vector2 Projected), Vector3>();
            foreach (var triangle in projection.Triangles)
            {
                var a = result.SurfacePoint(triangle.LogicalA, triangle.ProjectedA);
                var b = result.SurfacePoint(triangle.LogicalB, triangle.ProjectedB);
                var c = result.SurfacePoint(triangle.LogicalC, triangle.ProjectedC);
                var normal = (b - a).Cross(c - a);
                if (normal.Z < 0)
                    normal = -normal;
                Accumulate(triangle.LogicalA, triangle.ProjectedA, normal);
                Accumulate(triangle.LogicalB, triangle.ProjectedB, normal);
                Accumulate(triangle.LogicalC, triangle.ProjectedC, normal);
            }
            void Accumulate(Vector2 logical, Vector2 projected, Vector3 normal)
            {
                var key = (logical, projected);
                normals[key] = normals.GetValueOrDefault(key) + normal;
            }
            var light = new Vector3(-0.4f, -0.6f, 1).Normalized();
            Color Shade(Vector2 logical, Vector2 projected)
            {
                var normal = normals[(logical, projected)].Normalized();
                var strength = Math.Clamp(normal.Dot(light) / light.Z, 0.70f, 1.15f);
                return new Color(strength, strength, strength);
            }
            foreach (var key in normals.Keys)
                result._shades.Add(key, Shade(key.Logical, key.Projected));
            result._projection = projection;
            projection.DepthPartitionsChanged += result.RebuildPartitions;
            result.RebuildPartitions();
            return result;
        }
        catch
        {
            result.Free();
            if (GodotObject.IsInstanceValid(logicalGround) && logicalGround.GetParent() is null)
                logicalGround.Free();
            throw;
        }
    }

    private void RebuildPartitions()
    {
        if (_projection is not { } projection || _viewport is null)
            return;
        var replacements = new List<Polygon2D>();
        try
        {
            const int density = 2;
            var texture = _viewport.GetTexture();
            var stripIndex = 0;
            foreach (var strip in projection.DepthPartitions)
            {
                var vertices = new List<Vector2>();
                var uv = new List<Vector2>();
                var colors = new List<Color>();
                var indices = new Godot.Collections.Array();
                foreach (var patch in strip.Patches)
                {
                    var triangle = patch.Source;
                    var shadeA = _shades[(triangle.LogicalA, triangle.ProjectedA)];
                    var shadeB = _shades[(triangle.LogicalB, triangle.ProjectedB)];
                    var shadeC = _shades[(triangle.LogicalC, triangle.ProjectedC)];
                    var polygonIndices = new int[patch.Vertices.Count];
                    for (var index = 0; index < patch.Vertices.Count; index++)
                    {
                        var vertex = patch.Vertices[index];
                        polygonIndices[index] = vertices.Count;
                        vertices.Add(vertex.Projected);
                        uv.Add(vertex.Logical * density);
                        colors.Add(shadeA * vertex.SourceWeights.X
                            + shadeB * vertex.SourceWeights.Y + shadeC * vertex.SourceWeights.Z);
                    }
                    indices.Add(polygonIndices);
                }
                var polygon = new Polygon2D { Name = $"GroundStrip_{stripIndex++}", ZAsRelative = false };
                replacements.Add(polygon);
                polygon.Polygon = vertices.ToArray();
                polygon.Polygons = indices;
                polygon.UV = uv.ToArray();
                polygon.VertexColors = colors.ToArray();
                polygon.Texture = texture;
                polygon.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
                polygon.ZIndex = projection.LayerAtDepth(strip.StartDepth, BoardDepthLayer.Surface);
            }
            if (_cliff is { } cliff)
                foreach (var group in projection.Faces.SelectMany(face => CliffLayers(face, cliff.FieldTopCells.Contains(face.HigherCell))
                    .SelectMany(layer => projection.FacePatches(layer.Geometry)
                        .Select(patch => (Face: face, Patch: patch, layer.Tint)))).GroupBy(item => item.Patch.Depth))
                {
                    var vertices = new List<Vector2>();
                    var uv = new List<Vector2>();
                    var indices = new Godot.Collections.Array();
                    var colors = new List<Color>();
                    foreach (var (face, patch, tint) in group)
                    {
                        indices.Add(Enumerable.Range(vertices.Count, patch.Vertices.Count).ToArray());
                        vertices.AddRange(patch.Vertices.Select(vertex => vertex.Projected));
                        colors.AddRange(Enumerable.Repeat(tint, patch.Vertices.Count));
                        var edge = face.LogicalB - face.LogicalA;
                        uv.AddRange(patch.Vertices.Select(vertex =>
                        {
                            var distance = (vertex.Logical - face.LogicalA).Dot(edge.Normalized());
                            var top = face.ProjectedA.Lerp(face.ProjectedB, distance / edge.Length());
                            return new Vector2(distance, vertex.Projected.Y - top.Y) / cliff.Period * cliff.Texture.GetSize();
                        }));
                    }
                    var polygon = new Polygon2D { Name = $"CliffStrip_{stripIndex++}", ZAsRelative = false };
                    replacements.Add(polygon);
                    polygon.Polygon = vertices.ToArray();
                    polygon.Polygons = indices;
                    polygon.UV = uv.ToArray();
                    polygon.Texture = cliff.Texture;
                    polygon.VertexColors = colors.ToArray();
                    polygon.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
                    polygon.ZIndex = projection.LayerAtDepth(group.Key, BoardDepthLayer.Surface);
                }

        }
        catch
        {
            foreach (var surface in replacements)
            {
                surface.Texture = null;
                surface.Free();
            }
            throw;
        }
        ClearSurfaces();
        foreach (var surface in replacements)
        {
            AddChild(surface);
            _surfaces.Add(surface);
        }
    }

    private static IEnumerable<(BoardSurfaceFace Geometry, Color Tint)> CliffLayers(BoardSurfaceFace face, bool fieldTop)
    {
        var normal = new Vector2(face.LowerCell.X - face.HigherCell.X,
            face.LowerCell.Y - face.HigherCell.Y).Normalized();
        var light = new Vector2(-0.4f, -0.6f).Normalized();
        var brightness = 0.88f + 0.16f * normal.Dot(light);
        if (!fieldTop)
        {
            yield return (face, new Color(brightness, brightness, brightness));
            yield break;
        }
        var grassA = face.ProjectedA + Vector2.Down * Math.Min(1.2f, face.ProjectedD.Y - face.ProjectedA.Y);
        var grassB = face.ProjectedB + Vector2.Down * Math.Min(1.2f, face.ProjectedC.Y - face.ProjectedB.Y);
        var soilA = face.ProjectedA + Vector2.Down * Math.Min(3f, face.ProjectedD.Y - face.ProjectedA.Y);
        var soilB = face.ProjectedB + Vector2.Down * Math.Min(3f, face.ProjectedC.Y - face.ProjectedB.Y);
        yield return (face with
        {
            ProjectedA = soilA,
            ProjectedB = soilB
        },
            new Color(brightness, brightness, brightness));
        yield return (face with
        {
            ProjectedA = grassA,
            ProjectedB = grassB,
            ProjectedC = soilB,
            ProjectedD = soilA
        },
            new Color(0.65f, 0.49f, 0.30f));
        yield return (face with
        {
            ProjectedC = grassB,
            ProjectedD = grassA
        },
            new Color(0.75f, 0.83f, 0.43f));
    }

    private void ClearSurfaces()
    {
        foreach (var surface in _surfaces)
            if (GodotObject.IsInstanceValid(surface))
            {
                surface.Texture = null;
                RemoveChild(surface);
                surface.Free();
            }
        _surfaces.Clear();
    }

    private Vector3 SurfacePoint(Vector2 logical, Vector2 projected) =>
        new(logical.X, logical.Y, _projection!.SurfaceHeight(logical, projected));

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete)
            return;
        if (_projection is not null)
            _projection.DepthPartitionsChanged -= RebuildPartitions;
        _projection = null;
        ClearSurfaces();
        _shades.Clear();
        if (GodotObject.IsInstanceValid(_viewport))
            _viewport!.Free();
        _viewport = null;
    }
}
