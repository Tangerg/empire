#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

/// <summary>Frozen full-clip reference versus the mounted surface; no broad-phase in the oracle.</summary>
public partial class PaintedSurfaceBroadPhaseChecks : Node2D
{
    private sealed record Strip(Vector2[] Points, int[][] Indices, Vector2[] Uv, int Depth);
    private int checks;
    private int emptyIntervalReuses;
    private int nonemptyIntervalReuses;
    public override void _Ready()
    {
        try
        {
            using var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                    image.SetPixel(x, y, new Color(.8f, .6f, .3f, (x + y) % 5 == 0 ? 0 : 1));
            using var texture = ImageTexture.CreateFromImage(image);
            foreach (var registered in new[] { false, true })
            {
                var projection = Projection(registered);
                var points = projection.Triangles.SelectMany(t => new[] { t.ProjectedA, t.ProjectedB, t.ProjectedC }).ToArray();
                var minimum = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
                var maximum = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
                var center = projection.CellCenter(new Coord(3, 2));
                var leftEdge = points.OrderBy(p => p.X).First();
                var cases = new (Vector2 Contact, Vector2 Scale, float Rotation, float Skew)[]
                {
                    (center, Vector2.One, 0, 0),
                    (center, new(6, 3), .4f, .15f),
                    (center, new(-2, 4), -.2f, 0),
                    (minimum - Vector2.One * 20, (maximum - minimum + Vector2.One * 40) / 8, 0, 0),
                    (maximum + Vector2.One * 5, Vector2.One, 0, 0),
                    (leftEdge - new Vector2(8, 4), Vector2.One, 0, 0),
                    (leftEdge - new Vector2(8.00001f, 4), Vector2.One, 0, 0),
                    (leftEdge - new Vector2(7.99999f, 4), Vector2.One, 0, 0),
                    (center, new(.001f, .001f), .1f, 0),
                };
                foreach (var fixture in cases)
                {
                    var source = new Sprite2D { Texture = texture, Centered = false, Scale = fixture.Scale, Rotation = fixture.Rotation, Skew = fixture.Skew };
                    var transform = new Transform2D(source.Rotation, source.Scale, source.Skew, fixture.Contact + source.Position)
                        * new Transform2D(0, source.GetRect().Position);
                    var bounds = new[] { Vector2.Zero, new Vector2(8, 0), new Vector2(8, 8), new Vector2(0, 8) }.Select(p => transform * p).ToArray();
                    var parent = new Node2D { Position = new Vector2(13, 7) + fixture.Contact, Rotation = .13f, Scale = new Vector2(1.2f, .8f) };
                    AddChild(parent);
                    var surface = PaintedSurface.Create(projection, source, fixture.Contact);
                    parent.AddChild(surface);
                    Compare(surface, projection, transform, bounds, image);
                    var previous = Cache(surface);
                    var retired = new Dictionary<(int, int), object>();
                    AssertCache(projection, previous);
                    foreach (var cuts in new float[][] { [23, 77, 105], [23, 55, 77, 105], [23, 55, 78, 105],
                        [23, 78, 105], [23, 78, 105], [], [23, 77, 105], [] })
                    {
                        var oldNodes = surface.GetChildren().OfType<Polygon2D>().ToArray();
                        var oldPartitions = projection.DepthPartitions;
                        projection.PrepareDepthPartitions(cuts, []);
                        Compare(surface, projection, transform, bounds, image);
                        var current = Cache(surface);
                        AssertCache(projection, current);
                        foreach (var (key, geometry) in current)
                        {
                            if (previous.TryGetValue(key, out var old))
                            {
                                Require(ReferenceEquals(old, geometry), "Unchanged interval geometry was recomputed, including empty intervals.");
                                if (!ReferenceEquals(oldPartitions, projection.DepthPartitions))
                                {
                                    var cachedPoints = (Vector2[])geometry.GetType().GetProperty("Points")!.GetValue(geometry)!;
                                    if (cachedPoints.Length == 0)
                                        emptyIntervalReuses++;
                                    else
                                        nonemptyIntervalReuses++;
                                }
                            }
                            else if (retired.TryGetValue(key, out var stale))
                                Require(!ReferenceEquals(stale, geometry), "Geometry from an older successful version remained cached.");
                        }
                        foreach (var (key, geometry) in previous.Where(pair => !current.ContainsKey(pair.Key)))
                            retired[key] = geometry;
                        if (!ReferenceEquals(oldPartitions, projection.DepthPartitions))
                            Require(oldNodes.All(node => !GodotObject.IsInstanceValid(node)), "Replaced Polygon2D nodes were not freed synchronously.");
                        else
                            Require(surface.GetChildren().OfType<Polygon2D>().SequenceEqual(oldNodes), "Repeated cuts unnecessarily replaced mounted nodes.");
                        previous = current;
                    }
                    RemoveChild(parent);
                    parent.Free();
                    source.Free();
                }
                var unbounded = PaintedSurface.Create(projection, texture, .5f);
                AddChild(unbounded);
                Compare(unbounded, projection, new Transform2D(0, Vector2.One * .5f, 0, Vector2.Zero), null, image);
                projection.PrepareDepthPartitions([23, 77, 105], []);
                Compare(unbounded, projection, new Transform2D(0, Vector2.One * .5f, 0, Vector2.Zero), null, image);
                projection.PrepareDepthPartitions([], []);
                Compare(unbounded, projection, new Transform2D(0, Vector2.One * .5f, 0, Vector2.Zero), null, image);
                Require(Cache(unbounded).Count == 0, "Unbounded surfaces should not retain bounded interval geometry.");
                RemoveChild(unbounded);
                unbounded.Free();
            }
            Require(emptyIntervalReuses > 0 && nonemptyIntervalReuses > 0, "Fixtures must exercise both empty and nonempty reuse across changed cuts.");
            CheckFailureRecovery(texture, image);
            GD.Print($"PAINTED_SURFACE_BROAD_PHASE_OK {checks} exact geometry/UV/depth/pointer assertions against full-clip reference");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static Dictionary<(int, int), object> Cache(PaintedSurface surface)
    {
        var field = typeof(PaintedSurface).GetField("_intervalGeometry", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing bounded interval geometry cache.");
        var cache = (IDictionary)(field.GetValue(surface)
            ?? throw new InvalidOperationException("Bounded interval geometry cache is null."));
        return cache.Keys.Cast<(int, int)>().ToDictionary(key => key, key => cache[key]!);
    }

    private void AssertCache(BoardProjection projection, Dictionary<(int, int), object> cache)
    {
        Require(cache.Keys.ToHashSet().SetEquals(projection.DepthPartitions.Select(strip => (strip.StartDepth, strip.EndDepthExclusive))),
            "Cache must contain exactly the latest successful intervals, including empty geometry.");
        foreach (var geometry in cache.Values)
        {
            var type = geometry.GetType();
            var points = (Vector2[])type.GetProperty("Points")!.GetValue(geometry)!;
            var indices = (int[][])type.GetProperty("Indices")!.GetValue(geometry)!;
            var uv = (Vector2[])type.GetProperty("Uv")!.GetValue(geometry)!;
            Require(points.Length == uv.Length && (points.Length != 0 || indices.Length == 0),
                "Cached managed geometry arrays are inconsistent.");
        }
    }

    private sealed class FailingVertices : IReadOnlyList<BoardSurfaceVertex>
    {
        internal const string Message = "Injected interval geometry preparation failure.";
        public int Count => throw new InvalidOperationException(Message);
        public BoardSurfaceVertex this[int index] => throw new InvalidOperationException(Message);
        public IEnumerator<BoardSurfaceVertex> GetEnumerator() => throw new InvalidOperationException(Message);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private void CheckFailureRecovery(Texture2D texture, Image image)
    {
        var projection = Projection(false);
        var projected = projection.Triangles.SelectMany(t => new[] { t.ProjectedA, t.ProjectedB, t.ProjectedC }).ToArray();
        var minimum = new Vector2(projected.Min(p => p.X), projected.Min(p => p.Y)) - Vector2.One;
        var maximum = new Vector2(projected.Max(p => p.X), projected.Max(p => p.Y)) + Vector2.One;
        var source = new Sprite2D { Texture = texture, Centered = false, Scale = (maximum - minimum) / 8 };
        var transform = new Transform2D(0, source.Scale, 0, minimum);
        var bounds = new[] { minimum, new Vector2(maximum.X, minimum.Y), maximum, new Vector2(minimum.X, maximum.Y) };
        var inject = false;
        IReadOnlyList<BoardDepthPartition>? validPartitions = null;
        var partitionsProperty = typeof(BoardProjection).GetProperty(nameof(BoardProjection.DepthPartitions))!;
        // Run before the surface's event handler, after the projection has prepared the new request.
        projection.DepthPartitionsChanged += () =>
        {
            if (!inject)
                return;
            validPartitions = projection.DepthPartitions;
            var corrupted = validPartitions.ToArray();
            var last = corrupted[^1];
            corrupted[^1] = last with
            {
                Patches = [last.Patches[0] with { Vertices = new FailingVertices() }]
            };
            partitionsProperty.SetValue(projection, corrupted);
        };
        var surface = PaintedSurface.Create(projection, source, minimum);
        AddChild(surface);
        try
        {
            var before = Cache(surface);
            var mounted = surface.GetChildren().OfType<Polygon2D>().ToArray();
            var pointerPieces = surface.PointerPolygons(this).ToArray();
            inject = true;
            var failed = false;
            try
            {
                projection.PrepareDepthPartitions([23, 77, 105], []);
            }
            catch (InvalidOperationException error) when (error.Message == FailingVertices.Message) { failed = true; }
            finally
            {
                inject = false;
                if (validPartitions is not null)
                    partitionsProperty.SetValue(projection, validPartitions);
            }
            Require(failed, "Fixture did not fail during later interval geometry preparation.");
            var afterFailure = Cache(surface);
            Require(before.Keys.ToHashSet().SetEquals(afterFailure.Keys)
                && before.All(pair => ReferenceEquals(pair.Value, afterFailure[pair.Key])),
                "Failed rebuild published partial geometry or discarded the last successful cache.");
            Require(mounted.All(GodotObject.IsInstanceValid)
                && surface.GetChildren().OfType<Polygon2D>().SequenceEqual(mounted),
                "Failed rebuild changed or freed mounted Polygon2D nodes.");
            var afterPointers = surface.PointerPolygons(this).ToArray();
            Require(afterPointers.Length == pointerPieces.Length
                && afterPointers.Zip(pointerPieces).All(pair => pair.First.SequenceEqual(pair.Second)),
                "Failed rebuild changed pointer material.");
            // Keep the early intervals from the failed request. Resetting old cuts first would hide premature publication.
            projection.PrepareDepthPartitions([23, 77, 106], []);
            Compare(surface, projection, transform, bounds, image);
            AssertCache(projection, Cache(surface));
            Require(mounted.All(node => !GodotObject.IsInstanceValid(node)), "Recovery did not free the old mounted nodes.");
        }
        finally
        {
            RemoveChild(surface);
            surface.Free();
            source.Free();
        }
    }

    private void Compare(PaintedSurface surface, BoardProjection projection, Transform2D transform, Vector2[]? bounds, Image image)
    {
        var expected = Reference(projection, transform, bounds);
        var actual = surface.GetChildren().OfType<Polygon2D>().ToArray();
        Require(actual.Length == expected.Count, "Strip count differs from full clipping.");
        for (var i = 0; i < actual.Length; i++)
        {
            var source = expected[i];
            Require(actual[i].Polygon.SequenceEqual(source.Points) && actual[i].UV.SequenceEqual(source.Uv)
                && actual[i].ZIndex == source.Depth && !actual[i].ZAsRelative,
                "Exact emitted coordinates, UV order or depth changed.");
            Require(actual[i].Polygons.Select(p => p.AsInt32Array()).Zip(source.Indices).All(pair => pair.First.SequenceEqual(pair.Second))
                && actual[i].Polygons.Count == source.Indices.Length, "Piece index order changed.");
        }
        var expectedPointer = expected.SelectMany(strip => strip.Indices.Select(piece => piece.Select(i => surface.ToGlobal(strip.Points[i])).ToArray())).ToArray();
        var pointers = surface.PointerPolygons(this).ToArray();
        Require(pointers.Length == expectedPointer.Length && pointers.Zip(expectedPointer).All(pair => pair.First.SequenceEqual(pair.Second)),
            "Transformed pointer material differs from reference pieces.");
        var inverse = transform.AffineInverse();
        var probes = Enumerable.Range(-1, 11).SelectMany(y => Enumerable.Range(-1, 11).Select(x => transform * new Vector2(x * .8f, y * .8f)))
            .Concat(expected.SelectMany(strip => strip.Indices.Select(piece => piece.Select(i => strip.Points[i]).Aggregate(Vector2.Zero, (sum, p) => sum + p) / piece.Length)));
        foreach (var point in probes)
        {
            var global = surface.ToGlobal(point);
            var local = surface.ToLocal(global);
            var uv = inverse * local;
            var opaque = uv.X >= 0 && uv.Y >= 0 && uv.X < 8 && uv.Y < 8 && image.GetPixel((int)uv.X, (int)uv.Y).A > .1f;
            int? depth = null;
            if (opaque)
                foreach (var strip in expected)
                    if ((depth is null || strip.Depth >= depth) && strip.Indices.Any(piece => Geometry2D.IsPointInPolygon(local, piece.Select(i => strip.Points[i]).ToArray())))
                        depth = strip.Depth;
            Require(surface.HitPolygon(global)?.ZIndex == depth, "Alpha/depth pointer result changed after surface/parent transforms.");
        }
    }

    // Literal pre-optimization construction, intentionally traversing every patch.
    private static List<Strip> Reference(BoardProjection projection, Transform2D transform, Vector2[]? bounds)
    {
        var result = new List<Strip>();
        foreach (var strip in projection.DepthPartitions)
            Add(strip.StartDepth, strip.Patches.Select(patch => patch.Vertices));
        if (bounds is null)
            foreach (var group in projection.Faces.SelectMany(projection.FacePatches).GroupBy(patch => patch.Depth))
                Add(group.Key, group.Select(patch => patch.Vertices));
        return result;
        void Add(int depth, IEnumerable<IReadOnlyList<BoardSurfaceVertex>> patches)
        {
            var points = new List<Vector2>();
            var indices = new List<int[]>();
            foreach (var patch in patches)
            {
                var projected = patch.Select(vertex => vertex.Projected).ToArray();
                var clipped = bounds is null ? new[] { projected } : Geometry2D.IntersectPolygons(projected, bounds).ToArray();
                foreach (var piece in clipped)
                {
                    indices.Add(Enumerable.Range(points.Count, piece.Length).ToArray());
                    points.AddRange(piece);
                }
            }
            if (points.Count > 0)
                result.Add(new(points.ToArray(), indices.ToArray(), points.Select(p => transform.AffineInverse() * p).ToArray(), projection.LayerAtDepth(depth, BoardDepthLayer.Surface)));
        }
    }

    private static BoardProjection Projection(bool registered)
    {
        const int width = 6, height = 5;
        var map = new JsonObject
        {
            ["width"] = width,
            ["height"] = height,
            ["tiles"] = new JsonArray(Enumerable.Repeat("plain", width * height).Select(t => (JsonNode?)JsonValue.Create(t)).ToArray()),
            ["elevation"] = new JsonArray(Enumerable.Range(0, width * height).Select(i => (JsonNode?)JsonValue.Create(registered && i % width is >= 2 and <= 4 && i / width < 3 ? 2 : 0)).ToArray()),
            ["cliffs"] = new JsonArray()
        };
        var art = new PaintedGroundDocument
        {
            LevelId = "profile-check",
            LevelSourceSha256 = "test",
            ResourcePath = "test",
            PixelSize = [640, 480],
            WorldScale = .5f,
            OriginPixels = [96, 128],
            CellXPixels = [64, 0],
            CellYPixels = [0, 48],
            Occluders = []
        };
        return new BoardProjection(new Square4Grid(), registered ? art.CreatePlane : BoardPlane.Dimetric, map,
            Enumerable.Range(0, width * height).Select(i => new Coord(i % width, i / width)).ToHashSet());
    }
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        checks++;
    }
}
