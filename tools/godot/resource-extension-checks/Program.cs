using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Transactions;

using var stream = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal)
    ? new StreamReader(new GZipStream(stream, CompressionMode.Decompress)) : new StreamReader(stream);
var input = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var checks = 0;
void Check(string name, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (!EcmaJson.DeepEquals(actual, expected))
        throw new Exception($"{name}: {EcmaJson.Stringify(actual)} != {EcmaJson.Stringify(expected)}");
}
void Assert(string name, bool condition) => Check(name, JsonValue.Create(condition), JsonValue.Create(true));
JsonNode? Node(object? value) => value is JsonNode node ? node.DeepClone() : JsonSerializer.SerializeToNode(value);
JsonObject Observe(Func<object?> action)
{
    try
    {
        return new()
        {
            ["result"] = Node(action()),
            ["error"] = null
        };
    }
    catch (DomainInvariantException error)
    {
        return new()
        {
            ["result"] = null,
            ["error"] = new JsonObject { ["kind"] = "DomainInvariant", ["message"] = error.Message }
        };
    }
}
string FixtureText(JsonObject value, string key) => StateJson.Text(value, key);
double FixtureNumber(JsonNode? value) => value is JsonValue item && item.TryGetValue<string>(out var symbolic) ? symbolic switch
{
    "NaN" => double.NaN,
    "Infinity" => double.PositiveInfinity,
    "-Infinity" => double.NegativeInfinity,
    _ => throw new Exception($"Unknown fixture number {symbolic}"),
} : StateJson.Number(value, "number");
BattleResources Policies(List<string> trace)
{
    var adapters = DefaultResourcePolicies.Adapters.Clone();
    foreach (var (id, integer, weight) in new[] { ("supply", true, 3.5), ("fuel", false, 0.25), ("big", true, 1.0) })
        adapters.Register(new(id, "squad", integer, weight, subject =>
        {
            trace.Add("account:" + id);
            if (subject.Holder["stores"]?[id] is not JsonObject state)
                return null;
            return new ResourceAccount(() =>
            {
                trace.Add("read:" + id);
                return new(state["balance"] is { } balance ? FixtureNumber(balance) : null,
                    state["limit"] is { } limit ? FixtureNumber(limit) : null);
            }, current => { trace.Add("write:" + id); state["balance"] = current; });
        }));
    var subjects = DefaultResourcePolicies.Subjects.Clone().Register(new("squad", (context, transaction) =>
    {
        trace.Add($"subject:{transaction.Resource}:{EcmaJson.Stringify(JsonValue.Create(transaction.Amount))}");
        return new("squad", context.Unit["squad"]!.AsObject());
    }, subject =>
    {
        trace.Add("ref:squad");
        return new()
        {
            ["kind"] = "squad",
            ["id"] = subject.Holder["code"]!.DeepClone(),
            ["slot"] = subject.Holder["location"]!.DeepClone()
        };
    }));
    return new(adapters, subjects);
}
ResourceTransaction Transaction(JsonObject value) => new(FixtureText(value, "subject"), FixtureText(value, "resource"), FixtureNumber(value["amount"]));
ResourceTransactionContext Context(JsonObject value) => new(value["unit"]!.AsObject(), FixtureText(value, "weapon"), value["player"] as JsonObject);

foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>())
{
    var data = item.DeepClone().AsObject();
    var name = FixtureText(data, "name");
    var trace = new List<string>();
    var events = new JsonArray();
    var resources = Policies(trace);
    var kind = FixtureText(data, "kind");
    var subject = new BattleResources.Subject(kind, data["holder"]!.AsObject(), kind == "weapon" ? "bow" : null);
    if (data["replace"] is JsonObject replacement)
    {
        var id = FixtureText(replacement, "id");
        var prior = resources.Adapters.Get(id);
        resources.Adapters.Replace(prior with
        {
            Integer = replacement["integer"]?.GetValue<bool>() ?? prior.Integer,
            AiWeight = replacement["aiWeight"] is { } weight ? FixtureNumber(weight) : prior.AiWeight,
        });
    }
    if (data["swapOnAccount"]?.GetValue<bool>() == true)
    {
        var adapter = resources.Adapters.Get(FixtureText(data, "resource"));
        resources.Adapters.Replace(adapter with
        {
            Account = holder =>
        {
            resources.Adapters.Replace(adapter with
            {
                Integer = false
            });
            return adapter.Account(holder);
        }
        });
    }
    var result = Observe(() =>
    {
        var resource = data["resource"]?.GetValue<string>() ?? "";
        switch (FixtureText(data, "operation"))
        {
            case "inspect":
                var snapshot = resources.Inspect(subject, resource);
                return new
                {
                    current = snapshot.Current,
                    capacity = snapshot.Capacity
                };
            case "balance":
                return resources.Balance(subject, resource);
            case "has":
                return resources.HasAccount(subject, resource);
            case "canSpend":
                return resources.CanSpend(subject, resource, FixtureNumber(data["requested"]));
            case "credit":
                return resources.Credit(subject, resource, FixtureNumber(data["requested"]));
            case "spend":
                return resources.Spend(subject, resource, FixtureNumber(data["requested"]));
            case "afford":
                return resources.CanAfford(subject, data["costs"]!.AsArray());
            case "spendAll":
                return resources.SpendAll(subject, data["costs"]!.AsArray()).Select(amount => new { resource = amount.Resource, amount = amount.Value }).ToArray();
            case "value":
                return resources.PlanningValue(data["costs"]!.AsArray());
            case "announce":
                resources.Announce(subject, resource, FixtureNumber(data["amount"]), value => events.Add(value));
                return null;
            case "ref":
                return resources.RefOf(subject);
            case "subject":
                return resources.RefOf(resources.SubjectFor(Context(data["context"]!.AsObject()), Transaction(data["transaction"]!.AsObject())));
            case "transactions":
                return resources.CanAffordTransactions(Context(data["context"]!.AsObject()), data["transactions"]!.AsArray().OfType<JsonObject>().Select(Transaction));
            case "accounts":
                return resources.Accounts(subject).Select(account => new { resource = account.Resource, current = account.Current, capacity = account.Capacity }).ToArray();
            default:
                throw new Exception("Unknown fixture operation");
        }
    });
    var expectedResult = FixtureText(data, "operation") == "accounts"
        ? new JsonArray(item["result"]!.AsArray().OfType<JsonObject>().Select(account => (JsonNode?)new JsonObject
        {
            ["resource"] = account["resource"]!.DeepClone(),
            ["current"] = account["current"]?.DeepClone(),
            ["capacity"] = account["capacity"]?.DeepClone(),
        }).ToArray()) : item["result"];
    Check(name + " result", result["result"], expectedResult);
    Check(name + " error", result["error"], item["error"]);
    Check(name + " holder", data["holder"], item["after"]);
    Check(name + " context", data["context"], item["contextAfter"]);
    Check(name + " events", events, item["events"]);
    Check(name + " trace", Node(trace), item["trace"]);
}

ResourceAdapter Dummy(string id) => new(id, "player", true, 3, _ => null);
ResourceSubjectResolver Resolver(string kind) => new(kind, (_, _) => null!, _ => null!);
foreach (var item in input["registries"]!.AsArray().OfType<JsonObject>())
{
    var name = FixtureText(item, "name");
    var actual = Observe(() =>
    {
        if (name.StartsWith("register id ", StringComparison.Ordinal))
            return new ResourceAdapterRegistry().Register(Dummy(EcmaJson.Parse(name[12..])!.GetValue<string>())).Keys();
        switch (name)
        {
            case "default adapter keys":
                return DefaultResourcePolicies.Adapters.Keys();
            case "default subject keys":
                return DefaultResourcePolicies.Subjects.Keys();
            case "adapter duplicate":
                return new ResourceAdapterRegistry().Register(Dummy("a")).Register(Dummy("a"));
            case "subject duplicate":
                return new ResourceSubjectResolverRegistry().Register(Resolver("a")).Register(Resolver("a"));
            case "adapter unknown":
                return new ResourceAdapterRegistry().Get("missing");
            case "subject unknown":
                return new ResourceSubjectResolverRegistry().Get("missing");
            case "adapter ask missing":
                return new ResourceAdapterRegistry().TryGet("missing");
            case "subject ask missing":
                return new ResourceSubjectResolverRegistry().TryGet("missing");
            case "blank replace permitted":
                return new ResourceAdapterRegistry().Replace(Dummy(" ")).Keys();
            case "blank clone rejected":
                return new ResourceAdapterRegistry().Replace(Dummy(" ")).Clone();
            case "blank register precedes sealed rejection":
                return new ResourceAdapterRegistry().Seal().Register(Dummy(" "));
            case "sealed adapter register":
                return new ResourceAdapterRegistry().Seal().Register(Dummy("a"));
            case "sealed adapter replace":
                return new ResourceAdapterRegistry().Seal().Replace(Dummy("a"));
            case "sealed subject register":
                return new ResourceSubjectResolverRegistry().Seal().Register(Resolver("a"));
            case "sealed subject replace":
                return new ResourceSubjectResolverRegistry().Seal().Replace(Resolver("a"));
            case "adapter replace ordering":
                return new ResourceAdapterRegistry().Register(Dummy("b")).Register(Dummy("a")).Replace(Dummy("b")).Replace(Dummy("c")).Keys();
            case "subject replace ordering":
                return new ResourceSubjectResolverRegistry().Register(Resolver("b")).Register(Resolver("a")).Replace(Resolver("b")).Replace(Resolver("c")).Keys();
            case "detached keys and all":
                var registry = new ResourceAdapterRegistry().Register(Dummy("a"));
                ((string[])registry.Keys())[0] = "outside";
                ((ResourceAdapter[])registry.All())[0] = Dummy("outside");
                return new
                {
                    keys = registry.Keys(),
                    size = registry.Count,
                    has = registry.Has("a")
                };
            case "resource clone isolation":
                var original = new BattleResources(new BattleResourceRules(DefaultResourcePolicies.Adapters.Clone(), DefaultResourcePolicies.Subjects.Clone())).Seal().Seal();
                var cloned = original.Clone();
                var adapterIdentity = ReferenceEquals(cloned.Adapters.Get("funds"), original.Adapters.Get("funds"));
                var resolverIdentity = ReferenceEquals(cloned.Subjects.Get("player"), original.Subjects.Get("player"));
                cloned.Adapters.Replace(Dummy("funds")).Register(Dummy("new"));
                cloned.Subjects.Replace(Resolver("player")).Register(Resolver("new"));
                return new
                {
                    originalSealed = original.Adapters.IsSealed && original.Subjects.IsSealed,
                    cloneSealed = cloned.Adapters.IsSealed || cloned.Subjects.IsSealed,
                    adapterIdentity,
                    resolverIdentity,
                    originalAdapters = original.Adapters.Keys(),
                    clonedAdapters = cloned.Adapters.Keys(),
                    originalSubjects = original.Subjects.Keys(),
                    clonedSubjects = cloned.Subjects.Keys(),
                    originalFundsWeight = original.Adapters.Get("funds").AiWeight,
                    clonedFundsWeight = cloned.Adapters.Get("funds").AiWeight,
                };
            case "default resource independence":
                var left = new BattleResources(new BattleResourceRules(DefaultResourcePolicies.Adapters.Clone(), DefaultResourcePolicies.Subjects.Clone()));
                var right = new BattleResources(new BattleResourceRules(DefaultResourcePolicies.Adapters.Clone(), DefaultResourcePolicies.Subjects.Clone()));
                left.Adapters.Replace(Dummy("funds"));
                return new
                {
                    leftWeight = left.Adapters.Get("funds").AiWeight,
                    rightWeight = right.Adapters.Get("funds").AiWeight,
                    defaultWeight = DefaultResourcePolicies.Adapters.Get("funds").AiWeight
                };
            default:
                throw new Exception($"Unknown registry fixture {name}");
        }
    });
    var expectedResult = name switch
    {
        "resource clone isolation" => Node(new
        {
            originalSealed = true,
            cloneSealed = false,
            adapterIdentity = true,
            resolverIdentity = true,
            originalAdapters = new[] { "funds", "command_points", "momentum", "weapon_uses" },
            clonedAdapters = new[] { "funds", "command_points", "momentum", "weapon_uses", "new" },
            originalSubjects = new[] { "player", "unit", "weapon" },
            clonedSubjects = new[] { "player", "unit", "weapon", "new" },
            originalFundsWeight = 1d,
            clonedFundsWeight = 3d,
        }),
        "default resource independence" => Node(new { leftWeight = 3d, rightWeight = 1d, defaultWeight = 1d }),
        _ => item["result"],
    };
    Check(name + " result", actual["result"], expectedResult);
    Check(name + " error", actual["error"], item["error"]);
}

// Resource adapters hold draft-owned accounts; Battle remains the only state/event commit boundary.
var initial = EcmaJson.Parse("{\"unit\":{\"id\":7,\"squad\":{\"code\":\"squad-a\",\"location\":\"chest\",\"stores\":{\"supply\":{\"balance\":5,\"limit\":8}}}}}")!.AsObject();
var isolatedResources = Policies(new()).Clone().Seal();
JsonObject? retainedDraft = null;
var actions = new BattleActionRegistry().Register("charge", (draft, action, emit) =>
{
    retainedDraft = draft;
    var context = new ResourceTransactionContext(draft["unit"]!.AsObject(), "bow");
    var subject = isolatedResources.SubjectFor(context, new("squad", "supply", 2));
    var amount = isolatedResources.Spend(subject, "supply", 2);
    isolatedResources.Announce(subject, "supply", -amount, emit);
    if (action["fail"]?.GetValue<bool>() == true)
        throw new DomainInvariantException("reject after resource write");
});
var battle = new Battle(new BattleTransactionRules(actions, null), initial);
var before = battle.Snapshot();
var hash = battle.StateHash;
var refused = Observe(() => battle.Dispatch(new JsonObject { ["kind"] = "charge", ["fail"] = true }));
Assert("transaction rejects after adapter write", refused["error"] is not null);
Check("adapter rejection restores snapshot", battle.Snapshot(), before);
Assert("adapter rejection restores hash", battle.StateHash == hash);
initial["unit"]!["squad"]!["stores"]!["supply"]!["balance"] = 500;
Check("initial input isolation", battle.Snapshot(), before);
var receipt = battle.Dispatch(new JsonObject { ["kind"] = "charge" });
Assert("successful resource receipt has one event only", receipt.Events.Count == 1);
Check("custom holder event through canonical writer", receipt.Events[0], EcmaJson.Parse("{\"type\":\"resourceChanged\",\"resource\":\"supply\",\"subject\":{\"kind\":\"squad\",\"id\":\"squad-a\",\"slot\":\"chest\"},\"amount\":-2,\"current\":3}"));
var committed = battle.Snapshot();
retainedDraft!["unit"]!["squad"]!["stores"]!["supply"]!["balance"] = 999;
var snapshotCopy = battle.Snapshot();
snapshotCopy["unit"]!["squad"]!["stores"]!["supply"]!["balance"] = 888;
receipt.Events[0]["current"] = 777;
receipt.Before["unit"]!["squad"]!["stores"]!["supply"]!["balance"] = 666;
Check("adapter state snapshot isolation", battle.Snapshot(), committed);
Check("resource event isolation", receipt.Events[0]["current"], JsonValue.Create(3));
battle.Restore(receipt);
Check("resource receipt restores original accounts", battle.Snapshot(), before);
ResourceConsumerChecks.Run(input, Check);
Console.WriteLine($"PASS {input["cases"]!.AsArray().Count} source resource cases, {input["registries"]!.AsArray().Count} source registry cases, {input["consumers"]!.AsArray().Count} command/weapon/tactic consumer cases, transactional rollback and snapshot/event isolation ({checks} assertions)");
