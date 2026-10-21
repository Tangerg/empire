namespace Empire.Core.Geometry;

/// <summary>Pointy-top hexagons in rectangular odd-row offset storage.</summary>
public sealed class OddRowHexGrid : ITacticalGrid
{
    private static readonly IReadOnlyList<DirectionDef> Facings = Array.AsReadOnly(new DirectionDef[]
    {
        new("hexNortheast"), new("hexEast"), new("hexSoutheast"),
        new("hexSouthwest"), new("hexWest"), new("hexNorthwest"),
    });

    private static readonly double RowSpacing = Math.Sqrt(3) / 2;
    private static readonly double Radius = 1 / Math.Sqrt(3);

    public string Id => "hex";
    public string RestingFacing => "hexSoutheast";
    public IReadOnlyList<DirectionDef> Directions => Facings;

    public int Distance(Coord a, Coord b)
    {
        var left = ToCube(a);
        var right = ToCube(b);
        return checked((int)Math.Max(Math.Abs(left.Q - right.Q),
            Math.Max(Math.Abs(left.R - right.R), Math.Abs(left.S - right.S))));
    }

    public Coord Step(Coord at, string direction)
    {
        var odd = at.Y & 1;
        checked
        {
            return direction switch
            {
                "hexEast" => new Coord(at.X + 1, at.Y),
                "hexWest" => new Coord(at.X - 1, at.Y),
                "hexNortheast" => new Coord(at.X + odd, at.Y - 1),
                "hexNorthwest" => new Coord(at.X + (odd - 1), at.Y - 1),
                "hexSoutheast" => new Coord(at.X + odd, at.Y + 1),
                "hexSouthwest" => new Coord(at.X + (odd - 1), at.Y + 1),
                _ => throw GridDirections.Unknown(Id, direction),
            };
        }
    }

    public string Opposite(string direction) => GridDirections.Opposite(Id, direction);

    public string Toward(Coord from, Coord to)
    {
        var best = Directions[0].Id;
        var bestDistance = double.PositiveInfinity;
        foreach (var direction in Directions)
        {
            var candidate = Distance(Step(from, direction.Id), to);
            if (candidate < bestDistance)
            {
                bestDistance = candidate;
                best = direction.Id;
            }
        }
        return best;
    }

    public IReadOnlyList<Coord> Adjacent(Coord at)
    {
        var result = new Coord[Directions.Count];
        for (var index = 0; index < Directions.Count; index++)
            result[index] = Step(at, Directions[index].Id);
        return result;
    }

    public IReadOnlyList<Coord> Within(Coord at, int min, int max)
    {
        var result = new List<Coord>();
        if (max < 0)
            return result;
        var origin = ToCube(at);
        for (var dq = -(long)max; dq <= max; dq++)
        {
            for (var dr = Math.Max(-(long)max, -dq - max); dr <= Math.Min(max, -dq + max); dr++)
            {
                var steps = Math.Max(Math.Abs(dq), Math.Max(Math.Abs(dr), Math.Abs(-dq - dr)));
                if (steps < min || steps > max)
                    continue;
                result.Add(FromCube(new Cube(origin.Q + dq, origin.R + dr, origin.S - dq - dr)));
            }
        }
        return result;
    }

    public IReadOnlyList<Coord> Line(Coord from, Coord to)
    {
        var steps = Distance(from, to);
        if (steps == 0)
            return new[] { from };
        var start = ToCube(from);
        var end = ToCube(to);
        var result = new List<Coord>();
        for (long step = 0; step <= steps; step++)
        {
            var t = step / (double)steps;
            var cube = RoundCube(
                start.Q + (end.Q - start.Q) * t,
                start.R + (end.R - start.R) * t,
                start.S + (end.S - start.S) * t);
            result.Add(FromCube(cube));
        }
        return result;
    }

    public TilePoint Center(Coord at) =>
        new(at.X + 0.5 + (at.Y & 1) * 0.5, at.Y * RowSpacing + Radius);

    public Coord CellAt(TilePoint point)
    {
        var px = point.X - 0.5;
        var py = point.Y - Radius;
        var q = px - py / Math.Sqrt(3);
        var r = py / RowSpacing;
        return FromCube(RoundCube(q, r, -q - r));
    }

    public IReadOnlyList<TilePoint> Outline()
    {
        var result = new TilePoint[6];
        for (var corner = 0; corner < result.Length; corner++)
        {
            var angle = (Math.PI / 180) * (60 * corner - 90);
            result[corner] = new TilePoint(Radius * Math.Cos(angle), Radius * Math.Sin(angle));
        }
        return result;
    }

    public TilePoint Extent(GridExtent size) =>
        new(size.Width + (size.Height > 1 ? 0.5 : 0),
            ((double)size.Height - 1) * RowSpacing + Radius * 2);

    private readonly record struct Cube(double Q, double R, double S);

    private static Cube ToCube(Coord at)
    {
        var q = at.X - (at.Y - (at.Y & 1)) / 2.0;
        var r = (double)at.Y;
        return new Cube(q, r, -q - r);
    }

    private static Coord FromCube(Cube cube)
    {
        var row = checked((int)cube.R);
        return new Coord(checked((int)(cube.Q + (cube.R - (row & 1)) / 2)), row);
    }

    private static Cube RoundCube(double q, double r, double s)
    {
        var rq = GridDirections.Round(q);
        var rr = GridDirections.Round(r);
        var rs = GridDirections.Round(s);
        var dq = Math.Abs(rq - q);
        var dr = Math.Abs(rr - r);
        var ds = Math.Abs(rs - s);
        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;
        else
            rs = -rq - rr;
        return new Cube(rq, rr, rs);
    }
}
