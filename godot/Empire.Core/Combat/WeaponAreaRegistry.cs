using Empire.Core.Geometry;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed record WeaponAreaShape(
    string Id,
    bool NeedsOccupant,
    Func<TacticalBoard, Coord, Coord, IReadOnlyList<Coord>> Cells);

/// <summary>Per-engine shape policies shared by combat planning, delayed casts and threat queries.</summary>
public sealed class WeaponAreaRegistry
{
    private readonly Dictionary<string, WeaponAreaShape> entries = new(StringComparer.Ordinal);
    private readonly List<string> order = new();
    public int Count => entries.Count;
    public bool IsSealed { get; private set; }

    public WeaponAreaRegistry Register(WeaponAreaShape shape)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(shape.Cells);
        if (!entries.TryAdd(shape.Id, shape))
            throw new DomainInvariantException($"weapon area shape already registered: \"{shape.Id}\"");
        order.Add(shape.Id);
        return this;
    }

    public WeaponAreaRegistry Replace(WeaponAreaShape shape)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(shape.Cells);
        if (!entries.ContainsKey(shape.Id)) order.Add(shape.Id);
        entries[shape.Id] = shape;
        return this;
    }

    public WeaponAreaShape Get(string id) => entries.TryGetValue(id, out var shape) ? shape :
        throw new DomainInvariantException($"unknown weapon area shape \"{id}\"");
    public WeaponAreaShape? TryGet(string id) => entries.GetValueOrDefault(id);
    public bool Has(string id) => entries.ContainsKey(id);
    public IReadOnlyList<WeaponAreaShape> All() => order.Select(id => entries[id]).ToArray();
    public IReadOnlyList<string> Keys() => order.ToArray();

    public IReadOnlyList<Coord> Coverage(TacticalBoard board, Coord from, Coord target, string area)
    {
        var seen = new HashSet<int>();
        var cells = new List<Coord>();
        foreach (var cell in Get(area).Cells(board, from, target))
            if (board.Contains(cell) && seen.Add(board.IndexOf(cell))) cells.Add(cell);
        return cells.AsReadOnly();
    }

    public WeaponAreaRegistry Seal()
    {
        IsSealed = true;
        return this;
    }

    public WeaponAreaRegistry Clone()
    {
        var result = new WeaponAreaRegistry();
        foreach (var id in order) result.Register(entries[id]);
        return result;
    }

    private void RequireMutable()
    {
        if (IsSealed) throw new DomainInvariantException("weapon area shape registry is sealed after composition");
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
