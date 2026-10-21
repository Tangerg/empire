using Empire.Presentation.Feedback;
using Empire.Checks;
using Empire.Core;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;

if (args.Length != 2) throw new ArgumentException("Usage: CampFlowChecks <repository> <catalog.json>");
var gameText = FixtureText.Load(args[^1]);

var root = args.Length > 0 ? args[0] : ".";
JsonNode Read(string file) => EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data", file)))!;
var definition = Read("grayflag-campaign.json").AsObject();
var levels = Read("grayflag-campaign-levels.json").AsArray();
var packs = Read("content-packs.json").AsArray();
var engine = new GrayFlagEngine(packs);
var presentationText = FixtureBattleText.Session(root, engine, gameText);
GrayFlagCampaign Create() => new(definition, levels, engine, presentationText);
var assertions = 0;
void Require(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
void Same(JsonNode? actual, JsonNode? expected, string message)
{
    if (!EcmaJson.DeepEquals(actual, expected))
        File.WriteAllText("/tmp/empire-camp-flow-mismatch.json", EcmaJson.Stringify(new JsonObject { ["message"] = message, ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }));
    Require(EcmaJson.DeepEquals(actual, expected), message);
}
void SameSave(JsonObject actual, JsonObject expected, string message)
{
    // Save intentionally stamps the current time; timestamps are not campaign or battle state.
    JsonObject Facts(JsonObject source)
    {
        var facts = source.DeepClone().AsObject();
        facts["campaign"]!.AsObject().Remove("savedAt");
        (facts["battle"] as JsonObject)?.Remove("savedAt");
        return facts;
    }
    Same(Facts(actual), Facts(expected), message);
}
void Act(GrayFlagCampaign campaign)
{
    var battle = campaign.Battle!;
    var action = engine.Ai.ChooseAction(battle.Snapshot());
    if (battle.IsComputerTurn) Require(battle.AdvanceComputer(), "Computer action was not advanced");
    else Require(battle.Dispatch(EcmaJson.ToElement(action), battle.GetSnapshot().Revision).Accepted, "Native planned action was refused");
}
var campaign = Create();
while (campaign.Node()["type"]!.GetValue<string>() != "battle")
{
    if (campaign.Node()["type"]!.GetValue<string>() == "choice") campaign.Choose(campaign.Choices()[0]!["id"]!.GetValue<string>());
    else campaign.Advance();
}
var stagingSave = campaign.Save();
var beforeBeginning = campaign.Snapshot();
campaign.BeginBattle();
var pendingRuntime = campaign.Snapshot();
var request = campaign.RequestSnapshot()!.DeepClone();
var initial = campaign.Battle!.Snapshot();
Require(!campaign.AtCamp, "Initial battle must not be marked as exited");
Same(campaign.Snapshot()["flags"], beforeBeginning["flags"], "Beginning battle changed prior decision flags");
for (var step = 0; step < 8; step++) Act(campaign);
Require(!EcmaJson.DeepEquals(campaign.Battle.Snapshot(), initial), "Test did not advance the initial battle");
campaign.RestartBattle();
Same(campaign.Battle.Snapshot(), initial, "Retry must restore initial canonical battle");
Same(campaign.Snapshot(), pendingRuntime, "Retry must not repeat campaign effects or choices");
Same(campaign.RequestSnapshot(), request, "Retry must retain the exact pending request");
Require(campaign.Battle.Events.Count == 0 && campaign.Battle.ActionLog.Count == 0, "Retry must clear battle history");
for (var step = 0; step < 8; step++) Act(campaign);
campaign.ExitBattle();
Require(campaign.AtCamp, "Exit must show camp");
Same(campaign.Snapshot(), pendingRuntime, "Exit must not commit battle outcome or change prior decisions");
Same(campaign.RequestSnapshot(), request, "Exit must retain pending request identity and context");
Same(campaign.Battle!.Snapshot(), initial, "Exited battle must be staged from its opening");
Require(campaign.Battle.Events.Count == 0, "Exit must not retain played event history for reentry");
var campSave = campaign.Save();
var restored = Create(); restored.Load(campSave);
Require(restored.AtCamp, "Camp save/load must preserve the camp screen");
SameSave(restored.Save(), campSave, "Camp save must round-trip all canonical facts");
restored.BeginBattle();
Require(!restored.AtCamp, "Reentry must leave camp");
Same(restored.Battle!.Snapshot(), initial, "Reentry must restart the same authored request from opening");
Same(restored.Snapshot(), pendingRuntime, "Reentry must not begin a second campaign battle");
Same(restored.RequestSnapshot(), request, "Reentry must preserve seeded choice context");
Require(restored.Battle.GetSnapshot().Transition is null, "Reentry must not replay the previous battle receipt");
var snapshotBeforeInvalid = restored.Save();
var invalidDocuments = new List<JsonObject>();
foreach (var bad in new JsonNode[] { JsonValue.Create("true")!, JsonValue.Create(1)!, new JsonObject(), new JsonArray() })
{
    var malformed = campSave.DeepClone().AsObject(); malformed["mode"] = bad; invalidDocuments.Add(malformed);
}
var noRequest = stagingSave.DeepClone().AsObject(); noRequest["mode"] = "camp"; invalidDocuments.Add(noRequest);
var wrongIdentity = campSave.DeepClone().AsObject(); wrongIdentity["campaign"]!["state"]!["pendingBattle"]!["requestId"] = "wrong"; invalidDocuments.Add(wrongIdentity);
foreach (var invalid in invalidDocuments)
{
    var rejected = false;
    try { restored.Load(invalid); }
    catch (StoredDocumentException) { rejected = true; }
    Require(rejected, "Invalid camp save was accepted");
    SameSave(restored.Save(), snapshotBeforeInvalid, "Invalid camp load changed the current battle/campaign/AtCamp state");
}
var actions = 0;
while (restored.Battle.Snapshot()["phase"]!.GetValue<string>() != "over")
{
    if (++actions > 5000) throw new Exception("Real battle failed to terminate within diagnostic guard; not a pass");
    if (restored.Battle.IsComputerTurn) Require(restored.Battle.AdvanceComputer(), "Enemy AI did not advance");
    else Require(restored.Battle.Dispatch(EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" }),
        restored.Battle.GetSnapshot().Revision).Accepted, "Passive human fixture must use legal end-turn orders");
}
var outcome = restored.BattleSummary().Outcome;
Require(outcome == "defeat", $"Defeat retry fixture requires a real loss, not {outcome}");
var terminalSave = restored.Save();
restored.RestartBattle();
Same(restored.Battle.Snapshot(), initial, "Defeat retry did not restore the opening");
Same(restored.Snapshot(), pendingRuntime, "Defeat retry must not enter campaign-failed");
Require(!restored.AtCamp, "Defeat retry must return directly to battle");
restored.Load(terminalSave); restored.ExitBattle();
Require(restored.AtCamp, "Defeat exit must return to camp");
Same(restored.Snapshot(), pendingRuntime, "Defeat exit must not complete a loss or change prior choices");
Same(restored.RequestSnapshot(), request, "Defeat exit must retain original request");
var lossCampSave = restored.Save();
var lossReloaded = Create(); lossReloaded.Load(lossCampSave); lossReloaded.BeginBattle();
Same(lossReloaded.Battle!.Snapshot(), initial, "Reloaded defeat camp must restart original battle");
Same(lossReloaded.Snapshot(), pendingRuntime, "Reloaded defeat camp reentry changed campaign progress");
Require(lossReloaded.Snapshot()["battleHistory"]!.AsArray().Count == 0, "Retry/exit must not record defeat as a completed battle");
Console.WriteLine($"PASS {assertions} camp-flow assertions: actual defeat after {actions} actions; retry/exit/retained choices; camp save/load/reentry; six invalid loads atomic");
