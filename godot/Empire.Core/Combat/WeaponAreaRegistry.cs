using Empire.Core.Geometry;
using Empire.Core.Registries;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed record WeaponAreaShape(
    string Id,
    bool NeedsOccupant,
    Func<TacticalBoard, Coord, Coord, IReadOnlyList<Coord>> Cells);

/// <summary>Per-engine shape policies shared by combat planning, delayed casts and threat queries.</summary>
public sealed class WeaponAreaRegistry : KeyedRegistry<WeaponAreaShape, WeaponAreaRegistry>
{
    public WeaponAreaRegistry() : base("weapon area shape") { }
    protected override string KeyOf(WeaponAreaShape entry) => entry.Id;
    protected override void ValidateEntry(WeaponAreaShape entry) => ArgumentNullException.ThrowIfNull(entry.Cells);
    public WeaponAreaRegistry Clone() => CopyInto(new WeaponAreaRegistry());

    public IReadOnlyList<Coord> Coverage(TacticalBoard board, Coord from, Coord target, string area)
    {
        var seen = new HashSet<int>();
        var cells = new List<Coord>();
        foreach (var cell in Get(area).Cells(board, from, target))
            if (board.Contains(cell) && seen.Add(board.IndexOf(cell)))
                cells.Add(cell);
        return cells.AsReadOnly();
    }

    /// <summary>Unsealed built-in definitions for the application composition root to install or replace.</summary>
    public static WeaponAreaRegistry Core() => new WeaponAreaRegistry()
        .Register(new("single", true, (_, _, target) => new[] { target }))
        .Register(new("cross1", false, (board, _, target) => board.Ring(target, 0, 1)))
        .Register(new("ring1", false, (_, _, target) =>
            Enumerable.Range(-1, 3).SelectMany(y =>
                Enumerable.Range(-1, 3).Select(x => new Coord(target.X + x, target.Y + y))).ToArray()))
        .Register(new("line", false, (board, from, target) => board.Grid.Line(from, target).Skip(1).ToArray()));
}
