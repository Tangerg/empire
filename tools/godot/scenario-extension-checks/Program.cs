using System.Text.Json;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Objectives;
using Empire.Core.References;
using Empire.Core.Resources;
using Empire.Core.Scenario;
using Empire.Core.State;
using Empire.Core.Tactics;
using static TestJson;

using var inputFile = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal)
    ? new StreamReader(new GZipStream(inputFile, CompressionMode.Decompress)) : new StreamReader(inputFile);
var oracle = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var packs = oracle["packs"]!.AsArray();
var engine = new GrayFlagEngine(packs);
var catalog = engine.Catalog;
var commands = engine.ScenarioCommands;
var baseConditions = DefaultScenarioConditions.Create(new DefaultBattleRandomSource());
var baseEffects = DefaultScenarioEffects.Create();
var baseObjectives = DefaultObjectives.Create();
var conditions = baseConditions.Clone().Register(new("testGate", (context, value) => context.Evaluate(Object(value["child"], "child")))
{ Children = value => [Object(value["child"], "child")], References = value => new PayloadReferences().Zone(Text(value, "zone")) });
var effects = baseEffects.Clone().Replace(new("setVariable", (context, value) => context.State["scenario"]!["variables"]![Text(value, "key")] = Number(value["value"], "value") * 2))
    .Register(new("testAward", (context, value) =>
    {
        context.Rules.ScenarioEffects.Apply(context.Rules, context.State,
            new JsonObject { ["type"] = "setVariable", ["key"] = "score", ["value"] = value["amount"]!.DeepClone() }, context.Emit);
        context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "awarded" });
    }) { References = value => new PayloadReferences().Zone(Text(value, "zone")) });
var objectives = baseObjectives.Clone().Register(new("testGoal", "Custom goal",
    (context, _) => Number(context.State["scenario"]!["variables"]!.AsObject(), "score", 0) >= 12 ? "success" : "pending",
    (context, _) => $"score={Number(context.State["scenario"]!["variables"]!.AsObject(), "score", 0)}")
{ Role = "optional", References = value => new PayloadReferences().Zone(Text(value, "zone")) })
    .Register(new("testSelf", "Self only", (_, _) => "pending", (_, _) => "self")
    { Children = value => Objects(value["objectives"], "objectives").ToArray() });
var rules = new ScenarioRules(catalog, conditions, effects, commands);
var scenarios = new ScenarioSystem(rules);
var victory = new VictorySystem(new(catalog, conditions), objectives);
var state = oracle["initial"]!.DeepClone().AsObject();
var events = new JsonArray(); var hashes = new JsonArray();
void Emit(JsonObject value) { events.Add(value); hashes.Add(StateDigest.Hash(state)); }
var goal = state["players"]![0]!["objectives"]![0]!.AsObject();
var self = state["players"]![0]!["objectives"]![1]!.AsObject();
var beforeProgress = victory.Progress(state, 1, goal);
scenarios.Run(state, "afterAction", Emit); victory.RefreshObjectiveStates(state, Emit);
scenarios.Run(state, "afterAction", Emit); victory.RefreshObjectiveStates(state, Emit);
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, serialization);
var metadata = new JsonObject
{
    ["conditionChildren"] = Node(conditions.Children(state["scenario"]!["triggers"]![1]!["condition"]!.AsObject())),
    ["conditionReferences"] = Node(conditions.References(state["scenario"]!["triggers"]![1]!["condition"]!.AsObject())),
    ["effectReferences"] = Node(effects.References(state["scenario"]!["triggers"]![1]!["effects"]![0]!.AsObject())),
    ["objectiveReferences"] = Node(objectives.References(goal)), ["description"] = objectives.Describe(goal),
    ["role"] = objectives.Role(goal), ["refresh"] = objectives.RefreshMode(self),
    ["beforeProgress"] = beforeProgress, ["afterProgress"] = victory.Progress(state, 1, goal),
};
var checks = 0;
void Equal(string name, JsonNode? actual, JsonNode? expected)
{
    if (!EcmaJson.DeepEquals(actual, expected)) throw new Exception($"Mismatch: {name}\nactual={actual}\nexpected={expected}");
    checks++;
}
void Require(string name, bool condition) { if (!condition) throw new Exception("Failed: " + name); checks++; }
void Reject(string name, Action action)
{
    try { action(); } catch (DomainInvariantException) { checks++; return; }
    throw new Exception("Expected domain rejection: " + name);
}
Equal("custom scenario events", events, oracle["events"]);
Equal("custom scenario event-time hashes", hashes, oracle["hashes"]);
Equal("custom scenario full state", state, oracle["after"]);
Equal("handler-owned metadata", metadata, oracle["metadata"]);
Equal("ordered condition keys", Node(baseConditions.Keys()), oracle["keys"]!["conditions"]);
Equal("ordered effect keys", Node(baseEffects.Keys()), oracle["keys"]!["effects"]);
Equal("ordered objective keys", Node(baseObjectives.Keys()), oracle["keys"]!["objectives"]);
foreach (var item in oracle["metadataCases"]!.AsArray().OfType<JsonObject>())
{
    var payload = item["payload"]!.AsObject();
    var family = Text(item, "family");
    IRulePayloadRegistry registry = family switch { "condition" => baseConditions, "objective" => baseObjectives, _ => baseEffects };
    Equal($"default {family} references", Node(registry.References(payload)), item["references"]);
    if (registry is IRuleTreeRegistry tree) Equal($"default {family} children", Node(tree.Children(payload)), item["children"]);
    if (family == "objective")
    {
        Equal("default objective role", Node(baseObjectives.Role(payload)), item["role"]);
        Equal("default objective refresh", Node(baseObjectives.RefreshMode(payload)), item["refresh"]);
        Equal("default objective description", Node(baseObjectives.Describe(payload)), item["description"]);
    }
}
foreach (var edge in oracle["edgeConditions"]!.AsArray().OfType<JsonObject>())
{
    var edgeState = oracle["initial"]!.DeepClone().AsObject();
    edgeState["units"] = new JsonArray(edgeState["units"]![0]!.DeepClone()); edgeState["units"]![0]!["hp"] = edge["hp"]!.DeepClone();
    Equal("unitHealth clamps", Node(baseConditions.Evaluate(edgeState, edge["condition"]!.AsObject(), catalog)), edge["result"]);
}
foreach (var edge in oracle["edgeMaxima"]!.AsArray().OfType<JsonObject>())
{
    var edgePacks = packs.DeepClone().AsArray();
    foreach (var pack in edgePacks.OfType<JsonObject>())
        foreach (var unit in (pack["units"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()) unit["maxHp"] = edge["maximum"]!.DeepClone();
    Equal("unitHealth nonpositive maximum is whole", Node(baseConditions.Evaluate(oracle["initial"]!.AsObject(), edge["condition"]!.AsObject(), new ReadOnlyContent(edgePacks))), edge["result"]);
}
var unknown = new JsonObject { ["type"] = "missing" };
Equal("unknown condition references", Node(conditions.References(unknown)), Node(new PayloadReferences()));
Equal("unknown effect references", Node(effects.References(unknown)), Node(new PayloadReferences()));
Equal("unknown objective references", Node(objectives.References(unknown)), Node(new PayloadReferences()));
Reject("unknown condition execution", () => conditions.Evaluate(state, unknown, catalog));
Reject("unknown condition children", () => conditions.Children(unknown));
Reject("unknown effect execution", () => effects.Apply(rules, state, unknown, _ => { }));
Reject("unknown objective execution", () => objectives.Evaluate(victory.Rules, state, 1, unknown));
Reject("unknown objective children", () => objectives.Children(unknown));
Equal("undeclared objective child is not inferred", Node(objectives.Children(new JsonObject { ["type"] = "interact", ["objective"] = unknown.DeepClone() })), new JsonArray());
Require("clones do not leak custom kinds", !baseConditions.Has("testGate") && !baseEffects.Has("testAward") && !baseObjectives.Has("testGoal"));
Reject("duplicate conditions", () => conditions.Register(conditions.Get("testGate")));
Reject("duplicate effects", () => effects.Register(effects.Get("testAward")));
Reject("duplicate objectives", () => objectives.Register(objectives.Get("testGoal")));
var replacement = baseObjectives.Clone().Replace(new("score", "Replacement score", (_, _) => "failure", (_, _) => "replacement-progress") { Role = "critical" });
var score = new JsonObject { ["type"] = "score", ["variable"] = "score", ["atLeast"] = 1 };
Require("built-in objective replacement", replacement.Evaluate(victory.Rules, state, 1, score) == "failure" && replacement.Role(score) == "critical" && replacement.Progress(victory.Rules, state, 1, score) == "replacement-progress" && replacement.Describe(score) == "Replacement score");
var replacedCondition = baseConditions.Clone().Replace(new("turnAtLeast", (_, _) => false));
Require("built-in condition replacement", !replacedCondition.Evaluate(state, new JsonObject { ["type"] = "all", ["conditions"] = new JsonArray(new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 1 }) }, catalog));
var random = new ObservingRandom();
var randomConditions = baseConditions.Clone(random);
Require("random sees full state", randomConditions.Evaluate(state, new JsonObject { ["type"] = "chance", ["percent"] = 99, ["stream"] = "custom" }, catalog) && ReferenceEquals(random.Seen, state));
baseConditions.Seal(); baseEffects.Seal(); baseObjectives.Seal();
Reject("sealed conditions", () => baseConditions.Replace(baseConditions.Get("chance")));
Reject("sealed effects", () => baseEffects.Replace(baseEffects.Get("emitSignal")));
Reject("sealed objectives", () => baseObjectives.Replace(baseObjectives.Get("score")));
Require("sealed clones remain independently extendable", !baseConditions.Clone().IsSealed && !baseEffects.Clone().IsSealed && !baseObjectives.Clone().IsSealed);
Console.WriteLine($"PASS {checks} scenario/objective extension checks: live source differential, ordered keys 16/34/17, metadata, open composition, random injection, self-refresh and failures");

sealed class ObservingRandom : IBattleRandomSource
{
    public JsonObject? Seen { get; private set; }
    public string Id => "observing";
    public bool Chance(JsonObject state, string stream, double percent) { Seen = state; return stream == "custom" && percent == 99; }
    public double Next(JsonObject state, string stream) => throw new NotSupportedException();
    public double Peek(JsonObject state, string stream) => throw new NotSupportedException();
    public double Int(JsonObject state, string stream, double bound) => throw new NotSupportedException();
    public T Pick<T>(JsonObject state, string stream, IReadOnlyList<T> items) => throw new NotSupportedException();
}

static class TestJson
{
    public static JsonObject Object(JsonNode? value, string _) => value!.AsObject();
    public static IEnumerable<JsonObject> Objects(JsonNode? value, string _) => value!.AsArray().OfType<JsonObject>();
    public static string Text(JsonObject value, string key) => value[key]!.GetValue<string>();
    public static int Integer(JsonObject value, string key) => (int)value[key]!.GetValue<double>();
    public static double Number(JsonNode? value, string _) => value!.GetValue<double>();
    public static double Number(JsonObject value, string key, double fallback) => value[key]?.GetValue<double>() ?? fallback;
}
