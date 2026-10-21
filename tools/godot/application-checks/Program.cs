using Empire.Presentation.Feedback;
using Empire.Checks;
using Empire.Core;
using System.Text.Json.Nodes;
using System.Text.Json;
using Empire.Application;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Campaign;

if (args.Length != 2) throw new ArgumentException("Usage: ApplicationChecks <repository> <catalog.json>");
var gameText = FixtureText.Load(args[^1]);
var root = args.Length > 0 ? args[0] : ".";
JsonNode Read(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, "godot/data", file)))!;
var packs = Read("content-packs.json").AsArray();
var engine = new BattleEngine(packs);
var presentationText = FixtureBattleText.Session(root, engine, gameText);
GrayFlagCampaign Create() => new(new(engine, presentationText), Read("grayflag-campaign.json").AsObject(), Read("grayflag-campaign-levels.json").AsArray());
CheckBridgeTruce();
var campaign = Create();
while (campaign.Node()["type"]!.GetValue<string>() != "battle")
{
    if (campaign.Node()["type"]!.GetValue<string>() == "choice")
        campaign.Choose(campaign.Choices()[0]!["id"]!.GetValue<string>());
    else
        campaign.Advance();
}
campaign.BeginBattle();
var initial = StateDigest.Hash(campaign.Battle!.Snapshot());
if (campaign.Battle.GetSnapshot().Transition is not null) throw new Exception("Initial session replays history");
Act(campaign);
campaign.RestartBattle();
if (StateDigest.Hash(campaign.Battle.Snapshot()) != initial) throw new Exception("Restart did not restore initial battle");
for (var step = 0; step < 15; step++) Act(campaign);
var saved = campaign.Save();
var restored = Create();
restored.Load(saved);
if (restored.Battle!.GetSnapshot().Transition is not null) throw new Exception("Loaded session replays stored history");
if (StateDigest.Hash(restored.Battle!.Snapshot()) != StateDigest.Hash(campaign.Battle!.Snapshot())) throw new Exception("Battle save roundtrip mismatch");
var actions = 0;
while (campaign.Battle.Snapshot()["phase"]!.GetValue<string>() != "over")
{
    if (++actions > 5000)
        throw new Exception("Battle did not terminate within diagnostic guard; not a pass");
    Act(campaign);
    Act(restored);
    if (StateDigest.Hash(restored.Battle!.Snapshot()) != StateDigest.Hash(campaign.Battle.Snapshot()))
        throw new Exception("Restored action divergence");
}
var summary = campaign.BattleSummary();
var restoredSummary = restored.BattleSummary();
if (summary.Outcome != restoredSummary.Outcome || summary.Summary.Outcome != restoredSummary.Summary.Outcome ||
   summary.Summary.StrikeCount != restoredSummary.Summary.StrikeCount || !summary.Summary.Signals.SequenceEqual(restoredSummary.Summary.Signals) ||
   !summary.Summary.Fallen.SequenceEqual(restoredSummary.Summary.Fallen))
    throw new Exception("Postbattle summary changed after save/load");
campaign.FinishBattle();
restored.FinishBattle();
if (StateDigest.Hash(campaign.Snapshot()) != StateDigest.Hash(restored.Snapshot())) throw new Exception("Campaign result divergence after save");
var before = StateDigest.Hash(restored.Snapshot());
var invalid = saved.DeepClone().AsObject();
invalid["campaign"]!["state"]!["pendingBattle"]!["requestId"] = "wrong";
try { restored.Load(invalid); throw new Exception("Invalid save accepted"); } catch (StoredDocumentException) { }
if (StateDigest.Hash(restored.Snapshot()) != before) throw new Exception("Invalid load mutated campaign");
foreach (var badType in new JsonNode?[] { null, JsonValue.Create(2), JsonValue.Create(true), JsonValue.Create("") })
{
    var malformed = saved.DeepClone().AsObject();
    malformed["events"] = new JsonArray(new JsonObject { ["type"] = badType?.DeepClone() });
    try
    {
        restored.Load(malformed);
        throw new Exception("Malformed event type accepted");
    }
    catch (StoredDocumentException) { }
    if (StateDigest.Hash(restored.Snapshot()) != before)
        throw new Exception("Malformed event load mutated campaign");
}
Console.WriteLine($"PASS application story/choice -> battle -> midbattle save/load -> {actions} matching actions -> campaign result; invalid load atomic");
void CheckBridgeTruce()
{
    var level = Read("grayflag-campaign-levels.json").AsArray().OfType<JsonObject>().Single(candidate => candidate["id"]!.GetValue<string>() == "c01-02");
    var request = new JsonObject { ["levelId"] = "c01-02", ["level"] = level.DeepClone() };
    var original = StateDigest.Hash(request);
    foreach (var promised in new[] { true, false })
    {
        var context = new JsonObject
        {
            ["resources"] = new JsonObject { ["supplies"] = 0, ["treasury"] = 0 },
            ["flags"] = promised ? new JsonArray() : new JsonArray("bridge_no_promise")
        };
        var prepared = GrayFlagCampaignPolicy.ApplyBattleContext(request, context);
        if (StateDigest.Hash(request) != original)
            throw new Exception("Bridge choice changed the authored battle request");
        var battle = engine.CreateBattle(prepared["level"]!.AsObject(), 17);
        var signals = 0;
        for (var step = 0; step < 8; step++)
        {
            var state = battle.Snapshot();
            if (state["turn"]!.GetValue<int>() != step / 2 + 1 || state["currentPlayer"]!.GetValue<int>() != step % 2 + 1)
                throw new Exception("Bridge truce check lost the authored two-side round order");
            var truceActive = state["scenario"]!["engagementRules"]!.AsArray().Any(rule => rule!["id"]!.GetValue<string>() == "bridge-truce");
            if (truceActive != (promised && step < 6))
                throw new Exception("Bridge truce did not cover exactly the first three complete rounds");
            var receipt = battle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
            signals += receipt.Events.Count(item => item["type"]!.GetValue<string>() == "scenarioSignal" && item["signal"]!.GetValue<string>() == "accepted_bridge_truce");
            if (signals != (promised && step >= 5 ? 1 : 0))
                throw new Exception("Bridge truce signal occurred early, late, repeatedly, or without a promise");
        }
    }
    Console.WriteLine("PASS bridge promise covers three full rounds and emits once; no-promise emits no truce signal");
}

void Act(GrayFlagCampaign value)
{
    var battle = value.Battle!;
    var before = battle.Snapshot();
    var previous = battle.GetSnapshot();
    var computer = battle.IsComputerTurn;
    var action = engine.Ai.ChooseAction(before);
    if (computer)
        battle.AdvanceComputer();
    else
    {
        var result = battle.Dispatch(EcmaJson.ToElement(action), previous.Revision);
        if (!result.Accepted)
            throw new Exception(result.Message);
    }
    var after = battle.GetSnapshot();
    var transition = after.Transition ?? throw new Exception("Accepted action has no presentation receipt");
    if (transition.EventDescriptions.Count != transition.Events.Count)
        throw new Exception("Event descriptions lost receipt alignment");
    if (transition.Revision != after.Revision || after.Revision != previous.Revision + 1 || transition.IsComputerAction != computer)
        throw new Exception("Presentation revision or actor attribution differs");
    if (!EcmaJson.DeepEquals(EcmaJson.Parse(transition.Before.GetRawText()), before) ||
       !EcmaJson.DeepEquals(EcmaJson.Parse(transition.After.GetRawText()), battle.Snapshot()) ||
       !EcmaJson.DeepEquals(EcmaJson.Parse(transition.Action.GetRawText()), action))
        throw new Exception("Presentation receipt differs from committed facts");
    var frozen = transition.Before.GetRawText();
    before["turn"] = -1;
    if (transition.Before.GetRawText() != frozen)
        throw new Exception("Presentation receipt aliases mutable input");
    var rejected = battle.Dispatch(EcmaJson.ToElement(action), previous.Revision);
    if (rejected.Accepted || !ReferenceEquals(battle.GetSnapshot().Transition, transition))
        throw new Exception("Rejected stale action replaced presentation history");
}
