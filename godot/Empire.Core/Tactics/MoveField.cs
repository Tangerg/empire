using Empire.Core.Geometry;

namespace Empire.Core.Tactics;

public sealed record ReachableTile(int Index, int X, int Y, int Cost, int From, bool Free)
{
    public int Index { get; set; } = Index;
    public int X { get; set; } = X;
    public int Y { get; set; } = Y;
    public int Cost { get; set; } = Cost;
    public int From { get; set; } = From;
    public bool Free { get; set; } = Free;
}

public sealed class MoveField
{
    public Coord Origin
    {
        get; set;
    }
    public int Unit
    {
        get; set;
    }
    public Dictionary<int, ReachableTile> Tiles
    {
        get; set;
    }
    public HashSet<int> Stops
    {
        get; set;
    }

    public MoveField(Coord origin, int unit, Dictionary<int, ReachableTile> tiles, HashSet<int>? stops = null)
    {
        Origin = origin;
        Unit = unit;
        Tiles = tiles;
        Stops = stops ?? tiles.Values.Where(tile => tile.Free).Select(tile => tile.Index).ToHashSet();
    }

    public IReadOnlyList<Coord>? TracePath(int target)
    {
        if (!Tiles.ContainsKey(target))
            return null;
        var path = new List<Coord>();
        var current = target;
        var guard = Tiles.Count + 2;
        for (var count = 0; count <= guard; count++)
        {
            if (!Tiles.TryGetValue(current, out var node))
                return null;
            path.Add(new(node.X, node.Y));
            if (node.From == -1)
            {
                path.Reverse();
                return path;
            }
            current = node.From;
        }
        return null;
    }
}
