using Empire.Core;
using Empire.Core.Progression;
using Empire.Core.Lifecycle;
using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.State;

if (args.Length != 2) throw new ArgumentException("Usage: DamageTests <repository> <oracle.json>");
var root = args[0];
var oraclePath = args[1];
var packs = JsonNode.Parse(File.ReadAllText(root + "/godot/data/content-packs.json"))!.AsArray();
var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid());
var damage = new BattleDamage(new BattleDamageRules(new ReadOnlyContent(packs), grids, new CombatEffects(new ReadOnlyContent(packs), new ThresholdRankProgressionPolicy()), DefaultUnitDepartureHandlers.Create().Seal()));
var cases = JsonNode.Parse(File.ReadAllText(oraclePath))!.AsArray();
int passed = 0;
int nativeRejections = 0;
JsonObject Trace(JsonObject state) => new()
{
    ["units"] = new JsonArray(state["units"]!.AsArray().Select(n => n!["id"]!.DeepClone()).ToArray()),
    ["markers"] = new JsonArray(state["markers"]!.AsArray().Select(n => n!["id"]!.DeepClone()).ToArray()),
    ["passengers"] = new JsonArray(state["embarkedUnits"]!.AsArray().Select(n => n!["unit"]!["id"]!.DeepClone()).ToArray()),
    ["casts"] = new JsonArray(state["pendingCasts"]!.AsArray().Select(n => n!["caster"]!.DeepClone()).ToArray()),
    ["capture"] = state["map"]!["captureProgress"]!.AsArray()[StateJson.Integer(state["map"]!["width"], "width")]!.DeepClone(),
};
void Check(string name, string key, JsonNode? actual, JsonNode? expected)
{
    if (JsonNode.DeepEquals(JsonNode.Parse(actual?.ToJsonString() ?? "null"), expected)) return;
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(oraclePath)!, $"failed-{key}.json"), new JsonObject { ["actual"] = actual?.DeepClone(), ["expected"] = expected?.DeepClone() }.ToJsonString());
    throw new Exception($"Mismatch in {name}: {key}");
}
foreach (var item in cases.OfType<JsonObject>())
{
    var name = item["name"]!.GetValue<string>();
    var state = item["initial"]!.DeepClone().AsObject();
    var parameters = item["args"]!.AsObject();
    var events = new JsonArray(); var traces = new JsonArray();
    void Emit(JsonObject ev) { events.Add(ev); traces.Add(Trace(state)); }
    var id = StateJson.Integer(parameters, "unit");
    JsonNode? outcome = null;
    if (name is "damage -100" or "damage -0.5")
    {
        try
        {
            damage.Resolve(state, id, StateJson.Number(parameters["amount"], "amount"), false,
                _ => throw new Exception("Invalid damage reached reporting"), Emit);
            throw new Exception($"Invalid damage was accepted: {name}");
        }
        catch (DomainInvariantException) { }
        Check(name, "native-events", events, new JsonArray());
        Check(name, "native-traces", traces, new JsonArray());
        Check(name, "native-atomic-state", state, item["initial"]);
        nativeRejections++;
        continue;
    }
    switch (item["op"]!.GetValue<string>())
    {
        case "resolve":
            var blow = damage.Resolve(state, id, StateJson.Number(parameters["amount"], "amount"), StateJson.IsTrue(parameters["nonlethal"]),
                result => new JsonObject { ["type"] = "statusTick", ["unit"] = id, ["status"] = "poisoned", ["amount"] = result.Amount, ["hpAfter"] = result.HpAfter }, Emit);
            outcome = new JsonObject { ["landed"] = blow.Landed, ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter,
                ["killed"] = blow.Killed, ["leftField"] = blow.LeftField, ["at"] = TacticalQueries.CoordinateJson(blow.At) };
            break;
        case "status":
            var subject = state["units"]![0]!.DeepClone().AsObject(); subject["id"] = id;
            outcome = JsonValue.Create(damage.DamageStatus(state, subject, new JsonObject { ["id"] = "poisoned", ["remaining"] = 1, ["stacks"] = 1 }, StateJson.Number(parameters["amount"],"amount"), StateJson.IsTrue(parameters["nonlethal"]), Emit));
            break;
        case "morale": outcome = JsonValue.Create(damage.ChangeMorale(state, id, StateJson.Number(parameters["amount"], "amount"), "test", Emit)); break;
        case "surrender": outcome = damage.Surrender(state, id, parameters["to"] is null ? null : StateJson.Integer(parameters,"to"), Emit); break;
        case "withdraw": damage.Withdraw(state, id, StateJson.IsTrue(parameters["leaveCorpse"]), Emit); break;
        default: throw new Exception("Unknown operation");
    }
    Check(name, "events", events, item["events"]);
    Check(name, "traces", traces, item["traces"]);
    Check(name, "outcome", outcome, item["outcome"]);
    Check(name, "after", state, item["after"]);
    passed++;
}
Console.WriteLine($"PASS {passed} TypeScript-oracle damage/departure cases (events, intermediate traces, outcomes, complete states); {nativeRejections} explicit native invalid-damage rejections");
if (nativeRejections != 2) throw new Exception("Expected two deliberately hardened negative-damage cases");
