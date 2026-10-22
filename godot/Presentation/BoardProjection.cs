#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Godot;

namespace Empire.Presentation;

public enum BoardDepthLayer
{
    Surface = 0, Range = 1, Standing = 2
}

public sealed record BoardSurfaceTriangle(Coord Cell,
    Vector2 LogicalA, Vector2 LogicalB, Vector2 LogicalC,
    Vector2 ProjectedA, Vector2 ProjectedB, Vector2 ProjectedC);

public sealed record BoardSurfaceFace(Coord HigherCell, Coord LowerCell,
    Vector2 LogicalA, Vector2 LogicalB, Vector2 ProjectedA, Vector2 ProjectedB, Vector2 ProjectedC, Vector2 ProjectedD, float Depth);

public sealed record BoardSurfaceHit(Coord? Cell, float Depth);

public sealed record BoardFacePatch(int Depth, IReadOnlyList<BoardSurfaceVertex> Vertices);

public readonly record struct BoardSurfaceVertex(Vector2 Logical, Vector2 Projected, Vector3 SourceWeights);

public sealed record BoardSurfacePatch(int Depth, BoardSurfaceTriangle Source,
    IReadOnlyList<BoardSurfaceVertex> Vertices);

public sealed record BoardDepthPartition(int StartDepth, int EndDepthExclusive,
    IReadOnlyList<BoardSurfacePatch> Patches);

/// <summary>
/// Owns the presentation-only surface and its inverse. Logical tactical coordinates
/// never move; every rendered contact and hit test uses this same detached mesh.
/// </summary>
public sealed class BoardProjection
{
    public const float TileSize = 32f;
    public const float HeightStep = 12f;
    private const float Epsilon = 0.0001f;
    private readonly ITacticalGrid _grid;
    private readonly HashSet<Coord> _playable;
    private readonly Dictionary<Coord, BoardSurfaceTriangle[]> _cells = new();
    private readonly BoardPlane _plane;
    private readonly int _surfaceWidth;
    private readonly int _surfaceHeight;
    private readonly int[] _surfaceHeights;
    private readonly HashSet<string> _surfaceCliffs;
    private readonly Vector2 _logicalSize;
    private readonly HashSet<int> _fixedDepthCuts = new();
    private int[] _partitionCuts = [];
    public event Action? DepthPartitionsChanged;

    public Vector2 Size
    {
        get;
    }
    public IReadOnlyList<BoardSurfaceTriangle> Triangles
    {
        get;
    }
    public IReadOnlyList<BoardSurfaceFace> Faces
    {
        get;
    }
    public IReadOnlyList<BoardSurfacePatch> DepthPatches { get; private set; } = Array.Empty<BoardSurfacePatch>();
    public IReadOnlyList<BoardDepthPartition> DepthPartitions { get; private set; } = Array.Empty<BoardDepthPartition>();

    public BoardProjection(ITacticalGrid grid, BoardPlaneFactory createPlane, JsonObject map, IReadOnlySet<Coord> playable)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(playable);
        _grid = grid;
        _playable = new HashSet<Coord>(playable);
        var width = Integer(map["width"], "map width");
        var height = Integer(map["height"], "map height");
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Board projection requires positive map dimensions.", nameof(map));
        var count = checked(width * height);
        var elevation = map["elevation"] as JsonArray
            ?? throw new ArgumentException("Board projection requires canonical elevation.", nameof(map));
        var tiles = map["tiles"] as JsonArray
            ?? throw new ArgumentException("Board projection requires canonical tiles.", nameof(map));
        var cliffs = map["cliffs"] as JsonArray
            ?? throw new ArgumentException("Board projection requires canonical cliffs.", nameof(map));
        if (elevation.Count != count || tiles.Count != count)
            throw new ArgumentException("Canonical tiles and elevation must match map dimensions.", nameof(map));
        var heights = elevation.Select((node, index) => Integer(node, $"elevation[{index}]")).ToArray();
        _surfaceWidth = width;
        _surfaceHeight = height;
        _surfaceHeights = heights;
        if (tiles.Any(node => node is not JsonValue scalar || scalar.GetValueKind() != JsonValueKind.String))
            throw new ArgumentException("Canonical tiles must contain terrain identifiers.", nameof(map));
        foreach (var cell in _playable)
            if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
                throw new ArgumentException($"Playable cell {cell} is outside canonical map bounds.", nameof(playable));
        _logicalSize = Pixel(grid.Extent(new GridExtent(width, height)));
        RequireFinite(_logicalSize);
        _plane = createPlane(_logicalSize, heights.Min(), heights.Max());
        if (Depth(_logicalSize) + 8 * TileSize > 2048)
            throw new NotSupportedException("Board depth exceeds Godot's absolute Z range at one logical-pixel resolution with four-tile scenery margins.");
        Size = _plane.CanvasSize;
        var outline = grid.Outline();
        if (outline.Count < 3)
            throw new NotSupportedException("Board projection requires a polygonal grid outline.");

        // All canonical cells contribute, including scenery-only cells outside the
        // playable mask. The mask controls interaction, not the authored terrain.
        var cliffEdges = new Dictionary<string, (Coord From, Coord To)>();
        foreach (var node in cliffs)
        {
            if (node is not JsonObject edge || edge["from"] is not JsonObject fromNode
                || edge["to"] is not JsonObject toNode)
                throw new ArgumentException("Canonical cliff edges require from/to coordinates.", nameof(map));
            var from = new Coord(Integer(fromNode["x"], "cliff from x"), Integer(fromNode["y"], "cliff from y"));
            var to = new Coord(Integer(toNode["x"], "cliff to x"), Integer(toNode["y"], "cliff to y"));
            if (!GridStorage.InBounds(new GridExtent(width, height), from.X, from.Y)
                || !GridStorage.InBounds(new GridExtent(width, height), to.X, to.Y)
                || !GridStorage.SharesEdge(from, to))
                throw new ArgumentException("Canonical cliff endpoints must share an in-bounds storage edge.", nameof(map));
            cliffEdges.TryAdd(GridStorage.EdgeKey(from, to), (from, to));
        }
        _surfaceCliffs = cliffEdges.Keys.ToHashSet(StringComparer.Ordinal);
        var corners = new Dictionary<(long X, long Y), List<Coord>>();
        var outlines = new Dictionary<Coord, Vector2[]>();
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var cell = new Coord(x, y);
                var center = Pixel(grid.Center(cell));
                var points = outline.Select(point => center + Pixel(point)).ToArray();
                outlines.Add(cell, points);
                foreach (var point in points)
                {
                    var key = CornerKey(point);
                    if (!corners.TryGetValue(key, out var incident))
                        corners.Add(key, incident = new List<Coord>());
                    incident.Add(cell);
                }
            }
        float Height(Coord cell) => heights[cell.Y * width + cell.X];
        bool IsCliff(Coord a, Coord b) => cliffEdges.ContainsKey(GridStorage.EdgeKey(a, b));
        float CornerHeight(Coord cell, Vector2 point)
        {
            var incident = corners[CornerKey(point)];
            var connected = new HashSet<Coord> { cell };
            var pending = new Queue<Coord>();
            pending.Enqueue(cell);
            while (pending.TryDequeue(out var current))
                foreach (var next in incident)
                    if (!connected.Contains(next) && !IsCliff(current, next)
                        && outlines[current].Count(a => outlines[next].Any(b => CornerKey(a) == CornerKey(b))) == 2)
                    {
                        connected.Add(next);
                        pending.Enqueue(next);
                    }
            return connected.Average(Height);
        }
        var splitEdges = new Dictionary<(Coord Cell, int Edge), Coord>();
        foreach (var (from, to) in cliffEdges.Values)
        {
            var shared = outlines[from].Where(a => outlines[to].Any(b => CornerKey(a) == CornerKey(b))).ToArray();
            if (shared.Length != 2)
                throw new NotSupportedException($"Canonical storage cliff {from} -> {to} is not a shared edge of the composed tactical outline.");
            foreach (var (cell, other) in new[] { (from, to), (to, from) })
                for (var index = 0; index < outline.Count; index++)
                    if (shared.Any(a => CornerKey(a) == CornerKey(outlines[cell][index]))
                        && shared.Any(a => CornerKey(a) == CornerKey(outlines[cell][(index + 1) % outline.Count])))
                        splitEdges.Add((cell, index), other);
        }
        var triangles = new List<BoardSurfaceTriangle>();
        var faces = new List<BoardSurfaceFace>();
        foreach (var cell in _playable.OrderBy(value => value.Y).ThenBy(value => value.X))
        {
            var center = Pixel(grid.Center(cell));
            var projectedCenter = Raise(center, Height(cell));
            var fan = new List<BoardSurfaceTriangle>();
            for (var index = 0; index < outline.Count; index++)
            {
                var a = outlines[cell][index];
                var b = outlines[cell][(index + 1) % outline.Count];
                var projectedA = Raise(a, CornerHeight(cell, a));
                var projectedB = Raise(b, CornerHeight(cell, b));
                if (splitEdges.TryGetValue((cell, index), out var other))
                {
                    var midpoint = (a + b) * 0.5f;
                    var projectedMidpoint = Raise(midpoint, Height(cell));
                    AddTriangle(a, midpoint, projectedA, projectedMidpoint);
                    AddTriangle(midpoint, b, projectedMidpoint, projectedB);
                    if (Depth(center) < Depth(Pixel(grid.Center(other))))
                    {
                        AddFace(a, midpoint, projectedA, projectedMidpoint,
                            Raise(a, CornerHeight(other, a)), Raise(midpoint, Height(other)), other);
                        AddFace(midpoint, b, projectedMidpoint, projectedB,
                            Raise(midpoint, Height(other)), Raise(b, CornerHeight(other, b)), other);
                    }
                }
                else
                    AddTriangle(a, b, projectedA, projectedB);
            }
            _cells.Add(cell, fan.ToArray());
            // A steep but valid heightfield can turn away from this camera. Keep
            // that logical fan for contacts, but never draw or pick its underside.
            triangles.AddRange(fan.Where(triangle =>
                Cross(triangle.LogicalB - triangle.LogicalA, triangle.LogicalC - triangle.LogicalA)
                * Cross(triangle.ProjectedB - triangle.ProjectedA, triangle.ProjectedC - triangle.ProjectedA) > Epsilon));

            void AddTriangle(Vector2 a, Vector2 b, Vector2 projectedA, Vector2 projectedB)
            {
                var logicalArea = Cross(a - center, b - center);
                var projectedArea = Cross(projectedA - projectedCenter, projectedB - projectedCenter);
                if (!float.IsFinite(logicalArea) || !float.IsFinite(projectedArea)
                    || Math.Abs(logicalArea) <= Epsilon)
                    throw new NotSupportedException($"Board surface at {cell} has a degenerate logical fan or nonfinite projected geometry.");
                fan.Add(new BoardSurfaceTriangle(cell, center, a, b, projectedCenter, projectedA, projectedB));
            }

            void AddFace(Vector2 a, Vector2 b, Vector2 topA, Vector2 topB,
                Vector2 bottomA, Vector2 bottomB, Coord lower)
            {
                if (topA.Y >= bottomA.Y - Epsilon && topB.Y >= bottomB.Y - Epsilon)
                    return;
                if (topA.Y > bottomA.Y + Epsilon || topB.Y > bottomB.Y + Epsilon)
                    throw new NotSupportedException($"Cliff face {cell} -> {lower} crosses its neighboring surface.");
                var area = Math.Abs(Cross(topB - topA, bottomB - topA))
                    + Math.Abs(Cross(bottomB - topA, bottomA - topA));
                if (area > Epsilon)
                    faces.Add(new BoardSurfaceFace(cell, lower, a, b, topA, topB, bottomB, bottomA, Depth((a + b) * 0.5f)));
            }
        }
        Faces = faces.AsReadOnly();
        Triangles = triangles.AsReadOnly();
        var overlapReach = (heights.Max() - (double)heights.Min()) * HeightStep;
        foreach (var triangle in _cells.Values.SelectMany(fan => fan))
        {
            if (Cross(triangle.LogicalB - triangle.LogicalA, triangle.LogicalC - triangle.LogicalA)
                * Cross(triangle.ProjectedB - triangle.ProjectedA, triangle.ProjectedC - triangle.ProjectedA) <= Epsilon)
                KeepOcclusionCuts(Math.Min(Depth(triangle.LogicalA), Math.Min(Depth(triangle.LogicalB), Depth(triangle.LogicalC))),
                    Math.Max(Depth(triangle.LogicalA), Math.Max(Depth(triangle.LogicalB), Depth(triangle.LogicalC))));
        }
        foreach (var face in Faces)
            KeepOcclusionCuts(Math.Min(Depth(face.LogicalA), Depth(face.LogicalB)),
                Math.Max(Depth(face.LogicalA), Depth(face.LogicalB)));
        void KeepOcclusionCuts(float first, float last)
        {
            var lower = (int)Math.Max(0, Math.Floor(first - overlapReach));
            var upper = (int)Math.Min(Math.Ceiling(Depth(_logicalSize)), Math.Ceiling(last + overlapReach));
            for (var depth = lower; depth <= upper; depth++)
                _fixedDepthCuts.Add(depth);
        }
        PrepareDepthPartitions(Array.Empty<float>(), Array.Empty<float>());
    }

    public Vector2 Project(Vector2 logicalPixel)
    {
        RequireFinite(logicalPixel);
        if (logicalPixel.X < 0 || logicalPixel.Y < 0
            || logicalPixel.X >= _logicalSize.X || logicalPixel.Y >= _logicalSize.Y)
            return Raise(logicalPixel, 0);
        var cell = _grid.CellAt(new TilePoint(logicalPixel.X / TileSize, logicalPixel.Y / TileSize));
        if (!_cells.TryGetValue(cell, out var fan))
            return Raise(logicalPixel, 0);
        foreach (var triangle in fan)
            if (TryWeights(logicalPixel, triangle.LogicalA, triangle.LogicalB, triangle.LogicalC, out var weights))
                return Blend(triangle.ProjectedA, triangle.ProjectedB, triangle.ProjectedC, weights);
        throw new InvalidOperationException($"Grid cell {cell} does not contain its logical point {logicalPixel} in the supplied outline.");
    }

    public Vector2 CellCenter(Coord cell) => Project(Pixel(_grid.Center(cell)));

    public Vector2 LogicalCellContact(Coord cell) => Pixel(_grid.Center(cell)) + new Vector2(0, 15);
    public Vector2 CellContact(Coord cell) => Project(LogicalCellContact(cell));

    public Vector2 FacingVector(string? facing)
    {
        if (facing is null)
            return Vector2.Zero;
        var origin = _grid.Center(new Coord(0, 0));
        var target = _grid.Center(_grid.Step(new Coord(0, 0), facing));
        return GroundPlane(new Vector2((float)(target.X - origin.X), (float)(target.Y - origin.Y)));
    }

    public float Depth(Vector2 logicalPixel)
    {
        RequireFinite(logicalPixel);
        return GroundPlane(logicalPixel).Y;
    }

    public int DrawDepth(Vector2 logicalPixel, BoardDepthLayer layer) => LayerAtDepth(Depth(logicalPixel), layer);

    public int LayerAtDepth(float cameraDepth, BoardDepthLayer layer)
    {
        if (!float.IsFinite(cameraDepth) || !Enum.IsDefined(layer))
            throw new ArgumentOutOfRangeException(nameof(cameraDepth));
        var depth = (Math.Floor(cameraDepth) + 4 * TileSize) * 4 - 4096 + (int)layer;
        if (depth < -4096 || depth > 4096)
            throw new ArgumentOutOfRangeException(nameof(cameraDepth), "Board draw depth is outside Godot's absolute Z range.");
        return (int)depth;
    }

    public float SurfaceHeight(Vector2 logical, Vector2 projected) => Depth(logical) + _plane.Origin.Y - projected.Y;

    public BoardSurfaceHit? Pick(Vector2 projectedPixel)
    {
        RequireFinite(projectedPixel);
        Coord? nearest = null;
        var nearestDepth = float.NegativeInfinity;
        foreach (var triangle in Triangles)
        {
            if (!TryWeights(projectedPixel, triangle.ProjectedA, triangle.ProjectedB, triangle.ProjectedC, out var weights))
                continue;
            var logical = Blend(triangle.LogicalA, triangle.LogicalB, triangle.LogicalC, weights);
            var cell = _grid.CellAt(new TilePoint(logical.X / TileSize, logical.Y / TileSize));
            if (cell != triangle.Cell || !_playable.Contains(cell))
                continue;
            var depth = Depth(logical);
            if (depth > nearestDepth || depth == nearestDepth &&
                (nearest is null || cell.Y > nearest.Value.Y || cell.Y == nearest.Value.Y && cell.X > nearest.Value.X))
            {
                nearest = cell;
                nearestDepth = depth;
            }
        }
        foreach (var face in Faces)
        {
            Vector2 logical;
            if (TryWeights(projectedPixel, face.ProjectedA, face.ProjectedB, face.ProjectedC, out var weights))
                logical = Blend(face.LogicalA, face.LogicalB, face.LogicalB, weights);
            else if (TryWeights(projectedPixel, face.ProjectedA, face.ProjectedC, face.ProjectedD, out weights))
                logical = Blend(face.LogicalA, face.LogicalB, face.LogicalA, weights);
            else
                continue;
            var depth = Depth(logical);
            if (depth >= nearestDepth)
            {
                nearest = null;
                nearestDepth = depth;
            }
        }
        return float.IsNegativeInfinity(nearestDepth) ? null : new BoardSurfaceHit(nearest, nearestDepth);
    }

    public bool MatchesPlane(BoardPlane plane) =>
        _plane.BasisX.IsEqualApprox(plane.BasisX) && _plane.BasisY.IsEqualApprox(plane.BasisY)
        && _plane.Origin.IsEqualApprox(plane.Origin) && _plane.CanvasSize.IsEqualApprox(plane.CanvasSize);

    public bool MatchesSurface(JsonObject map, IReadOnlySet<Coord> playable) =>
        _playable.SetEquals(playable) && MatchesTerrainGeometry(map);

    public bool MatchesTerrainGeometry(JsonObject map)
    {
        if (Integer(map["width"], "map width") != _surfaceWidth
            || Integer(map["height"], "map height") != _surfaceHeight
            || map["elevation"] is not JsonArray elevation || map["cliffs"] is not JsonArray cliffs)
            return false;
        if (!_surfaceHeights.SequenceEqual(elevation.Select((node, index) => Integer(node, $"elevation[{index}]"))))
            return false;
        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in cliffs)
        {
            if (node is not JsonObject edge || edge["from"] is not JsonObject from || edge["to"] is not JsonObject to)
                return false;
            edges.Add(GridStorage.EdgeKey(new Coord(Integer(from["x"], "cliff from x"), Integer(from["y"], "cliff from y")),
                new Coord(Integer(to["x"], "cliff to x"), Integer(to["y"], "cliff to y"))));
        }
        return _surfaceCliffs.SetEquals(edges);
    }

    public void PrepareDepthPartitions(IEnumerable<float> standingCameraDepths, IEnumerable<float> movementCameraDepths)
    {
        ArgumentNullException.ThrowIfNull(standingCameraDepths);
        ArgumentNullException.ThrowIfNull(movementCameraDepths);
        if (Triangles.Count == 0)
            return;
        var first = (int)Math.Floor(Triangles.Min(triangle => Math.Min(Depth(triangle.LogicalA),
            Math.Min(Depth(triangle.LogicalB), Depth(triangle.LogicalC)))));
        var end = (int)Math.Ceiling(Triangles.Max(triangle => Math.Max(Depth(triangle.LogicalA),
            Math.Max(Depth(triangle.LogicalB), Depth(triangle.LogicalC)))));
        var cuts = new HashSet<int>(_fixedDepthCuts.Where(depth => depth >= first && depth <= end)) { first, end };
        foreach (var depth in standingCameraDepths.Concat(movementCameraDepths))
        {
            _ = LayerAtDepth(depth, BoardDepthLayer.Standing);
            var cut = (int)Math.Floor(depth) + 1;
            if (cut > first && cut < end)
                cuts.Add(cut);
        }
        var ordered = cuts.Order().ToArray();
        if (_partitionCuts.SequenceEqual(ordered))
            return;
        SetDepthPartitions(ordered);
        DepthPartitionsChanged?.Invoke();
    }

    private void SetDepthPartitions(int[] cuts)
    {
        var currentPatches = CreateDepthPatches(Triangles, cuts).AsReadOnly();
        var groups = new SortedDictionary<int, List<BoardSurfacePatch>>();
        foreach (var patch in currentPatches.OrderBy(patch => patch.Depth))
        {
            var index = Array.BinarySearch(cuts, patch.Depth);
            if (index < 0)
                index = ~index - 1;
            if (!groups.TryGetValue(index, out var patches))
                groups.Add(index, patches = new List<BoardSurfacePatch>());
            patches.Add(patch);
        }
        var partitions = groups.Select(pair => new BoardDepthPartition(cuts[pair.Key], cuts[pair.Key + 1],
            pair.Value.AsReadOnly())).ToList().AsReadOnly();
        _partitionCuts = cuts;
        DepthPatches = currentPatches;
        DepthPartitions = partitions;
    }

    private List<BoardSurfacePatch> CreateDepthPatches(IReadOnlyList<BoardSurfaceTriangle> triangles, int[] cuts)
    {
        var patches = new List<BoardSurfacePatch>();
        foreach (var triangle in triangles)
        {
            var source = new List<BoardSurfaceVertex>
            {
                new(triangle.LogicalA, triangle.ProjectedA, new Vector3(1, 0, 0)),
                new(triangle.LogicalB, triangle.ProjectedB, new Vector3(0, 1, 0)),
                new(triangle.LogicalC, triangle.ProjectedC, new Vector3(0, 0, 1)),
            };
            var first = (int)Math.Floor(source.Min(vertex => Depth(vertex.Logical)));
            var end = (int)Math.Ceiling(source.Max(vertex => Depth(vertex.Logical)));
            var startIndex = Array.BinarySearch(cuts, first);
            if (startIndex < 0)
                startIndex = ~startIndex - 1;
            for (var index = startIndex; index + 1 < cuts.Length && cuts[index] < end; index++)
            {
                var depth = cuts[index];
                var polygon = ClipDepth(source, depth, true);
                polygon = ClipDepth(polygon, cuts[index + 1], false);
                polygon = polygon.Distinct().ToList();
                if (polygon.Count < 3)
                    continue;
                var area = 0f;
                for (var vertex = 1; vertex + 1 < polygon.Count; vertex++)
                    area += Cross(polygon[vertex].Logical - polygon[0].Logical,
                        polygon[vertex + 1].Logical - polygon[0].Logical);
                if (Math.Abs(area) > Epsilon)
                    patches.Add(new BoardSurfacePatch(depth, triangle, polygon.AsReadOnly()));
            }
        }
        return patches;
    }

    public IReadOnlyList<BoardFacePatch> FacePatches(BoardSurfaceFace face)
    {
        var source = new List<BoardSurfaceVertex>
        {
            new(face.LogicalA, face.ProjectedA, Vector3.Zero),
            new(face.LogicalB, face.ProjectedB, Vector3.Zero),
            new(face.LogicalB, face.ProjectedC, Vector3.Zero),
            new(face.LogicalA, face.ProjectedD, Vector3.Zero)
        };
        var result = new List<BoardFacePatch>();
        for (var index = 0; index + 1 < _partitionCuts.Length; index++)
        {
            var polygon = ClipDepth(source, _partitionCuts[index], true);
            polygon = ClipDepth(polygon, _partitionCuts[index + 1], false).Distinct().ToList();
            if (polygon.Count < 3)
                continue;
            var area = 0f;
            for (var vertex = 1; vertex + 1 < polygon.Count; vertex++)
                area += Cross(polygon[vertex].Projected - polygon[0].Projected,
                    polygon[vertex + 1].Projected - polygon[0].Projected);
            if (Math.Abs(area) > Epsilon)
                result.Add(new BoardFacePatch(_partitionCuts[index], polygon.AsReadOnly()));
        }
        return result.AsReadOnly();
    }

    private List<BoardSurfaceVertex> ClipDepth(List<BoardSurfaceVertex> polygon, float boundary, bool keepAbove)
    {
        var result = new List<BoardSurfaceVertex>();
        if (polygon.Count == 0)
            return result;
        var previous = polygon[^1];
        var previousDistance = keepAbove ? Depth(previous.Logical) - boundary : boundary - Depth(previous.Logical);
        foreach (var current in polygon)
        {
            var distance = keepAbove ? Depth(current.Logical) - boundary : boundary - Depth(current.Logical);
            if ((distance > 0 && previousDistance < 0) || (distance < 0 && previousDistance > 0))
            {
                var amount = previousDistance / (previousDistance - distance);
                var logical = previous.Logical.Lerp(current.Logical, amount);
                result.Add(new BoardSurfaceVertex(logical,
                    previous.Projected.Lerp(current.Projected, amount),
                    previous.SourceWeights.Lerp(current.SourceWeights, amount)));
            }
            if (distance >= 0)
                result.Add(current);
            previous = current;
            previousDistance = distance;
        }
        return result;
    }

    private Vector2 GroundPlane(Vector2 point) => _plane.Ground(point);
    private Vector2 Raise(Vector2 point, float height) => GroundPlane(point) + _plane.Origin - new Vector2(0, height * HeightStep);
    private static Vector2 Pixel(TilePoint point) => new((float)point.X * TileSize, (float)point.Y * TileSize);
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static Vector2 Blend(Vector2 a, Vector2 b, Vector2 c, Vector3 weights) => a * weights.X + b * weights.Y + c * weights.Z;

    private static bool TryWeights(Vector2 point, Vector2 a, Vector2 b, Vector2 c, out Vector3 weights)
    {
        var area = Cross(b - a, c - a);
        if (Math.Abs(area) <= Epsilon)
        {
            weights = default;
            return false;
        }
        var second = Cross(point - a, c - a) / area;
        var third = Cross(b - a, point - a) / area;
        weights = new Vector3(1 - second - third, second, third);
        return weights.X >= -Epsilon && weights.Y >= -Epsilon && weights.Z >= -Epsilon;
    }

    private static (long X, long Y) CornerKey(Vector2 point)
    {
        RequireFinite(point);
        // Float roundoff from composed non-square outlines must not split one
        // shared vertex into several independently averaged height samples.
        return ((long)Math.Round(point.X * 1000d), (long)Math.Round(point.Y * 1000d));
    }

    private static void RequireFinite(Vector2 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            throw new ArgumentException("Board projection coordinates must be finite.");
    }

    private static int Integer(JsonNode? node, string subject)
    {
        if (node is JsonValue scalar && scalar.GetValueKind() == JsonValueKind.Number
            && double.TryParse(scalar.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number)
            && number == Math.Truncate(number) && number >= int.MinValue && number <= int.MaxValue)
            return (int)number;
        throw new ArgumentException($"{subject} must be an integer.");
    }
}
