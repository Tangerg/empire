namespace Empire.Core.Composition;

/// <summary>A capability's stable ID owns its identity; the type is its value contract.</summary>
public abstract class CapabilityKey
{
    private protected CapabilityKey(string id, Type valueType)
    {
        ArgumentNullException.ThrowIfNull(id);
        Id = id;
        ValueType = valueType;
    }

    public string Id
    {
        get;
    }
    public Type ValueType
    {
        get;
    }
    public override string ToString() => Id;
}

public sealed class CapabilityKey<T>(string id) : CapabilityKey(id, typeof(T));
