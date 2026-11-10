using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Localization;
using Empire.Presentation.Tactical;

if (args.Length != 3)
    throw new ArgumentException("Usage: Probe <repository-root> <harness-directory> <output-directory>");
var repository = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[2]);
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
var expectedTrace = Read(Path.Combine(evidence, "derivation-actions.json")).AsArray();
var history = new JsonArray();
for (var step = 0; step < 108; step++)
{
    var expected = expectedTrace[step]!.AsObject();
    var session = campaign.Battle!;
    var beforeSave = campaign.Save();
    var before = session.Snapshot();
    var computer = session.IsComputerTurn;
    Require(computer == expected["computer"]!.GetValue<bool>(), "Controller mismatch.");
    var eventCount = session.Events.Count;
    if (computer)
        Require(session.AdvanceComputerStep() is { Committed: true }, "Normal opponent AI refused.");
    else
        Require(session.Dispatch(EcmaJson.ToElement(expected["action"]!), session.Revision).Accepted, "Preserved human order refused.");
    var actual = CheckReceipt(campaign, beforeSave, eventCount, step, computer);
    Require(EcmaJson.DeepEquals(actual, expected), $"Preserved action/events/hash changed at step {step}.");
    history.Add(actual);
    if (step == 57)
        SaveWitness("friendly-east", beforeSave, campaign.Save(), actual, history, null);
}
Write("shared-prefix-actions.json", history);
Require(EcmaJson.DeepEquals(WithoutTimestamps(campaign.Save()), WithoutTimestamps(Read(Path.Combine(evidence, "before.json")).AsObject())), "The original shot checkpoint changed.");
var checkpoint = campaign.Save();
var state = campaign.Battle!.Snapshot();
var source = TacticalQueries.RequireUnit(state, 9);
var field = engine.Space.ComputeMoveField(state, source);
var candidates = new JsonArray();
JsonObject? selected = null;
foreach (var stop in field.Stops)
{
    var at = engine.Space.Board(state).CoordOf(stop);
    foreach (var option in engine.CommandsAt(state, source, at).Where(option => option.Ability == "attack"))
    {
        foreach (var targetAt in option.Targets)
        {
            var target = state["units"]!.AsArray().OfType<JsonObject>().SingleOrDefault(unit => TacticalQueries.Position(unit) == targetAt);
            if (target?["type"]?.GetValue<string>() != "c01.archer" || target["owner"]!.GetValue<int>() != 2)
                continue;
            var order = new JsonObject
            {
                ["kind"] = "command", ["unit"] = 9, ["path"] = TacticalQueries.PathJson(field.TracePath(stop)!),
                ["command"] = new JsonObject { ["ability"] = "attack", ["weapon"] = option.Weapon, ["target"] = new JsonObject { ["x"] = targetAt.X, ["y"] = targetAt.Y } }
            };
            var core = engine.LoadSavedBattle(checkpoint["battle"]!);
            var receipt = core.Dispatch(order);
            var hits = receipt.Events.Where(e => e["type"]?.GetValue<string>() == "attack").ToArray();
            if (hits.Length != 1 || hits[0]["killed"]!.GetValue<bool>() || hits[0]["damage"]!.GetValue<double>() <= 0)
                continue;
            candidates.Add(new JsonObject { ["order"] = order.DeepClone(), ["targetBefore"] = target.DeepClone(), ["events"] = new JsonArray(receipt.Events.Select(e => (JsonNode)e.DeepClone()).ToArray()) });
            if (at.X == 22 && at.Y == 13 && target["id"]!.GetValue<int>() == 14)
                selected = order;
        }
    }
}
Write("candidate-orders.json", candidates);
Require(selected is not null, "Preferred legal source9 (22,13) to target14 (22,16) witness not found.");
var inputs = VerifyUi(campaign.Battle!, selected!);
var count = campaign.Battle!.Events.Count;
Require(campaign.Battle.Dispatch(EcmaJson.ToElement(selected!), campaign.Battle.Revision).Accepted, "Selected human archer hit refused.");
var chosenReceipt = CheckReceipt(campaign, checkpoint, count, 108, false);
var completeHistory = (JsonArray)history.DeepClone();
completeHistory.Add(chosenReceipt.DeepClone());
SaveWitness("enemy-west", checkpoint, campaign.Save(), chosenReceipt, completeHistory, inputs);
var southern = new GrayFlagCampaign(rules, definition, levels);
southern.Load(checkpoint);
var southernHistory = (JsonArray)history.DeepClone();
var southernTrace = Read(Path.Combine(repository, "docs/art-gravekeeper-cast-20261110/verification/legal-cast/heal-right/derivation-actions.json")).AsArray();
JsonObject? triggerSave = null;
for (var step = 108; step <= 149; step++)
{
    var expected = southernTrace[step]!.AsObject();
    var session = southern.Battle!;
    var beforeSave = southern.Save();
    var computer = session.IsComputerTurn;
    Require(computer == expected["computer"]!.GetValue<bool>(), "Southern controller mismatch.");
    var priorCount = session.Events.Count;
    if (step == 148)
    {
        triggerSave = beforeSave;
        var controller = new TacticalSelectionController(session.GetTacticalQueries(0));
        var request = controller.Key(controller.Stamp, "e").Request;
        Require(request?.Action is { } endTurn && EcmaJson.DeepEquals(EcmaJson.Parse(endTurn.GetRawText()), expected["action"]), "Southern human E must emit the exact canonical endTurn.");
    }
    if (computer)
        Require(session.AdvanceComputerStep() is { Committed: true }, "Southern normal AI refused.");
    else
        Require(session.Dispatch(EcmaJson.ToElement(expected["action"]!), session.Revision).Accepted, "Southern preserved human order refused.");
    var receipt = CheckReceipt(southern, beforeSave, priorCount, step, computer);
    Require(EcmaJson.DeepEquals(receipt, expected), $"Southern exact action/events/hash changed at step {step}.");
    southernHistory.Add(receipt);
    if (step == 149)
    {
        SaveWitness("southern-bows", beforeSave, southern.Save(), receipt, southernHistory, null);
        SaveFile.WriteDocument(Path.Combine(output, "southern-bows/ui-trigger-before.json"), triggerSave!);
        SaveFile.WriteDocument(Path.Combine(output, "southern-bows/ui-trigger-action.json"), southernTrace[148]!["action"]!);
    }
}
var settledHistory = (JsonArray)southernHistory.DeepClone();
var extraActions = new JsonArray();
while (southern.Battle!.IsComputerTurn)
{
    var session = southern.Battle;
    Require(!session.IsConcluded, "Southern AI concluded before returning control to the human player.");
    var step = settledHistory.Count;
    Require(step < southernTrace.Count, "The preserved southern route has no expected continuation.");
    var expected = southernTrace[step]!.AsObject();
    Require(expected["computer"]!.GetValue<bool>(), "Southern AI exceeded the preserved computer-turn boundary.");
    var beforeSave = southern.Save();
    var priorCount = session.Events.Count;
    Require(session.AdvanceComputerStep() is { Committed: true }, "Southern normal AI continuation refused.");
    var receipt = CheckReceipt(southern, beforeSave, priorCount, step, true);
    Require(EcmaJson.DeepEquals(receipt, expected), $"Southern AI continuation changed at step {step}.");
    extraActions.Add(receipt.DeepClone());
    settledHistory.Add(receipt);
}
Require(extraActions.Count == 1 && extraActions[0]!["action"]!["kind"]!.GetValue<string>() == "endTurn",
    "The preserved southern action must be followed only by one normal AI endTurn.");
var settledSave = southern.Save();
var settledState = southern.Battle!.Snapshot();
Require(!southern.Battle.IsConcluded && !southern.Battle.IsComputerTurn
    && settledState["turn"]!.GetValue<int>() == 9 && settledState["currentPlayer"]!.GetValue<int>() == 1,
    "Southern AI must settle on the ninth-round human turn.");
var southernDirectory = Path.Combine(output, "southern-bows");
var settledPath = Path.Combine(southernDirectory, "ui-settled.json");
SaveFile.WriteDocument(settledPath, settledSave);
SaveFile.WriteDocument(Path.Combine(southernDirectory, "ui-settled-extra-actions.json"), extraActions);
SaveFile.WriteDocument(Path.Combine(southernDirectory, "ui-settled-derivation-actions.json"), settledHistory);
var settledReload = new GrayFlagCampaign(rules, definition, levels);
settledReload.Load(SaveFile.ReadDocument(settledPath));
Require(EcmaJson.DeepEquals(settledReload.Battle!.Snapshot(), settledState)
    && EcmaJson.DeepEquals(WithoutTimestamps(settledReload.Save()), WithoutTimestamps(settledSave)),
    "Official southern settled save changed on reload.");
var nativeReplay = new GrayFlagCampaign(rules, definition, levels);
nativeReplay.Load(SaveFile.ReadDocument(Path.Combine(southernDirectory, "before.json")));
var nativeActions = new JsonArray();
while (nativeReplay.Battle!.IsComputerTurn)
{
    var session = nativeReplay.Battle;
    var step = 149 + nativeActions.Count;
    Require(step < settledHistory.Count, "Reloaded southern AI exceeded the verified human-turn boundary.");
    var beforeSave = nativeReplay.Save();
    var priorCount = session.Events.Count;
    Require(session.AdvanceComputerStep() is { Committed: true }, "Reloaded southern AI continuation refused.");
    var receipt = CheckReceipt(nativeReplay, beforeSave, priorCount, step, true);
    Require(EcmaJson.DeepEquals(receipt, settledHistory[step]), $"Reloaded southern AI changed at step {step}.");
    nativeActions.Add(receipt);
}
Require(nativeActions.Count == 2 && !nativeReplay.Battle.IsConcluded
    && EcmaJson.DeepEquals(nativeReplay.Battle.Snapshot(), settledState)
    && EcmaJson.DeepEquals(WithoutTimestamps(nativeReplay.Save()), WithoutTimestamps(settledSave)),
    "Loading the native AI checkpoint must reproduce the complete settled official save.");
SaveFile.WriteDocument(Path.Combine(southernDirectory, "ui-settled-verification.json"), new JsonObject
{
    ["passed"] = true, ["normalAiActionsAfterWitness"] = extraActions.Count,
    ["normalAiActionsFromNativeBefore"] = nativeActions.Count,
    ["reloadedNativeActions"] = nativeActions, ["officialSettledRoundtrip"] = true,
    ["completeReloadedSaveEquality"] = true, ["independentCoreStateAndEvents"] = true,
    ["witnessAfterHash"] = southernHistory[149]!["afterHash"]!.DeepClone(),
    ["settledHash"] = StateDigest.Hash(settledState),
    ["settledEventsSha256"] = EcmaJson.Sha256(settledSave["events"]),
    ["comparison"] = "Compare the complete official save, omitting only campaign.savedAt and battle.savedAt.",
    ["turn"] = settledState["turn"]!.DeepClone(), ["currentPlayer"] = settledState["currentPlayer"]!.DeepClone(),
    ["computerTurn"] = false, ["concluded"] = false
});
Console.WriteLine($"PASS southern UI settled: {nativeActions.Count} normal AI actions from before, hash {StateDigest.Hash(settledState)}, full official save and receipts replay identically.");
Write("verification-result.json", new JsonObject
{
    ["passed"] = true, ["originalCampaignStart"] = true, ["preservedPrefixActions"] = 108,
    ["candidateCount"] = candidates.Count, ["chosenWitness"] = "enemy-west", ["oppositeFacingWitness"] = "friendly-east", ["openGroundBackup"] = "southern-bows",
    ["independentCoreStateAndEvents"] = true, ["officialSaveRoundtrip"] = true, ["reloadedNextActionEquality"] = true,
    ["productionUiControllerOrderEquality"] = true, ["noStatePatching"] = true
});
Console.WriteLine($"PASS: 108 exact original legal orders, {candidates.Count} legal candidates; enemy-west, friendly-east and southern-bows official witnesses verified.");

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

JsonArray VerifyUi(GrayFlagSession session, JsonObject order)
{
    var control = new TacticalSelectionController(session.GetTacticalQueries(0));
    var operations = new JsonArray();
    TacticalInputResult Key(string key)
    {
        var result = control.Key(control.Stamp, key);
        operations.Add(new JsonObject { ["key"] = key, ["selection"] = control.Selection.Kind.ToString(), ["cursor"] = control.Cursor is { } cursor ? new JsonObject { ["x"] = cursor.X, ["y"] = cursor.Y } : null, ["committed"] = result.Request is not null });
        Require(result.Status == TacticalInputStatus.Ready, "UI input refused: " + key);
        return result;
    }
    for (var index = 0; control.Selection.UnitId != 9 && index < 20; index++)
        Require(Key(" ").Request is null, "Unit cycle unexpectedly dispatched.");
    Require(control.Selection.UnitId == 9, "Cycle could not select source9.");
    Require(Key("arrowright").Request is null, "Destination cursor unexpectedly dispatched.");
    Require(Key("enter").Request is null && control.Selection.Kind == TacticalSelectionKind.Destination, "Destination must be a noncommitting preview.");
    var choose = Key("a");
    TacticalRequest? request = choose.Request;
    if (request is null)
    {
        Require(Key("arrowdown").Request is null, "Target cursor dispatched.");
        Require(Key("arrowdown").Request is null, "Target cursor dispatched.");
        Require(Key("arrowdown").Request is null, "Target cursor dispatched.");
        request = Key("enter").Request;
    }
    Require(request?.Action is { } action && EcmaJson.DeepEquals(EcmaJson.Parse(action.GetRawText()), order), "UI-emitted order differs from the canonical legal candidate.");
    return operations;
}

void SaveWitness(string name, JsonObject beforeSave, JsonObject afterSave, JsonObject receipt, JsonArray trace, JsonArray? inputs)
{
    var directory = Path.Combine(output, name);
    Directory.CreateDirectory(directory);
    var before = beforeSave["battle"]!["state"]!.AsObject();
    var after = afterSave["battle"]!["state"]!.AsObject();
    var action = receipt["action"]!.AsObject();
    foreach (var (filename, document) in new[] { ("before.json", (JsonNode)beforeSave), ("after.json", afterSave), ("receipt.json", receipt), ("derivation-actions.json", trace) })
        SaveFile.WriteDocument(Path.Combine(directory, filename), document);
    var hit = receipt["events"]!.AsArray().OfType<JsonObject>().First(e => e["type"]!.GetValue<string>() == "attack");
    var target = TacticalQueries.RequireUnit(before, hit["defender"]!.GetValue<int>());
    var actor = TacticalQueries.RequireUnit(before, hit["attacker"]!.GetValue<int>());
    Require(target["type"]!.GetValue<string>() == "c01.archer" && !hit["killed"]!.GetValue<bool>(), "Witness is not a nonfatal archer hit.");
    Require(engine.Space.IsUnitVisible(before, 1, target) && engine.Space.IsUnitVisible(before, 1, actor), "Both participants must be visible.");
    foreach (var filename in new[] { "before.json", "after.json" })
    {
        var document = SaveFile.ReadDocument(Path.Combine(directory, filename));
        var reloaded = new GrayFlagCampaign(rules, definition, levels);
        reloaded.Load(document);
        Require(EcmaJson.DeepEquals(WithoutTimestamps(reloaded.Save()), WithoutTimestamps(document!.AsObject())), "Official save roundtrip changed data.");
    }
    var replay = new GrayFlagCampaign(rules, definition, levels);
    replay.Load(SaveFile.ReadDocument(Path.Combine(directory, "before.json")));
    var eventCount = replay.Battle!.Events.Count;
    if (receipt["computer"]!.GetValue<bool>())
        Require(replay.Battle.AdvanceComputerStep() is { Committed: true }, "Reloaded normal AI refused.");
    else
        Require(replay.Battle.Dispatch(EcmaJson.ToElement(action), replay.Battle.Revision).Accepted, "Reloaded human order refused.");
    Require(EcmaJson.DeepEquals(replay.Battle.Snapshot(), after) && EcmaJson.DeepEquals(replay.Battle.ActionLog.Last(), action), "Reloaded action/state changed.");
    Require(EcmaJson.DeepEquals(new JsonArray(replay.Battle.Events.Skip(eventCount).Select(e => (JsonNode)e.DeepClone()).ToArray()), receipt["events"]), "Reloaded events changed.");
    var manifest = new JsonObject
    {
        ["passed"] = true, ["step"] = receipt["step"]!.DeepClone(), ["computer"] = receipt["computer"]!.DeepClone(),
        ["targetBefore"] = target.DeepClone(), ["targetAfter"] = TacticalQueries.RequireUnit(after, hit["defender"]!.GetValue<int>()).DeepClone(),
        ["sourceBefore"] = actor.DeepClone(), ["sourceAfter"] = TacticalQueries.RequireUnit(after, hit["attacker"]!.GetValue<int>()).DeepClone(),
        ["beforeHash"] = receipt["beforeHash"]!.DeepClone(), ["afterHash"] = receipt["afterHash"]!.DeepClone(),
        ["targetFacingBefore"] = target["facing"]!.DeepClone(), ["eventsSha256"] = EcmaJson.Sha256(receipt["events"]),
        ["visibleBefore"] = true, ["officialRoundtripAndReplay"] = true,
        ["readyUnitOrder"] = new JsonArray(engine.Turns.Actors(before).Select(unit => (JsonNode)unit["id"]!.DeepClone()).ToArray()),
        ["uiInputs"] = inputs?.DeepClone()
    };
    SaveFile.WriteDocument(Path.Combine(directory, "verification-result.json"), manifest);
    Console.WriteLine("WITNESS " + name + " " + EcmaJson.Stringify(manifest));
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
