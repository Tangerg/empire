#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Single compiled geometry consumed by drawing, material coverage and support depth.</summary>
internal sealed class CompiledSceneSurface
{
    private sealed record Primitive(Vector2[] Polygon, Vector2[] Uv, Color Tint, bool NativeAlpha,
        Vector2 A, Vector2 B, Vector2 C, Vector2 Ua, Vector2 Ub, Vector2 Uc)
    {
        public Vector2 TexturePoint(Vector2 point)
        {
            var v = B - A;
            var w = C - A;
            var q = point - A;
            return Ua + q.Cross(w) / v.Cross(w) * (Ub - Ua) + v.Cross(q) / v.Cross(w) * (Uc - Ua);
        }
    }
    private sealed record Slice(Vector2 Front, Primitive[] Primitives);
    private readonly Slice[] _slices;
    private readonly Texture2D _texture;

    private CompiledSceneSurface(Texture2D texture, Slice[] slices)
    {
        _texture = texture;
        _slices = slices;
    }

    internal static CompiledSceneSurface Compile(SceneSurfaceDocument document, Texture2D texture, string identity)
    {
        if (document.Schema != 1 || document.Supports is not { Length: > 0 } || document.Members is not { Length: > 0 })
            throw new InvalidOperationException($"Invalid authored surface document: {identity}.");
        var supports = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        foreach (var support in document.Supports)
        {
            if (support is null || string.IsNullOrWhiteSpace(support.Id) || supports.ContainsKey(support.Id)
                || support.Kind is not ("physical" or "structural"))
                throw new InvalidOperationException($"Invalid authored surface support: {identity}.");
            supports.Add(support.Id, ReadPoint(support.FrontPixels, identity + " support"));
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        var triangles = new List<(string Support, Primitive Primitive)>();
        var primitiveKeys = new HashSet<string>(StringComparer.Ordinal);
        using var image = texture.GetImage();
        if (image is null)
            throw new InvalidOperationException($"Surface texture cannot supply native pixels: {identity}.");
        foreach (var member in document.Members)
        {
            if (member is null || string.IsNullOrWhiteSpace(member.Id) || !names.Add(member.Id)
                || !supports.ContainsKey(member.SupportId)
                || member.PolygonPixels is not { Length: >= 3 } || member.UvPixels?.Length != member.PolygonPixels.Length
                || member.Coverage is not ("opaque" or "native-alpha") || !Color.HtmlIsValid(member.Tint))
                throw new InvalidOperationException($"Invalid authored surface member: {identity}.");
            var tint = Color.FromHtml(member.Tint);
            if (tint.A != 1)
                throw new InvalidOperationException($"Surface tint must preserve material alpha: {identity}/{member.Id}.");
            var polygon = member.PolygonPixels.Select(p => ReadPoint(p, identity + " polygon")).ToArray();
            var uv = member.UvPixels.Select(p => ReadPoint(p, identity + " UV")).ToArray();
            StandingVisual.ValidatePolygon(polygon, identity + "/" + member.Id);
            if (uv.Any(p => p.X < 0 || p.Y < 0 || p.X >= image.GetWidth() || p.Y >= image.GetHeight()))
                throw new InvalidOperationException($"Surface UV lies outside its material: {identity}/{member.Id}.");
            var indices = Geometry2D.TriangulatePolygon(polygon);
            for (var index = 0; index < indices.Length; index += 3)
            {
                var ids = indices.Skip(index).Take(3).ToArray();
                var points = ids.Select(i => polygon[i]).ToArray();
                var texels = ids.Select(i => uv[i]).ToArray();
                var key = string.Join(";", points.Select(point =>
                    FormattableString.Invariant($"{point.X:R},{point.Y:R}")).Order(StringComparer.Ordinal));
                if (!primitiveKeys.Add(key))
                    throw new InvalidOperationException($"Duplicate surface primitive: {identity}/{member.Id}.");
                if (member.Coverage == "opaque")
                    ValidateOpaque(image, texels, identity + "/" + member.Id);
                triangles.Add((member.SupportId, new Primitive(points, texels, tint, member.Coverage == "native-alpha",
                    points[0], points[1], points[2], texels[0], texels[1], texels[2])));
            }
        }
        var slices = new List<Slice>();
        foreach (var (supportId, support) in supports)
        {
            var owned = triangles.Where(t => t.Support == supportId).Select(t => t.Primitive).ToArray();
            if (owned.Length == 0)
                throw new InvalidOperationException($"Unused authored support: {identity}/{supportId}.");
            var bounds = StandingVisual.BoundsOf(owned.SelectMany(p => p.Polygon).ToArray());
            if (support.X < bounds.Position.X || support.X > bounds.End.X)
                throw new InvalidOperationException($"Surface contact must lie within its owned horizontal span: {identity}/{supportId}.");
            if (document.Supports.Single(s => s.Id == supportId).Kind == "physical"
                && !owned.Any(p => Geometry2D.IsPointInPolygon(support, p.Polygon)))
                throw new InvalidOperationException($"Physical surface contact lies outside its owned geometry: {identity}/{supportId}.");
            slices.Add(new(support, owned));
        }
        return new(texture, slices.ToArray());
    }

    private static Vector2 ReadPoint(double[]? point, string identity)
    {
        if (point is null)
            throw new InvalidOperationException($"Missing surface coordinate: {identity}.");
        return RasterResources.Pair(point, identity);
    }

    private static void ValidateOpaque(Image image, Vector2[] uv, string identity)
    {
        void Check(Vector2 point)
        {
            if (image.GetPixel((int)point.X, (int)point.Y).A < .96f)
                throw new InvalidOperationException($"Opaque surface material includes transparency: {identity} at texel ({(int)point.X}, {(int)point.Y}), alpha={image.GetPixel((int)point.X, (int)point.Y).A}.");
        }
        foreach (var point in uv)
            Check(point);
        var bounds = StandingVisual.BoundsOf(uv);
        for (var y = (int)bounds.Position.Y; y <= (int)bounds.End.Y; y++)
            for (var x = (int)bounds.Position.X; x <= (int)bounds.End.X; x++)
            {
                var corners = new[] { new Vector2(x, y), new Vector2(x + 1, y), new Vector2(x + 1, y + 1), new Vector2(x, y + 1) };
                // Include line/point UV images produced by clamping, not only nonzero triangle interiors.
                var touched = uv.Any(p => p.X >= x && p.X <= x + 1 && p.Y >= y && p.Y <= y + 1)
                    || ((uv[1] - uv[0]).Cross(uv[2] - uv[0]) != 0 && corners.Any(p => Geometry2D.IsPointInPolygon(p, uv)));
                for (var edge = 0; !touched && edge < 3; edge++)
                    for (var side = 0; !touched && side < 4; side++)
                        touched = Geometry2D.SegmentIntersectsSegment(uv[edge], uv[(edge + 1) % 3],
                            corners[side], corners[(side + 1) % 4]).VariantType != Variant.Type.Nil;
                if (touched)
                    Check(new Vector2(x, y));
            }
    }

    internal IEnumerable<(Node2D Node, StandingVisual Visual)> Instantiate(Node owner, string identity, float scale,
        Vector2 anchor, bool pointer, Vector2[]? pointerPolygon, Sprite2D sampler)
    {
        for (var index = 0; index < _slices.Length; index++)
        {
            var slice = _slices[index];
            var node = new Node2D { Name = $"{identity}__surface_{index}", Scale = Vector2.One * scale };
            owner.AddChild(node);
            var vertices = new List<Vector2>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var polygons = new Godot.Collections.Array();
            foreach (var primitive in slice.Primitives)
            {
                // Keep independent vertices and original primitive order: UV seams and alpha overlap are material facts.
                polygons.Add(Enumerable.Range(vertices.Count, primitive.Polygon.Length).ToArray());
                vertices.AddRange(primitive.Polygon.Select(p => p - anchor));
                uv.AddRange(primitive.Uv);
                colors.AddRange(Enumerable.Repeat(primitive.Tint, primitive.Polygon.Length));
            }
            node.AddChild(new Polygon2D
            {
                Polygon = vertices.ToArray(),
                Polygons = polygons,
                UV = uv.ToArray(),
                VertexColors = colors.ToArray(),
                Texture = _texture,
                Color = Colors.White,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                TextureRepeat = CanvasItem.TextureRepeatEnum.Disabled
            });
            yield return (node, new SurfaceVisual(node, slice, anchor, sampler, pointer, pointerPolygon));
        }
    }

    private sealed class SurfaceVisual(Node2D node, Slice slice, Vector2 anchor, Sprite2D sampler,
        bool pointer, Vector2[]? pointerPolygon) : StandingVisual(node)
    {
        public override bool Covers(Vector2 globalPoint)
        {
            var point = Node.ToLocal(globalPoint) + anchor;
            return slice.Primitives.Any(p => Geometry2D.IsPointInPolygon(point, p.Polygon)
                && (!p.NativeAlpha || sampler.IsPixelOpaque(p.TexturePoint(point))));
        }
        public override bool ContainsPointer(Vector2 globalPoint) => pointer && Covers(globalPoint)
            && (pointerPolygon is null || Geometry2D.IsPointInPolygon(Node.ToLocal(globalPoint), pointerPolygon));
        public override bool IntersectsPointer(Vector2[] globalPolygon)
        {
            if (!pointer)
                return false;
            var local = globalPolygon.Select(Node.ToLocal).ToArray();
            var query = pointerPolygon is null ? new[] { local } : Geometry2D.IntersectPolygons(local, pointerPolygon).ToArray();
            return query.Any(q => slice.Primitives.Any(p => Geometry2D.IntersectPolygons(q,
                p.Polygon.Select(v => v - anchor).ToArray()).Count > 0));
        }
        public override Rect2 GlobalBounds => BoundsOf(slice.Primitives.SelectMany(p => p.Polygon).Select(p => Node.ToGlobal(p - anchor)).ToArray());
        protected override Vector2 ParentFrontOffset => Node.Transform * (slice.Front - anchor) - Node.Position;
    }
}
