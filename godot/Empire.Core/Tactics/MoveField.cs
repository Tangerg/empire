using System.Collections.ObjectModel;
using Empire.Core.Geometry;

namespace Empire.Core.Tactics;

public sealed record ReachableTile(int Index, int X, int Y, int Cost, int From, bool Free);

public sealed class MoveField
{
    public Coord Origin { get; }
    public int Unit { get; }
    public IReadOnlyDictionary<int, ReachableTile> Tiles { get; }
    public IReadOnlySet<int> Stops { get; }

    internal MoveField(Coord origin, int unit, Dictionary<int, ReachableTile> tiles)
    {
        Origin = origin;
        Unit = unit;
        Tiles = new ReadOnlyDictionary<int, ReachableTile>(tiles);
        Stops = tiles.Values.Where(tile => tile.Free).Select(tile => tile.Index).ToHashSet();
    }
}
