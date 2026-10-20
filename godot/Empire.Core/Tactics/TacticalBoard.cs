using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed class TacticalBoard
{
    public JsonObject Map { get; }
    public ITacticalGrid Grid { get; }
    public GridExtent Extent { get; }

    public TacticalBoard(JsonObject map, ITacticalGrid grid)
    {
        Map = map;
        Grid = grid;
        Extent = new GridExtent(StateJson.Integer(map, "width"), StateJson.Integer(map, "height"));
    }

    public bool Contains(Coord at) => GridStorage.InBounds(Extent, at.X, at.Y);
    public int IndexOf(Coord at) => GridStorage.Index(Extent, at.X, at.Y);
    public Coord CoordOf(int index) => new(index % Extent.Width, index / Extent.Width);
    public IReadOnlyList<Coord> Neighbours(Coord at) => Grid.Adjacent(at).Where(Contains).ToArray();
    public IReadOnlyList<Coord> Ring(Coord at, int minimum, int maximum) =>
        Grid.Within(at, minimum, maximum).Where(Contains).ToArray();
}
