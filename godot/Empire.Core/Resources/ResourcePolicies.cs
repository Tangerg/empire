using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Registries;
using Empire.Core.State;

namespace Empire.Core.Resources;

public sealed record ResourceSnapshot(double? Current, double? Capacity);
public sealed record ResourceAccount(Func<ResourceSnapshot> Read, Action<double> Write);
public sealed record ResourceAdapter(string Id, string SubjectKind, bool Integer,
    double AiWeight, Func<BattleResources.Subject, ResourceAccount?> Account);
public sealed record ResourceTransaction(string Subject, string Resource, double Amount);
public sealed record ResourceTransactionContext(JsonObject Unit, string Weapon, JsonObject? Player = null);
public sealed record ResourceSubjectResolver(string Kind,
    Func<ResourceTransactionContext, ResourceTransaction, BattleResources.Subject> SubjectFor,
    Func<BattleResources.Subject, JsonObject> Ref);

public sealed class ResourceAdapterRegistry : KeyedRegistry<ResourceAdapter, ResourceAdapterRegistry>
{
    public ResourceAdapterRegistry() : base("resource adapter") { }
    protected override string KeyOf(ResourceAdapter entry) => entry.Id;
    public override ResourceAdapterRegistry Register(ResourceAdapter entry)
    {
        if (EcmaText.IsBlank(entry.Id))
            throw new DomainInvariantException("resource id cannot be empty");
        return base.Register(entry);
    }
    public ResourceAdapterRegistry Clone() => CopyInto(new ResourceAdapterRegistry());
}

public sealed class ResourceSubjectResolverRegistry : KeyedRegistry<ResourceSubjectResolver, ResourceSubjectResolverRegistry>
{
    public ResourceSubjectResolverRegistry() : base("resource subject") { }
    protected override string KeyOf(ResourceSubjectResolver entry) => entry.Kind;
    public ResourceSubjectResolverRegistry Clone() => CopyInto(new ResourceSubjectResolverRegistry());
}

public static class DefaultResourcePolicies
{
    public static ResourceAdapterRegistry Adapters { get; } = new ResourceAdapterRegistry()
        .Register(Stored("funds", "player", 1, subject => subject.Holder["resources"]))
        .Register(Stored("command_points", "player", 8, subject => subject.Holder["resources"]))
        .Register(Stored("momentum", "unit", 2, subject => subject.Holder["resources"]))
        .Register(Stored("weapon_uses", "weapon", 20,
            subject => subject.Holder["weaponState"]?[subject.Weapon ?? ""]?["resources"]))
        .Seal();

    public static ResourceSubjectResolverRegistry Subjects { get; } = new ResourceSubjectResolverRegistry()
        .Register(new("player", (context, transaction) => new("player", context.Player ??
            throw new DomainInvariantException($"resource \"{transaction.Resource}\" requires a player context")), Reference))
        .Register(new("unit", (context, _) => new("unit", context.Unit), Reference))
        .Register(new("weapon", (context, _) => new("weapon", context.Unit, context.Weapon), subject =>
        {
            var reference = Reference(subject);
            reference["slot"] = subject.Weapon;
            return reference;
        }))
        .Seal();

    private static JsonObject Reference(BattleResources.Subject subject) => new()
    {
        ["kind"] = subject.Kind, ["id"] = subject.Holder["id"]!.DeepClone(),
    };

    private static ResourceAdapter Stored(string id, string kind, double aiWeight,
        Func<BattleResources.Subject, JsonNode?> accounts) => new(id, kind, true, aiWeight, subject =>
    {
        if (accounts(subject)?[id] is not JsonObject state) return null;
        return new ResourceAccount(() => new ResourceSnapshot(
            state["current"] is { } current ? StateJson.Number(current, "current") : null,
            state["capacity"] is { } capacity ? StateJson.Number(capacity, "capacity") : null),
            current => state["current"] = current);
    });
}
