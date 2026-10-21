namespace Empire.Core.Registries;

public interface IPrioritizedEntry
{
    string Id
    {
        get;
    }
    double Priority
    {
        get;
    }
}

/// <summary>Rules consulted by numeric priority, then ordinal UTF-16 identity.</summary>
public abstract class PriorityRegistry<TEntry, TRegistry> : KeyedRegistry<TEntry, TRegistry>
    where TEntry : class, IPrioritizedEntry
    where TRegistry : PriorityRegistry<TEntry, TRegistry>
{
    private IReadOnlyList<TEntry>? orderedCache;

    protected PriorityRegistry(string subject) : base(subject) { }
    protected override string KeyOf(TEntry entry) => entry.Id;

    public override TRegistry Register(TEntry entry)
    {
        orderedCache = null;
        return base.Register(entry);
    }

    public override TRegistry Replace(TEntry entry)
    {
        orderedCache = null;
        return base.Replace(entry);
    }

    public IReadOnlyList<TEntry> Ordered() => orderedCache ??=
        Array.AsReadOnly(All().OrderBy(entry => entry, Comparer<TEntry>.Create(Compare)).ToArray());

    private static int Compare(TEntry left, TEntry right)
    {
        // Source subtraction falls back to the ID for zero or NaN, including equal infinities.
        var difference = left.Priority - right.Priority;
        return difference < 0 ? -1 : difference > 0 ? 1 : StringComparer.Ordinal.Compare(left.Id, right.Id);
    }
}
