using System.IO.Compression;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

using var inputStream = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal)
    ? new StreamReader(new GZipStream(inputStream, CompressionMode.Decompress)) : new StreamReader(inputStream);
var input = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var engine = new BattleEngine(input["packs"]!.AsArray());
var checks = 0;
void Check(string name, string subject, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (EcmaJson.DeepEquals(actual, expected))
        return;
    var destination = Path.Combine(Path.GetDirectoryName(args[0])!, "failed-casting-" + subject + ".json");
    File.WriteAllText(destination, new JsonObject
    {
        ["case"] = name,
        ["actual"] = actual?.DeepClone(),
        ["expected"] = expected?.DeepClone()
    }.ToJsonString(new()
    {
        WriteIndented = true
    }));
    throw new Exception($"Mismatch in {name}: {subject}; inspect {destination}");
}
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name");
    var state = item["initial"]!.DeepClone().AsObject();
    foreach (var stage in item["stages"]!.AsArray().OfType<JsonObject>())
    {
        var operation = stage["operation"]!.AsObject();
        var kind = StateJson.Text(operation, "type");
        var events = new JsonArray();
        var eventHashes = new JsonArray();
        JsonNode? error = null;
        JsonNode? returned = null;
        JsonNode? sameStoredObject = null;
        JsonNode? savedState = null;
        void Emit(JsonObject value)
        {
            events.Add(value);
            eventHashes.Add(StateDigest.Hash(state));
        }
        try
        {
            switch (kind)
            {
                case "declare":
                    var caster = TacticalQueries.RequireUnit(state, StateJson.Integer(operation, "caster"));
                    var cast = engine.Combat.BeginCast(state, new(caster, StateJson.Text(operation, "ability"),
                        operation["weapon"]!.DeepClone().AsObject(), TacticalQueries.Position(operation["target"]!.AsObject()),
                        TacticalQueries.Position(operation["origin"]!.AsObject())), Emit);
                    sameStoredObject = ReferenceEquals(cast, state["pendingCasts"]!.AsArray().Last());
                    returned = cast.DeepClone();
                    if (StateJson.IsTrue(operation["mutateEvent"]))
                        events.Last()!["at"]!["x"] = -888;
                    break;
                case "clock":
                    state["actorTurns"] = operation["at"]!.DeepClone();
                    break;
                case "resolve":
                    engine.Combat.ResolveDueCasts(state, Emit);
                    break;
                case "saveLoad":
                    var before = state.DeepClone();
                    var save = engine.SaveBattle(state, "2026-10-20T00:00:00.000Z");
                    Check(name, "save-input-unchanged", state, before);
                    savedState = save["state"]!.DeepClone();
                    var storedBefore = save.DeepClone();
                    state = engine.LoadBattle(save);
                    Check(name, "load-input-unchanged", save, storedBefore);
                    // The native load owns a detached copy. Reading it must not normalize metadata.
                    Check(name, "loaded-state", state, savedState);
                    break;
                case "cooldown":
                    state["units"]![0]!["weaponState"]!["mage_meteor"]!["cooldownRemaining"] = 3;
                    break;
                case "vacate":
                    state["units"]![1]!["x"] = 6;
                    state["units"]![1]!["y"] = 0;
                    break;
                case "moveCaster":
                    state["units"]![0]!["x"] = 0;
                    state["units"]![0]!["y"] = 0;
                    break;
                case "protect":
                    state["scenario"]!["zones"]!["sanctuary"] = new JsonArray(operation["at"]!.DeepClone());
                    state["scenario"]!["engagementRules"]!.AsArray().Add(new JsonObject
                    {
                        ["id"] = "truce",
                        ["zone"] = "sanctuary",
                        ["mode"] = "no-attacks",
                        ["players"] = new JsonArray(1)
                    });
                    break;
                case "depart":
                    var departing = state["units"]![0]!.AsObject();
                    state["units"]!.AsArray().Remove(departing);
                    if (StateJson.IsTrue(operation["announce"]))
                        engine.Combat.Damage.AnnounceDeparture(state, departing, Emit);
                    break;
                case "reverse":
                    var reversed = state["pendingCasts"]!.AsArray().Reverse().Select(value => value!.DeepClone()).ToArray();
                    state["pendingCasts"] = new JsonArray(reversed);
                    break;
                default:
                    throw new Exception($"unknown operation {kind}");
            }
        }
        catch (IllegalActionException caught)
        {
            error = new JsonObject { ["kind"] = "IllegalAction", ["refusal"] = System.Text.Json.JsonSerializer.SerializeToNode(caught.Refusal) };
        }
        catch (Exception caught) when (caught is DomainInvariantException or StoredDocumentException)
        {
            error = new JsonObject
            {
                ["kind"] = caught.GetType().Name.Replace("Exception", "", StringComparison.Ordinal),
                ["message"] = caught.Message
            };
        }
        JsonNode? expectedError = stage["error"];
        if (expectedError?["kind"]?.GetValue<string>() == "IllegalAction")
        {
            if (name != "duplicate cast is illegal and unchanged")
                throw new Exception("Unspecified semantic refusal expectation: " + name);
            expectedError = new JsonObject
            {
                ["kind"] = "IllegalAction",
                ["refusal"] = new JsonObject
                {
                    ["code"] = "combat.already_casting",
                    ["details"] = new JsonObject { ["unitId"] = StateJson.Integer(operation, "caster") }
                }
            };
        }
        Check(name, "error", error, expectedError);
        Check(name, "events", events, stage["events"]);
        Check(name, "event-hashes", eventHashes, stage["eventHashes"]);
        Check(name, "returned", returned, stage["returned"]);
        Check(name, "stored-identity", sameStoredObject, stage["sameStoredObject"]);
        Check(name, "saved-state", savedState, stage["savedState"]);
        Check(name, "state", state, stage["after"]);
        Check(name, "hash", JsonValue.Create(StateDigest.Hash(state)), stage["hash"]);
    }
}
Console.WriteLine($"PASS casting declarations: {input["cases"]!.AsArray().Count} source cases, {checks} exact comparisons");

// Native persistence deliberately rejects malformed cast records earlier than the source reader.
// Preserve these existing structural/timing constraints without treating metadata as a capability.
var nativeFixture = input["cases"]![0]!["initial"]!.DeepClone().AsObject();
var nativeOperation = input["cases"]![0]!["stages"]![0]!["operation"]!.AsObject();
engine.Combat.BeginCast(nativeFixture, new(nativeFixture["units"]![0]!.AsObject(), "unregistered.metadata",
    nativeOperation["weapon"]!.DeepClone().AsObject(), new Coord(4, 3), new Coord(1, 3)), _ => { });
var validSave = engine.SaveBattle(nativeFixture, "2026-10-20T00:00:00.000Z");
var nativeGuards = 0;
foreach (var edit in new Action<JsonObject>[]
{
    save => save["state"]!["pendingCasts"]![0]!["ability"] = 42,
    save => save["state"]!["pendingCasts"]![0]!["caster"] = "1",
    save => save["state"]!["pendingCasts"]![0]!["resolveAt"] = save["state"]!["pendingCasts"]![0]!["declaredAt"]!.DeepClone(),
    save => save["state"]!["pendingCasts"] = new JsonObject(),
})
{
    var invalid = validSave.DeepClone().AsObject();
    edit(invalid);
    var before = invalid.DeepClone();
    try
    {
        engine.LoadBattle(invalid);
        throw new Exception("native cast structure guard accepted invalid data");
    }
    catch (StoredDocumentException) { }
    Check("native structural guard", "input-unchanged", invalid, before);
    nativeGuards++;
}
Console.WriteLine($"PASS {nativeGuards} separate native cast structural/timing guards");
