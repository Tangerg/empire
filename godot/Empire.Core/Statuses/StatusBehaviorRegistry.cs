using Empire.Core.Registries;

namespace Empire.Core.Statuses;

public sealed record StatusBehavior(string Id, Action<StatusLifecycleContext>? OnOwnerTurnStart = null);

public sealed class StatusBehaviorRegistry : KeyedRegistry<StatusBehavior, StatusBehaviorRegistry>
{
    public StatusBehaviorRegistry() : base("status behavior") { }
    protected override string KeyOf(StatusBehavior entry) => entry.Id;
    public void OwnerTurnStart(StatusLifecycleContext context) => TryGet(context.Status["id"]!.GetValue<string>())?.OnOwnerTurnStart?.Invoke(context);
    public StatusBehaviorRegistry Clone() => CopyInto(new StatusBehaviorRegistry());
}
