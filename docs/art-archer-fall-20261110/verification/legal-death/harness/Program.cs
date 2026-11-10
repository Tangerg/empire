using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation.Tactical;

if (args.Length != 2)
    throw new ArgumentException("Usage: Probe <repository-root> <output-directory>");
var repository = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
var root = Path.Combine(repository, "godot");
var evidence = Path.Combine(repository, "docs/art-archer-shot-20261110/verification/legal-shot/right");
Require(output != repository && !output.StartsWith(repository + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Output must be outside the repository.");
Require(!File.Exists(Path.Combine(output, "verification-result.json")), "Refusing to overwrite an existing run.");
var engine = new BattleEngine(Read(Path.Combine(root, "data/content-packs.json")).AsArray());
var definition = Read(Path.Combine(root, "data/grayflag-campaign.json")).AsObject();
var levels = Read(Path.Combine(root, "data/grayflag-campaign-levels.json")).AsArray();
var lookup = Read(Path.Combine(output, "translation-lookup.json")).AsObject();
var messages = new GameText(key => lookup[key]?.GetValue<string>(), CultureInfo.GetCultureInfo("zh-CN"));
var identities = Read(Path.Combine(root, "data/grayflag-story.json"))["identityNameKeys"]!.AsObject().ToDictionary(entry => entry.Key, entry => entry.Value!.GetValue<string>());
var presentation = SessionPresentationText.Compose(new(engine, messages, identities), levels,
    File.ReadAllText(Path.Combine(root, "data/grayflag-battle-text.json")), File.ReadAllText(Path.Combine(root, "data/grayflag-content-text.json")));
var rules = new CampaignApplicationRules(engine, presentation);
var campaign = new GrayFlagCampaign(rules, definition, levels);
var opening = new JsonArray();
while (campaign.Node()["type"]!.GetValue<string>() == "story")
{
    opening.Add(new JsonObject { ["kind"] = "advance", ["node"] = campaign.Node()["id"]!.DeepClone() });
    campaign.Advance();
}
opening.Add(new JsonObject { ["kind"] = "choose", ["choice"] = "steady-advance" });
campaign.Choose("steady-advance");
campaign.BeginBattle();
Require(EcmaJson.DeepEquals(opening, Read(Path.Combine(evidence, "opening-actions.json"))), "Opening actions differ.");
Require(EcmaJson.DeepEquals(WithoutTimestamps(campaign.Save()), WithoutTimestamps(Read(Path.Combine(evidence, "source-campaign-save.json")).AsObject())), "Original campaign save differs.");
Write("source-campaign-save.json", campaign.Save());
Write("opening-actions.json", opening);
var expectedTrace = Read(Path.Combine(repository, "docs/art-archer-hit-20261110/verification/legal-hit/southern-bows/ui-settled-derivation-actions.json")).AsArray();
var history = new JsonArray();
JsonObject? triggerBefore = null;
for (var step = 0; step < 185; step++)
{
    var session = campaign.Battle!;
    if (session.IsConcluded) break;
    var beforeSave = campaign.Save();
    if (step == 166) triggerBefore = beforeSave;
    var computer = session.IsComputerTurn;
    var count = session.Events.Count;
    if (computer) Require(session.AdvanceComputerStep() is { Committed: true }, "AI refused.");
    else {
        var action = step < expectedTrace.Count ? expectedTrace[step]!["action"]! : new JsonObject { ["kind"] = "endTurn" };
        Require(session.Dispatch(EcmaJson.ToElement(action), session.Revision).Accepted, "Human order refused.");
    }
    var receipt = CheckReceipt(campaign, beforeSave, count, step, computer);
    if (step < expectedTrace.Count) Require(EcmaJson.DeepEquals(receipt, expectedTrace[step]), "Prefix changed " + step);
    history.Add(receipt);
    if (step >= 151) Console.WriteLine(EcmaJson.Stringify(receipt));
    if (step < 151) continue;
    foreach (var hit in receipt["events"]!.AsArray().OfType<JsonObject>().Where(e => e["killed"]?.GetValue<bool>() == true)) {
        var target = TacticalQueries.RequireUnit(beforeSave["battle"]!["state"]!.AsObject(), hit["defender"]!.GetValue<int>());
        if (target["type"]!.GetValue<string>() != "c01.archer") continue;
        Write("before.json", beforeSave); Write("after.json", campaign.Save()); Write("receipt.json", receipt); Write("derivation-actions.json", history);
        Require(step == 167 && computer && target["id"]!.GetValue<int>() == 3 && target["hp"]!.GetValue<double>() == 4, "Unexpected death witness.");
        Require(receipt["beforeHash"]!.GetValue<string>() == "489512a2" && receipt["afterHash"]!.GetValue<string>() == "329dacf4", "Death hashes changed.");
        var beforeState = beforeSave["battle"]!["state"]!.AsObject();
        Require(engine.Space.IsUnitVisible(beforeState, 1, target) && engine.Space.IsUnitVisible(beforeState, 1, TacticalQueries.RequireUnit(beforeState, 18)), "Participants must be visible.");
        Write("ui-trigger-before.json", triggerBefore!);
        var trigger = new GrayFlagCampaign(rules, definition, levels); trigger.Load(triggerBefore!);
        var controller = new TacticalSelectionController(trigger.Battle!.GetTacticalQueries(0));
        var request = controller.Key(controller.Stamp, "e").Request;
        Require(request?.Action is { } uiAction && EcmaJson.DeepEquals(EcmaJson.Parse(uiAction.GetRawText()), history[166]!["action"]), "Human E differs.");
        var immediateAfter = campaign.Save();
        var extra = new JsonArray();
        while (campaign.Battle!.IsComputerTurn && !campaign.Battle.IsConcluded) {
            var extraBefore = campaign.Save(); var prior = campaign.Battle.Events.Count;
            Require(campaign.Battle.AdvanceComputerStep() is { Committed: true }, "Settling AI refused.");
            var entry = CheckReceipt(campaign, extraBefore, prior, history.Count, true); history.Add(entry); extra.Add(entry.DeepClone());
        }
        Require(extra.Count == 1 && extra[0]!["action"]!["kind"]!.GetValue<string>() == "endTurn", "Expected one normal AI endTurn.");
        var settled = campaign.Save(); Write("ui-settled.json", settled); Write("ui-settled-extra-actions.json", extra); Write("ui-settled-derivation-actions.json", history);
        foreach (var document in new[] { beforeSave, immediateAfter, settled, triggerBefore! }) {
            var reload = new GrayFlagCampaign(rules, definition, levels); reload.Load(document);
            Require(EcmaJson.DeepEquals(WithoutTimestamps(reload.Save()), WithoutTimestamps(document)), "Official roundtrip changed.");
        }
        var replay = new GrayFlagCampaign(rules, definition, levels); replay.Load(beforeSave);
        var replayCount = 0;
        while (replay.Battle!.IsComputerTurn && !replay.Battle.IsConcluded) {
            var replayBefore = replay.Save(); var prior = replay.Battle.Events.Count;
            Require(replay.Battle.AdvanceComputerStep() is { Committed: true }, "Reloaded AI refused.");
            var entry = CheckReceipt(replay, replayBefore, prior, 167 + replayCount, true);
            Require(EcmaJson.DeepEquals(entry, history[167 + replayCount]), "Reloaded receipt changed."); replayCount++;
        }
        Require(replayCount == 2 && EcmaJson.DeepEquals(WithoutTimestamps(replay.Save()), WithoutTimestamps(settled)), "Full native settled replay differs.");
        Write("verification-result.json", new JsonObject {
            ["passed"] = true, ["preservedPrefixActions"] = 151, ["deathStep"] = 167,
            ["targetBefore"] = target.DeepClone(), ["sourceBefore"] = TacticalQueries.RequireUnit(beforeState, 18).DeepClone(),
            ["beforeHash"] = receipt["beforeHash"]!.DeepClone(), ["afterHash"] = receipt["afterHash"]!.DeepClone(),
            ["settledHash"] = StateDigest.Hash(campaign.Battle!.Snapshot()), ["eventsSha256"] = EcmaJson.Sha256(receipt["events"]),
            ["nativeReplayAiActions"] = replayCount, ["completeOfficialSaveEquality"] = true,
            ["independentCoreStateAndEvents"] = true, ["productionUiEndTurnEquality"] = true,
            ["bothVisibleToPlayer"] = true, ["noStatePatching"] = true
        });
        Console.WriteLine("PASS open-ground east-facing archer death, exact prefix and two native AI actions verified."); return;
    }
}
throw new InvalidOperationException("No death found.");
JsonObject CheckReceipt(GrayFlagCampaign current, JsonObject beforeSave, int priorEventCount, int step, bool computer)
{
    var session = current.Battle!;
    var action = session.ActionLog.Last();
    var core = engine.LoadSavedBattle(beforeSave["battle"]!);
    var receipt = core.Dispatch(action);
    var events = new JsonArray(session.Events.Skip(priorEventCount).Select(e => (JsonNode)e.DeepClone()).ToArray());
    Require(EcmaJson.DeepEquals(session.Snapshot(), core.Snapshot()), "Independent core state mismatch.");
    Require(EcmaJson.DeepEquals(events, new JsonArray(receipt.Events.Select(e => (JsonNode)e.DeepClone()).ToArray())), "Independent core receipt mismatch.");
    return new JsonObject { ["step"] = step, ["computer"] = computer, ["action"] = action.DeepClone(), ["beforeHash"] = StateDigest.Hash(beforeSave["battle"]!["state"]!.AsObject()), ["afterHash"] = receipt.StateHash, ["events"] = events };
}

void Write(string filename, JsonNode value) => SaveFile.WriteDocument(Path.Combine(output, filename), value);
static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
static JsonObject WithoutTimestamps(JsonObject value)
{
    var result = (JsonObject)value.DeepClone();
    result["campaign"]!.AsObject().Remove("savedAt");
    result["battle"]!.AsObject().Remove("savedAt");
    return result;
}
static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
