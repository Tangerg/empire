using System.Globalization;
using System.Security.Cryptography;
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
var source = Path.Combine(evidence, "source-campaign-save.json");
Require(output != repository && !output.StartsWith(repository + Path.DirectorySeparatorChar, StringComparison.Ordinal),
    "Output must be outside the repository; audit evidence is read-only.");
Require(Directory.Exists(output), "The caller must provide an output directory.");
foreach (var name in new[] { "candidate-001-before.json", "candidate-001-after.json", "candidate-001-receipt.json",
    "derivation-actions.json", "verification-result.json" })
    Require(!File.Exists(Path.Combine(output, name)), $"Refusing to overwrite an existing result: {name}");

var sourceHash = FileHash(source);
Require(sourceHash == "2228e4c0a56006d88f83817b3fca8423a757d4a4bc68adafe70f4f07351ec3ca",
    "The source must be the preserved original application save.");
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
campaign.Load(SaveFile.ReadDocument(source));
var history = new JsonArray();

var aiActionsThisTurn = 0;
var found = false;
for (var step = 0; step < 22; step++)
{
    var session = campaign.Battle ?? throw new InvalidOperationException("A battle is required.");
    Require(!session.IsConcluded, "Battle concluded before Torren's lethal hit.");
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
        var command = EcmaJson.ToElement(new JsonObject { ["kind"] = "endTurn" });
        Require(session.Dispatch(command, session.Revision).Accepted, "The human end turn was refused.");
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
    if (hpAfter > 0) continue;
    Require(computer && hpBefore > 0, "Death must come from a natural computer action.");
    Require(emitted.OfType<JsonObject>().Any(e => e["defender"]?.GetValue<int>() == 2 && e["killed"]?.GetValue<bool>() == true),
        "A real killed receipt for Torren must be present.");
    Require(step == 21 && aiActionsThisTurn == 1 && hpBefore == 16 && hpAfter == 0,
        "The lethal hit must be the first AI action of turn 6, after exactly 21 preceding actions.");
    Require(StateDigest.Hash(before) == "c1578a7a" && receipt.StateHash == "ebb17884",
        "The lethal witness state hashes changed.");
    Require(EcmaJson.Sha256(emitted) == "92b2f43f282b42db31fd6c3729fb32ee2eab82a4dfb6c63d595d6faf9cdb851b",
        "The lethal witness receipt changed.");
    Require(EcmaJson.DeepEquals(trace, Read(Path.Combine(evidence, "candidate-001-receipt.json"))),
        "The preserved lethal receipt must reproduce exactly.");
    var afterSave = campaign.Save();
    VerifySavedEvidence(beforeSave, "candidate-001-before.json");
    VerifySavedEvidence(afterSave, "candidate-001-after.json");
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
    Require(replaySession.AdvanceComputerStep() is { Committed: true }, "Reloaded normal AI did not commit.");
    Require(EcmaJson.DeepEquals(action, replaySession.ActionLog.Last()), "Reloaded AI chose a different action.");
    Require(EcmaJson.DeepEquals(after, replaySession.Snapshot()), "Reloaded AI produced a different state.");
    var replayEvents = new JsonArray(replaySession.Events.Skip(replayCount).Select(e => (JsonNode)e.DeepClone()).ToArray());
    Require(EcmaJson.DeepEquals(emitted, replayEvents), "Reloaded AI emitted different receipt events.");
    SaveFile.WriteDocument(Path.Combine(output, "verification-result.json"), new JsonObject
    {
        ["passed"] = true,
        ["sourceSha256"] = sourceHash,
        ["beforeStateHash"] = StateDigest.Hash(before),
        ["afterStateHash"] = receipt.StateHash,
        ["eventsSha256"] = EcmaJson.Sha256(emitted),
        ["officialBeforeAfterLoadPassed"] = true,
        ["loadedCheckpointNormalAiActionEqualityPassed"] = true,
        ["independentRealReceiptEqualityPassed"] = true,
        ["preservedEvidenceEqualityPassed"] = true,
        ["ignoredSaveMetadata"] = new JsonArray("campaign.savedAt", "battle.savedAt"),
        ["actionCount"] = history.Count,
        ["aiActionNumberThisTurn"] = aiActionsThisTurn,
    });
    found = true;
    Console.WriteLine("PASS: official before/after loads, next normal AI action equality and independent real receipt equality.");
    break;
}
Require(found, "No Torren death was found within the permitted normal-action sequence.");
Require(FileHash(source) == sourceHash, "The original source save must remain unchanged.");
Require(EcmaJson.DeepEquals(history, Read(Path.Combine(evidence, "derivation-actions.json"))),
    "The complete 22-action trace must reproduce exactly.");
SaveFile.WriteDocument(Path.Combine(output, "derivation-actions.json"), history);
Console.WriteLine("PASS: legal endTurn/normal AI actions only and unchanged source save.");

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
    var expected = Read(Path.Combine(evidence, filename)).AsObject();
    Require(EcmaJson.DeepEquals(WithoutSaveTimestamps(actual), WithoutSaveTimestamps(expected)),
        $"The official save differs from preserved evidence beyond savedAt timestamps: {filename}");
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
