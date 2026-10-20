using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transport;
using Empire.Core.Transactions;
using Empire.Core.Engine;

if (args.Length != 1) throw new ArgumentException("Usage: TransportTests <oracle.json>");
var oraclePath = args[0];
var oracle = JsonNode.Parse(File.ReadAllText(oraclePath))!.AsObject();
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
JsonNode? Snapshot(object? value) => JsonSerializer.SerializeToNode(value, serialization);
int IntegerOr(JsonObject value, string key, int fallback) => value[key] is null ? fallback : StateJson.Integer(value, key);
void Check(string name, string key, JsonNode? actual, JsonNode? expected)
{
    if (JsonNode.DeepEquals(JsonNode.Parse(actual?.ToJsonString() ?? "null"), expected)) return;
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(oraclePath)!, $"failed-{key}.json"),
        new JsonObject { ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }.ToJsonString());
    throw new Exception($"Mismatch in {name}: {key}");
}
var passed = 0;
foreach (var item in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = item["name"]!.GetValue<string>();
    var packs = oracle["packs"]!.DeepClone().AsArray();
    foreach (var patch in item["patches"]!.AsArray().OfType<JsonObject>())
    {
        var family = StateJson.Text(patch, "family");
        var definition = packs.OfType<JsonObject>().SelectMany(pack => pack[family]?.AsArray().OfType<JsonObject>() ?? [])
            .First(entry => StateJson.Text(entry, "id") == StateJson.Text(patch, "id"));
        foreach (var property in patch["values"]!.AsObject()) definition[property.Key] = property.Value?.DeepClone();
    }
    var state = item["initial"]!.DeepClone().AsObject();
    var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid());
    var transport = new BattleTransport(packs, grids);
    var damage = new BattleDamage(packs, grids);
    var events = new JsonArray();
    var hashes = new JsonArray();
    void Emit(JsonObject value) { events.Add(value); hashes.Add(StateDigest.Hash(state)); }
    object? result = null;
    JsonObject? error = null;
    try
    {
        foreach (var step in item["steps"]!.AsArray().OfType<JsonObject>())
        {
            var unitId = IntegerOr(step, "unit", 1);
            var carrierId = IntegerOr(step, "carrier", 2);
            var unit = TacticalQueries.UnitById(state, unitId);
            var carrier = TacticalQueries.UnitById(state, carrierId);
            Coord At() => TacticalQueries.Position(step["at"]!.AsObject());
            switch (StateJson.Text(step, "op"))
            {
                case "query":
                    result = new { passengers = BattleTransport.PassengersOf(state, carrierId),
                        carriers = transport.CarrierOptions(state, unit!), passengersOptions = transport.PassengerOptions(state, carrier!) };
                    break;
                case "embarkRefusal": result = transport.EmbarkRefusal(state, unit!, carrier!); break;
                case "disembarkRefusal": result = transport.DisembarkRefusal(state, carrierId, unitId, At()); break;
                case "embark": transport.Embark(state, unitId, carrierId, Emit); break;
                case "disembark": transport.Disembark(state, carrierId, unitId, At(), Emit); break;
                case "damage":
                    var victim = IntegerOr(step, "unit", 2);
                    result = damage.Resolve(state, victim, StateJson.Number(step["amount"], "amount"), false,
                        blow => new JsonObject { ["type"] = "statusTick", ["unit"] = victim, ["status"] = "poisoned",
                            ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter }, Emit);
                    break;
                case "morale": result = damage.ChangeMorale(state, IntegerOr(step, "unit", 2), StateJson.Number(step["amount"], "amount"), "test", Emit); break;
                case "surrender": result = damage.Surrender(state, IntegerOr(step, "unit", 2), step["to"] is null ? null : StateJson.Integer(step, "to"), Emit); break;
                case "withdraw": damage.Withdraw(state, IntegerOr(step, "unit", 2), StateJson.IsTrue(step["leaveCorpse"]), Emit); break;
                case "return":
                    var marker = step["externalMarker"]?.AsObject() ?? state["markers"]!.AsArray().OfType<JsonObject>().First(entry => StateJson.Integer(entry, "id") == StateJson.Integer(step, "marker"));
                    result = BattleUnitReturn.Restore(state, marker, new UnitReturnRequest(At(),
                        step["owner"] is null ? null : StateJson.Integer(step, "owner"), step["hp"] is null ? null : StateJson.Number(step["hp"], "hp")), Emit);
                    break;
                case "embarkClone":
                    transport.Embark(state, unitId, carrierId, Emit);
                    var snapshot = BattleTransport.PassengersOf(state, carrierId).First(entry => StateJson.Integer(entry, "id") == unitId);
                    snapshot["morale"]!["current"] = 1; snapshot["statuses"]![0]!["remaining"] = 99;
                    snapshot["resources"]!["momentum"]!["current"] = 2;
                    snapshot["weaponState"]!["c01.border-blade"]!["cooldownRemaining"] = 3;
                    snapshot["career"]!["mastery"]!["test"] = 100;
                    snapshot["directive"]!["waypoints"]![0]!["x"] = 6;
                    snapshot["learnedAbilities"]!.AsArray().Add("heal");
                    result = unit;
                    break;
                default: throw new Exception("Unknown test operation");
            }
        }
    }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
    {
        error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", "", StringComparison.Ordinal), ["message"] = caught.Message };
    }
    Check(name, "error", error, item["error"]);
    Check(name, "result", Snapshot(result), item["result"]);
    Check(name, "events", events, item["events"]);
    Check(name, "hashes", hashes, item["hashes"]);
    Check(name, "after", state, item["after"]);
    passed++;
}
Console.WriteLine($"PASS {passed} live TypeScript transport/departure/return cases: queries, refusals, results, events, every emission-time hash and complete states");
var engine = new GrayFlagEngine(oracle["packs"]!.AsArray());
// Test-only access composes the real dispatcher around deliberately adversarial snapshots,
// without weakening the public save reader or adding a production restore bypass.
var registry = typeof(GrayFlagEngine).GetMethod("Registry", BindingFlags.Instance | BindingFlags.NonPublic)!;
var dispatchCount = 0;
foreach (var item in oracle["dispatchCases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name");
    var battle = new Battle(item["initial"]!.AsObject(), (BattleActionRegistry)registry.Invoke(engine, null)!);
    foreach (var expected in item["receipts"]!.AsArray().OfType<JsonObject>())
    {
        Check(name, "dispatch-before", battle.Snapshot(), expected["before"]);
        var events = new JsonArray();
        JsonObject? error = null;
        try
        {
            var receipt = battle.Dispatch(expected["action"]!.AsObject());
            Check(name, "receipt-before", receipt.Before, expected["before"]);
            foreach (var ev in receipt.Events) events.Add(ev.DeepClone());
        }
        catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
        {
            error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", "", StringComparison.Ordinal), ["message"] = caught.Message };
        }
        Check(name, "dispatch-error", error, expected["error"]);
        Check(name, "dispatch-events", events, expected["events"]);
        Check(name, "dispatch-after", battle.Snapshot(), expected["after"]);
        Check(name, "dispatch-hash", JsonValue.Create(battle.StateHash), expected["hash"]);
        dispatchCount++;
    }
}
Console.WriteLine($"PASS {dispatchCount} live TypeScript full-dispatch steps, including entitlement, action completion, lifecycle and transaction rollback");
var saveCount = 0;
foreach (var item in oracle["saveCases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name");
    var document = item["document"]!.AsObject();
    Check(name, "save-document", engine.SaveBattle(document["state"]!.AsObject(), StateJson.Text(document, "savedAt")), document);
    var battle = engine.LoadSavedBattle(JsonNode.Parse(document.ToJsonString()));
    foreach (var expected in item["receipts"]!.AsArray().OfType<JsonObject>())
    {
        Check(name, "resumed-before", battle.Snapshot(), expected["before"]);
        var receipt = battle.Dispatch(expected["action"]!.AsObject());
        Check(name, "resumed-events", Snapshot(receipt.Events), expected["events"]);
        Check(name, "resumed-after", battle.Snapshot(), expected["after"]);
        Check(name, "resumed-hash", JsonValue.Create(battle.StateHash), expected["hash"]);
        saveCount++;
    }
}
Console.WriteLine($"PASS {oracle["saveCases"]!.AsArray().Count} source-authored transport saves and {saveCount} resumed dispatch steps");
var validSave = oracle["saveCases"]![0]!["document"]!.AsObject();
var hardening = new (string Name, Action<JsonObject> Mutate)[]
{
    ("missing carrier", state => state["embarkedUnits"]![0]!["carrier"] = 999),
    ("missing carrier reference", state => state["embarkedUnits"]![0]!.AsObject().Remove("carrier")),
    ("nontransport carrier", state => state["embarkedUnits"]![0]!["carrier"] = 3),
    ("carrier as passenger", state => state["embarkedUnits"]![0]!["unit"]!["type"] = "c01.supply-wagon"),
    ("duplicate living identity", state => state["embarkedUnits"]![0]!["unit"]!["id"] = 2),
    ("duplicate living key", state => state["embarkedUnits"]![0]!["unit"]!["key"] = "test-2"),
    ("unknown passenger owner", state => state["embarkedUnits"]![0]!["unit"]!["owner"] = 999),
    ("dead passenger", state => state["embarkedUnits"]![0]!["unit"]!["hp"] = 0),
    ("missing passenger runtime field", state => state["embarkedUnits"]![0]!["unit"]!.AsObject().Remove("morale")),
    ("unsupported nested status", state => state["embarkedUnits"]![0]!["unit"]!["statuses"]!.AsArray().Add(new JsonObject { ["id"] = "missing-status", ["remaining"] = 2, ["stacks"] = 1 })),
};
foreach (var test in hardening)
{
    var malformed = validSave.DeepClone().AsObject();
    test.Mutate(malformed["state"]!.AsObject());
    var before = malformed.DeepClone();
    try { engine.LoadSavedBattle(malformed); throw new Exception($"Malformed transport save accepted: {test.Name}"); }
    catch (StoredDocumentException) { }
    Check(test.Name, "reader-does-not-mutate", malformed, before);
}
Console.WriteLine($"PASS {hardening.Length} native malformed transport-save guards (explicit hardening beyond source reader)");
