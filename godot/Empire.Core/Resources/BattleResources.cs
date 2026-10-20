using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Resources;

/// <summary>Canonical account arithmetic for the four shipped resource adapters.</summary>
public sealed class BattleResources
{
    public sealed record Subject(string Kind, JsonObject Holder, string? Weapon = null);
    public sealed record Amount(string Resource, double Value);

    private static readonly IReadOnlyDictionary<string, string> Kinds = new Dictionary<string, string>
    {
        ["funds"] = "player", ["command_points"] = "player",
        ["momentum"] = "unit", ["weapon_uses"] = "weapon"
    };

    public static IReadOnlyList<string> SupportedResources { get; } = System.Array.AsReadOnly(Kinds.Keys.ToArray());

    private static JsonObject? FindAccount(Subject subject, string resource)
    {
        if (!Kinds.TryGetValue(resource, out var kind))
            throw new DomainInvariantException($"unknown resource adapter \"{resource}\"");
        if (kind != subject.Kind) return null;
        var resources = subject.Kind == "weapon"
            ? subject.Holder["weaponState"]?[subject.Weapon ?? ""]?["resources"]
            : subject.Holder["resources"];
        return resources?[resource] as JsonObject;
    }

    private static JsonObject Account(Subject subject, string resource)
    {
        var account = FindAccount(subject, resource);
        if (Kinds[resource] != subject.Kind)
            throw new DomainInvariantException($"resource \"{resource}\" requires {Kinds[resource]} subject, received {subject.Kind}");
        return account ?? throw new DomainInvariantException($"resource \"{resource}\" is not available for this {subject.Kind}");
    }

    private static double Normalize(string resource, double requested)
    {
        if (!double.IsFinite(requested)) throw new DomainInvariantException($"resource \"{resource}\" amount must be finite");
        return Math.Floor(Math.Max(0, requested) + 0.5);
    }

    public bool HasAccount(Subject subject, string resource) => FindAccount(subject, resource) is not null;
    public double? Balance(Subject subject, string resource) => Account(subject, resource)["current"] is { } value
        ? StateJson.Number(value, "current") : null;
    public bool CanSpend(Subject subject, string resource, double requested)
    {
        var current = Balance(subject, resource);
        var amount = Normalize(resource, requested);
        return current is null || current >= amount;
    }

    public IReadOnlyList<Amount> Totals(JsonArray costs)
    {
        var amounts = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var cost in StateJson.Objects(costs, "costs"))
        {
            var id = StateJson.Text(cost, "resource");
            amounts[id] = amounts.GetValueOrDefault(id) + StateJson.Number(cost["amount"], "amount");
        }
        return amounts.Select(pair => new Amount(pair.Key, pair.Value)).ToArray();
    }

    public double PlanningValue(JsonArray amounts) => StateJson.Objects(amounts, "amounts").Sum(amount =>
        StateJson.Number(amount["amount"], "amount") * (StateJson.Text(amount, "resource") switch
        {
            "funds" => 1, "command_points" => 8, "momentum" => 2, "weapon_uses" => 20,
            var id => throw new DomainInvariantException($"unknown resource adapter \"{id}\"")
        }));

    public bool CanAfford(Subject subject, JsonArray costs) =>
        Totals(costs).All(cost => CanSpend(subject, cost.Resource, cost.Value));

    public IReadOnlyList<Amount> SpendAll(Subject subject, JsonArray costs)
    {
        var totals = Totals(costs);
        foreach (var cost in totals)
            if (!CanSpend(subject, cost.Resource, cost.Value))
                throw new DomainInvariantException($"insufficient resource \"{cost.Resource}\": need {cost.Value}");
        foreach (var cost in totals) Spend(subject, cost.Resource, cost.Value);
        return totals;
    }

    public double Credit(Subject subject, string resource, double requested)
    {
        var account = Account(subject, resource);
        var amount = Normalize(resource, requested);
        if (Balance(subject, resource) is not { } current || amount <= 0) return 0;
        var next = account["capacity"] is { } capacity
            ? Math.Min(StateJson.Number(capacity, "capacity"), current + amount) : current + amount;
        account["current"] = next;
        return next - current;
    }

    public double Spend(Subject subject, string resource, double requested)
    {
        var account = Account(subject, resource);
        var amount = Normalize(resource, requested);
        if (Balance(subject, resource) is not { } current || amount <= 0) return 0;
        if (current < amount) throw new DomainInvariantException($"insufficient resource \"{resource}\": need {amount}");
        account["current"] = current - amount;
        return amount;
    }

    public void Announce(Subject subject, string resource, double amount, Action<JsonObject> emit)
    {
        var current = Balance(subject, resource);
        if (amount == 0 || current is null) return;
        var reference = new JsonObject { ["kind"] = subject.Kind, ["id"] = subject.Holder["id"]!.DeepClone() };
        if (subject.Kind == "weapon") reference["slot"] = subject.Weapon;
        emit(new JsonObject { ["type"] = "resourceChanged", ["resource"] = resource,
            ["subject"] = reference, ["amount"] = amount, ["current"] = current.Value });
    }
}
