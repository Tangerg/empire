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
Require(new[] { "right", "left", "mirror-left" }.All(name => !Directory.Exists(Path.Combine(output, name))),
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
var scene = Read(Path.Combine(root, "data/scenes/c01-01.json"));
var registration = Read(Path.Combine(root, scene["assembledGroundPath"]!.GetValue<string>().Replace("res://", "")));
var expectedRegistration = Read(Path.Combine(evidence, "projection-registration.json"));
foreach (var key in new[] { "worldScale", "originPixels", "cellXPixels", "cellYPixels" })
    Require(EcmaJson.DeepEquals(registration[key], expectedRegistration[key]), "Actual C01 ground registration changed.");
foreach (var scenario in new[] { "right", "left", "mirror-left" })
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
        Require(!computer && action["unit"]!.GetValue<int>() == 3 && action["command"]!["ability"]!.GetValue<string>() == "attack",
            "Final action must be a human archer attack command.");
        var actor = TacticalQueries.RequireUnit(before, 3);
        var targetAt = TacticalQueries.Position(action["command"]!["target"]!.AsObject());
        var target = before["units"]!.AsArray().OfType<JsonObject>().Single(u => TacticalQueries.Position(u) == targetAt);
        Require(engine.Space.IsUnitVisible(before, 1, actor) && engine.Space.IsUnitVisible(before, 1, target), "Source and recipient must be visible.");
        Require(actor["y"]!.GetValue<int>() >= 18 && targetAt.Y >= 18, "Both participants must be on the open southern ground.");
        Require(TacticalQueries.RequireUnit(after, 3)["hp"]!.GetValue<double>() > 0, "Source must survive the action.");
        Require(events.Any(e => e?["type"]?.GetValue<string>() == "attackActivated"), "Missing real action receipt.");
        Console.WriteLine("READY_COMMANDS " + scenario + " " + string.Join(";", engine.CommandsAt(before, actor, TacticalQueries.Position(actor)).Select(o => o.Ability + ":" + o.Weapon + ":" + o.Targets.Count + ":" + string.Join(",", o.Targets))));
        var from = TacticalQueries.Position(action["path"]!.AsArray().Last()!.AsObject());
        var facing = engine.Space.Board(before).Grid.Toward(from, targetAt);
        var facingStep = engine.Space.Board(before).Grid.Step(from, facing);
        var scale = registration["worldScale"]!.GetValue<double>();
        var facingX = ((facingStep.X - from.X) * registration["cellXPixels"]![0]!.GetValue<double>()
            + (facingStep.Y - from.Y) * registration["cellYPixels"]![0]!.GetValue<double>()) * scale;
        var targetDeltaX = ((targetAt.X - from.X) * registration["cellXPixels"]![0]!.GetValue<double>()
            + (targetAt.Y - from.Y) * registration["cellYPixels"]![0]!.GetValue<double>()) * scale;
        var targetDeltaY = ((targetAt.X - from.X) * registration["cellXPixels"]![1]!.GetValue<double>()
            + (targetAt.Y - from.Y) * registration["cellYPixels"]![1]!.GetValue<double>()) * scale;
        Require((facingX < 0) == (scenario == "mirror-left"), "Actual registered facing mirror mismatch.");
        Require((targetDeltaX < 0) == (scenario == "mirror-left"), "Actual target screen-side mismatch.");
        Require(engine.Space.Board(before).Distance(from, targetAt) >= 2, "Not a ranged-distance shot.");
        var hits = events.OfType<JsonObject>().Where(e => e["type"]?.GetValue<string>() == "attack").ToArray();
        Require(hits.Length == 1 && !hits[0]["killed"]!.GetValue<bool>(), "Expected one nonlethal hit.");
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
            ["readyUnitOrder"] = new JsonArray(engine.Turns.Actors(before).Select(u => (JsonNode)u["id"]!.DeepClone()).ToArray()),
            ["originAttackTargetCount"] = engine.CommandsAt(before, actor, TacticalQueries.Position(actor)).FirstOrDefault(o => o.Ability == "attack" && o.Weapon == "c01.ranger-bow")?.Targets.Count ?? 0,
            ["destinationAttackTargetCount"] = engine.CommandsAt(before, actor, from).Single(o => o.Ability == "attack" && o.Weapon == "c01.ranger-bow").Targets.Count,
            ["actualRegisteredFacingX"] = facingX,
            ["expectedMirrored"] = facingX < 0,
            ["targetScreenDeltaX"] = targetDeltaX,
            ["targetScreenDeltaY"] = targetDeltaY,
            ["routeMeaning"] = scenario == "left" ? "move-right" : scenario,
            ["facing"] = facing,
            ["shotSource"] = action["path"]!.AsArray().Last()!.DeepClone(),
            ["distance"] = engine.Space.Board(before).Distance(from, targetAt),
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
