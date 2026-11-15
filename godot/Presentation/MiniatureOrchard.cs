using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Low planted detail projected onto supplied canonical forest cells; never a picking or movement owner.</summary>
public static class MiniatureOrchard
{
    private const int CrownSides = 6;
    private const int TreesPerCell = 2;
    public const int TrianglesPerCell = TreesPerCell * (2 + 6 + CrownSides * 2);
    public const int MaximumTriangles = 1200;
    private static readonly Color Soil = new("#555b46");
    private static readonly Color Bark = new("#624c3a");
    private static readonly Color Leaf = new("#627559");
    private static readonly Color YoungLeaf = new("#758154");

    private static Vector3 Radial(int index, int sides)
    {
        var angle = Mathf.Tau * index / sides;
        return new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
    }

    public static MeshInstance3D Create(MiniatureBoardGeometry geometry, IReadOnlyCollection<BoardCell> forestCells)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(forestCells);
        var cells = forestCells.Distinct().OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
        if (cells.Length == 0 || cells.Length * TrianglesPerCell > MaximumTriangles)
            throw new ArgumentException("Orchard requires 1–30 canonical forest cells within its triangle budget.", nameof(forestCells));
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var cell in cells)
        {
            var triangles = geometry.Surface.Where(triangle => triangle.Cell == cell).ToArray();
            if (triangles.Length == 0)
                throw new ArgumentException($"Orchard cell {cell} has no canonical surface.", nameof(forestCells));
            var rowOffset = (cell.Y & 1) == 0 ? .0f : .08f;
            Plant(.28f + rowOffset, .32f, tall: (cell.X & 1) == 0);
            Plant(.72f - rowOffset, .68f, tall: (cell.X & 1) != 0);

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

            void Plant(float offsetX, float offsetZ, bool tall)
            {
                var x = cell.X + offsetX;
                var z = cell.Y + offsetZ;
                var foot = Contact(x, z);
                var lift = Vector3.Up * .012f;
                var southwest = Contact(x - .16f, z - .11f) + lift;
                var southeast = Contact(x + .16f, z - .11f) + lift;
                var northeast = Contact(x + .16f, z + .11f) + lift;
                var northwest = Contact(x - .16f, z + .11f) + lift;
                Triangle(southwest, northeast, southeast, Soil);
                Triangle(southwest, northwest, northeast, Soil);

                var trunkHeight = tall ? .24f : .17f;
                var radius = tall ? .17f : .135f;
                var canopy = tall ? Leaf : YoungLeaf;
                var crown = foot + new Vector3(.03f, trunkHeight, 0);
                var crownTop = crown + new Vector3(-.035f, radius * 1.50f, .025f);
                var crownBottom = crown - Vector3.Up * (radius * .18f);
                for (var side = 0; side < 3; side++)
                {
                    var a = Radial(side, 3);
                    var b = Radial(side + 1, 3);
                    Triangle(foot + .04f * a, foot + .04f * b, crown + .024f * b, Bark);
                    Triangle(foot + .04f * a, crown + .024f * b, crown + .024f * a, Bark);
                }
                for (var side = 0; side < CrownSides; side++)
                {
                    var a = crown + radius * Radial(side, CrownSides);
                    var b = crown + radius * Radial(side + 1, CrownSides);
                    var facet = side % 2 == 0 ? canopy : canopy.Darkened(.07f);
                    Triangle(crownTop, b, a, facet);
                    Triangle(crownBottom, a, b, facet.Darkened(.08f));
                }
            }
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
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
