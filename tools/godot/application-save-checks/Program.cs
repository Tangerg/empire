using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Determinism;
using Empire.Core.Engine;

var root = args.Length == 1 ? Path.GetFullPath(args[0]) : throw new ArgumentException("Pass the repository root.");
JsonNode Read(string name) => EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot", "data", name)))!;
var definition = Read("grayflag-campaign.json").AsObject();
var levels = Read("grayflag-campaign-levels.json").AsArray();
var engine = new GrayFlagEngine(Read("content-packs.json").AsArray());
var battleNode = definition["nodes"]!.AsArray().OfType<JsonObject>().First(node => node["type"]!.GetValue<string>() == "battle");
battleNode["effects"] = new JsonArray(new JsonObject { ["type"] = "addVariable", ["key"] = "saveAdmissionCount", ["amount"] = 1 });
GrayFlagCampaign Create() => new(definition, levels, engine);
var assertions = 0;
void Require(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Same(JsonNode? actual, JsonNode? expected, string message) => Require(EcmaJson.DeepEquals(actual, expected), message);
JsonObject Facts(GrayFlagCampaign value)
{
    var facts = value.Save();
    facts["campaign"]!.AsObject().Remove("savedAt");
    (facts["battle"] as JsonObject)?.Remove("savedAt");
    return facts;
}
void EnterBattle(GrayFlagCampaign value)
{
    while (value.Node()["type"]!.GetValue<string>() != "battle")
    {
        if (value.Node()["type"]!.GetValue<string>() == "choice") value.Choose(value.Choices()[0]!["id"]!.GetValue<string>());
        else value.Advance();
    }
    value.BeginBattle();
}
void Act(GrayFlagCampaign value)
{
    var battle = value.Battle!;
    if (battle.IsComputerTurn) Require(battle.AdvanceComputer(), "Expected a computer commitment.");
    else Require(battle.Dispatch(EcmaJson.ToElement(engine.Ai.ChooseAction(battle.Snapshot())), battle.GetSnapshot().Revision).Accepted,
        "Expected a legal planned player order.");
}
var campaign = Create(); EnterBattle(campaign);
var request = campaign.RequestSnapshot()!;
var opening = campaign.Battle!.Snapshot();
var pending = campaign.Snapshot();
Require(pending["variables"]!["saveAdmissionCount"]!.GetValue<double>() == 1, "Battle-node effect did not run exactly once.");
for (var i = 0; i < 8; i++) Act(campaign);
var played = campaign.Save();
Require(!played.ContainsKey("request") && !played.ContainsKey("atCamp"), "Derived request/navigation duplicates were persisted.");
var restored = Create(); restored.Load(EcmaJson.Parse(EcmaJson.Stringify(played)));
Same(restored.Snapshot(), pending, "Readmission repeated node effects or changed sequence.");
Same(restored.RequestSnapshot(), request, "Readmission did not derive the exact original request.");
Same(restored.Battle!.Snapshot(), campaign.Battle.Snapshot(), "Played state did not survive native save readmission.");
Require(restored.Battle.GetSnapshot().Transition is null, "Load replayed historical presentation.");

var escapedRequest = restored.RequestSnapshot()!;
escapedRequest.Remove("level");
escapedRequest["rosterBindings"] = new JsonArray();
escapedRequest["perspectivePlayer"] = 999;
var escapedLevel = restored.BattleLevel(); escapedLevel["id"] = "forged";
var escapedState = restored.Snapshot(); escapedState["battleSequence"] = -1;
Same(restored.RequestSnapshot(), request, "A request read model mutated the authoritative pending battle.");
Same(restored.Snapshot(), pending, "A campaign read model mutated owned state.");
restored.RestartBattle();
Same(restored.Battle!.Snapshot(), opening, "Mutating a detached request changed restart semantics.");
restored.Load(played);

void Reject(JsonNode? invalid, string reason)
{
    var before = Facts(restored);
    var beforeRequest = restored.RequestSnapshot();
    var beforeBattle = restored.Battle;
    var refused = false;
    try { restored.Load(invalid); }
    catch (StoredDocumentException) { refused = true; }
    Require(refused, "Accepted " + reason);
    Require(ReferenceEquals(restored.Battle, beforeBattle), "Failed load replaced active session: " + reason);
    Same(restored.RequestSnapshot(), beforeRequest, "Failed load changed request: " + reason);
    Same(Facts(restored), before, "Failed load changed campaign/battle/location: " + reason);
}
Reject(null, "null application document");
Reject(new JsonArray(), "array application document");
var oldFormat = played.DeepClone().AsObject(); oldFormat["schema"] = 1;
Reject(oldFormat, "obsolete application schema");
foreach (var field in new[] { "schema", "campaign", "mode", "battle", "events" })
{
    var invalid = played.DeepClone().AsObject(); invalid.Remove(field); Reject(invalid, "missing " + field);
}
foreach (var bad in new JsonNode?[] { null, JsonValue.Create(true), JsonValue.Create(1), JsonValue.Create("unknown") })
{
    var invalid = played.DeepClone().AsObject(); invalid["mode"] = bad?.DeepClone(); Reject(invalid, "invalid mode");
}
foreach (var defect in new[] { "level omission", "binding loss", "foreign perspective" })
{
    var injected = request.DeepClone().AsObject();
    if (defect == "level omission") injected.Remove("level");
    else if (defect == "binding loss") injected["rosterBindings"] = new JsonArray();
    else injected["perspectivePlayer"] = 999;
    var invalid = played.DeepClone().AsObject(); invalid["request"] = injected;
    Reject(invalid, "obsolete independently editable request with " + defect);
}
var falseCamp = played.DeepClone().AsObject(); falseCamp["mode"] = "camp";
Reject(falseCamp, "forged camp opening containing played battle state");
var wrongSequence = played.DeepClone().AsObject(); wrongSequence["campaign"]!["state"]!["battleSequence"] = 11;
Reject(wrongSequence, "pending request and sequence mismatch");
var wrongRequest = played.DeepClone().AsObject(); wrongRequest["campaign"]!["state"]!["pendingBattle"]!["requestId"] = "wrong";
Reject(wrongRequest, "pending request identity mismatch");
foreach (var bad in new JsonNode[] { JsonValue.Create(1)!, JsonValue.Create("")!, new JsonObject(), new JsonArray() })
{
    var invalid = played.DeepClone().AsObject();
    invalid["campaign"]!["state"]!["roster"]!.AsObject().First().Value!["career"] = bad;
    Reject(invalid, "malformed campaign roster career");
}
foreach (var omitted in new[] { false, true })
{
    var valid = played.DeepClone().AsObject();
    var member = valid["campaign"]!["state"]!["roster"]!.AsObject().First().Value!.AsObject();
    if (omitted) member.Remove("career"); else member["career"] = null;
    var withoutCareer = Create(); withoutCareer.Load(valid); withoutCareer.RestartBattle();
    Require(withoutCareer.HasPendingBattle, "Null/omitted optional roster career was rejected.");
}
foreach (var field in new[] { "unitType", "career" })
{
    var invalid = played.DeepClone().AsObject();
    invalid["campaign"]!["state"]!["roster"]!.AsObject().First().Value![field] = "unknown-content";
    Reject(invalid, "unknown restart-opening roster " + field);
}
foreach (var bad in new JsonNode?[] { null, JsonValue.Create(1), JsonValue.Create(""), new JsonObject() })
{
    var invalid = played.DeepClone().AsObject(); invalid["events"] = new JsonArray(new JsonObject { ["type"] = bad?.DeepClone() });
    Reject(invalid, "invalid event type");
    invalid = played.DeepClone().AsObject(); invalid["events"] = new JsonArray(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = bad?.DeepClone() });
    Reject(invalid, "invalid settlement signal");
}
restored.ExitBattle();
var camp = restored.Save();
Require(camp["mode"]!.GetValue<string>() == "camp" && camp["battle"] is null && camp["events"]!.AsArray().Count == 0,
    "Camp persisted a second authoritative opening.");
var campReloaded = Create(); campReloaded.Load(camp);
Require(campReloaded.AtCamp && campReloaded.HasPendingBattle, "Camp location was not restored.");
Same(campReloaded.RequestSnapshot(), request, "Camp regenerated different bindings/context.");
Same(campReloaded.Battle!.Snapshot(), opening, "Camp did not regenerate the original opening.");
Same(campReloaded.Snapshot(), pending, "Camp readmission replayed node effects.");
campReloaded.BeginBattle();
Same(campReloaded.Battle!.Snapshot(), opening, "Camp reentry changed the opening.");
Same(campReloaded.Snapshot(), pending, "Camp reentry advanced the pending campaign twice.");

var directory = Path.Combine(Path.GetTempPath(), "empire-application-save-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var slot = Path.Combine(directory, "manual.json");
    SaveFile.WriteDocument(slot, played);
    var disk = Create(); disk.Load(SaveFile.ReadDocument(slot));
    Same(disk.RequestSnapshot(), request, "Disk read did not regenerate the canonical request.");
    var original = File.ReadAllText(slot);
    var defectiveLive = new JsonObject { ["notFinite"] = double.NaN };
    var defectEscaped = false;
    try { SaveFile.WriteDocument(slot, defectiveLive); }
    catch (ArgumentException) { defectEscaped = true; }
    Require(defectEscaped && File.ReadAllText(slot) == original, "A live serialization defect became a storage failure or damaged the old slot.");
    var blocked = Path.Combine(directory, "blocked"); Directory.CreateDirectory(blocked);
    var classified = false;
    try { SaveFile.WriteDocument(blocked, played); }
    catch (SaveStorageException error) { classified = error.Operation == SaveFileOperation.Write && error.FilePath == blocked; }
    Require(classified && Directory.Exists(blocked), "Expected filesystem failure lacked its classified operation/target.");
    Require(!Directory.EnumerateFiles(directory, ".*.tmp").Any(), "Failed write left temporary debris.");
    File.WriteAllText(slot, "{not json");
    try { SaveFile.ReadDocument(slot); throw new Exception("Malformed file was accepted."); }
    catch (StoredDocumentException) { assertions++; }
    File.WriteAllText(slot, "{\"tooLarge\":1e500}");
    try { SaveFile.ReadDocument(slot); throw new Exception("Non-finite external number was accepted."); }
    catch (StoredDocumentException error) { Require(error.InnerException is System.Text.Json.JsonException, "Non-finite input was not classified at the parser boundary."); }
    File.WriteAllBytes(slot, new byte[] { 0xff });
    try { SaveFile.ReadDocument(slot); throw new Exception("Invalid UTF-8 was accepted."); }
    catch (StoredDocumentException) { assertions++; }
}
finally { Directory.Delete(directory, recursive: true); }
Console.WriteLine($"PASS {assertions} native application-admission assertions: derived request, read-model isolation, atomic rejections, canonical camp opening, effects once, classified files.");
