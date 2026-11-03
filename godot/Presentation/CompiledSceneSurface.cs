#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Single compiled geometry consumed by drawing, material coverage and support depth.</summary>
internal sealed class CompiledSceneSurface
{
    private const float OpaqueMaterialThreshold = .96f;
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

    internal sealed class Material : IDisposable
    {
        private readonly Image _pixels;
        private int[]? _nonOpaquePrefix;
        internal Texture2D Texture
        {
            get;
        }
        internal bool IsOpaque
        {
            get;
        }
        internal int Width => _pixels.GetWidth();
        internal int Height => _pixels.GetHeight();
        internal Material(Texture2D texture)
        {
            Texture = texture;
            _pixels = texture.GetImage() ?? throw new InvalidOperationException("Surface texture cannot supply native pixels.");
            IsOpaque = _pixels.DetectAlpha() == Image.AlphaMode.None;
        }
        internal float Alpha(int x, int y) => _pixels.GetPixel(x, y).A;
        internal bool ContainsOnlyOpaqueTexels(Rect2 bounds)
        {
            // A conservative rectangle certificate skips triangle/texel intersection work
            // only when every texel could pass the original material assertion. The index
            // is a projection of this immutable pixel snapshot, shared by all its members.
            var width = Width;
            var height = Height;
            var stride = checked(width + 1);
            if (_nonOpaquePrefix is null)
            {
                var prefix = new int[checked(stride * (height + 1))];
                for (var y = 0; y < height; y++)
                {
                    var row = 0;
                    for (var x = 0; x < width; x++)
                    {
                        if (Alpha(x, y) < OpaqueMaterialThreshold)
                            row++;
                        prefix[(y + 1) * stride + x + 1] = prefix[y * stride + x + 1] + row;
                    }
                }
                _nonOpaquePrefix = prefix;
            }
            var left = (int)bounds.Position.X;
            var top = (int)bounds.Position.Y;
            var right = (int)bounds.End.X + 1;
            var bottom = (int)bounds.End.Y + 1;
            var count = _nonOpaquePrefix[bottom * stride + right] - _nonOpaquePrefix[top * stride + right]
                - _nonOpaquePrefix[bottom * stride + left] + _nonOpaquePrefix[top * stride + left];
            return count == 0;
        }
        public void Dispose()
        {
            _pixels.Dispose();
            _nonOpaquePrefix = null;
        }
    }

    internal sealed class Materials : IDisposable
    {
        private readonly Dictionary<Texture2D, Material> _materials = new();
        internal Material Get(Texture2D texture)
        {
            if (!_materials.TryGetValue(texture, out var material))
                _materials.Add(texture, material = new Material(texture));
            return material;
        }
        public void Dispose()
        {
            foreach (var material in _materials.Values)
                material.Dispose();
            _materials.Clear();
        }
    }

    internal static CompiledSceneSurface Compile(SceneSurfaceDocument document, Texture2D texture, string identity)
    {
        using var materials = new Materials();
        return Compile(document, materials.Get(texture), identity);
    }

    internal static CompiledSceneSurface Compile(SceneSurfaceDocument document, Material material, string identity)
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
            // Native-alpha UVs describe continuous canvas edges, including normalized UV 1.
            // Opaque subdomains require actual texel endpoints for exhaustive material validation.
            var nativeAlpha = member.Coverage == "native-alpha";
            if (uv.Any(p => p.X < 0 || p.Y < 0 || (nativeAlpha
                    ? p.X > material.Width || p.Y > material.Height
                    : p.X >= material.Width || p.Y >= material.Height)))
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
                if (member.Coverage == "opaque" && !material.IsOpaque)
                    ValidateOpaque(material, texels, identity + "/" + member.Id);
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
        return new(material.Texture, slices.ToArray());
    }

    private static Vector2 ReadPoint(double[]? point, string identity)
    {
        if (point is null)
            throw new InvalidOperationException($"Missing surface coordinate: {identity}.");
        return RasterResources.Pair(point, identity);
    }

    private static void ValidateOpaque(Material material, Vector2[] uv, string identity)
    {
        void Check(Vector2 point)
        {
            if (material.Alpha((int)point.X, (int)point.Y) < OpaqueMaterialThreshold)
                throw new InvalidOperationException($"Opaque surface material includes transparency: {identity} at texel ({(int)point.X}, {(int)point.Y}), alpha={material.Alpha((int)point.X, (int)point.Y)}.");
        }
        foreach (var point in uv)
            Check(point);
        var bounds = StandingVisual.BoundsOf(uv);
        if (material.ContainsOnlyOpaqueTexels(bounds))
            return;
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

    internal sealed class Instance
    {
        private readonly Slice[] _slices;
        private readonly Vector2 _anchor;
        private readonly Polygon2D[] _draw;
        private readonly int[] _leaders;
        private readonly int[] _nextLeaders;
        internal IReadOnlyList<(Node2D Node, StandingVisual Visual)> Pieces
        {
            get;
        }

        internal Instance(CompiledSceneSurface source, Vector2 anchor,
            (Node2D Node, StandingVisual Visual)[] pieces, Polygon2D[] draw)
        {
            _slices = source._slices;
            _anchor = anchor;
            Pieces = pieces;
            _draw = draw;
            _leaders = Enumerable.Range(0, _slices.Length).ToArray();
            _nextLeaders = new int[_slices.Length];
        }

        // Physical pieces and their pick/alpha geometry stay separate. Only the drawing
        // projection combines true consecutive siblings at the already assigned depth.
        internal void ApplyDrawOrder()
        {
            bool changed = false;
            for (var index = 0; index < Pieces.Count; index++)
            {
                var leader = index > 0 && CanJoin(index - 1, index) ? _nextLeaders[index - 1] : index;
                _nextLeaders[index] = leader;
                changed |= leader != _leaders[index];
            }
            if (!changed)
                return;
            for (var index = 0; index < Pieces.Count; index++)
            {
                _draw[index].Visible = _nextLeaders[index] == index;
                if (_nextLeaders[index] != index)
                    continue;
                var end = index + 1;
                while (end < Pieces.Count && _nextLeaders[end] == index)
                    end++;
                SetGeometry(_draw[index], _slices.Skip(index).Take(end - index).SelectMany(slice => slice.Primitives), _anchor);
            }
            Array.Copy(_nextLeaders, _leaders, _leaders.Length);
        }

        private bool CanJoin(int previous, int current)
        {
            var left = Pieces[previous].Node;
            var right = Pieces[current].Node;
            var a = _draw[previous];
            var b = _draw[current];
            // Additional children (including contact shadows) are ordering barriers.
            // Child visibility belongs to this draw projection, never to physical coverage.
            return left.Visible && right.Visible
                && !left.ZAsRelative && !right.ZAsRelative && left.ZIndex == right.ZIndex
                && left.GetParent() == right.GetParent() && right.GetIndex() == left.GetIndex() + 1
                && left.GetChildCount() == 1 && right.GetChildCount() == 1
                && a.GetChildCount() == 0 && b.GetChildCount() == 0
                && left.Transform == right.Transform && left.Modulate == right.Modulate
                && left.SelfModulate == right.SelfModulate
                && a.Transform == b.Transform && a.Offset == b.Offset
                && a.Texture == b.Texture && a.Material == b.Material && !a.UseParentMaterial && !b.UseParentMaterial
                && a.TextureFilter == b.TextureFilter && a.TextureRepeat == b.TextureRepeat
                && a.Color == b.Color && a.Modulate == b.Modulate && a.SelfModulate == b.SelfModulate
                && a.ZIndex == 0 && b.ZIndex == 0 && a.ZAsRelative && b.ZAsRelative
                && !a.ShowBehindParent && !b.ShowBehindParent && !a.Antialiased && !b.Antialiased
                && !a.InvertEnabled && !b.InvertEnabled;
        }
    }

    internal Instance Instantiate(Node owner, string identity, float scale,
        Vector2 anchor, bool pointer, Vector2[]? pointerPolygon, Sprite2D sampler)
    {
        var pieces = new (Node2D Node, StandingVisual Visual)[_slices.Length];
        var draws = new Polygon2D[_slices.Length];
        for (var index = 0; index < _slices.Length; index++)
        {
            var slice = _slices[index];
            var node = new Node2D { Name = $"{identity}__surface_{index}", Scale = Vector2.One * scale };
            owner.AddChild(node);
            var draw = new Polygon2D
            {
                Texture = _texture,
                Color = Colors.White,
                TextureFilter = CanvasItem.TextureFilterEnum.Linear,
                TextureRepeat = CanvasItem.TextureRepeatEnum.Disabled
            };
            SetGeometry(draw, slice.Primitives, anchor);
            node.AddChild(draw);
            draws[index] = draw;
            pieces[index] = (node, new SurfaceVisual(node, slice, anchor, sampler, pointer, pointerPolygon));
        }
        return new Instance(this, anchor, pieces, draws);
    }

    private static void SetGeometry(Polygon2D draw, IEnumerable<Primitive> primitives, Vector2 anchor)
    {
        var vertices = new List<Vector2>();
        var uv = new List<Vector2>();
        var colors = new List<Color>();
        var polygons = new Godot.Collections.Array();
        foreach (var primitive in primitives)
        {
            // Independent vertices retain source primitive order, UV seams and alpha overlap.
            polygons.Add(Enumerable.Range(vertices.Count, primitive.Polygon.Length).ToArray());
            vertices.AddRange(primitive.Polygon.Select(point => point - anchor));
            uv.AddRange(primitive.Uv);
            colors.AddRange(Enumerable.Repeat(primitive.Tint, primitive.Polygon.Length));
        }
        draw.Polygon = vertices.ToArray();
        draw.Polygons = polygons;
        draw.UV = uv.ToArray();
        draw.VertexColors = colors.ToArray();
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
