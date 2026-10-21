using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Resources;
using Empire.Core.Scenario;
using Empire.Core.State;
using Empire.Core.Tactics;

if (args.Length != 1) throw new ArgumentException("Usage: ScenarioTests <oracle.json>");
var input = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var packs = input["packs"]!.AsArray();
var engine = new GrayFlagEngine(packs);
var combat = engine.Combat;
var scenarios = engine.Scenarios;
var effects = engine.ScenarioEffects;
var conditions = engine.ScenarioConditions;
var victory = engine.Victory;
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var count = 0;
var failures = new List<string>();
void Check(string name, string key, JsonNode? actual, JsonNode? expected)
{
    if (EcmaJson.DeepEquals(actual, expected)) return;
    var destination = Path.Combine(Path.GetDirectoryName(args[0])!, $"failed-{count}-{key}.json");
    File.WriteAllText(destination, EcmaJson.Stringify(new JsonObject { ["name"] = name, ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }));
    throw new Exception($"Mismatch in {name}: {key}; inspect {destination}");
}
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name");
    var state = item["initial"]!.DeepClone().AsObject();
    var events = new JsonArray(); var hashes = new JsonArray();
    void Emit(JsonObject ev) { events.Add(ev); hashes.Add(StateDigest.Hash(state)); }
    JsonNode? result = null, error = null;
    try
    {
        foreach (var effect in item["effects"]!.AsArray().OfType<JsonObject>())
        {
            effects.Apply(scenarios.Rules, state, effect, Emit);
        }
        for (var round = 0; round < StateJson.Number(item, "overlayRounds", 0); round++) Empire.Core.Terrain.BattleOverlays.AdvanceRound(state, Emit);
        if (item["condition"] is JsonObject condition) result = JsonValue.Create(scenarios.ConditionMet(state, condition));
        if (item["condition"] is JsonArray conditionList) result = new JsonArray(conditionList.OfType<JsonObject>().Select(condition => (JsonNode?)JsonValue.Create(scenarios.ConditionMet(state, condition))).ToArray());
        if (item["composite"] is { } composite) result = JsonSerializer.SerializeToNode(CompositeRules.Status(state, composite.GetValue<string>()), serialization);
        if (item["objective"] is JsonObject objective) result = new JsonObject
        {
            ["outcome"] = victory.Outcome(state, 1, objective),
            ["description"] = victory.Describe(objective),
        };
        if (StateJson.IsTrue(item["victory"])) result = JsonSerializer.SerializeToNode(victory.Evaluate(state, Emit), serialization);
    }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException or ArgumentOutOfRangeException)
    {
        error = new JsonObject { ["kind"] = caught is ArgumentOutOfRangeException ? "Range" : caught.GetType().Name.Replace("Exception", ""), ["message"] = caught.Message };
    }
    catch (Exception caught)
    {
        error = new JsonObject { ["kind"] = "Unexpected:" + caught.GetType().Name, ["message"] = caught.Message };
    }
    try
    {
        Check(name, "error", error, item["error"]);
        Check(name, "result", result, item["result"]);
        Check(name, "events", events, item["events"]);
        Check(name, "hashes", hashes, item["hashes"]);
        Check(name, "after", state, item["after"]);
    }
    catch (Exception failure) { failures.Add(failure.Message); }
    count++;
}
if (failures.Count > 0) throw new Exception($"{failures.Count}/{count} scenario cases diverged:\n" + string.Join("\n", failures));
Console.WriteLine($"PASS {count} live TypeScript scenario cases: ordered events, every event-time digest, complete states, queries and typed failures");
if (input["effectKinds"] is JsonArray kinds) Check("supported effect IDs", "effect-kinds",
    JsonSerializer.SerializeToNode(effects.Keys().Order(StringComparer.Ordinal)), kinds);
if (input["conditionKinds"] is JsonArray conditionKinds) Check("supported condition IDs", "condition-kinds",
    JsonSerializer.SerializeToNode(conditions.Keys().Order(StringComparer.Ordinal)), conditionKinds);
var dispatchCount = 0;
foreach (var item in input["integrationCases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name");
    var battle = engine.LoadSavedBattle(EcmaJson.Parse(EcmaJson.Stringify(engine.SaveBattle(item["initial"]!.AsObject()))));
    foreach (var expected in item["steps"]!.AsArray().OfType<JsonObject>())
    {
        Check(name, "dispatch-before", battle.Snapshot(), expected["before"]);
        var events = new JsonArray(); JsonObject? error = null;
        try
        {
            var receipt = battle.Dispatch(expected["action"]!.AsObject());
            foreach (var ev in receipt.Events) events.Add(ev.DeepClone());
        }
        catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
        { error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", ""), ["message"] = caught.Message }; }
        Check(name, "dispatch-error", error, expected["error"]);
        Check(name, "dispatch-events", events, expected["events"]);
        Check(name, "dispatch-after", battle.Snapshot(), expected["after"]);
        Check(name, "dispatch-hash", JsonValue.Create(battle.StateHash), expected["hash"]);
        battle = engine.LoadSavedBattle(EcmaJson.Parse(EcmaJson.Stringify(engine.SaveBattle(battle.Snapshot()))));
        Check(name, "resumed-state", battle.Snapshot(), expected["after"]);
        Check(name, "resumed-hash", JsonValue.Create(battle.StateHash), expected["hash"]);
        dispatchCount++;
    }
}
Console.WriteLine($"PASS {dispatchCount} full-engine scenario/trigger/lifecycle/rollback steps, saved/reloaded through JSON text after every action");
foreach (var duration in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
{
    var unit = input["cases"]![0]!["initial"]!["units"]![0]!.DeepClone().AsObject();
    var before = unit.DeepClone();
    var rejected = false;
    try { combat.Effects.AddStatus(unit, "shaken", duration, null, null); }
    catch (DomainInvariantException error) when (error.Message == "status duration must be >= 1") { rejected = true; }
    if (!rejected || !EcmaJson.DeepEquals(unit, before)) throw new Exception("Non-finite status duration must reject before mutation");
}
Console.WriteLine("PASS 3 native public-API non-finite status-duration guards (outside JSON's finite-number boundary)");
