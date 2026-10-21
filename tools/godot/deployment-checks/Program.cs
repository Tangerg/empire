using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, path)))!;
var packs = Read("godot/data/content-packs.json").AsArray();
var vectors = Read("tools/godot/deployment-checks/vectors.json").AsObject();
using var nativeStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("NativeRefusals")
    ?? throw new Exception("Missing explicit native refusal expectations");
var nativeRefusals = JsonNode.Parse(nativeStream)!.AsObject();
var nativeCases = nativeRefusals["cases"]!.AsObject();
if (!vectors["cases"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).SequenceEqual(nativeCases.Select(pair => pair.Key)))
    throw new Exception("Native refusal expectation cases are missing or stale");
var engine = new GrayFlagEngine(packs);
var stateRules = new BattleStateRules(engine.Catalog, engine.Grids, engine.Reactions);
var passed = 0;
var failed = 0;
void Check(string name, Action run)
{
    try { run(); passed++; }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
}
void Equal(JsonNode? expected, JsonNode? actual, string subject)
{
    if (expected is null && actual is null) return;
    if (expected is null || actual is null || StateDigest.Hash(expected) != StateDigest.Hash(actual))
        throw new Exception($"{subject}: expected {expected?.ToJsonString() ?? "null"}; got {actual?.ToJsonString() ?? "null"}");
}
Coord Position(JsonNode at) => new(at["x"]!.GetValue<int>(), at["y"]!.GetValue<int>());
JsonObject Coordinate(Coord at) => new() { ["x"] = at.X, ["y"] = at.Y };
void Outcome(Battle battle, JsonObject expected, JsonNode? nativeRefusal = null)
{
    var action = expected["action"]!.AsObject();
    var before = battle.StateHash;
    if (expected["error"] is JsonObject)
    {
        try { battle.Dispatch(action); throw new Exception("Expected action refusal"); }
        catch (IllegalActionException error)
        {
            Equal(nativeRefusal, JsonSerializer.SerializeToNode(error.Refusal), "semantic action refusal");
        }
        if (before != battle.StateHash) throw new Exception("Refused action changed state");
    }
    else
    {
        if (nativeRefusal is not null) throw new Exception("Native expectation rejects an accepted source action");
        var receipt = battle.Dispatch(action);
        Equal(expected["events"], new JsonArray(receipt.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()), "events");
    }
    if (battle.StateHash != expected["stateHash"]!.GetValue<string>())
        throw new Exception($"State hash mismatch: {battle.StateHash} != {expected["stateHash"]}");
}

// Only deliberately malformed or directly edited oracle states use this test envelope.
// Ordinary levels run through the production composition root below.
Battle Isolated(JsonObject initial)
{
    var registry = new BattleActionRegistry();
    void Dispatch(JsonObject state, JsonObject action, Action<JsonObject> output)
    {
        var phase = state["phase"]!.GetValue<string>();
        var kind = action["kind"]!.GetValue<string>();
        if (phase == "over") throw new IllegalActionException(new ActionRefusal("action.over"));
        var deploying = kind is "deployUnit" or "finishDeployment";
        if (phase == "deployment" && !deploying) throw new IllegalActionException(new ActionRefusal("action.deployment_required"));
        if (phase == "playing" && deploying) throw new IllegalActionException(new ActionRefusal("action.deployment_finished"));
        void Emit(JsonObject value)
        {
            var counts = state["scenario"]!["eventCounts"]!.AsObject();
            var type = value["type"]!.GetValue<string>();
            counts[type] = (counts[type]?.GetValue<int>() ?? 0) + 1;
            output(value);
        }
        var lifecycle = new BattleLifecycle(new(engine.Catalog, engine.Turns, engine.Scenarios, engine.Victory, engine.Combat, engine.Resources,
            new StatusLifecycle(engine.Catalog, new StatusBehaviorRegistry().Seal(), engine.Combat)), state, Emit);
        if (kind == "deployUnit") engine.Deployment.Deploy(state, action["unit"]!.GetValue<int>(), Position(action["at"]!), Emit);
        else if (kind == "finishDeployment") engine.Deployment.Confirm(state, lifecycle.BeginPlaying, Emit);
        else throw new Exception("Non-deployment action unexpectedly passed phase gate");
    }
    foreach (var kind in new[] { "deployUnit", "finishDeployment", "endTurn" }) registry.Register(kind, Dispatch);
    return new Battle(initial, registry);
}

foreach (var fixture in vectors["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = fixture["name"]!.GetValue<string>();
    var native = nativeCases[name]!.AsObject();
    var nativeQueries = native["queries"]!.AsArray();
    if (!fixture["queries"]!.AsArray().Select(query => query!["unit"]!.GetValue<int>())
        .SequenceEqual(nativeQueries.Select(query => query!["unit"]!.GetValue<int>())))
        throw new Exception("Native deployment query expectations are missing or stale: " + name);
    var level = fixture["level"]!.AsObject();
    var state = fixture["state"]!.AsObject();
    var rawHash = fixture["rawStateHash"]!.GetValue<string>();
    Check($"{name} state construction", () =>
    {
        if (StateDigest.Hash(BattleStateFactory.Create(stateRules, level)) != rawHash) throw new Exception("Raw initial state differs");
    });
    Check($"{name} roster", () =>
    {
        var before = StateDigest.Hash(state);
        var roster = engine.Deployment.Roster(state);
        var actual = roster is null ? null : new JsonObject
        {
            ["player"] = roster.Player,
            ["units"] = new JsonArray(roster.Units.Select(unit => unit["id"]!.DeepClone()).ToArray()),
            ["zone"] = new JsonArray(roster.Zone.Select(at => (JsonNode)Coordinate(at)).ToArray()),
        };
        Equal(fixture["roster"], actual, "roster");
        if (StateDigest.Hash(state) != before) throw new Exception("Roster query changed state");
    });
    foreach (var query in fixture["queries"]!.AsArray().OfType<JsonObject>())
    {
        var id = query["unit"]!.GetValue<int>();
        var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(item => item["id"]!.GetValue<int>() == id);
        var expectedRefusals = nativeQueries.OfType<JsonObject>().Single(query => query["unit"]!.GetValue<int>() == id)["refusals"]!.AsArray();
        if (expectedRefusals.Count != query["refusals"]!.AsArray().Count)
            throw new Exception("Native deployment refusal slots are missing or stale: " + name);
        Check($"{name} unit {id} spots", () =>
        {
            var before = StateDigest.Hash(state);
            var spots = new JsonArray(engine.Deployment.Spots(state, unit).Select(spot => (JsonNode)new JsonObject
            { ["at"] = Coordinate(spot.At), ["swaps"] = spot.Swaps?["id"]?.DeepClone() }).ToArray());
            Equal(query["spots"], spots, "spots");
            if (StateDigest.Hash(state) != before) throw new Exception("Spots query changed state");
        });
        foreach (var (refusal, index) in query["refusals"]!.AsArray().OfType<JsonObject>().Select((value, index) => (value, index)))
            Check($"{name} unit {id} refusal {refusal["at"]}", () =>
            {
                var actual = engine.Deployment.Refusal(state, unit, Position(refusal["at"]!));
                if ((refusal["reason"] is null) != (expectedRefusals[index] is null))
                    throw new Exception("Native deployment query changed refusal acceptance");
                Equal(expectedRefusals[index], JsonSerializer.SerializeToNode(actual), "semantic deployment query refusal");
            });
    }
    var production = StateDigest.Hash(state) == rawHash;
    Battle Open() => production ? engine.CreateBattle(level) : Isolated(state);
    if (native["attempts"]!.AsArray().Count != fixture["attempts"]!.AsArray().Count ||
        native["confirmations"]!.AsArray().Count != fixture["confirmations"]!.AsArray().Count)
        throw new Exception("Native deployment action slots are missing or stale: " + name);
    foreach (var (attempt, index) in fixture["attempts"]!.AsArray().OfType<JsonObject>().Select((value, index) => (value, index)))
        Check($"{name} {(production ? "engine" : "isolated")} action {attempt["action"]}", () => Outcome(Open(), attempt, native["attempts"]![index]));
    Check($"{name} sequential confirmation", () =>
    {
        var battle = Open();
        foreach (var (confirmation, index) in fixture["confirmations"]!.AsArray().OfType<JsonObject>().Select((value, index) => (value, index)))
            Outcome(battle, confirmation, native["confirmations"]![index]);
    });
    if (production) Check($"{name} persisted deployment resumes through battle start", () =>
    {
        var battle = engine.CreateBattle(level);
        var saved = engine.SaveBattle(battle.Snapshot(), "2026-01-01T00:00:00.000Z");
        var resumed = engine.LoadSavedBattle(JsonNode.Parse(saved.ToJsonString()));
        if (battle.StateHash != resumed.StateHash) throw new Exception("Deployment save round trip differs");
        foreach (var confirmation in fixture["confirmations"]!.AsArray().OfType<JsonObject>())
        {
            Outcome(battle, confirmation);
            Outcome(resumed, confirmation);
            // Exercise the next deployment player's index and the final retired aggregate too.
            resumed = engine.LoadSavedBattle(engine.SaveBattle(resumed.Snapshot(), "2026-01-01T00:00:00.000Z"));
            if (battle.StateHash != resumed.StateHash) throw new Exception("Mid-deployment resume differs");
        }
    });
}
foreach (var fixture in vectors["invalidLevels"]!.AsArray().OfType<JsonObject>())
    Check($"construction refusal {fixture["name"]}", () =>
    {
        try { BattleStateFactory.Create(stateRules, fixture["level"]!.AsObject()); throw new Exception("Expected construction refusal"); }
        catch (DomainInvariantException error)
        {
            if (error.Message != fixture["error"]!.GetValue<string>()) throw new Exception($"Unexpected refusal: {error.Message}");
        }
    });
var firstLevel = vectors["cases"]![0]!["level"]!.AsObject();
var validSave = engine.SaveBattle(engine.CreateState(firstLevel), "2026-01-01T00:00:00.000Z");
void RejectDeployment(string name, Action<JsonObject> edit)
{
    Check($"stored deployment refusal {name}", () =>
    {
        var damaged = (JsonObject)validSave.DeepClone();
        edit(damaged["state"]!.AsObject());
        var before = StateDigest.Hash(damaged);
        try { engine.LoadSavedBattle(damaged); throw new Exception("Expected damaged deployment refusal"); }
        catch (StoredDocumentException) { }
        if (StateDigest.Hash(damaged) != before) throw new Exception("Rejected load mutated its document");
    });
}
RejectDeployment("missing aggregate", state => state["deployment"] = null);
RejectDeployment("empty order", state => state["deployment"]!["order"] = new JsonArray());
RejectDeployment("duplicate order", state => state["deployment"]!["order"] = new JsonArray(1, 1));
RejectDeployment("unknown player", state => state["deployment"]!["order"]![0] = 99);
RejectDeployment("negative index", state => state["deployment"]!["currentIndex"] = -1);
RejectDeployment("fractional index", state => state["deployment"]!["currentIndex"] = 0.5);
RejectDeployment("exhausted index", state => state["deployment"]!["currentIndex"] = 2);
RejectDeployment("mismatched current player", state => state["currentPlayer"] = 2);
RejectDeployment("null assignment", state => state["deployment"]!["assignments"]![0] = null);
RejectDeployment("missing assignment field", state => state["deployment"]!["assignments"]![0]!.AsObject().Remove("unitIds"));
RejectDeployment("unknown assignment owner", state => state["deployment"]!["assignments"]![0]!["player"] = 99);
RejectDeployment("absent assigned zone", state => state["scenario"]!["zones"]!.AsObject().Remove("blue"));
RejectDeployment("offboard zone", state => state["scenario"]!["zones"]!["blue"]![0]!["x"] = 99);
RejectDeployment("fractional zone", state => state["scenario"]!["zones"]!["blue"]![0]!["y"] = 0.5);
RejectDeployment("unknown assignee", state => state["deployment"]!["assignments"]![0]!["unitIds"]!.AsArray().Add(999));
RejectDeployment("duplicate assignee", state => state["deployment"]!["assignments"]![0]!["unitIds"]!.AsArray().Add(1));
RejectDeployment("wrong owner assignee", state => state["deployment"]!["assignments"]![0]!["unitIds"]!.AsArray().Add(3));
RejectDeployment("ordered player without assignment", state => state["deployment"]!["assignments"]!.AsArray().RemoveAt(1));
Check("relocation save retains full unit state and resumes", () =>
{
    var fixture = vectors["cases"]!.AsArray().OfType<JsonObject>().Single(item => item["name"]!.GetValue<string>() == "initiative-handoff");
    var battle = engine.LoadSavedBattle(engine.SaveBattle(fixture["state"]!.AsObject(), "2026-01-01T00:00:00.000Z"));
    var move = fixture["attempts"]!.AsArray().OfType<JsonObject>().Single(item => item["action"]!["kind"]!.GetValue<string>() == "deployUnit" &&
        item["action"]!["unit"]!.GetValue<int>() == 1 && Position(item["action"]!["at"]!) == new Coord(1, 0));
    Outcome(battle, move);
    var reloaded = engine.LoadSavedBattle(engine.SaveBattle(battle.Snapshot(), "2026-01-01T00:00:00.000Z"));
    if (battle.StateHash != reloaded.StateHash) throw new Exception("Relocation state differs after reload");
    for (var side = 0; side < 2; side++)
    {
        var command = new JsonObject { ["kind"] = "finishDeployment" };
        var expected = battle.Dispatch(command);
        var actual = reloaded.Dispatch(command);
        if (expected.StateHash != actual.StateHash) throw new Exception("Resumed initiative handoff differs");
        Equal(new JsonArray(expected.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()),
            new JsonArray(actual.Events.Select(item => (JsonNode)item.DeepClone()).ToArray()), "resumed handoff events");
    }
});
Console.WriteLine($"Deployment checks: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
