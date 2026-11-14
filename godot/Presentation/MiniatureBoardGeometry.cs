#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Empire.Core.Geometry;

namespace Empire.Presentation;

/// <summary>3D encoding of the existing surface owner; no independent corner-height or walkability model.</summary>
public sealed class MiniatureBoardGeometry
{
    public const float ElevationStep = .75f;
    public BoardProjection Projection
    {
        get;
    }
    public TriangleMesh SurfaceTree
    {
        get;
    }
    public TriangleMesh? CliffTree
    {
        get;
    }
    public IReadOnlyList<(BoardCell Cell, Vector3 A, Vector3 B, Vector3 C)> Surface
    {
        get;
    }
    public IReadOnlyList<(Vector3 A, Vector3 B, Vector3 C)> Cliffs
    {
        get;
    }

    public MiniatureBoardGeometry(BoardProjection projection)
    {
        Projection = projection;
        Surface = projection.Triangles.Select(t => (new BoardCell(t.Cell.X, t.Cell.Y),
            Point(t.LogicalA, t.ProjectedA), Point(t.LogicalB, t.ProjectedB), Point(t.LogicalC, t.ProjectedC))).ToArray();
        var cliffs = new List<(Vector3, Vector3, Vector3)>();
        foreach (var face in projection.Faces)
        {
            var a = Point(face.LogicalA, face.ProjectedA);
            var b = Point(face.LogicalB, face.ProjectedB);
            var c = Point(face.LogicalB, face.ProjectedC);
            var d = Point(face.LogicalA, face.ProjectedD);
            cliffs.Add((a, b, c));
            cliffs.Add((a, c, d));
        }
        Cliffs = cliffs;
        SurfaceTree = CreateTree(Surface.SelectMany(t => new[] { t.A, t.B, t.C }).ToArray());
        CliffTree = cliffs.Count == 0 ? null : CreateTree(cliffs.SelectMany(t => new[] { t.Item1, t.Item2, t.Item3 }).ToArray());
    }

    public IReadOnlyList<(Vector3 A, Vector3 B, Vector3 C)> OutsideSkirt()
    {
        var edges = new Dictionary<(Vector2, Vector2), (Vector3 A, Vector3 B, int Count)>();
        foreach (var triangle in Surface)
            foreach (var (a, b) in new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) })
            {
                var first = new Vector2(a.X, a.Z);
                var second = new Vector2(b.X, b.Z);
                var key = first.X < second.X || first.X == second.X && first.Y <= second.Y ? (first, second) : (second, first);
                edges[key] = edges.TryGetValue(key, out var edge) ? (edge.A, edge.B, edge.Count + 1) : (a, b, 1);
            }
        var result = new List<(Vector3, Vector3, Vector3)>();
        foreach (var (a, b, count) in edges.Values)
        {
            if (count != 1)
                continue;
            var belowA = new Vector3(a.X, -.65f, a.Z);
            var belowB = new Vector3(b.X, -.65f, b.Z);
            result.Add((a, b, belowB));
            result.Add((a, belowB, belowA));
        }
        return result;
    }

    public static TriangleMesh CreateTree(Vector3[] faces)
    {
        var tree = new TriangleMesh();
        if (!tree.CreateFromFaces(faces))
            throw new InvalidOperationException("Cannot build miniature geometric picking BVH.");
        return tree;
    }

    public Vector3 Point(Vector2 logical, Vector2 projected) => new(logical.X / BoardProjection.TileSize,
        Projection.SurfaceHeight(logical, projected) / BoardProjection.HeightStep * ElevationStep,
        logical.Y / BoardProjection.TileSize);

    public Vector3 Contact(BoardCell cell)
    {
        var coord = new Coord(cell.X, cell.Y);
        return Point(Projection.LogicalCellContact(coord), Projection.CellContact(coord));
    }

    public static float? Intersect(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        var hit = Geometry3D.RayIntersectsTriangle(origin, direction, a, b, c);
        return hit.VariantType == Variant.Type.Vector3 ? origin.DistanceTo(hit.AsVector3()) : null;
    }

    public static ArrayMesh Mesh(IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> triangles)
    {
        var surface = new SurfaceTool();
        surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var count = 0;
        foreach (var (a, b, c) in triangles)
        {
            surface.AddVertex(a);
            surface.AddVertex(c);
            surface.AddVertex(b);
            count++;
        }
        if (count == 0)
            throw new ArgumentException("A miniature surface requires triangles.", nameof(triangles));
        surface.GenerateNormals();
        return surface.Commit();
    }
}
