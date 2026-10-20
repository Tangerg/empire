namespace Empire.Core.Geometry;

/// <summary>
/// Explicit, per-engine composition. Construct an empty registry and register the
/// grids that engine supports; the library never installs content on import.
/// </summary>
public sealed class GridRegistry
{
    private readonly Dictionary<string, ITacticalGrid> entries = new(StringComparer.Ordinal);
    private readonly List<string> order = new();

    public int Count => entries.Count;
    public bool IsSealed { get; private set; }

    public GridRegistry Register(ITacticalGrid grid)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(grid);
        if (!entries.TryAdd(grid.Id, grid))
            throw new DomainInvariantException($"tactical grid already registered: \"{grid.Id}\"");
        order.Add(grid.Id);
        return this;
    }

    public GridRegistry Replace(ITacticalGrid grid)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(grid);
        if (!entries.ContainsKey(grid.Id)) order.Add(grid.Id);
        entries[grid.Id] = grid;
        return this;
    }

    public ITacticalGrid Get(string id) => entries.TryGetValue(id, out var grid) ? grid :
        throw new DomainInvariantException($"unknown tactical grid \"{id}\"");

    public ITacticalGrid? TryGet(string id) => entries.GetValueOrDefault(id);

    public bool Has(string id) => entries.ContainsKey(id);

    public IReadOnlyList<ITacticalGrid> All() => order.Select(id => entries[id]).ToArray();

    public IReadOnlyList<string> Keys() => order.ToArray();

    public GridRegistry Seal()
    {
        IsSealed = true;
        return this;
    }

    public GridRegistry Clone()
    {
        var result = new GridRegistry();
        foreach (var id in order) result.Register(entries[id]);
        return result;
    }

    private void RequireMutable()
    {
        if (IsSealed)
            throw new DomainInvariantException("tactical grid registry is sealed after composition");
    }
}
