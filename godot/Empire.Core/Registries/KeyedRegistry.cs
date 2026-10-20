namespace Empire.Core.Registries;

/// <summary>Ordered, per-composition rule storage. Cloning copies the table, not its rule objects.</summary>
public abstract class KeyedRegistry<TEntry, TRegistry>
    where TEntry : class
    where TRegistry : KeyedRegistry<TEntry, TRegistry>
{
    private readonly Dictionary<string, TEntry> entries = new(StringComparer.Ordinal);
    private readonly List<string> order = new();
    private readonly string subject;

    protected KeyedRegistry(string subject) => this.subject = subject;
    protected abstract string KeyOf(TEntry entry);
    protected virtual void ValidateEntry(TEntry entry) { }

    public int Count => entries.Count;
    public bool IsSealed { get; private set; }

    public virtual TRegistry Register(TEntry entry)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);
        var key = KeyOf(entry);
        if (!entries.TryAdd(key, entry))
            throw new DomainInvariantException($"{subject} already registered: \"{key}\"");
        order.Add(key);
        return (TRegistry)this;
    }

    public virtual TRegistry Replace(TEntry entry)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);
        var key = KeyOf(entry);
        if (!entries.ContainsKey(key)) order.Add(key);
        entries[key] = entry;
        return (TRegistry)this;
    }

    public TEntry Get(string key) => entries.TryGetValue(key, out var entry) ? entry :
        throw new DomainInvariantException($"unknown {subject} \"{key}\"");

    public TEntry? TryGet(string key) => entries.GetValueOrDefault(key);
    public bool Has(string key) => entries.ContainsKey(key);
    public IReadOnlyList<TEntry> All() => order.Select(key => entries[key]).ToArray();
    public IReadOnlyList<string> Keys() => order.ToArray();

    public TRegistry Seal()
    {
        IsSealed = true;
        return (TRegistry)this;
    }

    protected TRegistry CopyInto(TRegistry target)
    {
        foreach (var key in order) target.Register(entries[key]);
        return target;
    }

    private void RequireMutable()
    {
        if (IsSealed)
            throw new DomainInvariantException($"{subject} registry is sealed after composition");
    }
}
