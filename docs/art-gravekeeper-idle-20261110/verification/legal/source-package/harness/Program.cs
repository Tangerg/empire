using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Localization;

if (args.Length != 3)
    throw new ArgumentException("Usage: Probe <repository-root> <evidence-directory> <output-directory>");

var repository = Path.GetFullPath(args[0]);
var evidence = Path.GetFullPath(args[1]);
var output = Path.GetFullPath(args[2]);
var root = Path.Combine(repository, "godot");

Require(output != repository && !output.StartsWith(repository + Path.DirectorySeparatorChar, StringComparison.Ordinal),
    "Output must be outside the repository; audit evidence is read-only.");
Require(Directory.Exists(output), "The caller must provide an output directory.");
Require(new[] { "cast", "heal-right", "heal-left" }.All(name => !Directory.Exists(Path.Combine(output, name))),
    "Refusing to overwrite an existing replay result.");

var engine = new BattleEngine(Read(Path.Combine(root, "data/content-packs.json")).AsArray());
var definition = Read(Path.Combine(root, "data/grayflag-campaign.json")).AsObject();
var levels = Read(Path.Combine(root, "data/grayflag-campaign-levels.json")).AsArray();
var lookup = Read(Path.Combine(output, "translation-lookup.json")).AsObject();
var messages = new GameText(key => lookup[key]?.GetValue<string>(), CultureInfo.GetCultureInfo("zh-CN"));
var identities = Read(Path.Combine(root, "data/grayflag-story.json"))["identityNameKeys"]!.AsObject()
    .ToDictionary(entry => entry.Key, entry => entry.Value!.GetValue<string>());
var presentation = SessionPresentationText.Compose(new(engine, messages, identities), levels,
    File.ReadAllText(Path.Combine(root, "data/grayflag-battle-text.json")),
    File.ReadAllText(Path.Combine(root, "data/grayflag-content-text.json")));
var rules = new CampaignApplicationRules(engine, presentation);
foreach (var scenario in new[] { "cast", "heal-right", "heal-left" })
{
    var expectedDirectory = Path.Combine(evidence, scenario);
    var resultDirectory = Path.Combine(output, scenario);
    Directory.CreateDirectory(resultDirectory);
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
    Require(EcmaJson.DeepEquals(opening, Read(Path.Combine(expectedDirectory, "opening-actions.json"))), "Opening actions changed.");
    CompareSave(campaign.Save(), Path.Combine(expectedDirectory, "source-campaign-save.json"));
    SaveFile.WriteDocument(Path.Combine(resultDirectory, "source-campaign-save.json"), campaign.Save());
    SaveFile.WriteDocument(Path.Combine(resultDirectory, "opening-actions.json"), opening);
    var trace = Read(Path.Combine(expectedDirectory, "derivation-actions.json")).AsArray();
    var history = new JsonArray();
    foreach (var expected in trace.OfType<JsonObject>())
    {
        var session = campaign.Battle!;
        Require(!session.IsConcluded, "The campaign concluded before the checkpoint.");
        var beforeSave = campaign.Save();
        var before = session.Snapshot();
        var computer = session.IsComputerTurn;
        Require(computer == expected["computer"]!.GetValue<bool>(), "Controller differs from the preserved route.");
        var eventCount = session.Events.Count;
        if (computer)
            Require(session.AdvanceComputerStep() is { Committed: true }, "Normal opponent AI refused to commit.");
        else
            Require(session.Dispatch(EcmaJson.ToElement(expected["action"]!), session.Revision).Accepted, "Preserved human order refused.");
        var action = session.ActionLog.Last();
        var core = engine.LoadSavedBattle(beforeSave["battle"]!);
        var receipt = core.Dispatch(action);
        var after = session.Snapshot();
        var events = new JsonArray(session.Events.Skip(eventCount).Select(e => (JsonNode)e.DeepClone()).ToArray());
        Require(EcmaJson.DeepEquals(after, core.Snapshot()), "Independent core state mismatch.");
        Require(EcmaJson.DeepEquals(events, new JsonArray(receipt.Events.Select(e => (JsonNode)e.DeepClone()).ToArray())), "Independent core events mismatch.");
        var actual = new JsonObject
        {
            ["step"] = history.Count,
            ["computer"] = computer,
            ["action"] = action.DeepClone(),
            ["beforeHash"] = StateDigest.Hash(before),
            ["afterHash"] = receipt.StateHash,
            ["events"] = events.DeepClone()
        };
        Require(EcmaJson.DeepEquals(actual, expected), "Preserved trace/action/events/state mismatch.");
        history.Add(actual);
        if (history.Count != trace.Count)
            continue;
        Require(!computer && action["unit"]!.GetValue<int>() == 4 && action["path"]!.AsArray().Count == 1,
            "Final action must be a stationary human gravekeeper command.");
        var actor = TacticalQueries.RequireUnit(before, 4);
        var targetAt = TacticalQueries.Position(action["command"]!["target"]!.AsObject());
        var target = before["units"]!.AsArray().OfType<JsonObject>().Single(u => TacticalQueries.Position(u) == targetAt);
        Require(engine.Space.IsUnitVisible(before, 1, actor) && engine.Space.IsUnitVisible(before, 1, target), "Source and recipient must be visible.");
        Require(actor["y"]!.GetValue<int>() >= 18 && targetAt.Y >= 18, "Both participants must be on the open southern ground.");
        Require(TacticalQueries.RequireUnit(after, 4)["hp"]!.GetValue<double>() > 0, "Source must survive the action.");
        Require(events.Any(e => e?["type"]?.GetValue<string>() == (scenario == "cast" ? "attackActivated" : "heal")), "Missing real action receipt.");
        CompareSave(beforeSave, Path.Combine(expectedDirectory, "before.json"));
        CompareSave(campaign.Save(), Path.Combine(expectedDirectory, "after.json"));
        Require(EcmaJson.DeepEquals(actual, Read(Path.Combine(expectedDirectory, "receipt.json"))), "Final receipt changed.");
        var beforePath = Path.Combine(resultDirectory, "before.json");
        var afterPath = Path.Combine(resultDirectory, "after.json");
        SaveFile.WriteDocument(beforePath, beforeSave);
        SaveFile.WriteDocument(afterPath, campaign.Save());
        SaveFile.WriteDocument(Path.Combine(resultDirectory, "receipt.json"), actual);
        VerifyReload(beforePath, before);
        VerifyReload(afterPath, after);
        var replay = new GrayFlagCampaign(rules, definition, levels);
        replay.Load(SaveFile.ReadDocument(beforePath));
        var replaySession = replay.Battle!;
        var replayEventCount = replaySession.Events.Count;
        Require(replaySession.Dispatch(EcmaJson.ToElement(action), replaySession.Revision).Accepted, "Reloaded checkpoint action refused.");
        Require(EcmaJson.DeepEquals(replaySession.Snapshot(), after), "Reloaded checkpoint state mismatch.");
        Require(EcmaJson.DeepEquals(new JsonArray(replaySession.Events.Skip(replayEventCount).Select(e => (JsonNode)e.DeepClone()).ToArray()), events), "Reloaded checkpoint events mismatch.");
        SaveFile.WriteDocument(Path.Combine(resultDirectory, "verification-result.json"), new JsonObject
        {
            ["passed"] = true,
            ["originalCampaignStart"] = true,
            ["actionCount"] = history.Count,
            ["independentCoreStateAndEvents"] = true,
            ["preservedTraceEquality"] = true,
            ["officialBeforeAfterRoundtrip"] = true,
            ["reloadedNextActionEquality"] = true,
            ["beforeHash"] = StateDigest.Hash(before),
            ["afterHash"] = receipt.StateHash,
            ["eventsSha256"] = EcmaJson.Sha256(events),
            ["visibleBefore"] = true,
            ["sourceBefore"] = actor.DeepClone(),
            ["targetBefore"] = target.DeepClone()
        });
    }
    Require(history.Count > 0, "Empty route.");
    SaveFile.WriteDocument(Path.Combine(resultDirectory, "derivation-actions.json"), history);
    Console.WriteLine($"PASS {scenario}: {history.Count} legal actions, independent receipts, official save roundtrips and checkpoint replay.");
}

void CompareSave(JsonObject actual, string expectedPath) => Require(
    EcmaJson.DeepEquals(WithoutSaveTimestamps(actual), WithoutSaveTimestamps(Read(expectedPath).AsObject())),
    $"Official save differs beyond writer timestamps: {expectedPath}");

void VerifyReload(string path, JsonObject expectedState)
{
    var reloaded = new GrayFlagCampaign(rules, definition, levels);
    reloaded.Load(SaveFile.ReadDocument(path));
    Require(EcmaJson.DeepEquals(reloaded.Battle!.Snapshot(), expectedState),
        $"Official application save reload changed the state: {Path.GetFileName(path)}");
    Require(EcmaJson.DeepEquals(WithoutSaveTimestamps(reloaded.Save()), WithoutSaveTimestamps(Read(path).AsObject())),
        $"Official reload changed saved fields beyond timestamps: {Path.GetFileName(path)}");
}

static JsonObject WithoutSaveTimestamps(JsonObject document)
{
    // Omit timestamps only from detached comparison copies, never from an official save.
    var comparison = (JsonObject)document.DeepClone();
    comparison["campaign"]!.AsObject().Remove("savedAt");
    comparison["battle"]!.AsObject().Remove("savedAt");
    return comparison;
}

static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
