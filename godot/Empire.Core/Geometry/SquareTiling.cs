namespace Empire.Core.Geometry;

public abstract class SquareTiling : ITacticalGrid
{
    public abstract string Id
    {
        get;
    }
    public abstract IReadOnlyList<DirectionDef> Directions
    {
        get;
    }
    public string RestingFacing => "south";
    public abstract int Distance(Coord a, Coord b);

    public Coord Step(Coord at, string direction)
    {
        // The source admits all square vectors here, even for square4. Directions
        // controls adjacency; tightening Step would change the existing contract.
        var vector = direction switch
        {
            "north" => new Coord(0, -1),
            "east" => new Coord(1, 0),
            "south" => new Coord(0, 1),
            "west" => new Coord(-1, 0),
            "northeast" => new Coord(1, -1),
            "southeast" => new Coord(1, 1),
            "southwest" => new Coord(-1, 1),
            "northwest" => new Coord(-1, -1),
            _ => throw GridDirections.Unknown(Id, direction),
        };
        return new Coord(checked(at.X + vector.X), checked(at.Y + vector.Y));
    }

    public string Opposite(string direction) => GridDirections.Opposite(Id, direction);

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
        for (var dy = -(long)max; dy <= max; dy++)
        {
            for (var dx = -(long)max; dx <= max; dx++)
            {
                var cell = new Coord(checked((int)(at.X + dx)), checked((int)(at.Y + dy)));
                var steps = Distance(at, cell);
                if (steps >= min && steps <= max)
                    result.Add(cell);
            }
        }
        return result;
    }

    public virtual string Toward(Coord from, Coord to)
    {
        var dx = (long)to.X - from.X;
        var dy = (long)to.Y - from.Y;
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx >= 0 ? "east" : "west";
        return dy >= 0 ? "south" : "north";
    }

    public IReadOnlyList<Coord> Line(Coord from, Coord to) => GridStorage.LineBetween(from, to);

    public TilePoint Center(Coord at) => new(at.X + 0.5, at.Y + 0.5);

    public Coord CellAt(TilePoint point) =>
        new(checked((int)Math.Floor(point.X)), checked((int)Math.Floor(point.Y)));

    public IReadOnlyList<TilePoint> Outline() => new TilePoint[]
    {
        new(-0.5, -0.5), new(0.5, -0.5), new(0.5, 0.5), new(-0.5, 0.5),
    };

    public TilePoint Extent(GridExtent size) => new(size.Width, size.Height);
}

public sealed class Square4Grid : SquareTiling
{
    public override string Id => "square4";
    public override IReadOnlyList<DirectionDef> Directions => GridDirections.Square;

    public override int Distance(Coord a, Coord b) =>
        checked((int)(Math.Abs((long)a.X - b.X) + Math.Abs((long)a.Y - b.Y)));
}

public sealed class Square8Grid : SquareTiling
{
    private static readonly IReadOnlyList<DirectionDef> Facings =
        Array.AsReadOnly(GridDirections.Square.Concat(new DirectionDef[]
        {
            new("northeast"), new("southeast"),
            new("southwest"), new("northwest"),
        }).ToArray());

    private static readonly string[] Sectors =
    {
        "east", "southeast", "south", "southwest", "west", "northwest", "north", "northeast",
    };

    public override string Id => "square8";
    public override IReadOnlyList<DirectionDef> Directions => Facings;

    public override int Distance(Coord a, Coord b) =>
        checked((int)Math.Max(Math.Abs((long)a.X - b.X), Math.Abs((long)a.Y - b.Y)));

    public override string Toward(Coord from, Coord to)
    {
        var dx = (double)to.X - from.X;
        var dy = (double)to.Y - from.Y;
        if (dx == 0 && dy == 0)
            return "south";
        var sector = ((int)GridDirections.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) % 8 + 8) % 8;
        return Sectors[sector];
    }
}
