using System.Text.Json.Nodes;
using System.IO.Compression;
using Empire.Core;
using Empire.Core.Actions;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Scenario;
using Empire.Core.Transactions;

using var input = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal)
    ? new StreamReader(new GZipStream(input, CompressionMode.Decompress)) : new StreamReader(input);
var fixture = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var packs = fixture["packs"]!.AsArray();
var engine = new BattleEngine(packs);
var rules = new ActionRules(engine.Commands, engine.Deployment, engine.Transport, engine.Progression,
    engine.AbilityCommands, engine.Recruitment, engine.Support, engine.Combat,
    (state, emit) => new BattleLifecycle(new(engine.Catalog, engine.Turns, engine.Scenarios, engine.Victory, engine.Combat, engine.Resources,
        new StatusLifecycle(engine.Catalog, new StatusBehaviorRegistry().Seal(), engine.Combat)), state, emit));
var pipeline = new ActionExecutionPipeline(rules);
var count = 0;
void Equal(JsonNode? actual, JsonNode? expected, string name)
{
    if (EcmaJson.Stringify(actual) != EcmaJson.Stringify(expected))
    {
        File.WriteAllText(Path.Combine(args.Length > 1 ? args[1] : Path.GetTempPath(), $"action-failure-{Environment.ProcessId}.json"),
            new JsonObject { ["name"] = name, ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }.ToJsonString());
        throw new Exception(name);
    }
    count++;
}
Equal(new JsonArray(DefaultActionHandlers.Create().Keys().Select(key => (JsonNode?)JsonValue.Create(key)).ToArray()), fixture["keys"], "source registration order");
foreach (var entry in fixture["cases"]!.AsArray().OfType<JsonObject>())
{
    var mode = entry["mode"]!.GetValue<string>();
    var handlers = DefaultActionHandlers.Create().Register("test-order", (context, _) =>
    {
        context.State["scenario"]!["variables"]!["testOrder"] = 7;
        context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "test.order" });
        context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "test.order.again" });
        if (mode == "fail")
            throw new IllegalActionException(new ActionRefusal("test.refused"));
        if (mode == "finish")
            context.State["phase"] = "over";
        if (mode == "begin")
            context.State["phase"] = "playing";
    }, handsOffTurn: true, duringDeployment: entry["during"]!.GetValue<bool>());
    var battle = new Battle(new BattleTransactionRules(handlers, pipeline), entry["before"]!.AsObject());
    ActionRefusal? error = null;
    var events = new JsonArray();
    try
    {
        var receipt = battle.Dispatch(entry["action"]!.AsObject());
        foreach (var value in receipt.Events)
            events.Add(value.DeepClone());
        if (!receipt.HandsOffTurn)
            throw new Exception("custom handoff metadata lost");
        count++;
    }
    catch (IllegalActionException caught) { error = caught.Refusal; }
    var name = $"{entry["phase"]}/{entry["during"]}/{mode}";
    var phase = entry["phase"]!.GetValue<string>();
    var during = entry["during"]!.GetValue<bool>();
    var expectedCode = phase == "over" ? "action.over"
        : phase == "deployment" && (mode == "unknown" || !during) ? "action.deployment_required"
        : mode == "unknown" ? "action.unsupported"
        : phase == "playing" && during ? "action.deployment_finished"
        : mode == "fail" ? "test.refused" : null;
    if ((entry["error"] is null) != (expectedCode is null))
        throw new Exception(name + "/source refusal status");
    Equal(JsonValue.Create(error?.Code), JsonValue.Create(expectedCode), name + "/error");
    Equal(error is null ? null : EcmaJson.Parse(error.Details.GetRawText()), expectedCode is null ? null :
        expectedCode == "action.unsupported" ? new JsonObject { ["kind"] = "missing-order" } : new JsonObject(), name + "/details");
    Equal(events, entry["events"], name + "/events");
    Equal(battle.Snapshot(), entry["after"], name + "/state");
    Equal(JsonValue.Create(battle.StateHash), entry["hash"], name + "/hash");
}
Console.WriteLine($"PASS {fixture["cases"]!.AsArray().Count} source custom action pipeline cases / {count} exact assertions.");
