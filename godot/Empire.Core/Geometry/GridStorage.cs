using System.Globalization;

namespace Empire.Core.Geometry;

/// <summary>Row-major storage geometry, independent of the battlefield's tiling.</summary>
public static class GridStorage
{
    public static int Index(GridExtent map, int x, int y) => checked(y * map.Width + x);

    public static bool InBounds(GridExtent map, int x, int y) =>
        x >= 0 && y >= 0 && x < map.Width && y < map.Height;

    public static bool SameCoord(Coord a, Coord b) => a == b;

    // Cliffs divide storage rows and columns, even when the tactical grid is hexagonal.
    public static bool SharesEdge(Coord a, Coord b) =>
        Math.Abs((long)a.X - b.X) + Math.Abs((long)a.Y - b.Y) == 1;

    public static string EdgeKey(Coord a, Coord b)
    {
        var left = string.Create(CultureInfo.InvariantCulture, $"{a.X},{a.Y}");
        var right = string.Create(CultureInfo.InvariantCulture, $"{b.X},{b.Y}");
        return string.CompareOrdinal(left, right) <= 0 ? $"{left}|{right}" : $"{right}|{left}";
    }

    /// <summary>Bresenham sight ray including both endpoints, not an orthogonal walk.</summary>
    public static IReadOnlyList<Coord> LineBetween(Coord from, Coord to)
    {
        var result = new List<Coord>();
        var x = from.X;
        var y = from.Y;
        var dx = Math.Abs((long)to.X - from.X);
        var dy = Math.Abs((long)to.Y - from.Y);
        var sx = from.X < to.X ? 1 : -1;
        var sy = from.Y < to.Y ? 1 : -1;
        var error = dx - dy;
        for (; ; )
        {
            result.Add(new Coord(x, y));
            if (x == to.X && y == to.Y)
                return result;
            var twice = error * 2;
            if (twice > -dy)
            {
                error -= dy;
                x += sx;
            }
            if (twice < dx)
            {
                error += dx;
                y += sy;
            }
        }
    }
}
