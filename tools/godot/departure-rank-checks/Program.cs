using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Progression;
using Empire.Core.State;
using Empire.Core.Tactics;

using var inputStream = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal)
    ? new StreamReader(new GZipStream(inputStream, CompressionMode.Decompress)) : new StreamReader(inputStream);
var input = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var packs = input["packs"]!.AsArray();
var baseline = new GrayFlagEngine(packs);
var serializer = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var checks = 0;
JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, serializer);
void Check(string name, string subject, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (EcmaJson.DeepEquals(actual, expected)) return;
    var destination = Path.Combine(Path.GetDirectoryName(args[0])!, "failed-departure-rank-" + subject + ".json");
    File.WriteAllText(destination, new JsonObject { ["case"] = name, ["actual"] = actual?.DeepClone(),
        ["expected"] = expected?.DeepClone() }.ToJsonString(new() { WriteIndented = true }));
    throw new Exception($"Mismatch in {name}: {subject}; inspect {destination}");
}
UnitDepartureHandler Witness(string id, string? label = null, bool fail = false) => new(id, departure =>
{
    var state = departure.State; var unit = departure.Unit; var rules = departure.Rules;
    var variables = state["scenario"]!["variables"]!.AsObject();
    if (variables["visits"] is null) variables["visits"] = new JsonArray();
    variables["visits"]!.AsArray().Add(new JsonObject
    {
        ["label"] = label ?? id, ["unit"] = unit["id"]!.DeepClone(), ["owner"] = unit["owner"]!.DeepClone(),
        ["hp"] = unit["hp"]!.DeepClone(), ["onBoard"] = TacticalQueries.UnitById(state, StateJson.Integer(unit, "id")) is not null,
        ["markers"] = state["markers"]!.AsArray().Count,
        ["casts"] = new JsonArray(state["pendingCasts"]!.AsArray().Select(cast => cast!["caster"]!.DeepClone()).ToArray()),
        ["handlers"] = Node(rules.UnitDepartures.Keys()),
        ["name"] = rules.Content.Definition("units", StateJson.Text(unit, "type"))!.Value.GetProperty("name").GetString(),
        ["distance"] = rules.Grids.Get(StateJson.Text(state["rules"]!.AsObject(), "grid"))
            .Distance(TacticalQueries.Position(unit), new(0, 0)),
    });
    departure.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = $"{label ?? id}:{StateJson.Integer(unit, "id")}" });
    if (fail) throw new DomainInvariantException("custom departure failed");
});
UnitDepartureHandlerRegistry Departures(string kind)
{
    var registry = new UnitDepartureHandlerRegistry();
    if (kind is not ("only" or "empty"))
    {
        var defaults = kind == "reverse"
            ? new[] { DefaultUnitDepartureHandlers.CastingCancellation, DefaultUnitDepartureHandlers.CommanderDefeat }
            : new[] { DefaultUnitDepartureHandlers.CommanderDefeat, DefaultUnitDepartureHandlers.CastingCancellation };
        foreach (var handler in defaults) registry.Register(handler);
    }
    if (kind == "replaceCommander") registry.Replace(Witness("commander.defeat", "commander.replacement"));
    if (kind == "replaceCast") registry.Replace(Witness("casting.cancel", "casting.replacement"));
    if (kind == "snapshot") registry.Register(new("test.mutate-snapshot", departure => { departure.Unit["hp"] = 77; departure.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = "mutated-snapshot" }); }));
    if (kind is not ("default" or "empty")) registry.Register(Witness("test.first", "first", kind == "throw")).Register(Witness("test.second", "second"));
    return registry.Seal();
}
IRankProgressionPolicy Policy(JsonObject config) => config["kind"]?.GetValue<string>() == "custom"
    ? new CustomProgression() : new ThresholdRankProgressionPolicy(StateJson.Number(config, "veteran", 120), StateJson.Number(config, "elite", 320));
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>())
{
    var name = StateJson.Text(item, "name"); var op = StateJson.Text(item, "op");
    var parameters = item["params"]!.AsObject(); var config = item["config"]!.AsObject();
    var state = item["initial"]!.DeepClone().AsObject();
    var events = new JsonArray(); var eventHashes = new JsonArray();
    JsonNode? result = null; JsonNode? error = null; JsonNode? keys = null;
    void Emit(JsonObject value) { events.Add(value); eventHashes.Add(StateDigest.Hash(state)); }
    try
    {
        var policy = Policy(config["progression"]?.AsObject() ?? new());
        var registry = Departures(config["departures"]?.GetValue<string>() ?? "default");
        var engine = BattleEngineFactory.Create(baseline.Catalog,
        [
            BattleEngineFactory.OverridePlugin(BattleCapabilities.Progression, policy),
            BattleEngineFactory.OverridePlugin(BattleCapabilities.UnitDepartures, registry),
        ]);
        var combat = engine.Combat;
        keys = Node(registry.Keys());
        var actor = state["units"]![0]!.AsObject();
        switch (op)
        {
            case "policy":
                result = new JsonObject
                {
                    ["ranks"] = Node(parameters["progress"]!.AsArray().Select(value => policy.RankFor(StateJson.Number(value, "progress"))).ToArray()),
                    ["thresholds"] = Node(parameters["ranks"]!.AsArray().Select(value => combat.Effects.NextRankThreshold(StateJson.Integer(value, "rank"))).ToArray()),
                }; break;
            case "rank": foreach (var amount in parameters["amounts"]!.AsArray()) combat.Effects.AwardRankProgress(actor, StateJson.Number(amount, "amount"), Emit); break;
            case "combatProgress": combat.Effects.AwardCombatProgress(actor, StateJson.Number(parameters["damage"], "damage"), StateJson.IsTrue(parameters["defeated"]), Emit); break;
            case "damage":
                var unitId = parameters["unit"] is null ? 1 : StateJson.Integer(parameters, "unit");
                result = Node(combat.Damage.Resolve(state, unitId, StateJson.Number(parameters, "amount", 999), StateJson.IsTrue(parameters["nonlethal"]),
                    blow => new JsonObject { ["type"] = "statusTick", ["unit"] = unitId, ["status"] = "poisoned", ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter }, Emit)); break;
            case "parting": combat.ResolvePartingShots(state, actor, new(1, 3), new(0, 3), Emit); break;
            case "status": result = JsonValue.Create(combat.Damage.DamageStatus(state, actor,
                new JsonObject { ["id"] = "poisoned", ["remaining"] = 1, ["stacks"] = 1 }, 999, false, Emit)); break;
            case "morale": result = JsonValue.Create(combat.Damage.ChangeMorale(state, 1, -999, "test", Emit)); break;
            case "surrender": result = combat.Damage.Surrender(state, 1, 2, Emit).DeepClone(); break;
            case "withdraw":
                engine.ScenarioEffects.Apply(engine.Scenarios.Rules, state, new JsonObject { ["type"] = "withdrawUnits", ["selector"] = new JsonObject { ["ids"] = new JsonArray(1) },
                    ["leaveCorpse"] = StateJson.IsTrue(parameters["leaveCorpse"]) }, Emit); break;
            case "announce": combat.Damage.AnnounceDeparture(state, actor, Emit); break;
            case "combat": combat.ExecutePlan(state, combat.Forecast(state, actor, TacticalQueries.Position(state["units"]![1]!.AsObject())), Emit); break;
            case "ability":
                engine.Abilities.Get(StateJson.Text(parameters, "ability")).Execute(engine.AbilityRules,
                    new(state, actor, TacticalQueries.Position(actor), false), parameters["target"] is JsonObject target ? TacticalQueries.Position(target) : null, Emit); break;
            case "collision": result = Node(combat.ForceMove(state, 1, new(2, 3), "push", 9, 999, Emit)); break;
            case "delayed":
                var caster = state["units"]![2]!.AsObject();
                var weapon = combat.Forecasts.UnitWeapons(caster).First(value => StateJson.Text(value, "id") == "mage_meteor");
                combat.BeginCast(state, new(caster, "metadata", weapon, new(2, 3), new(1, 4)), Emit);
                state["actorTurns"] = StateJson.Number(state, "actorTurns", 0) + 2;
                combat.ResolveDueCasts(state, Emit); break;
            default: throw new Exception($"unknown op {op}");
        }
    }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
    {
        error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", "", StringComparison.Ordinal), ["message"] = caught.Message };
    }
    Check(name, "keys", keys, item["keys"]); Check(name, "error", error, item["error"]);
    Check(name, "result", result, item["result"]); Check(name, "events", events, item["events"]);
    Check(name, "event-hashes", eventHashes, item["eventHashes"]); Check(name, "state", state, item["after"]);
    Check(name, "hash", JsonValue.Create(StateDigest.Hash(state)), item["hash"]);
}
var originalRegistry = DefaultUnitDepartureHandlers.Create();
string duplicateError = "", sealedError = "", missingError = "";
try { originalRegistry.Register(DefaultUnitDepartureHandlers.CommanderDefeat); } catch (DomainInvariantException error) { duplicateError = error.Message; }
var copiedRegistry = originalRegistry.Clone();
copiedRegistry.Replace(Witness("commander.defeat", "changed")).Replace(Witness("test.new"));
var shallowClone = ReferenceEquals(originalRegistry.Clone().Get("casting.cancel"), originalRegistry.Get("casting.cancel"));
originalRegistry.Seal();
try { originalRegistry.Register(Witness("test.blocked")); } catch (DomainInvariantException error) { sealedError = error.Message; }
try { originalRegistry.Get("missing"); } catch (DomainInvariantException error) { missingError = error.Message; }
Check("departure registry", "contracts", new JsonObject
{
    ["duplicateError"] = duplicateError, ["sealedError"] = sealedError, ["missingError"] = missingError,
    ["originalKeys"] = Node(originalRegistry.Keys()), ["copyKeys"] = Node(copiedRegistry.Keys()), ["shallowClone"] = shallowClone,
}, input["registryFacts"]);
var identityCatalog = new ReadOnlyContent(packs);
var identityGrids = baseline.Grids;
var identityRegistry = new UnitDepartureHandlerRegistry();
BattleDamage? identityDamage = null;
var identityCalls = 0;
identityRegistry.Register(new("test.identity", departure =>
{
    if (!ReferenceEquals(departure.Rules.Content, identityCatalog) ||
        !ReferenceEquals(departure.Rules.Grids, identityGrids) ||
        !ReferenceEquals(departure.Rules.Damage, identityDamage) ||
        !ReferenceEquals(departure.Rules.UnitDepartures, identityRegistry))
        throw new Exception("Mismatch in departure dependencies: exact injected identity");
    identityCalls++;
})).Seal();
identityDamage = new(identityCatalog, identityGrids,
    new CombatEffects(identityCatalog, new ThresholdRankProgressionPolicy()), identityRegistry);
var identityState = input["cases"]![0]!["initial"]!.DeepClone().AsObject();
identityDamage.AnnounceDeparture(identityState, identityState["units"]![0]!.AsObject(), _ => { });
if (identityCalls != 1) throw new Exception("Mismatch in departure dependencies: one dispatch");
Console.WriteLine("PASS exact injected catalog, grid, damage owner, and registry identities");
Console.WriteLine($"PASS departure/rank policies: {input["cases"]!.AsArray().Count} source cases, {checks} exact comparisons");

sealed class CustomProgression : IRankProgressionPolicy
{
    public int RankFor(double progress) => progress < 10 ? 0 : progress < 20 ? 2 : 1;
    public double? NextThreshold(int rank) => rank == 0 ? 10 : null;
}
