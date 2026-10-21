namespace Empire.Core.Composition;

public sealed class EnginePlugin
{
    public EnginePlugin(string id, double version, Action<KernelPluginContext> install,
        IEnumerable<CapabilityKey>? provides = null, IEnumerable<CapabilityKey>? overrides = null,
        IEnumerable<CapabilityKey>? requiresCapabilities = null, IEnumerable<string>? requires = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(install);
        Id = id;
        Version = version;
        Install = install;
        Provides = Array.AsReadOnly(provides?.ToArray() ?? []);
        Overrides = Array.AsReadOnly(overrides?.ToArray() ?? []);
        RequiresCapabilities = Array.AsReadOnly(requiresCapabilities?.ToArray() ?? []);
        Requires = Array.AsReadOnly(requires?.ToArray() ?? []);
    }

    public string Id
    {
        get;
    }
    public double Version
    {
        get;
    }
    public IReadOnlyList<CapabilityKey> Provides
    {
        get;
    }
    public IReadOnlyList<CapabilityKey> Overrides
    {
        get;
    }
    public IReadOnlyList<CapabilityKey> RequiresCapabilities
    {
        get;
    }
    public IReadOnlyList<string> Requires
    {
        get;
    }
    public Action<KernelPluginContext> Install
    {
        get;
    }
}
