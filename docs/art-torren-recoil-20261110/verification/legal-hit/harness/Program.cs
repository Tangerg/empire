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

for (var step = 0; step < 2; step++)
{
    var session = campaign.Battle ?? throw new InvalidOperationException("A battle is required.");
    Require(!session.IsConcluded && session.IsComputerTurn == (step == 1),
        "Expected exactly a human end turn followed by the normal AI's first action.");
    var beforeSave = campaign.Save();
    var before = session.Snapshot();
    var hpBefore = TorrenHealth(before);
    var computer = session.IsComputerTurn;
    var previousEventCount = session.Events.Count;

    if (computer)
    {
        Require(session.AdvanceComputerStep() is { Committed: true }, "The normal AI did not commit.");
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
        ["action"] = action.DeepClone(),
        ["beforeHash"] = StateDigest.Hash(before),
        ["afterHash"] = receipt.StateHash,
        ["eventsSha256"] = EcmaJson.Sha256(emitted),
        ["events"] = emitted.DeepClone(),
        ["torrenHpBefore"] = hpBefore,
        ["torrenHpAfter"] = hpAfter,
    };
    history.Add(trace);
    Console.WriteLine($"step={step}, AI={computer}, turn={before["turn"]}, " +
        $"action={EcmaJson.Stringify(action)}, Torren={hpBefore}->{hpAfter}");

    if (step == 0)
    {
        Require(hpBefore == 72 && hpAfter == 72, "End turn must not change Torren's HP.");
        continue;
    }

    Require(hpBefore == 72 && hpAfter == 43, "The chosen hit must deal 29 damage without killing Torren.");
    Require(StateDigest.Hash(before) == "f04b7e8f" && receipt.StateHash == "c1d94756",
        "The before/after canonical state must match the captured candidate.");
    Require(EcmaJson.Sha256(emitted) == "854477fb25ec86721038f10e36f6d9dba4660c9731d4f96221ec5958477380c0",
        "The emitted events must match the captured candidate.");
    Require(EcmaJson.DeepEquals(trace, Read(Path.Combine(evidence, "candidate-001-receipt.json"))),
        "The detached real-receipt evidence must reproduce exactly.");

    var afterSave = campaign.Save();
    var beforePath = Path.Combine(output, "candidate-001-before.json");
    var afterPath = Path.Combine(output, "candidate-001-after.json");
    SaveFile.WriteDocument(beforePath, beforeSave);
    SaveFile.WriteDocument(afterPath, afterSave);
    SaveFile.WriteDocument(Path.Combine(output, "candidate-001-receipt.json"), trace);

    VerifyReload(beforePath, before);
    VerifyReload(afterPath, after);
    VerifySavedEvidence(beforeSave, "candidate-001-before.json");
    VerifySavedEvidence(afterSave, "candidate-001-after.json");
    Console.WriteLine("PASS: official before/after campaign Load roundtrips and independent real core receipt equality.");
    Console.WriteLine("PASS: preserved save evidence matches in every field except the two official savedAt timestamps.");
}

Require(EcmaJson.DeepEquals(history, Read(Path.Combine(evidence, "derivation-actions.json"))),
    "The full two-action trace must reproduce exactly.");
Require(FileHash(source) == sourceHash, "The original source save must remain unchanged.");
SaveFile.WriteDocument(Path.Combine(output, "derivation-actions.json"), history);
SaveFile.WriteDocument(Path.Combine(output, "verification-result.json"), new JsonObject
{
    ["passed"] = true,
    ["sourceSha256"] = sourceHash,
    ["beforeStateHash"] = "f04b7e8f",
    ["afterStateHash"] = "c1d94756",
    ["eventsSha256"] = "854477fb25ec86721038f10e36f6d9dba4660c9731d4f96221ec5958477380c0",
    ["officialBeforeAfterLoadPassed"] = true,
    ["independentRealReceiptEqualityPassed"] = true,
    ["preservedEvidenceEqualityPassed"] = true,
    ["ignoredSaveMetadata"] = new JsonArray("campaign.savedAt", "battle.savedAt"),
    ["actionCount"] = history.Count,
});
Console.WriteLine("PASS: exactly two legal actions, preserved evidence, and unchanged source save.");

void VerifyReload(string path, JsonObject expectedState)
{
    var reloaded = new GrayFlagCampaign(rules, definition, levels);
    reloaded.Load(SaveFile.ReadDocument(path));
    Require(EcmaJson.DeepEquals(reloaded.Battle!.Snapshot(), expectedState),
        $"Official application save reload changed the state: {Path.GetFileName(path)}");
}

void VerifySavedEvidence(JsonObject actual, string filename)
{
    var expected = Read(Path.Combine(evidence, filename)).AsObject();
    // Only a detached comparison projection omits timestamps; neither saved document is modified.
    Require(EcmaJson.DeepEquals(WithoutSaveTimestamps(actual), WithoutSaveTimestamps(expected)),
        $"The official save differs from preserved evidence beyond savedAt timestamps: {filename}");
}

static JsonObject WithoutSaveTimestamps(JsonObject document)
{
    var comparison = (JsonObject)document.DeepClone();
    comparison["campaign"]!.AsObject().Remove("savedAt");
    comparison["battle"]!.AsObject().Remove("savedAt");
    return comparison;
}

static double TorrenHealth(JsonObject state) => TacticalQueries.RequireUnit(state, 2)["hp"]!.GetValue<double>();
static JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(path))!;
static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
