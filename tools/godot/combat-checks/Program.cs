using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

if (args.Length != 1) throw new ArgumentException("Usage: CombatTests <oracle.json>");
var input = JsonNode.Parse(File.ReadAllText(args[0]))!.AsObject();
var originalPacks = input["packs"]!.AsArray();
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var count = 0;
JsonNode? Serialize<T>(T value) => JsonSerializer.SerializeToNode(value, serialization);
// protectedUnit is an optional TS property. Other nulls remain meaningful.
void NormalizeOptionalProperties(JsonNode? node)
{
    if (node is JsonObject obj)
    {
        if (obj.ContainsKey("protectedUnit") && obj["protectedUnit"] is null) obj.Remove("protectedUnit");
        foreach (var child in obj.ToArray()) NormalizeOptionalProperties(child.Value);
    }
    else if (node is JsonArray array) foreach (var child in array) NormalizeOptionalProperties(child);
}
void Check(string name, string key, JsonNode? actual, JsonNode? expected)
{
    actual = JsonNode.Parse(actual?.ToJsonString() ?? "null");
    if (JsonNode.DeepEquals(actual, expected)) return;
    var destination = Path.Combine(Path.GetDirectoryName(args[0])!, $"failed-{key}.json");
    File.WriteAllText(destination, new JsonObject { ["name"] = name, ["actual"] = actual, ["expected"] = expected?.DeepClone() }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    throw new Exception($"Mismatch in {name}: {key}; inspect {destination}");
}
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name");
    var state = item["initial"]!.DeepClone().AsObject();
    var parameters = item["args"]!.AsObject();
    var packs = originalPacks.DeepClone().AsArray();
    foreach (var patch in item["patches"]!.AsArray().OfType<JsonObject>())
    {
        var family = StateJson.Text(patch, "family"); var id = StateJson.Text(patch, "id");
        var definition = packs.OfType<JsonObject>().SelectMany(pack => (pack[family] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            .Single(entry => StateJson.Text(entry, "id") == id);
        foreach (var value in patch["values"]!.AsObject()) definition[value.Key] = value.Value?.DeepClone();
    }
    var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid());
    var movement = new MovementRules(packs, grids);
    var areaShapes = WeaponAreaRegistry.Core();
    foreach (var area in item["areas"]!.AsArray().OfType<JsonObject>())
    {
        var offsets = area["offsets"]!.AsArray().OfType<JsonObject>().Select(TacticalQueries.Position).ToArray();
        areaShapes.Replace(new(StateJson.Text(area, "id"), StateJson.IsTrue(area["needsOccupant"]),
            (_, _, targetAt) => offsets.Select(offset => new Coord(targetAt.X + offset.X, targetAt.Y + offset.Y)).ToArray()));
    }
    var combat = new BattleCombat(packs, grids, new BattleResources(), movement, areaShapes.Seal());
    var events = new JsonArray(); var hashes = new JsonArray();
    void Emit(JsonObject ev) { events.Add(ev); hashes.Add(StateDigest.Hash(state)); }
    var attacker = TacticalQueries.UnitById(state, parameters["unit"] is null ? 1 : StateJson.Integer(parameters, "unit"));
    var from = parameters["from"] is JsonObject explicitFrom ? TacticalQueries.Position(explicitFrom) : attacker is null ? new Coord(0, 0) : TacticalQueries.Position(attacker);
    var target = parameters["target"] is JsonObject explicitTarget ? TacticalQueries.Position(explicitTarget) : new Coord(3, 3);
    var weapon = parameters["weapon"]?.GetValue<string>();
    JsonNode? result = null, error = null;
    try
    {
        switch (StateJson.Text(item, "op"))
        {
            case "forecast":
            case "attack":
                var plan = combat.Forecast(state, attacker!, target, from, weapon);
                result = Serialize(plan); NormalizeOptionalProperties(result);
                if (StateJson.Text(item, "op") == "attack") combat.ExecutePlan(state, plan, Emit);
                break;
            case "query": result = new JsonObject
                {
                    ["usable"] = combat.CanUseAttack(state, attacker!, from, StateJson.IsTrue(parameters["moved"]), weapon),
                    ["targets"] = Serialize(combat.AttackTargets(state, attacker!, from, weapon)),
                }; break;
            case "cast": combat.ExecuteAttack(state, attacker!, from, target, weapon, Emit); break;
            case "due": combat.ResolveDueCasts(state, Emit); break;
            case "force": result = Serialize(combat.ForceMove(state, StateJson.Integer(attacker!, "id"), TacticalQueries.Position(parameters["source"]!.AsObject()),
                StateJson.Text(parameters, "mode"), StateJson.Number(parameters["distance"], "distance"), StateJson.Number(parameters, "collisionDamage", 0), Emit)); break;
            case "parting": combat.ResolvePartingShots(state, attacker!, from, TacticalQueries.Position(parameters["to"]!.AsObject()), Emit); break;
            case "structure":
                var raw = parameters["raw"]!.GetValueKind() == JsonValueKind.String ? double.NaN : StateJson.Number(parameters["raw"], "raw");
                result = JsonValue.Create(combat.DamageStructure(state, StateJson.Text(parameters, "id"), raw, Emit)); break;
            case "coverage": result = Serialize(combat.Coverage(state, from, target, StateJson.Text(parameters, "area"))); break;
            case "status":
                foreach (var step in parameters["steps"]!.AsArray().OfType<JsonObject>())
                {
                    var id = StateJson.Text(step, "id");
                    if (StateJson.IsTrue(step["remove"])) combat.Effects.RemoveStatus(attacker!, id, Emit);
                    else combat.Effects.AddStatus(attacker!, id, StateJson.Integer(step, "duration"),
                        step["source"] is null ? null : StateJson.Integer(step, "source"), Emit);
                }
                result = Serialize(combat.Effects.Modifiers(attacker!)); break;
            case "progress":
                foreach (var amount in parameters["amounts"]!.AsArray())
                    combat.Effects.AwardRankProgress(attacker!, StateJson.Number(amount, "amount"), Emit);
                break;
            default: throw new Exception("Unknown test operation");
        }
    }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
    {
        error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", ""), ["message"] = caught.Message };
    }
    Check(name, "error", error, item["error"]);
    Check(name, "result", result, item["result"]);
    Check(name, "events", events, item["events"]);
    Check(name, "hashes", hashes, item["hashes"]);
    Check(name, "after", state, item["after"]);
    count++;
}
Console.WriteLine($"PASS {count} live TypeScript combat cases: forecasts/queries, ordered events, every event-time digest, complete states, and typed failures");
var registry = WeaponAreaRegistry.Core();
if (!registry.Keys().SequenceEqual(new[] { "single", "cross1", "ring1", "line" })) throw new Exception("Area key order changed");
void MustRefuse(Action operation)
{
    try { operation(); }
    catch (DomainInvariantException) { return; }
    throw new Exception("Area registry failed to reject invalid mutation");
}
MustRefuse(() => registry.Register(registry.Get("single")));
registry.Seal();
MustRefuse(() => registry.Replace(registry.Get("single")));
var copy = registry.Clone();
copy.Replace(new("single", false, (_, _, targetAt) => new[] { targetAt }));
if (copy.IsSealed || !registry.Get("single").NeedsOccupant || copy.Get("single").NeedsOccupant)
    throw new Exception("Area registry clone must be independent and unsealed");
if (registry.TryGet("absent") is not null || registry.Has("absent")) throw new Exception("Area registry query contract changed");
MustRefuse(() => registry.Get("absent"));
Console.WriteLine("PASS area registry duplicate rejection, sealing, independent unsealed clone, key order, and ask/demand contracts");
