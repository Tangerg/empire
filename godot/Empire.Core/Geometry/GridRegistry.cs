using Empire.Core.Registries;

namespace Empire.Core.Geometry;

/// <summary>Explicit, per-engine grid composition; no content is installed on import.</summary>
public sealed class GridRegistry : KeyedRegistry<ITacticalGrid, GridRegistry>
{
    public GridRegistry() : base("tactical grid") { }
    protected override string KeyOf(ITacticalGrid entry) => entry.Id;
    public GridRegistry Clone() => CopyInto(new GridRegistry());
}
