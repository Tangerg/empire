using System.IO.Compression;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Ai;
using Empire.Core.Campaign;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Registries;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

var oracleText = ReadOracle(args[0]);
if (args.Length > 2)
{
    Require(oracleText == ReadOracle(args[2]), "Frozen registry oracle differs from regenerated source; review source/runtime changes before refreshing it");
    Console.WriteLine("PASS frozen registry fixture matches regenerated source bytes");
}
var oracle = JsonNode.Parse(oracleText)!.AsObject();
var diagnostics = args.Length > 1 ? args[1] : Path.GetDirectoryName(args[0])!;
var checkedCount = 0;
void Check(string subject, string name, Func<object?> call)
{
    var cases = subject == "priority" ? oracle["priority"]!.AsArray() : oracle["keyed"]!.AsArray()
        .Single(item => item!["subject"]!.GetValue<string>() == subject)!["checks"]!.AsArray();
    var expected = cases.Single(item => item!["name"]!.GetValue<string>() == name)!.AsObject();
    JsonObject actual;
    try
    {
        actual = new()
        {
            ["name"] = name,
            ["value"] = JsonSerializer.SerializeToNode(call()),
            ["error"] = null
        };
    }
    catch (DomainInvariantException error)
    {
        actual = new()
        {
            ["name"] = name,
            ["value"] = null,
            ["error"] = new JsonObject { ["name"] = "DomainInvariantError", ["message"] = error.Message }
        };
    }
    Require(JsonNode.DeepEquals(actual, expected), $"{subject}: {name}\nExpected {expected}\nActual {actual}");
    checkedCount++;
}

void Keyed<TEntry, TRegistry>(string subject, TRegistry registry, Func<string, TEntry> entry, Func<TRegistry, TRegistry> copy)
    where TEntry : class where TRegistry : KeyedRegistry<TEntry, TRegistry>
{
    void Test(string name, Func<object?> call) => Check(subject, name, call);
    var z = entry("z");
    var a = entry("a");
    var replacement = entry("z");
    Test("empty", () => new { count = registry.Count, keys = registry.Keys(), all = registry.All(), @sealed = registry.IsSealed });
    Test("query absent", () => new { has = registry.Has("missing"), optional = registry.TryGet("missing") });
    Test("demand absent", () => registry.Get("missing"));
    Test("fluent register", () => ReferenceEquals(registry.Register(z), registry) && ReferenceEquals(registry.Register(a), registry));
    Test("entry identities and order", () => new
    {
        keys = registry.Keys(),
        identities = ReferenceEquals(registry.All()[0], z) && ReferenceEquals(registry.All()[1], a),
        get = ReferenceEquals(registry.Get("a"), a),
        optional = ReferenceEquals(registry.TryGet("a"), a),
        has = registry.Has("a"),
        count = registry.Count
    });
    Test("duplicate rejects", () => registry.Register(entry("z")));
    Test("replace retains position", () => new { same = ReferenceEquals(registry.Replace(replacement), registry), keys = registry.Keys(), entry = ReferenceEquals(registry.Get("z"), replacement) });
    Test("replace introduces key", () => new { same = ReferenceEquals(registry.Replace(entry("new")), registry), keys = registry.Keys(), count = registry.Count });
    var keys = registry.Keys();
    var all = registry.All();
    Test("detached snapshots", () => { registry.Register(entry("later")); return new { keys, count = all.Count, first = ReferenceEquals(all[0], replacement), current = registry.Keys() }; });
    Test("collection edits cannot change table", () =>
    {
        var editableKeys = (IList<string>)keys;
        editableKeys[0] = "forged";
        var editableAll = (IList<TEntry>)all;
        editableAll[0] = entry("forged");
        return registry.Keys();
    });
    Test("seal is fluent and idempotent", () => new { same = ReferenceEquals(registry.Seal(), registry) && ReferenceEquals(registry.Seal(), registry), @sealed = registry.IsSealed });
    Test("sealed duplicate rejects before duplicate check", () => registry.Register(entry("a")));
    Test("sealed new register rejects", () => registry.Register(entry("late")));
    Test("sealed existing replace rejects", () => registry.Replace(entry("a")));
    Test("sealed new replace rejects", () => registry.Replace(entry("late")));
    var clone = copy(registry);
    Test("clone from sealed is mutable and shallow", () => new
    {
        @sealed = clone.IsSealed,
        originalSealed = registry.IsSealed,
        keys = clone.Keys(),
        count = clone.Count,
        shared = clone.All().Select((value, index) => ReferenceEquals(value, registry.All()[index])).All(value => value)
    });
    Test("clone table mutations are independent", () =>
    {
        clone.Replace(entry("a"));
        clone.Register(entry("clone-only"));
        return new
        {
            original = registry.Keys(),
            cloned = clone.Keys(),
            originalEntry = ReferenceEquals(registry.Get("a"), a),
            cloneReplaced = !ReferenceEquals(clone.Get("a"), a),
            untouchedShared = ReferenceEquals(clone.Get("z"), registry.Get("z"))
        };
    });
}
Keyed<ITacticalGrid, GridRegistry>("tactical grid", new(), id => new RegistryGrid(id), registry => registry.Clone());
Keyed<WeaponAreaShape, WeaponAreaRegistry>("weapon area shape", new(), id => new(id, false, (_, _, _) => Array.Empty<Coord>()), registry => registry.Clone());
Keyed<UnitDirectiveBehavior, UnitDirectiveRegistry>("unit directive", new(), id => new(id, _ => 0, 0, 0), registry => registry.Clone());
Keyed<BattleActionRegistration, BattleActionRegistry>("action handler", new(), id => new(id, (_, _, _) => { }), registry => registry.Clone());
Keyed<AiIntent, AiIntentRegistry>("AI intent", new(), id => new(id, 0, _ => null), registry => registry.Clone());
Keyed<AbilityAiEvaluator, AbilityAiEvaluatorRegistry>("AI ability evaluator", new(), id => new(id, _ => null), registry => registry.Clone());
Keyed<AiObjectiveAdvisor, AiObjectiveAdvisorRegistry>("AI objective advisor", new(), id => new(id, (_, _) => { }), registry => registry.Clone());
Keyed<CampaignConditionHandler, CampaignConditionRegistry>("campaign condition handler", new(), id => new(id, (_, _, _) => true), registry => registry.Clone());
Keyed<CampaignEffectHandler, CampaignEffectRegistry>("campaign effect handler", new(), id => new(id, (_, _) => { }), registry => registry.Clone());
Keyed<CampaignNodeHandler, CampaignNodeRegistry>("campaign node handler", new(), id => new(id, (_, _) => { }), registry => registry.Clone());
Keyed<RuleEntry, TestRegistry>("test rule", new(), id => new(id), registry => registry.Clone());

void Priority()
{
    var registry = new TestPriorities();
    void Test(string name, Func<object?> call) => Check("priority", name, call);
    string[] Ids(IReadOnlyList<RuleEntry> entries) => entries.Select(entry => entry.Id).ToArray();
    Test("empty ordered cached and read only", () => new { keys = Ids(registry.Ordered()), cached = ReferenceEquals(registry.Ordered(), registry.Ordered()), frozen = ((IList<RuleEntry>)registry.Ordered()).IsReadOnly });
    foreach (var id in new[] { "a.", "\ue000", "\ud800\udc00", "Z", "a-", "$", "\ud83d\ude00", "a" })
        registry.Register(new(id, 0.25));
    registry.Register(new("negative", -1.5));
    registry.Register(new("fraction", 0.125));
    registry.Register(new("last", 10.5));
    Test("fractional priority and UTF-16 ties", () => Ids(registry.Ordered()));
    var prior = registry.Ordered();
    Test("replace invalidates cache and changes order", () => { registry.Replace(new("last", -2.75)); return new { keys = Ids(registry.Ordered()), changed = !ReferenceEquals(prior, registry.Ordered()), old = Ids(prior) }; });
    var beforeRegister = registry.Ordered();
    Test("register invalidates cache", () => { registry.Register(new("new", -3.5)); return new { changed = !ReferenceEquals(beforeRegister, registry.Ordered()), first = registry.Ordered()[0].Id }; });
    var beforeDuplicate = registry.Ordered();
    Test("duplicate still rejects", () => registry.Register(new("new")));
    Test("failed register invalidates cache without changing table", () => new { changed = !ReferenceEquals(beforeDuplicate, registry.Ordered()), keys = registry.Keys() });
    registry.Seal();
    var sealedOrder = registry.Ordered();
    Test("sealed priority replacement rejects", () => registry.Replace(new("new")));
    Test("failed sealed write invalidates cache", () => new { changed = !ReferenceEquals(sealedOrder, registry.Ordered()), keys = Ids(registry.Ordered()) });
    Test("ordered list cannot be modified", () => { try { ((IList<RuleEntry>)registry.Ordered())[0] = new("forged"); return false; } catch (NotSupportedException) { return true; } });
    var firstEntry = new RuleEntry("first");
    var secondEntry = new RuleEntry("second");
    var stable = new TestPriorities().Register(firstEntry).Register(secondEntry);
    secondEntry.Id = "first";
    Test("comparator ties preserve insertion order", () => new { first = ReferenceEquals(stable.Ordered()[0], firstEntry), second = ReferenceEquals(stable.Ordered()[1], secondEntry) });
    var clone = registry.Clone();
    Test("priority clone is independent mutable shallow", () =>
    {
        var original = registry.Get("new");
        clone.Replace(new("new", 99));
        return new
        {
            @sealed = clone.IsSealed,
            originalSame = ReferenceEquals(registry.Get("new"), original),
            shared = ReferenceEquals(clone.Get("fraction"), registry.Get("fraction")),
            originalFirst = registry.Ordered()[0].Id,
            cloneFirst = clone.Ordered()[0].Id
        };
    });
    var sharing = new TestPriorities().Register(new("payload")).Seal();
    var copy = sharing.Clone();
    Test("clone keeps mutable entry identity", () => { copy.Get("payload").Payload.Value = 7; return new { same = ReferenceEquals(copy.Get("payload"), sharing.Get("payload")), value = sharing.Get("payload").Payload.Value }; });
    var choose = new AiIntentRegistry();
    var visited = new List<string>();
    choose.Register(new("late", 0.75, _ => { visited.Add("late"); return new JsonObject { ["kind"] = "endTurn" }; }));
    choose.Register(new("early", -0.25, _ => { visited.Add("early"); return null; }));
    choose.Register(new("middle", 0.125, _ => { visited.Add("middle"); return new JsonObject { ["kind"] = "endTurn" }; }));
    Test("fractional ordering reaches AI dispatch", () => new { action = choose.Choose(null!), visited });
}
Priority();
var nonPortable = new JsonArray();
foreach (var test in oracle["numeric"]!.AsArray().OfType<JsonObject>())
{
    var registry = new TestPriorities();
    foreach (var value in test["entries"]!.AsArray().OfType<JsonObject>())
    {
        var raw = value["priority"]!;
        var priority = raw.GetValueKind() == JsonValueKind.Number ? raw.GetValue<double>() : raw.GetValue<string>() switch
        {
            "NaN" => double.NaN,
            "+Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            _ => throw new Exception("Unknown priority")
        };
        registry.Register(new(value["id"]!.GetValue<string>(), priority));
    }
    var actualOrder = JsonSerializer.SerializeToNode(registry.Ordered().Select(entry => entry.Id));
    if (test["portable"]!.GetValue<bool>())
    {
        Require(JsonNode.DeepEquals(actualOrder, test["expected"]), "numeric boundary: " + test["name"]);
        checkedCount++;
    }
    else
        nonPortable.Add(new JsonObject
        {
            ["name"] = test["name"]!.DeepClone(),
            ["entries"] = test["entries"]!.DeepClone(),
            ["source"] = test["expected"]!.DeepClone(),
            ["native"] = actualOrder,
            ["same"] = JsonNode.DeepEquals(actualOrder, test["expected"])
        });
}
Require(oracle["nonFiniteCharacterization"]!["cyclicComparison"]!.AsArray().All(value => value!.GetValue<bool>()), "Source NaN cycle characterization is missing");

var actionKeys = new BattleActionRegistry();
var executed = new List<string>();
foreach (var kind in new[] { "", "  ", "\t\n", "__proto__", "2", "10" })
{
    actionKeys.Register(kind, (_, _, _) => executed.Add($"first:{kind}"));
    new Battle(new BattleTransactionRules(actionKeys, null), new JsonObject()).Dispatch(new JsonObject { ["kind"] = kind });
    actionKeys.Replace(kind, (_, _, _) => executed.Add($"replaced:{kind}"), true);
    new Battle(new BattleTransactionRules(actionKeys, null), new JsonObject()).Dispatch(new JsonObject { ["kind"] = kind });
}
actionKeys.Replace("upsert", (_, _, _) => executed.Add("upsert"));
new Battle(new BattleTransactionRules(actionKeys, null), new JsonObject()).Dispatch(new JsonObject { ["kind"] = "upsert" });
Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(new
{
    keys = actionKeys.Keys(),
    executed,
    handsOff = actionKeys.Keys().Select(kind => actionKeys.HandsOffTurn(new JsonObject { ["kind"] = kind }))
}), oracle["actionKeyBoundaries"]), "Action key/dispatch boundary mismatch");
checkedCount++;
try { actionKeys.Register("invalid", (BattleActionHandler)null!); throw new Exception("Null CLR handler accepted"); } catch (ArgumentNullException) { }
try { actionKeys.Replace("invalid", (BattleActionHandler)null!); throw new Exception("Null CLR handler accepted"); } catch (ArgumentNullException) { }

try { actionKeys.Register("invalid", (RuleActionHandler)null!); throw new Exception("Null rule handler accepted"); } catch (ArgumentNullException) { }
try { actionKeys.Replace("invalid", (RuleActionHandler)null!); throw new Exception("Null rule handler accepted"); } catch (ArgumentNullException) { }

var metadata = new BattleActionRegistry().Register("custom", (_, _, _) => { }, handsOffTurn: true, duringDeployment: true);
var metadataClone = metadata.Clone().Seal();
var frozenMetadata = metadata.Freeze();
var registeredMetadata = metadata.Get("custom");
metadata.Replace("custom", (_, _, _) => { });
metadata.Replace("deployment", (_, _, _) => { }, duringDeployment: true);
object Flags(BattleActionRegistry registry, string kind) => new
{
    handsOff = registry.HandsOffTurn(new JsonObject { ["kind"] = kind }),
    duringDeployment = registry.DuringDeployment(new JsonObject { ["kind"] = kind })
};
Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(new
{
    clone = Flags(metadataClone, "custom"),
    replaced = Flags(metadata, "custom"),
    introduced = Flags(metadata, "deployment"),
    unknown = Flags(metadata, "unknown"),
    cloneSealed = metadataClone.IsSealed
}), oracle["actionMetadata"]), "Action metadata source parity");
Require(ReferenceEquals(frozenMetadata["custom"], registeredMetadata) && frozenMetadata["custom"].HandsOffTurn && frozenMetadata["custom"].DuringDeployment &&
    !frozenMetadata.ContainsKey("deployment"), "Frozen action metadata/table changed after caller mutation");
Require(ReferenceEquals(metadataClone.Get("custom"), registeredMetadata), "Action clone did not retain entry identity");
checkedCount++;

// The table and handoff metadata captured by Battle must not follow later caller writes.
var actions = new BattleActionRegistry().Register("change", (state, _, _) => state["value"] = "original", true);
var battle = new Battle(new BattleTransactionRules(actions, null), new JsonObject());
actions.Replace("change", (state, _, _) => state["value"] = "replacement");
actions.Replace("new", (state, _, _) => state["value"] = "new");
var receipt = battle.Dispatch(new JsonObject { ["kind"] = "change" });
Require(receipt.HandsOffTurn && battle.Snapshot()["value"]!.GetValue<string>() == "original", "Caller replacement changed Battle snapshot");
try { battle.Dispatch(new JsonObject { ["kind"] = "new" }); throw new Exception("Caller upsert reached existing Battle"); } catch (IllegalActionException) { }
var next = new Battle(new BattleTransactionRules(actions, null), new JsonObject());
Require(!next.Dispatch(new JsonObject { ["kind"] = "change" }).HandsOffTurn && next.Snapshot()["value"]!.GetValue<string>() == "replacement", "New Battle did not capture replacement");
next.Dispatch(new JsonObject { ["kind"] = "new" });
Require(next.Snapshot()["value"]!.GetValue<string>() == "new", "Action Replace is not upsert");
Require(!actions.HandsOffTurn(new JsonObject { ["kind"] = "unknown" }), "Unknown action has handoff metadata");
var expectedCount = oracle["keyed"]!.AsArray().Sum(test => test!["checks"]!.AsArray().Count) + oracle["priority"]!.AsArray().Count +
    oracle["numeric"]!.AsArray().Count(test => test!["portable"]!.GetValue<bool>()) + 2;
Require(checkedCount == expectedCount, "Source registry cases were not all consumed");
var differences = nonPortable.Count(item => !item!["same"]!.GetValue<bool>());
var characterizationPath = Path.Combine(diagnostics, "non-finite-characterization.json");
var characterization = new JsonObject
{
    ["sourceRuntime"] = oracle["sourceRuntime"]!.DeepClone(),
    ["nativeRuntime"] = RuntimeInformation.FrameworkDescription,
    ["contract"] = oracle["nonFiniteCharacterization"]!.DeepClone(),
    ["cases"] = nonPortable
};
File.WriteAllText(characterizationPath, characterization.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"CHARACTERIZED {nonPortable.Count} non-transitive mixed-NaN orders; {differences} differ between source and native sorting. Evidence: {characterizationPath}");
Console.WriteLine($"PASS {checkedCount} exact source registry outcomes across 11 native registries; Battle table/handoff isolation and action upsert; source NaN comparator cycle characterized.");

static string ReadOracle(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = path.EndsWith(".gz", StringComparison.Ordinal)
        ? new StreamReader(new GZipStream(stream, CompressionMode.Decompress)) : new StreamReader(stream);
    return reader.ReadToEnd();
}

static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
sealed class Payload
{
    public int Value { get; set; } = 1;
}
sealed class RuleEntry(string id, double priority = 0) : IPrioritizedEntry
{
    public string Id { get; set; } = id;
    public double Priority { get; set; } = priority;
    public Payload Payload { get; } = new();
}
sealed class TestRegistry : KeyedRegistry<RuleEntry, TestRegistry>
{
    public TestRegistry() : base("test rule") { }
    protected override string KeyOf(RuleEntry entry) => entry.Id;
    public TestRegistry Clone() => CopyInto(new TestRegistry());
}
sealed class TestPriorities : PriorityRegistry<RuleEntry, TestPriorities>
{
    public TestPriorities() : base("priority rule") { }
    public TestPriorities Clone() => CopyInto(new TestPriorities());
}
sealed class RegistryGrid(string id) : SquareTiling
{
    public override string Id => id;
    public override IReadOnlyList<DirectionDef> Directions => Array.Empty<DirectionDef>();
    public override int Distance(Coord a, Coord b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}
