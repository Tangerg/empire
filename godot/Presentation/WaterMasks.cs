#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Technical coverage ports of the original river and playable-shape adapters.</summary>
internal static class WaterMasks
{
    internal static Image Rasterize(int width, int height, string geometry)
    {
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Mask extent must be positive.");
        var image = new Image();
        var error = image.LoadSvgFromString($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width * 32}\" height=\"{height * 32}\">{geometry}</svg>", 2);
        if (error != Error.Ok)
        {
            image.Dispose();
            throw new InvalidOperationException($"Terrain alpha rasterization failed: {error}");
        }
        return image;
    }

    public static Image River(int width, int height, RiverProfile profile,
        Func<int, int, bool> occupied, Func<int, int, bool> bankAllowed, bool bank)
    {
        ValidateRiver(profile);
        var cells = new List<BoardCell>();
        var allowed = new List<BoardCell>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (occupied(x, y)) cells.Add(new BoardCell(x, y));
            if (bank ? bankAllowed(x, y) : occupied(x, y)) allowed.Add(new BoardCell(x, y));
        }
        var rim = (profile.BankWidth - profile.Width) / 2;
        var limits = string.Concat(cells.Select(cell => $"<rect x=\"{N(cell.X * 32 - rim)}\" y=\"{N(cell.Y * 32 - rim)}\" width=\"{N(32 + rim * 2)}\" height=\"{N(32 + rim * 2)}\" rx=\"{N(rim)}\"/>"));
        var path = profile.Widths is { } widths
            ? $"<path d=\"{Ribbon(profile, widths, bank ? rim : 0)}\" fill=\"white\"/>"
            : $"<path d=\"{Centerline(profile)}\" fill=\"none\" stroke=\"white\" stroke-width=\"{N(bank ? profile.BankWidth : profile.Width)}\" stroke-linecap=\"butt\"/>";
        return Rasterize(width, height,
            $"<defs><clipPath id=\"terrain\">{CellUnion(allowed)}</clipPath><clipPath id=\"rim\">{limits}</clipPath></defs>"
            + $"<g clip-path=\"url(#terrain)\"><g clip-path=\"url(#rim)\">{path}</g></g>");
    }

    public static Image PlayableShape(int width, int height, Func<int, int, bool> outside)
    {
        bool Exposed(int x, int y) => x < 0 || y < 0 || x >= width || y >= height || outside(x, y);
        var rows = new List<string>();
        for (var y = 0; y < height; y++)
        {
            int? start = null;
            for (var x = 0; x <= width; x++)
            {
                var filled = x < width && !outside(x, y);
                if (filled && start is null) start = x;
                if (filled || start is not { } first) continue;
                var left = first * 32;
                var right = x * 32;
                var top = y * 32;
                var bottom = (y + 1) * 32;
                var tl = Exposed(first, y - 1) ? 12 : 0;
                var tr = Exposed(x - 1, y - 1) ? 12 : 0;
                var bl = Exposed(first, y + 1) ? 12 : 0;
                var br = Exposed(x - 1, y + 1) ? 12 : 0;
                rows.Add($"M{left + tl} {top}H{right - tr}Q{right} {top} {right} {top + tr}"
                    + $"V{bottom - br}Q{right} {bottom} {right - br} {bottom}"
                    + $"H{left + bl}Q{left} {bottom} {left} {bottom - bl}"
                    + $"V{top + tl}Q{left} {top} {left + tl} {top}Z");
                start = null;
            }
        }
        return Rasterize(width, height, $"<path fill=\"white\" d=\"{string.Concat(rows)}\"/>");
    }

    internal static void ValidateRiver(RiverProfile profile)
    {
        if (!double.IsFinite(profile.Width) || !double.IsFinite(profile.BankWidth)
            || !(profile.Width > 0 && profile.BankWidth > profile.Width && profile.BankWidth <= 64
                && profile.BankWidth - profile.Width <= 10)
            || profile.Knots.Length < 2 || profile.Knots.Any(point => point.Length != 2 || point.Any(value => !double.IsFinite(value)))
            || profile.Knots.Skip(1).Where((point, index) => point[1] <= profile.Knots[index][1]).Any()
            || profile.Widths is { } widths && (widths.Length != profile.Knots.Length
                || widths.Any(value => !double.IsFinite(value) || value < 16 || value > profile.Width)))
            throw new InvalidOperationException("River requires positive bounded widths and strictly downstream knots.");
    }
    private static string Centerline(RiverProfile profile)
    {
        var points = profile.Knots.Select(point => (X: point[0] * 32, Y: point[1] * 32)).ToArray();
        return $"M {Point(points[0])}" + Trace(points);
    }
    private static string Ribbon(RiverProfile profile, double[] widths, double rim)
    {
        (double X, double Y)[] Side(int sign) => profile.Knots.Select((point, index) =>
            (point[0] * 32 + sign * (widths[index] / 2 + rim), point[1] * 32)).ToArray();
        var left = Side(-1);
        var right = Side(1).Reverse().ToArray();
        return $"M {Point(left[0])}" + Trace(left) + $" L {Point(right[0])}" + Trace(right) + " Z";
    }
    private static string Trace((double X, double Y)[] points) => string.Concat(points.Skip(1).Select((point, index) =>
    {
        var previous = points[index];
        var delta = (point.Y - previous.Y) / 3;
        return $" C {N(previous.X)} {N(previous.Y + delta)} {N(point.X)} {N(point.Y - delta)} {Point(point)}";
    }));
    internal static string CellUnion(IEnumerable<BoardCell> cells) => "<path fill-rule=\"nonzero\" clip-rule=\"nonzero\" d=\""
        + string.Concat(cells.Select(cell => $"M{cell.X * 32} {cell.Y * 32}h32v32h-32Z")) + "\"/>";
    private static string Point((double X, double Y) point) => $"{N(point.X)} {N(point.Y)}";
    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
