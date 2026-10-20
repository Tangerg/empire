namespace Empire.Core.Geometry;

public readonly record struct Coord(int X, int Y);

public readonly record struct TilePoint(double X, double Y);

public readonly record struct GridExtent(int Width, int Height);

public readonly record struct DirectionDef(string Id, string Name);

/// <summary>
/// Owns both tactical geometry and the corresponding tile-space presentation.
/// Coordinates are unclipped; board bounds and movement rules belong to callers.
/// </summary>
public interface ITacticalGrid
{
    string Id { get; }
    string Name { get; }
    IReadOnlyList<DirectionDef> Directions { get; }
    string RestingFacing { get; }
    int Distance(Coord a, Coord b);
    Coord Step(Coord at, string direction);
    string Opposite(string direction);
    string Toward(Coord from, Coord to);
    IReadOnlyList<Coord> Adjacent(Coord at);
    IReadOnlyList<Coord> Within(Coord at, int min, int max);
    IReadOnlyList<Coord> Line(Coord from, Coord to);
    TilePoint Center(Coord at);
    Coord CellAt(TilePoint point);
    IReadOnlyList<TilePoint> Outline();
    TilePoint Extent(GridExtent size);
}
