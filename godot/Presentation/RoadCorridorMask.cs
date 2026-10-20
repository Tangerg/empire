#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Native technical alpha port of candidate-01-road-corridor.ts. No movement rules.</summary>
internal static class RoadCorridorMask
{
    private static readonly BoardCell[] Directions = [new(-1, 0), new(0, -1), new(0, 1), new(1, 0)];

    public static Image Create(int width, int height, RoadCorridorProfile profile,
        Func<int, int, bool> painted, Func<int, int, bool> connected, Func<int, int, bool> plain)
    {
        var broad = profile.Width == 32 && profile.MaxTangentCut == 22 && profile.MaxPlainOverhang == 2;
        var narrow = profile.Width == 20 && profile.MaxTangentCut == 16 && profile.MaxPlainOverhang == 0;
        if (!broad && !narrow)
            throw new InvalidOperationException("Road corridor requires the reviewed 32/22/2 or 20/16/0 profile.");
        var cells = new List<BoardCell>();
        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
            if (painted(x, y)) cells.Add(new BoardCell(x, y));
        var occupied = cells.ToHashSet();
        var protectedCells = profile.ProtectedCells.Select(pair => pair.Length == 2
            ? new BoardCell(pair[0], pair[1]) : throw new InvalidOperationException("Invalid protected road cell.")).ToHashSet();
        if (cells.Any(protectedCells.Contains))
            throw new InvalidOperationException("Road overlaps a protected scene cell.");
        BoardCell[] Neighbors(BoardCell cell) => !connected(cell.X, cell.Y) ? [] : Directions
            .Select(delta => new BoardCell(cell.X + delta.X, cell.Y + delta.Y))
            .Where(next => occupied.Contains(next) && connected(next.X, next.Y)).ToArray();
        var visited = new HashSet<(BoardCell, BoardCell)>();
        var paths = new List<string>();
        foreach (var start in cells.OrderBy(cell => Neighbors(cell).Length == 2).ThenBy(cell => cell.X).ThenBy(cell => cell.Y))
        foreach (var next in Neighbors(start))
        {
            if (!visited.Add(Edge(start, next))) continue;
            var chain = new List<BoardCell> { start, next };
            var current = next;
            while (Neighbors(current).Length == 2)
            {
                var after = Neighbors(current).First(cell => cell != chain[^2]);
                if (!visited.Add(Edge(current, after))) break;
                chain.Add(after);
                current = after;
            }
            paths.Add(Rounded(chain, (float)profile.MaxTangentCut));
        }
        var filled = new List<string>();
        var limits = new List<string>();
        foreach (var cell in cells)
        {
            var center = Center(cell);
            if (Neighbors(cell).Length == 0)
                filled.Add($"<circle cx=\"{N(center.X)}\" cy=\"{N(center.Y)}\" r=\"{N(profile.Width / 2)}\"/>");
            foreach (var delta in Directions)
            {
                var next = new BoardCell(cell.X + delta.X, cell.Y + delta.Y);
                var outside = next.X < 0 || next.Y < 0 || next.X >= width || next.Y >= height;
                if (connected(cell.X, cell.Y) && (outside || !occupied.Contains(next) && connected(next.X, next.Y)))
                    paths.Add($"M {Point(center)} L {Point(center + new Vector2(delta.X, delta.Y) * 16)}");
            }
            if (broad && new[] { cell, new BoardCell(cell.X + 1, cell.Y),
                    new BoardCell(cell.X, cell.Y + 1), new BoardCell(cell.X + 1, cell.Y + 1) }
                .All(point => occupied.Contains(point) && connected(point.X, point.Y)))
                filled.Add(Rect(center.X, center.Y, 32, 32));
            var overhang = profile.MaxPlainOverhang;
            if (overhang > 0)
                limits.Add(Rect(cell.X * 32 - overhang, cell.Y * 32 - overhang,
                    32 + overhang * 2, 32 + overhang * 2, $" rx=\"{N(overhang)}\""));
            else limits.Add(Rect(cell.X * 32, cell.Y * 32, 32, 32));
        }
        var allowed = new List<string>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var cell = new BoardCell(x, y);
            if (!protectedCells.Contains(cell) && (occupied.Contains(cell) || plain(x, y)))
                allowed.Add(Rect(x * 32, y * 32, 32, 32));
        }
        // Godot's SVG rasterizer creates only technical coverage; source RGB remains a native texture.
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width * 32}\" height=\"{height * 32}\">"
            + $"<defs><clipPath id=\"limits\">{string.Concat(limits)}</clipPath>"
            + $"<clipPath id=\"allowed\">{string.Concat(allowed)}</clipPath></defs>"
            + "<g clip-path=\"url(#allowed)\"><g clip-path=\"url(#limits)\">"
            + $"<g fill=\"none\" stroke=\"white\" stroke-width=\"{N(profile.Width)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\">"
            + string.Concat(paths.Select(path => $"<path d=\"{path}\"/>")) + "</g>"
            + $"<g fill=\"white\">{string.Concat(filled)}</g></g></g></svg>";
        var image = new Image();
        var error = image.LoadSvgFromString(svg, 2);
        if (error != Error.Ok) throw new InvalidOperationException($"Native road alpha rasterization failed: {error}");
        return image;
    }

    private static string Rounded(IReadOnlyList<BoardCell> chain, float cut)
    {
        var closed = chain[0] == chain[^1];
        var points = (closed ? chain.Take(chain.Count - 1) : chain).Select(Center).ToArray();
        var simple = points.Where((point, index) =>
            !closed && (index == 0 || index == points.Length - 1)
            || !Mathf.IsZeroApprox((point - points[(index + points.Length - 1) % points.Length])
                .Cross(points[(index + 1) % points.Length] - point))).ToArray();
        (Vector2 End, string Command) Corner(int index)
        {
            var a = simple[(index + simple.Length - 1) % simple.Length];
            var b = simple[index];
            var c = simple[(index + 1) % simple.Length];
            var incoming = b.DistanceTo(a);
            var outgoing = b.DistanceTo(c);
            var radius = Math.Min(cut, Math.Min(incoming / 2, outgoing / 2));
            var start = b + (a - b) * (radius / incoming);
            var end = b + (c - b) * (radius / outgoing);
            return (end, $"L {Point(start)} Q {Point(b)} {Point(end)}");
        }
        var parts = new List<string> { $"M {Point(closed ? Corner(0).End : simple[0])}" };
        for (var index = 1; index < simple.Length - (closed ? 0 : 1); index++) parts.Add(Corner(index).Command);
        parts.Add(closed ? Corner(0).Command + " Z" : $"L {Point(simple[^1])}");
        return string.Join(" ", parts);
    }
    private static (BoardCell, BoardCell) Edge(BoardCell a, BoardCell b) =>
        a.X < b.X || a.X == b.X && a.Y < b.Y ? (a, b) : (b, a);
    private static Vector2 Center(BoardCell cell) => new((cell.X + 0.5f) * 32, (cell.Y + 0.5f) * 32);
    private static string Rect(double x, double y, double width, double height, string extra = "") =>
        $"<rect x=\"{N(x)}\" y=\"{N(y)}\" width=\"{N(width)}\" height=\"{N(height)}\"{extra}/>";
    private static string Point(Vector2 point) => $"{N(point.X)} {N(point.Y)}";
    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
