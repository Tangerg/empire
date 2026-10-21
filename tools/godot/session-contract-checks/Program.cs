using Empire.Localization;
using System.Globalization;
using Empire.Presentation.Feedback;
using Empire.Checks;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Transactions;

if (args.Length != 3) throw new ArgumentException("Usage: SessionContractChecks <oracle.json> <repository> <catalog.json>");
var gameText = FixtureText.Load(args[^1]);

// Historical source-v1 custom levels and runtime states still require explicit semantic-schema migration.
var oracle = JsonNode.Parse(File.ReadAllText(args[0]))!.AsObject();
var root = args[1];
var packs = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
var engine = new GrayFlagEngine(packs);
var presentation = FixtureBattleText.Session(root, engine, gameText);
var level = oracle["level"]!.AsObject();
var session = new GrayFlagSession(engine, presentation, level);
var count = 0;
foreach (var item in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var operation = item["operation"]!.GetValue<string>();
    var beforeRevision = session.GetSnapshot().Revision;
    var oldTransition = session.GetSnapshot().Transition;
    var oldActions = session.ActionLog.Count;
    var accepted = false;
    switch (operation)
    {
        case "dispatch": accepted = session.Dispatch(EcmaJson.ToElement(item["action"]!), beforeRevision).Accepted; break;
        case "undo": accepted = session.Undo(beforeRevision).Accepted; break;
        case "load": session = GrayFlagSession.FromSave(engine, presentation, session.Save()); accepted = true; break;
        case "restart": session = new GrayFlagSession(engine, presentation, level); accepted = true; break;
    }
    Require(accepted == item["accepted"]!.GetValue<bool>(), $"{count} {operation}: acceptance");
    Require(session.CanUndo == item["canUndo"]!.GetValue<bool>(), $"{count}: canUndo");
    Require(StateDigest.Hash(session.Snapshot()) == item["hash"]!.GetValue<string>(), $"{count}: digest");
    Require(EcmaJson.DeepEquals(new JsonArray(session.Events.Select(e => (JsonNode)e).ToArray()), item["events"]), $"{count}: events");
    if (operation is "dispatch" or "undo")
    {
        Require(session.GetSnapshot().Revision == beforeRevision + (accepted ? 1 : 0), $"{count}: monotonic revision");
        if (!accepted) Require(ReferenceEquals(oldTransition, session.GetSnapshot().Transition) && session.ActionLog.Count == oldActions, "refusal changed history");
        if (accepted && operation == "undo") Require(session.GetSnapshot().Transition is null && session.ActionLog.Count == oldActions - 1, "undo did not invalidate presentation/action");
        if (accepted && operation == "dispatch") Require(session.LastActionHandsOffTurn == item["handsOff"]!.GetValue<bool>(), "handoff receipt");
    }
    count++;
}

foreach (var metadata in oracle["metadata"]!.AsArray().OfType<JsonObject>())
    Require(engine.HandsOffTurn(new JsonObject { ["kind"] = metadata["kind"]!.DeepClone() }) == metadata["handsOff"]!.GetValue<bool>(), "registered source metadata mismatch");
Require(!engine.HandsOffTurn(new JsonObject { ["kind"] = "unknown" }), "unknown kind hands off");
foreach (var fixture in oracle["extraCases"]!.AsArray().OfType<JsonObject>())
{
    var extra = new GrayFlagSession(engine, presentation, fixture["level"]!.AsObject());
    var steps = fixture["steps"] is JsonArray sequence ? sequence.OfType<JsonObject>() : new[] { fixture };
    foreach (var step in steps)
    {
        Require(extra.Dispatch(EcmaJson.ToElement(step["action"]!), extra.GetSnapshot().Revision).Accepted, "extra fixture refused");
        Require(StateDigest.Hash(extra.Snapshot()) == step["hash"]!.GetValue<string>(), "extra fixture digest");
        Require(EcmaJson.DeepEquals(new JsonArray(extra.Events.Select(e => (JsonNode)e).ToArray()), step["events"]), "extra fixture events");
        Require(extra.CanUndo == step["canUndo"]!.GetValue<bool>(), "extra fixture canUndo");
        Require(extra.LastActionHandsOffTurn == step["handsOff"]!.GetValue<bool>(), "extra fixture metadata");
    }
    Require(!extra.Undo(extra.GetSnapshot().Revision).Accepted, "terminal/handoff undo permitted");
}

// Native persisted semantic history intentionally survives resume, unlike standalone TS load.
var initial = new GrayFlagSession(engine, presentation, level);
var marker = new JsonObject { ["type"] = "priorCampaignEvent" };
var resumed = GrayFlagSession.FromSave(engine, presentation, initial.Save(), new JsonArray(marker));
var actor = resumed.Snapshot()["units"]!.AsArray().OfType<JsonObject>().First(u => u["owner"]!.GetValue<int>() == resumed.Snapshot()["currentPlayer"]!.GetValue<int>());
var face = new JsonObject { ["kind"] = "face", ["unit"] = actor["id"]!.DeepClone(), ["facing"] = "north" };
Require(!resumed.CanUndo && resumed.LastActionHandsOffTurn is null, "resume retained undo metadata");
Require(resumed.Dispatch(EcmaJson.ToElement(face), 0).Accepted, "resumed face rejected");
Require(!resumed.Undo(0).Accepted && resumed.GetSnapshot().Revision == 1 && resumed.CanUndo, "stale undo succeeded");
Require(resumed.Undo(1).Accepted && resumed.Events.Count == 1 && EcmaJson.DeepEquals(resumed.Events[0], marker), "undo erased preloaded campaign events");
Require(!resumed.Dispatch(EcmaJson.ToElement(face), 1).Accepted, "stale callback accepted after undo");
Require(resumed.Dispatch(EcmaJson.ToElement(face), 2).Accepted, "fresh callback refused");

var computerLevel = (JsonObject)level.DeepClone();
computerLevel["players"]![0]!["controller"] = "ai";
var computerSession = new GrayFlagSession(engine, presentation, computerLevel);
Require(computerSession.AdvanceComputer(), "computer fixture did not advance");
Require(!computerSession.CanUndo && !computerSession.Undo(computerSession.GetSnapshot().Revision).Accepted, "computer-controlled undo permitted");

// All nested battle fields are restored by the single writer, including unknown rule-owned data.
var registry = new BattleActionRegistry();
registry.Register("change", (state, action, emit) => {
    state["rng"]!["counter"] = 100;
    state["accounts"]!["gold"] = 0;
    state["casts"]!.AsArray().Clear();
    state["transport"]!["carrier"] = 9;
    state["scenario"]!["flag"] = true;
    emit(new JsonObject { ["type"] = "changed" });
});
registry.Register("endTurn", (_, _, _) => { }); // Name must not dictate handoff.
registry.Register("retreat", (_, _, _) => { }, handsOffTurn: true);
registry.Register("fail", (state, _, emit) => { state["rng"]!["counter"] = 999; emit(new JsonObject { ["type"] = "partial" }); throw new IllegalActionException(new ActionRefusal("test.refused")); });
registry.Register("broken", (state, _, emit) => { state["rng"]!["counter"] = 999; emit(new JsonObject { ["type"] = "partial" }); throw new DomainInvariantException("broken rule"); });
var state0 = JsonNode.Parse("""{"rng":{"counter":7},"accounts":{"gold":50},"casts":[{"caster":1}],"transport":{"carrier":null},"scenario":{"flag":false}}""")!.AsObject();
var battle = new Battle(state0, registry);
JsonObject Action(string kind) => new() { ["kind"] = kind };
var originalHash = battle.StateHash;
var first = battle.Dispatch(Action("change"));
var changedHash = battle.StateHash;
first.Before["rng"]!["counter"] = -1;
first.Events[0]["type"] = "forged";
Require(first.Events[0]["type"]!.GetValue<string>() == "changed", "mutable receipt event alias");
Refuses(() => battle.Dispatch(Action("fail")), typeof(IllegalActionException));
Require(battle.StateHash == changedHash, "failed dispatch leaked draft");
Refuses(() => battle.Dispatch(Action("broken")), typeof(DomainInvariantException));
Refuses(() => battle.Dispatch(Action("unknown")), typeof(IllegalActionException));
Require(battle.StateHash == changedHash, "invariant or unknown-action failure leaked draft");
var second = battle.Dispatch(Action("endTurn"));
Require(!second.HandsOffTurn, "hardcoded endTurn handoff mutant survived");
Refuses(() => battle.Restore(first), typeof(DomainInvariantException));
Require(battle.StateHash == changedHash, "out-of-order restore mutated state");
var other = new Battle(state0, registry);
Refuses(() => other.Restore(second), typeof(DomainInvariantException));
Require(other.StateHash == originalHash, "cross-battle restore mutated state");
battle.Restore(second);
Refuses(() => battle.Restore(second), typeof(DomainInvariantException));
battle.Restore(first);
Require(battle.StateHash == originalHash, "trusted snapshot did not restore exact nested state");
var branch = battle.Dispatch(Action("change"));
Refuses(() => battle.Restore(first), typeof(DomainInvariantException));
var retreat = battle.Dispatch(Action("retreat"));
Require(retreat.HandsOffTurn && !oracle["customHandoffCanUndo"]!.GetValue<bool>(), "renamed handoff negative control failed");
Refuses(() => battle.Restore(branch), typeof(DomainInvariantException));
Refuses(() => battle.Restore(retreat), typeof(DomainInvariantException));

// Frozen handler metadata and replacement semantics cannot be changed beneath a live battle.
registry.Replace("retreat", (_, _, _) => { });
Require(battle.Dispatch(Action("retreat")).HandsOffTurn, "registry mutation changed live battle");
var replacementBattle = new Battle(state0, registry);
Require(!replacementBattle.Dispatch(Action("retreat")).HandsOffTurn, "replacement retained stale metadata");

Battle? reentrant = null;
BattleReceipt? checkpoint = null;
var nested = new BattleActionRegistry();
nested.Register("safe", (_, _, _) => { });
nested.Register("dispatch", (state, _, _) => { state["mutated"] = true; reentrant!.Dispatch(Action("safe")); });
nested.Register("restore", (state, _, _) => { state["mutated"] = true; reentrant!.Restore(checkpoint!); });
reentrant = new Battle(state0, nested);
checkpoint = reentrant.Dispatch(Action("safe"));
foreach (var kind in new[] { "dispatch", "restore" }) {
    Refuses(() => reentrant.Dispatch(Action(kind)), typeof(DomainInvariantException));
    Require(reentrant.StateHash == originalHash, "reentrant operation leaked draft");
}
reentrant.Restore(checkpoint);
Require(reentrant.StateHash == originalHash, "failed reentrancy invalidated prior checkpoint");

// Refusal facts are immutable; a language change cannot alter the rejected transaction.
var sourceDetails = new JsonObject { ["kind"] = "plugin.command", ["nested"] = new JsonArray(1) };
var immutableRefusal = new ActionRefusal("action.unsupported", sourceDetails);
sourceDetails["kind"] = "mutated";
sourceDetails["nested"]![0] = 2;
Require(immutableRefusal.Details.GetProperty("kind").GetString() == "plugin.command" &&
    immutableRefusal.Details.GetProperty("nested")[0].GetInt32() == 1, "refusal details escaped owner isolation");
var alternateText = new GameText(key => key switch
{
    "error.action.reason_separator" => "; ",
    "error.action.computer_refused" => "Computer order refused: {0}",
    "test.action.unsupported" => "Order rejected ({0}).",
    _ => null,
}, CultureInfo.InvariantCulture);
var alternateRefusals = new ActionRefusalText(alternateText)
    .Register(new("action.unsupported", "test.action.unsupported", (_, details) => [details.GetProperty("kind").GetString()]))
    .Seal();
// Hold other presentation fixed while changing only the injected refusal wording.
var localized = new GrayFlagSession(engine, presentation, level);
var reworded = new GrayFlagSession(engine, presentation with { Refusals = alternateRefusals }, level);
var unknownAction = EcmaJson.ToElement(new JsonObject { ["kind"] = "plugin.command" });
var beforeRefusal = StateDigest.Hash(localized.Snapshot());
var originalResult = localized.Dispatch(unknownAction, 0);
var translatedResult = reworded.Dispatch(unknownAction, 0);
Require(!originalResult.Accepted && !translatedResult.Accepted && originalResult.Message != translatedResult.Message,
    "session refusal did not use explicitly injected presentation");
Require(translatedResult.Message == "Order rejected (plugin.command)." &&
    originalResult.Message == gameText.Format("error.action.unsupported", "plugin.command"), "wrong semantic refusal arguments");
Require(StateDigest.Hash(localized.Snapshot()) == beforeRefusal && StateDigest.Hash(reworded.Snapshot()) == beforeRefusal &&
    localized.Events.Count == 0 && reworded.Events.Count == 0 && localized.ActionLog.Count == 0 && reworded.ActionLog.Count == 0 &&
    localized.GetSnapshot().Revision == 0 && reworded.GetSnapshot().Revision == 0, "wording changed rejected battle facts");
var extended = alternateRefusals.Clone().Register(new("plugin.refused", "test.action.unsupported", (_, _) => ["extension"])).Seal();
Require(extended.Describe(new ActionRefusal("plugin.refused")) == "Order rejected (extension)." && !alternateRefusals.Has("plugin.refused"),
    "refusal extension registry is closed or clone aliases the owner");
Refuses(() => alternateRefusals.Describe(new ActionRefusal("plugin.unregistered")), typeof(LocalizationException));
var diagnostic = new IllegalActionException(immutableRefusal);
Require(ReferenceEquals(diagnostic.Refusal, immutableRefusal) && diagnostic.Message == "Action refused: action.unsupported",
    "exception flattened domain refusal into player prose");

Console.WriteLine($"PASS {count} live source session operations; detached trusted restore, lineage, history, stale revisions, handoff mutants, rollback and reentrancy");
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Refuses(Action action, Type type)
{
    try { action(); }
    catch (Exception error) when (error.GetType() == type) { return; }
    throw new Exception($"Expected refusal {type.Name}");
}
