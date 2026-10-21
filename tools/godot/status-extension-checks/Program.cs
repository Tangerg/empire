using System.Text.Json.Nodes;
using Empire.Core.Actions;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Scenario;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Transactions;

using var input = File.OpenRead(args[0]);
using Stream payload = args[0].EndsWith(".gz", StringComparison.Ordinal) ? new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress) : input;
using var reader = new StreamReader(payload);
var fixture = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var packs = fixture["packs"]!.AsArray();
var count = 0;
void Equal(JsonNode? actual, JsonNode? expected, string name)
{
    if (EcmaJson.Stringify(actual) != EcmaJson.Stringify(expected))
    {
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("EMPIRE_TEST_ARTIFACTS") ?? Path.GetTempPath(), "status-failure.json"),
            new JsonObject { ["name"] = name, ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }.ToJsonString());
        throw new Exception(name);
    }
    count++;
}
foreach (var item in fixture["cases"]!.AsArray().OfType<JsonObject>())
{
    var mode = item["mode"]!.GetValue<string>();
    var engine = new GrayFlagEngine(packs);
    var hooks = new StatusBehaviorRegistry().Register(new("test-hook", context =>
    {
        context.State["scenario"]!["variables"]!["sameContent"] = ReferenceEquals(context.Content, engine.Catalog);
        context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = $"hook:{context.Status["remaining"]!.GetValue<double>()}" });
        if (new[] { "damage", "nonlethal", "zero", "negative", "kill" }.Contains(mode))
        {
            var amount = context.Damage(mode is "kill" or "nonlethal" ? 100000 : mode == "zero" ? 0 : mode == "negative" ? -3 : 3, mode == "nonlethal");
            context.State["scenario"]!["variables"]!["damageResult"] = amount;
        }
        if (mode == "remove") StatusTransitions.Remove(context.Unit, "test-hook", context.Emit);
        if (mode == "replace-status")
        {
            StatusTransitions.Remove(context.Unit, "test-hook", context.Emit);
            StatusTransitions.Add(engine.Catalog, context.Unit, "test-hook", 7, null, context.Emit);
        }
        if (mode == "append-status") StatusTransitions.Add(engine.Catalog, context.Unit, "test-next", 4, null, context.Emit);
        if (mode == "remove-later") StatusTransitions.Remove(context.Unit, "test-next", context.Emit);
        if (mode == "replace-unit")
        {
            var units = context.State["units"]!.AsArray();
            var index = units.IndexOf(context.Unit);
            units[index] = context.Unit.DeepClone();
            units[index]!["hp"] = 20;
        }
    })).Register(new("test-next", context => context.Emit(new JsonObject
    { ["type"] = "scenarioSignal", ["signal"] = $"next:{context.Status["remaining"]!.GetValue<double>()}" })));
    var statuses = new StatusLifecycle(engine.Catalog, hooks, engine.Combat);
    var state = item["before"]!.DeepClone().AsObject();
    var events = new JsonArray(); string? error = null;
    void Emit(JsonObject value) => events.Add(value.DeepClone());
    try
    {
        if (item["lifecycle"]!.GetValue<bool>())
        {
            var rules = new ActionRules(engine.Commands, engine.Deployment, engine.Transport, engine.Progression,
                engine.AbilityCommands, engine.Recruitment, engine.Support, engine.Combat,
                (draft, emit) => new BattleLifecycle(new BattleLifecycleRules(engine.Catalog, engine.Turns, engine.Scenarios, engine.Victory, engine.Combat, engine.Resources, statuses), draft, emit));
            var battle = new Battle(state, DefaultActionHandlers.Create(), new ActionExecutionPipeline(rules));
            foreach (var value in battle.Dispatch(new JsonObject { ["kind"] = "endTurn" }).Events) Emit(value);
            state = battle.Snapshot();
        }
        else
        {
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(u => u["id"]!.GetValue<int>() == item["unit"]!.GetValue<int>());
            statuses.Resolve(state, new[] { unit }, Emit);
        }
    }
    catch (Exception caught) { error = caught.Message; }
    var name = $"{mode}/{item["lifecycle"]}";
    Equal(JsonValue.Create(error), item["error"], name + "/error");
    Equal(events, item["events"], name + "/events");
    Equal(state, item["after"], name + "/state");
    Equal(JsonValue.Create(StateDigest.Hash(state)), item["hash"], name + "/hash");
}
Console.WriteLine($"PASS {fixture["cases"]!.AsArray().Count} source status lifecycle cases / {count} exact assertions.");
