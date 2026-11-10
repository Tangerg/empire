using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Tactics;
using Empire.Core.Ai;
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
foreach (var name in new[] { "candidate-001-before.json", "candidate-001-after.json", "candidate-001-receipt.json",
    "derivation-actions.json", "verification-result.json" })
    Require(!File.Exists(Path.Combine(output, name)), $"Refusing to overwrite an existing result: {name}");

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
var campaign = new GrayFlagCampaign(rules, definition, levels);
var openingActions = new JsonArray();
while (campaign.Node()["type"]!.GetValue<string>() == "story")
{
    openingActions.Add(new JsonObject { ["kind"] = "advance", ["node"] = campaign.Node()["id"]!.DeepClone() });
    campaign.Advance();
}
openingActions.Add(new JsonObject { ["kind"] = "choose", ["choice"] = "steady-advance" });
campaign.Choose("steady-advance");
campaign.BeginBattle();
SaveFile.WriteDocument(Path.Combine(output, "source-campaign-save.json"), campaign.Save());
SaveFile.WriteDocument(Path.Combine(output, "opening-actions.json"), openingActions);
var sourceHash = FileHash(Path.Combine(output, "source-campaign-save.json"));
VerifySavedEvidence(campaign.Save(), "source-campaign-save.json");
Require(EcmaJson.DeepEquals(openingActions, Read(Path.Combine(evidence, "opening-actions.json"))),
    "Original campaign setup actions changed.");
var history = new JsonArray();

var aiActionsThisTurn = 0;
var found = false;
for (var step = 0; step < 1200; step++)
{
    var session = campaign.Battle ?? throw new InvalidOperationException("A battle is required.");
    Require(!session.IsConcluded, "Battle already concluded.");
    var beforeSave = campaign.Save();
    var before = session.Snapshot();
    var hpBefore = TorrenHealth(before);
    var computer = session.IsComputerTurn;
    if (!computer) aiActionsThisTurn = 0;
    var previousEventCount = session.Events.Count;
    if (computer)
    {
        Require(session.AdvanceComputerStep() is { Committed: true }, "The normal AI did not commit.");
        aiActionsThisTurn++;
    }
    else
    {
        var command = EcmaJson.ToElement(ChoosePlayerOrder(before));
        Require(session.Dispatch(command, session.Revision).Accepted, "The queried human order was refused.");
    }
    var action = session.ActionLog.Last();
    var receiptBattle = engine.LoadSavedBattle(beforeSave["battle"]!);
    var receipt = receiptBattle.Dispatch(action);
    var after = session.Snapshot();
    var emitted = new JsonArray(session.Events.Skip(previousEventCount)
        .Select(entry => (JsonNode)entry.DeepClone()).ToArray());
    var receiptEvents = new JsonArray(receipt.Events.Select(entry => (JsonNode)entry.DeepClone()).ToArray());
    Require(EcmaJson.DeepEquals(after, receiptBattle.Snapshot()), "Session and independent core state disagree.");
    Require(EcmaJson.DeepEquals(emitted, receiptEvents), "Session and independent core receipt events disagree.");
    var hpAfter = TorrenHealth(after);
    var trace = new JsonObject
    {
        ["step"] = step,
        ["computer"] = computer,
        ["aiActionNumberThisTurn"] = computer ? aiActionsThisTurn : 0,
        ["turn"] = before["turn"]!.DeepClone(),
        ["action"] = action.DeepClone(),
        ["beforeHash"] = StateDigest.Hash(before),
        ["afterHash"] = receipt.StateHash,
        ["eventsSha256"] = EcmaJson.Sha256(emitted),
        ["events"] = emitted.DeepClone(),
        ["torrenHpBefore"] = hpBefore,
        ["torrenHpAfter"] = hpAfter,
    };
    history.Add(trace);
    Console.WriteLine($"step={step}, AI={computer}, turn={before["turn"]}, aiOrdinal={aiActionsThisTurn}, " +
        $"action={EcmaJson.Stringify(action)}, Torren={hpBefore}->{hpAfter}");
    Require(hpAfter > 0, "Torren died during the legal route.");
    if (!session.IsConcluded) continue;
    Require(campaign.BattleSummary().Outcome == "victory", "The legal route did not win.");
    Require(emitted.OfType<JsonObject>().Any(e => e["type"]?.GetValue<string>() == "gameOver"), "Missing real terminal event.");
    var torrenAfter = after["units"]!.AsArray().OfType<JsonObject>().Single(u => u["id"]!.GetValue<int>() == 2);
    var winningTeam = after["conclusion"]!["team"]!.GetValue<int>();
    var owner = torrenAfter["owner"]!.GetValue<int>();
    Require(after["players"]!.AsArray().OfType<JsonObject>().Single(p => p["id"]!.GetValue<int>() == owner)["team"]!.GetValue<int>() == winningTeam, "Torren is not a winner.");
    Require(engine.Space.IsUnitVisible(before, 1, before["units"]!.AsArray().OfType<JsonObject>().Single(u => u["id"]!.GetValue<int>() == 2)) && engine.Space.IsUnitVisible(after, 1, torrenAfter), "Torren must be visible before and after gameOver.");
    var afterSave = campaign.Save();
    VerifySavedEvidence(beforeSave, "candidate-001-before.json");
    VerifySavedEvidence(afterSave, "candidate-001-after.json");
    Require(EcmaJson.DeepEquals(trace, Read(Path.Combine(evidence, "candidate-001-receipt.json"))),
        "The preserved terminal receipt must reproduce exactly.");
    Require(EcmaJson.DeepEquals(history, Read(Path.Combine(evidence, "derivation-actions.json"))),
        "The complete preserved legal action trace must reproduce exactly.");

    var beforePath = Path.Combine(output, "candidate-001-before.json");
    var afterPath = Path.Combine(output, "candidate-001-after.json");
    SaveFile.WriteDocument(beforePath, beforeSave);
    SaveFile.WriteDocument(afterPath, afterSave);
    SaveFile.WriteDocument(Path.Combine(output, "candidate-001-receipt.json"), trace);
    VerifyReload(beforePath, before);
    VerifyReload(afterPath, after);
    var replay = new GrayFlagCampaign(rules, definition, levels);
    replay.Load(SaveFile.ReadDocument(beforePath));
    var replaySession = replay.Battle!;
    var replayCount = replaySession.Events.Count;
    if (computer) Require(replaySession.AdvanceComputerStep() is { Committed: true }, "Reloaded normal AI did not commit.");
    else Require(replaySession.Dispatch(EcmaJson.ToElement(action), replaySession.Revision).Accepted, "Reloaded human order refused.");
    Require(EcmaJson.DeepEquals(action, replaySession.ActionLog.Last()), "Reloaded action chose a different action.");
    Require(EcmaJson.DeepEquals(after, replaySession.Snapshot()), "Reloaded action produced a different state.");
    var replayEvents = new JsonArray(replaySession.Events.Skip(replayCount).Select(e => (JsonNode)e.DeepClone()).ToArray());
    Require(EcmaJson.DeepEquals(emitted, replayEvents), "Reloaded action emitted different receipt events.");
    SaveFile.WriteDocument(Path.Combine(output, "verification-result.json"), new JsonObject
    {
        ["passed"] = true,
        ["sourceSha256"] = sourceHash,
        ["beforeStateHash"] = StateDigest.Hash(before),
        ["afterStateHash"] = receipt.StateHash,
        ["eventsSha256"] = EcmaJson.Sha256(emitted),
        ["officialBeforeAfterLoadPassed"] = true,
        ["loadedCheckpointNextActionEqualityPassed"] = true,
        ["independentRealReceiptEqualityPassed"] = true,
        ["originalCampaignStart"] = true,
        ["preservedEvidenceEqualityPassed"] = true,
        ["torrenVisibleBeforeAndAfter"] = true,
        ["torrenWinningTeam"] = winningTeam,
        ["torrenAfter"] = torrenAfter.DeepClone(),
        ["ignoredSaveMetadata"] = new JsonArray("campaign.savedAt", "battle.savedAt"),
        ["actionCount"] = history.Count,
        ["aiActionNumberThisTurn"] = aiActionsThisTurn,
    });
    found = true;
    Console.WriteLine("PASS: official before/after loads, next human action equality and independent real receipt equality.");
    break;
}
Require(found, "No victory with Torren alive was found within the permitted legal-action sequence.");
SaveFile.WriteDocument(Path.Combine(output, "derivation-actions.json"), history);
Console.WriteLine("PASS: original campaign start and legal player/normal AI orders only.");

void VerifyReload(string path, JsonObject expectedState)
{
    var reloaded = new GrayFlagCampaign(rules, definition, levels);
    reloaded.Load(SaveFile.ReadDocument(path));
    Require(EcmaJson.DeepEquals(reloaded.Battle!.Snapshot(), expectedState),
        $"Official application save reload changed the state: {Path.GetFileName(path)}");
    Require(EcmaJson.DeepEquals(WithoutSaveTimestamps(reloaded.Save()), WithoutSaveTimestamps(Read(path).AsObject())),
        $"Official reload changed saved fields beyond timestamps: {Path.GetFileName(path)}");
}

void VerifySavedEvidence(JsonObject actual, string filename)
{
    Require(EcmaJson.DeepEquals(WithoutSaveTimestamps(actual),
            WithoutSaveTimestamps(Read(Path.Combine(evidence, filename)).AsObject())),
        $"Preserved official save differs beyond writer timestamps: {filename}");
}

JsonObject ChoosePlayerOrder(JsonObject state)
{
    var torren = state["units"]!.AsArray().OfType<JsonObject>().Single(u => u["key"]?.GetValue<string>() == "campaign-torren");
    if (engine.Turns.CanAct(state, torren))
    {
        var field = engine.Space.ComputeMoveField(state, torren);
        var context = new AiTurnContext(engine.Ai.Planning, state, state["currentPlayer"]!.GetValue<int>(), 0.58);
        foreach (var stop in field.Stops.OrderBy(i => context.Threat.GetValueOrDefault(i) * 100 + (Math.Abs(context.Board.CoordOf(i).X - 21) + Math.Abs(context.Board.CoordOf(i).Y - 18)) * 2).ThenBy(i => field.Tiles[i].Cost))
        {
            var at = context.Board.CoordOf(stop);
            var path = field.TracePath(stop);
            if (path is null || !engine.CommandsAt(state, torren, at).Any(c => c.Ability == "wait")) continue;
            return new JsonObject { ["kind"] = "command", ["unit"] = torren["id"]!.DeepClone(), ["path"] = TacticalQueries.PathJson(path), ["command"] = new JsonObject { ["ability"] = "wait" } };
        }
    }
    var proposed = engine.Ai.ChooseAction(state, 0.58);
    if (proposed["path"] is JsonArray move && move.Count > 1 && proposed["command"]?["ability"]?.GetValue<string>() == "attack")
    {
        var preview = engine.LoadSavedBattle(campaign.Save()["battle"]!);
        var receipt = preview.Dispatch(proposed);
        if (receipt.Events.Any(e => e["type"]?.GetValue<string>() == "gameOver"))
            proposed["command"] = new JsonObject { ["ability"] = "wait" };
    }
    return proposed;
}

static JsonObject WithoutSaveTimestamps(JsonObject document)
{
    // Omit timestamps only from detached comparison copies, never from an official save.
    var comparison = (JsonObject)document.DeepClone();
    comparison["campaign"]!.AsObject().Remove("savedAt");
    comparison["battle"]!.AsObject().Remove("savedAt");
    return comparison;
}

static double TorrenHealth(JsonObject state) => state["units"]!.AsArray().OfType<JsonObject>()
    .SingleOrDefault(unit => unit["id"]!.GetValue<int>() == 2)?["hp"]?.GetValue<double>() ?? 0;
static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
