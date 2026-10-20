using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Campaign;
using Empire.Core.Determinism;
using Empire.Presentation;

if (args.Length != 2) throw new ArgumentException("Pass a source oracle and exported presentation metadata.");
var oraclePath = Path.GetFullPath(args[0]);
if (!oraclePath.StartsWith("/tmp/empire-campaign-slot.", StringComparison.Ordinal))
    throw new InvalidOperationException("Tests only run in a fresh /tmp/empire-campaign-slot.* directory.");
var root = Path.Combine(Path.GetDirectoryName(oraclePath)!, "filesystem");
if (Directory.Exists(root) || File.Exists(root)) throw new InvalidOperationException("Refusing to reuse or modify existing test/user files.");
Directory.CreateDirectory(root);
var oracle = EcmaJson.Parse(File.ReadAllText(oraclePath))!.AsObject();
var metadata = oracle["metadata"]!.AsObject();
var exported = EcmaJson.Parse(File.ReadAllText(args[1]))!.AsObject();
var definition = oracle["definition"]!.AsObject();
var initial = oracle["initial"]!.AsObject();
var pending = oracle["pending"]!.AsObject();
var checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Same(JsonNode? actual, JsonNode? expected, string message) => Check(EcmaJson.DeepEquals(actual, expected), message);
void NewFile(string path, string contents) { using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew)); writer.Write(contents); }
string Slot(string name) => Path.Combine(root, name);
int index = 0;
foreach (var item in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = item["name"]!.GetValue<string>();
    var path = Slot($"case-{index++}.json");
    var raw = item["raw"]?.GetValue<string>();
    if (item["readError"]!.GetValue<bool>()) Directory.CreateDirectory(path);
    else if (raw is not null) NewFile(path, raw);
    var expected = item["expected"]!.AsObject();
    LoadedCampaign? loaded = null;
    Exception? failure = null;
    try { loaded = CampaignSlotStorage.Load(item["definition"]!.AsObject(), path); }
    catch (Exception error) { failure = error; }
    if (expected["kind"]!.GetValue<string>() == "thrown")
    {
        Check(failure is CampaignInvariantException, $"{name}: definition defects must propagate, not become rejected saves");
        Check(failure!.Message == expected["message"]!.GetValue<string>(), $"{name}: defect changed");
    }
    else
    {
        Check(failure is null && loaded is not null, $"{name}: unexpected exception {failure}");
        Same(loaded!.State, expected["state"], $"{name}: state differs");
        if (item["syntax"]!.GetValue<bool>() || item["readError"]!.GetValue<bool>())
            Check(loaded.Rejected is { Length: > 0 }, $"{name}: native parser/OS failure needs a diagnostic");
        else Check(loaded.Rejected == expected["rejected"]?.GetValue<string>(), $"{name}: rejection differs: {loaded.Rejected}");
        Check(!loaded.LegacyApplicationEnvelope, $"{name}: original envelope is not a legacy application wrapper");
    }
    if (raw is not null && !item["readError"]!.GetValue<bool>()) Check(File.ReadAllText(path) == raw, $"{name}: read mutated stored progress");
}
foreach (var item in oracle["writeCases"]!.AsArray().OfType<JsonObject>())
{
    var name = item["name"]!.GetValue<string>();
    var path = Slot($"write-{index++}.json");
    var blocked = name == "write error";
    if (blocked) { Directory.CreateDirectory(path); NewFile(Path.Combine(path, "retained"), "existing"); }
    else NewFile(path, "original");
    Exception? failure = null;
    try { CampaignSlotStorage.Save(definition, item["state"]!.AsObject(), path); }
    catch (Exception error) { failure = error; }
    var expected = item["expected"]!.AsObject();
    if (expected["kind"]!.GetValue<string>() == "returned")
    {
        Check(failure is null, $"{name}: native save failed: {failure}");
        var actual = EcmaJson.Parse(File.ReadAllText(path))!.AsObject();
        actual["savedAt"] = expected["save"]!["savedAt"]!.DeepClone();
        Same(actual, expected["save"], "Canonical saved envelope differs from executing original saveCampaignState");
        Check(!actual.ContainsKey("request") && !actual.ContainsKey("battle"), "Autosave must not be the full manual application document");
        Same(CampaignSlotStorage.Load(definition, path).State, item["state"], "Fresh startup did not restore the clean campaign");
    }
    else if (blocked)
    {
        Check(failure is IOException or UnauthorizedAccessException, "Filesystem write failure was swallowed");
        Check(File.ReadAllText(Path.Combine(path, "retained")) == "existing", "Failed replacement damaged the destination");
    }
    else
    {
        Check(failure is CampaignActionException && failure.Message == expected["message"]!.GetValue<string>(), "Pending write must reject before I/O");
        Check(File.ReadAllText(path) == "original", "Rejected pending save overwrote existing autosave");
    }
    Check(!Directory.EnumerateFiles(root, ".*.tmp").Any(), "Temporary write debris was not removed");
}
var invalidEncodingPath = Slot("invalid-utf8.json");
using (var encoded = new FileStream(invalidEncodingPath, FileMode.CreateNew)) encoded.Write(new byte[] { 0x7b, 0x22, 0xff, 0x22, 0x3a, 0x31, 0x7d });
var invalidEncoding = CampaignSlotStorage.Load(definition, invalidEncodingPath);
Check(invalidEncoding.State is null && invalidEncoding.Rejected is { Length: > 0 }, "Invalid native file encoding must be rejected, never silently repaired");
Check(File.ReadAllBytes(invalidEncodingPath).Contains((byte)0xff), "Invalid encoding rejection changed stored bytes");
var clean = CampaignSave.Create(definition, initial, "2026-10-20T00:00:00.000Z");
JsonObject Wrapper() => new() { ["schema"] = 1, ["atCamp"] = false, ["campaign"] = clean.DeepClone(),
    ["request"] = null, ["battle"] = null, ["events"] = new JsonArray() };
var legacyPath = Slot("checkpoint-e6-clean.json");
NewFile(legacyPath, EcmaJson.Stringify(Wrapper()));
var legacyRaw = File.ReadAllText(legacyPath);
var legacy = CampaignSlotStorage.Load(definition, legacyPath);
Same(legacy.State, initial, "The e6 checkpoint's clean atCamp:false wrapper must remain readable");
Check(legacy.LegacyApplicationEnvelope && legacy.Rejected is null, "Clean legacy classification was lost");
Check(File.ReadAllText(legacyPath) == legacyRaw, "Legacy read must not migrate or rewrite user data");
var defectiveDefinition = (JsonObject)definition.DeepClone(); defectiveDefinition["start"] = "missing";
try { CampaignSlotStorage.Load(defectiveDefinition, legacyPath); throw new Exception("Legacy wrapper masked a definition defect"); }
catch (CampaignInvariantException) { checks++; }

foreach (var mode in new[] { "request", "battle", "pending", "camp", "events", "missing-request", "schema" })
{
    var document = Wrapper();
    switch (mode)
    {
        case "request": document["request"] = new JsonObject(); break;
        case "battle": document["battle"] = new JsonObject(); break;
        case "pending": document["campaign"] = CampaignSave.Create(definition, pending); break;
        case "camp": document["atCamp"] = true; break;
        case "events": document["events"] = new JsonArray(new JsonObject { ["type"] = "battleStarted" }); break;
        case "missing-request": document.Remove("request"); break;
        case "schema": document["schema"] = 2; break;
    }
    var path = Slot($"legacy-{mode}.json"); var text = EcmaJson.Stringify(document); NewFile(path, text);
    var result = CampaignSlotStorage.Load(definition, path);
    Check(result.State is null && result.Rejected is { Length: > 0 }, $"Legacy {mode} must not masquerade as campaign-only autosave");
    Check(File.ReadAllText(path) == text, "Rejected legacy/full manual save was changed");
}
var badState = (JsonObject)initial.DeepClone(); badState["currentNode"] = "missing";
try { CampaignSlotStorage.Save(definition, badState, legacyPath); throw new Exception("Invalid runtime state was accepted"); }
catch (CampaignInvariantException) { checks++; }
Check(File.ReadAllText(legacyPath) == legacyRaw, "Definition/state defect replaced the old save");
var manualPath = Slot("manual-application-save.json");
NewFile(manualPath, "manual battle state must remain untouched");
CampaignSlotStorage.Save(definition, initial, legacyPath);
Check(!CampaignSlotStorage.Load(definition, legacyPath).LegacyApplicationEnvelope, "New writes must use the original campaign envelope");
CampaignSlotStorage.ClearForNewCampaign(legacyPath);
Check(!File.Exists(legacyPath), "Caller-confirmed New must clear only its explicit autosave slot");
CampaignSlotStorage.ClearForNewCampaign(legacyPath);
Check(File.ReadAllText(manualPath) == "manual battle state must remain untouched", "Autosave/New changed the manual slot");
var invalidPath = Slot("unreadable-new-choice.json"); NewFile(invalidPath, "not json");
Check(CampaignSlotStorage.Load(definition, invalidPath).Rejected is not null && File.ReadAllText(invalidPath) == "not json", "Rejected load cannot clear the slot");
CampaignSlotStorage.ClearForNewCampaign(invalidPath);
Check(!File.Exists(invalidPath), "Explicit new-campaign intent may clear an invalid slot");
var undeletable = Slot("clear-failure"); Directory.CreateDirectory(undeletable);
NewFile(Path.Combine(undeletable, "retained"), "retained");
try { CampaignSlotStorage.ClearForNewCampaign(undeletable); throw new Exception("New-game clear failure was swallowed"); }
catch (Exception error) when (error is IOException or UnauthorizedAccessException) { checks++; }
Check(File.ReadAllText(Path.Combine(undeletable, "retained")) == "retained", "Failed clear removed unrelated contents");

var utf16 = (JsonObject)initial.DeepClone(); utf16["variables"]!["boundary"] = "\ud800 saved \udfff";
CampaignSlotStorage.Save(definition, utf16, legacyPath);
Same(CampaignSlotStorage.Load(definition, legacyPath).State, utf16, "Storage bypassed the canonical UTF-16 boundary");
foreach (var path in new[] { "relative.json", "user://autosave.json", "" })
{
    try { CampaignSlotStorage.Load(definition, path); throw new Exception("Relative/ambient path accepted"); }
    catch (ArgumentException) { checks++; }
}
foreach (var item in oracle["menuCases"]!.AsArray().OfType<JsonObject>())
{
    var loaded = new LoadedCampaign(item["state"] as JsonObject, item["rejected"]?.GetValue<string>());
    foreach (var data in new[] { metadata, exported })
    {
        var view = CampaignMenu.Create(loaded, EcmaJson.ToElement(data));
        var expected = item["expected"]!.AsObject();
        Check(view.Title == expected["title"]!.GetValue<string>(), "Menu title source mismatch");
        Check(view.Kicker == expected["kicker"]!.GetValue<string>() && view.Description == expected["description"]!.GetValue<string>(), "Menu prose mismatch");
        Check(view.ArtAlt == expected["artAlt"]!.GetValue<string>() && view.ArtSource == expected["artSource"]!.GetValue<string>(), "Original title art identity mismatch");
        Check(view.CanContinue == expected["canContinue"]!.GetValue<bool>(), "Continue visibility mismatch");
        if (view.CanContinue) Check(view.ContinueLabel == expected["continueLabel"]!.GetValue<string>(), "Continue text changed with status");
        Check(view.NewLabel == expected["newLabel"]!.GetValue<string>() && view.NewPrimary == expected["newPrimary"]!.GetValue<bool>(), "New/restart label/primary mismatch");
        Check(view.Progress == expected["progress"]?.GetValue<string>(), "Continue battle progress mismatch");
        Check(view.RejectedNotice == expected["notice"]?.GetValue<string>(), "Unreadable-save notice must preserve exact source text, not HTML entities");
        if (item["prompt"] is not null) Check(view.ReplacePrompt == item["prompt"]!.GetValue<string>(), "Replacement confirmation is not the source prompt");
    }
    var needsConfirmation = loaded.State is not null;
    Check(item["declined"]!["confirmations"]!.GetValue<int>() == (needsConfirmation ? 1 : 0), "Live source confirmation expectation changed");
    Check(item["declined"]!["deletes"]!.GetValue<int>() == (needsConfirmation ? 0 : 1), "Live source new-game deletion expectation changed");
    Check(item["accepted"]!["deletes"]!.GetValue<int>() == (needsConfirmation ? 1 : 2), "Live source acceptance must initiate new game");
    if (needsConfirmation) Check(item["continuedOriginalState"]!.GetValue<bool>(), "Live source Continue must use loaded state");
}
var changedMetadata = (JsonObject)metadata.DeepClone(); changedMetadata["battleTotal"] = 27;
Check(CampaignMenu.Create(new(initial, null), EcmaJson.ToElement(changedMetadata)).Progress == "0/27", "Projection hardcodes the shipped battle count instead of metadata");
var atomicPath = Slot("atomic-reader.json"); CampaignSlotStorage.Save(definition, initial, atomicPath);
var reading = true;
var reader = Task.Run(() =>
{
    while (Volatile.Read(ref reading))
    {
        var loaded = CampaignSlotStorage.Load(definition, atomicPath);
        if (loaded.State is null || loaded.Rejected is not null) throw new Exception("Concurrent reader observed missing/partial autosave");
    }
});
for (var count = 0; count < 30; count++) CampaignSlotStorage.Save(definition, initial, atomicPath);
Volatile.Write(ref reading, false); reader.GetAwaiter().GetResult(); checks++;
Console.WriteLine($"Campaign autosave/menu source checks passed: {checks} assertions. Only fresh isolated files under {root} were touched.");
