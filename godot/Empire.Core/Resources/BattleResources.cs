using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.State;

namespace Empire.Core.Resources;

public sealed record BattleResourceRules(ResourceAdapterRegistry Adapters, ResourceSubjectResolverRegistry Subjects);

/// <summary>Canonical account arithmetic; adapters expose storage and resolvers identify holders.</summary>
public sealed class BattleResources
{
    public sealed record Subject(string Kind, JsonObject Holder, string? Weapon = null);
    public sealed record Amount(string Resource, double Value);
    public sealed record Cost(string Resource, double Amount);
    public sealed record AccountSnapshot(string Resource, double? Current, double? Capacity);

    public ResourceAdapterRegistry Adapters
    {
        get;
    }
    public ResourceSubjectResolverRegistry Subjects
    {
        get;
    }

    public BattleResources(BattleResourceRules rules)
    {
        Adapters = rules.Adapters;
        Subjects = rules.Subjects;
    }

    public BattleResources Clone() => new(new BattleResourceRules(Adapters.Clone(), Subjects.Clone()));
    public BattleResources Seal()
    {
        Adapters.Seal();
        Subjects.Seal();
        return this;
    }

    public Subject SubjectFor(ResourceTransactionContext context, ResourceTransaction transaction) =>
        Subjects.Get(transaction.Subject).SubjectFor(context, transaction);

    public JsonObject RefOf(Subject subject) => Subjects.Get(subject.Kind).Ref(subject);

    public static IReadOnlyList<Cost> CopyCosts(JsonArray costs) => System.Array.AsReadOnly(
        StateJson.Objects(costs, "costs").Select(cost => new Cost(StateJson.Text(cost, "resource"),
            StateJson.Number(cost["amount"], "amount"))).ToArray());

    public AccountSnapshot Inspect(Subject subject, string resource)
    {
        var snapshot = RequireAccount(subject, resource).Account.Read();
        return new(resource, snapshot.Current, snapshot.Capacity);
    }

    public IReadOnlyList<AccountSnapshot> Accounts(Subject subject) => System.Array.AsReadOnly(
        Adapters.Keys().Where(resource => HasAccount(subject, resource)).Select(resource => Inspect(subject, resource)).ToArray());

    public bool HasAccount(Subject subject, string resource)
    {
        var adapter = Adapters.Get(resource);
        return adapter.SubjectKind == subject.Kind && adapter.Account(subject) is not null;
    }

    private (ResourceAdapter Adapter, ResourceAccount Account) RequireAccount(Subject subject, string resource)
    {
        var adapter = Adapters.Get(resource);
        if (adapter.SubjectKind != subject.Kind)
            throw new DomainInvariantException($"resource \"{resource}\" requires {adapter.SubjectKind} subject, received {subject.Kind}");
        var account = adapter.Account(subject) ?? throw new DomainInvariantException($"resource \"{resource}\" is not available for this {subject.Kind}");
        return (adapter, account);
    }

    private double Normalize(string resource, double requested) => Normalize(Adapters.Get(resource), requested);

    private static double Normalize(ResourceAdapter adapter, double requested)
    {
        if (!double.IsFinite(requested))
            throw new DomainInvariantException($"resource \"{adapter.Id}\" amount must be finite");
        var amount = Math.Max(0, requested);
        return adapter.Integer ? StateJson.Round(amount) : amount;
    }

    public double? Balance(Subject subject, string resource) => RequireAccount(subject, resource).Account.Read().Current;
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
        StateJson.Number(amount["amount"], "amount") * Adapters.Get(StateJson.Text(amount, "resource")).AiWeight);

    public bool CanAffordTransactions(ResourceTransactionContext context, IEnumerable<ResourceTransaction> transactions)
    {
        var totals = new Dictionary<string, (ResourceTransaction Transaction, double Amount)>(StringComparer.Ordinal);
        foreach (var transaction in transactions)
        {
            var key = transaction.Subject + '\0' + transaction.Resource;
            totals[key] = totals.TryGetValue(key, out var total)
                ? (total.Transaction, total.Amount + transaction.Amount) : (transaction, transaction.Amount);
        }
        return totals.Values.All(total =>
        {
            var transaction = total.Transaction;
            var subject = SubjectFor(context, transaction);
            return HasAccount(subject, transaction.Resource) && CanSpend(subject, transaction.Resource, total.Amount);
        });
    }

    public bool CanAfford(Subject subject, JsonArray costs) =>
        Totals(costs).All(cost => CanSpend(subject, cost.Resource, cost.Value));

    public IReadOnlyList<Amount> SpendAll(Subject subject, JsonArray costs)
    {
        var totals = Totals(costs);
        foreach (var cost in totals)
            if (!CanSpend(subject, cost.Resource, cost.Value))
                throw new DomainInvariantException($"insufficient resource \"{cost.Resource}\": need {EcmaNumber.Format(cost.Value)}");
        foreach (var cost in totals)
            Spend(subject, cost.Resource, cost.Value);
        return totals;
    }

    public double Credit(Subject subject, string resource, double requested)
    {
        var (adapter, account) = RequireAccount(subject, resource);
        var amount = Normalize(adapter, requested);
        var snapshot = account.Read();
        if (snapshot.Current is not { } current || amount <= 0)
            return 0;
        var next = snapshot.Capacity is { } capacity ? Math.Min(capacity, current + amount) : current + amount;
        account.Write(next);
        return next - current;
    }

    public double Spend(Subject subject, string resource, double requested)
    {
        var (adapter, account) = RequireAccount(subject, resource);
        var amount = Normalize(adapter, requested);
        if (account.Read().Current is not { } current || amount <= 0)
            return 0;
        if (current < amount)
            throw new DomainInvariantException($"insufficient resource \"{resource}\": need {EcmaNumber.Format(amount)}");
        account.Write(current - amount);
        return amount;
    }

    public void Announce(Subject subject, string resource, double amount, Action<JsonObject> emit)
    {
        var current = Balance(subject, resource);
        if (amount == 0 || current is null)
            return;
        emit(new JsonObject
        {
            ["type"] = "resourceChanged",
            ["resource"] = resource,
            ["subject"] = RefOf(subject),
            ["amount"] = amount,
            ["current"] = current.Value
        });
    }
}
