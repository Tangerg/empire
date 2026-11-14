using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Low planted detail projected onto supplied canonical forest cells; never a picking or movement owner.</summary>
public static class MiniatureOrchard
{
    public const int TrianglesPerCell = 40;
    public const int MaximumTriangles = 1200;
    private static readonly Color Soil = new("#555b46");
    private static readonly Color Leaf = new("#536f60");

    public static MeshInstance3D Create(MiniatureBoardGeometry geometry, IReadOnlyCollection<BoardCell> forestCells)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(forestCells);
        var cells = forestCells.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        if (cells.Length == 0 || cells.Length * TrianglesPerCell > MaximumTriangles)
            throw new ArgumentException("Orchard requires 1–30 canonical forest cells within its triangle budget.", nameof(forestCells));
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var cell in cells)
        {
            var triangles = geometry.Surface.Where(triangle => triangle.Cell == cell).ToArray();
            if (triangles.Length == 0)
                throw new ArgumentException($"Orchard cell {cell} has no canonical surface.", nameof(forestCells));
            foreach (var offset in new[] { new Vector2(.22f, .22f), new Vector2(.78f, .22f), new Vector2(.22f, .78f), new Vector2(.78f, .78f) })
            {
                var x = cell.X + offset.X;
                var z = cell.Y + offset.Y;
                Vector3 Contact(float px, float pz)
                {
                    foreach (var triangle in triangles)
                    {
                        var hit = Geometry3D.RayIntersectsTriangle(new Vector3(px, 100, pz), Vector3.Down,
                            triangle.A, triangle.B, triangle.C);
                        if (hit.VariantType == Variant.Type.Vector3)
                            return hit.AsVector3();
                    }
                    throw new InvalidOperationException($"Plant foot lies outside the canonical surface at {px},{pz}.");
                }
                var foot = Contact(x, z);
                var soil = new[] { Contact(x - .13f, z - .09f), Contact(x + .13f, z - .09f),
                    Contact(x + .13f, z + .09f), Contact(x - .13f, z + .09f) };
                for (var index = 0; index < soil.Length; index++)
                    soil[index] += Vector3.Up * .012f;
                Triangle(soil[0], soil[2], soil[1], Soil);
                Triangle(soil[0], soil[3], soil[2], Soil);
                // Four small, matte planted crowns leave the cell centre and all cell edges open.
                var rim = new[] { new Vector3(-.11f, .13f, 0), new Vector3(0, .13f, -.11f),
                    new Vector3(.11f, .13f, 0), new Vector3(0, .13f, .11f) };
                var top = foot + Vector3.Up * .27f;
                var bottom = foot + Vector3.Up * .035f;
                for (var side = 0; side < rim.Length; side++)
                {
                    var a = foot + rim[side];
                    var b = foot + rim[(side + 1) % rim.Length];
                    Triangle(top, b, a, Leaf);
                    Triangle(bottom, a, b, Leaf.Darkened(.08f));
                }
            }
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
        surface.Dispose();
        return new MeshInstance3D
        {
            Name = "CanonicalForestPlanting",
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                AlbedoColor = Colors.White,
                Roughness = 1,
                Metallic = 0,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            }
        };

        void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            surface.SetColor(color);
            surface.AddVertex(a);
            surface.AddVertex(b);
            surface.AddVertex(c);
        }
    }
}
