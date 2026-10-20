using System.Text.Json.Nodes;
using System.IO.Compression;
using Empire.Core;
using Empire.Core.Actions;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.State;
using Empire.Core.Scenario;
using Empire.Core.Transactions;

using var input = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal)
    ? new StreamReader(new GZipStream(input, CompressionMode.Decompress)) : new StreamReader(input);
var fixture = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var packs = fixture["packs"]!.AsArray();
var engine = new GrayFlagEngine(packs);
var rules = new ActionRules(engine.Commands, engine.Deployment, engine.Transport, engine.Progression,
    engine.AbilityCommands, engine.Recruitment, engine.Support, engine.Combat,
    (state, emit) => new BattleLifecycle(state, packs, emit, engine.Combat, engine.Resources, engine.Scenarios,
        CompositeRules.ObjectiveOutcome, CompositeRules.DescribeObjective, CompositeRules.ObjectiveProgress, engine.Victory));
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
        if (mode == "fail") throw new IllegalActionException("test refusal");
        if (mode == "finish") context.State["phase"] = "over";
        if (mode == "begin") context.State["phase"] = "playing";
    }, handsOffTurn: true, duringDeployment: entry["during"]!.GetValue<bool>());
    var battle = new Battle(entry["before"]!.AsObject(), handlers, pipeline);
    string? error = null;
    var events = new JsonArray();
    try
    {
        var receipt = battle.Dispatch(entry["action"]!.AsObject());
        foreach (var value in receipt.Events) events.Add(value.DeepClone());
        if (!receipt.HandsOffTurn) throw new Exception("custom handoff metadata lost");
        count++;
    }
    catch (IllegalActionException caught) { error = caught.Message; }
    var name = $"{entry["phase"]}/{entry["during"]}/{mode}";
    Equal(JsonValue.Create(error), entry["error"], name + "/error");
    Equal(events, entry["events"], name + "/events");
    Equal(battle.Snapshot(), entry["after"], name + "/state");
    Equal(JsonValue.Create(battle.StateHash), entry["hash"], name + "/hash");
}
Console.WriteLine($"PASS {fixture["cases"]!.AsArray().Count} source custom action pipeline cases / {count} exact assertions.");
