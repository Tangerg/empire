using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

if (args.Length != 1) throw new ArgumentException("Usage: CombatPolicyChecks <oracle.json[.gz]>");
using var file = File.OpenRead(args[0]);
using Stream inputStream = args[0].EndsWith(".gz", StringComparison.Ordinal) ? new GZipStream(file, CompressionMode.Decompress) : file;
var input = JsonNode.Parse(inputStream)!.AsObject();
var packs = input["packs"]!.AsArray();
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var assertions = 0;
JsonNode? Serialize<T>(T value) => JsonSerializer.SerializeToNode(value, options);
void Normalize(JsonNode? node)
{
    if (node is JsonObject obj)
    {
        if (obj.ContainsKey("protectedUnit") && obj["protectedUnit"] is null) obj.Remove("protectedUnit");
        foreach (var child in obj.ToArray()) Normalize(child.Value);
    }
    else if (node is JsonArray array) foreach (var child in array) Normalize(child);
}
void Check(string name, string key, JsonNode? actual, JsonNode? expected)
{
    assertions++;
    actual = JsonNode.Parse(actual?.ToJsonString() ?? "null"); Normalize(actual);
    if (JsonNode.DeepEquals(actual, expected)) return;
    var destination = Path.Combine(Path.GetDirectoryName(args[0])!, $"failed-{key}.json");
    File.WriteAllText(destination, new JsonObject { ["name"] = name, ["actual"] = actual, ["expected"] = expected?.DeepClone() }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    throw new Exception($"Mismatch in {name}: {key}; inspect {destination}");
}
GridRegistry Grids() => new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid());
double Numeric(JsonNode node) => node.GetValueKind() == JsonValueKind.String
    ? double.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture) : node.GetValue<double>();
CombatModifierProvider Provider(JsonObject spec) => new(StateJson.Text(spec, "id"), Numeric(spec["priority"]!), context =>
{
    if (StateJson.IsTrue(spec["context"])) return new[] { new CombatModifier(StateJson.Text(spec, "id"), StateJson.Text(spec, "id"), "plugin.context", "power", "add",
        context.Board.Grid.Distance(context.AttackerAt, context.DefenderAt) + context.Battlefield.Cell(context.DefenderAt).Elevation
        + context.Content.Definition("units", StateJson.Text(context.Attacker, "type"))!.Value.GetProperty("maxHp").GetDouble() / 100) };
    return StateJson.Objects(spec["modifiers"], "modifiers").Select(value => new CombatModifier(StateJson.Text(value, "id"), StateJson.Text(spec, "id"),
        StateJson.Text(value, "source"), StateJson.Text(value, "stage"), StateJson.Text(value, "operation"), StateJson.Number(value, "value", 0))).ToArray();
});
CombatModifierPipeline Pipeline(JsonObject config)
{
    var registry = StateJson.IsTrue(config["defaults"]) ? DefaultCombatModifiers.CreateProviders() : new CombatModifierProviderRegistry();
    foreach (var spec in StateJson.Objects(config["providers"], "providers"))
        if (StateJson.IsTrue(config["replace"])) registry.Replace(Provider(spec)); else registry.Register(Provider(spec));
    return new(registry, StateJson.Number(config, "cap", 0.6));
}
var grids = Grids(); var initial = input["initial"]!.AsObject();
var catalog = new ReadOnlyContent(packs);
var definitions = catalog.Source;
var forecast = new CombatForecasts(new CombatForecastRules(catalog, grids, new BattleResources(), DefaultCombatModifiers.Create(), ReactionRegistry.Core()));
var context = new UnitDamageContext(new(catalog, grids, forecast.Effectiveness), initial,
    initial["units"]![0]!.AsObject(), TacticalQueries.Position(initial["units"]![0]!.AsObject()), initial["units"]![1]!.AsObject(), TacticalQueries.Position(initial["units"]![1]!.AsObject()),
    definitions.Get("weapons", "c01.border-blade"), new TacticalBattlefield(initial, definitions), new TacticalBoard(initial["map"]!.AsObject(), grids.Get("square4")));
foreach (var item in StateJson.Objects(input["pipelineCases"], "pipelineCases"))
    Check(StateJson.Text(item, "name"), "pipeline", Serialize(Pipeline(item["config"]!.AsObject()).Evaluate(StateJson.Number(item, "power", 0), context)), item["result"]);

var registryChecks = new JsonObject();
CombatModifierProvider Entry(string id, double priority) => new(id, priority, _ => Array.Empty<CombatModifier>());
var a = Entry("a", 10); var registry = new CombatModifierProviderRegistry().Register(a).Register(Entry("b", 2));
registryChecks["insertion"] = Serialize(registry.Keys()); registryChecks["ordered"] = Serialize(registry.Ordered().Select(value => value.Id));
var oldOrder = registry.Ordered(); var copy = registry.Seal().Clone(); copy.Replace(Entry("a", -1)); copy.Replace(Entry("", 0));
registryChecks["oldSnapshot"] = Serialize(oldOrder.Select(value => value.Id)); registryChecks["original"] = Serialize(registry.Ordered().Select(value => value.Id)); registryChecks["copy"] = Serialize(copy.Ordered().Select(value => value.Id));
registryChecks["shallow"] = ReferenceEquals(registry.Clone().Get("a"), a); registryChecks["cloneMutable"] = !registry.Clone().IsSealed;
string? Fault(Action action) { try { action(); return null; } catch (DomainInvariantException error) { return error.Message; } }
registryChecks["duplicate"] = Fault(() => copy.Register(copy.Get("a"))); registryChecks["sealed"] = Fault(() => registry.Replace(a)); registryChecks["unknown"] = Fault(() => registry.Get("absent")); registryChecks["pipelineSealed"] = Fault(() => new CombatModifierPipeline(copy).Seal().Register(a));
var hits = DefaultWeaponHitEffects.Create(); var hitSnapshot = hits.Keys(); var hitClone = hits.Seal().Clone();
hitClone.Replace(new("custom", (_, _) => { }));
registryChecks["hitKeys"] = Serialize(hitSnapshot); registryChecks["hitClone"] = Serialize(hitClone.Keys()); registryChecks["hitShallow"] = ReferenceEquals(hits.Get("addStatus"), hitClone.Get("addStatus")); registryChecks["hitCloneMutable"] = !hitClone.IsSealed;
registryChecks["hitSealed"] = Fault(() => hits.Register(hitClone.Get("custom"))); registryChecks["hitUnknown"] = Fault(() => hitClone.Get("absent"));
Check("registry contracts", "registry", registryChecks, input["registryChecks"]);

WeaponHitEffectHandlerRegistry HitRegistry(JsonObject config)
{
    var result = DefaultWeaponHitEffects.Create(); WeaponHitEffectContext? previous = null;
    result.Register(new("observe", (effectContext, effect) =>
    {
        var target = effectContext.Target;
        target["capture"] = StateJson.Number(target, "capture", 0) + StateJson.Number(effect, "amount", 0);
        effectContext.Emit(new JsonObject { ["type"] = "extensionEffect", ["unit"] = StateJson.Integer(target, "id"),
            ["hp"] = StateJson.Number(target, "hp", 0), ["capture"] = StateJson.Number(target, "capture", 0), ["sameContext"] = ReferenceEquals(previous, effectContext),
            ["distance"] = effectContext.Rules.Grids.Get(StateJson.Text(effectContext.State["rules"]!.AsObject(), "grid")).Distance(TacticalQueries.Position(effectContext.Attacker), TacticalQueries.Position(target)),
            ["maxHp"] = effectContext.Content.Definition("units", StateJson.Text(target, "type"))!.Value.GetProperty("maxHp").GetDouble() });
        previous = effectContext;
    }));
    result.Register(new("pulse", (effectContext, effect) => effectContext.Rules.Damage.Resolve(effectContext.State,
        StateJson.Integer(effectContext.Target, "id"), StateJson.Number(effect, "amount", 0), StateJson.IsTrue(effect["nonlethal"]),
        blow => new JsonObject { ["type"] = "extensionDamage", ["unit"] = StateJson.Integer(effectContext.Target, "id"),
            ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter, ["killed"] = blow.Killed }, effectContext.Emit)));
    if (StateJson.IsTrue(config["replaceHit"])) result.Replace(new("addStatus", (effectContext, effect) =>
        effectContext.Rules.Effects.AddStatus(effectContext.Target, "armor_down", StateJson.Number(effect, "duration", 0) + 1,
            StateJson.Integer(effectContext.Attacker, "id"), effectContext.Emit)));
    return result;
}
foreach (var item in StateJson.Objects(input["consumers"], "consumers"))
{
    var name = StateJson.Text(item, "name"); var config = item["config"]!.AsObject();
    var state = item["initial"]!.DeepClone().AsObject(); var customPacks = packs.DeepClone().AsArray();
    var weapon = customPacks.OfType<JsonObject>().SelectMany(pack => (pack["weapons"] as JsonArray ?? new()).OfType<JsonObject>()).Single(value => StateJson.Text(value, "id") == "c01.border-blade");
    foreach (var property in item["weapon"]!.AsObject()) weapon[property.Key] = property.Value?.DeepClone();
    var localGrids = Grids(); var pipeline = Pipeline(config["pipeline"]?.AsObject() ?? new JsonObject { ["defaults"] = true }); var hitEffects = HitRegistry(config);
    var customCatalog = new ReadOnlyContent(customPacks);
    var combat = new BattleCombat(new BattleCombatRules(customCatalog, localGrids, new BattleResources(), new DefaultTacticalSpace(customCatalog, localGrids),
        WeaponAreaRegistry.Core(), pipeline, hitEffects, ReactionRegistry.Core(),
        new Empire.Core.Progression.ThresholdRankProgressionPolicy(), Empire.Core.Lifecycle.DefaultUnitDepartureHandlers.Create()));
    if (!ReferenceEquals(pipeline, combat.Forecasts.Modifiers) || !ReferenceEquals(hitEffects, combat.HitEffects)) throw new Exception("Supplied policy identity not retained");
    pipeline.Seal(); hitEffects.Seal();
    var events = new JsonArray(); var hashes = new JsonArray();
    void Emit(JsonObject ev) { events.Add(ev); hashes.Add(StateDigest.Hash(state)); }
    JsonNode? result = null, error = null;
    try
    {
        var attacker = state["units"]![0]!.AsObject();
        if (StateJson.IsTrue(config["direct"])) combat.ApplyHitEffects(state, attacker, state["units"]![1]!.AsObject(), StateJson.Objects(config["effects"], "effects").ToArray(), Emit);
        else { var plan = combat.Forecast(state, attacker, new Coord(3, 3), weapon: "c01.border-blade"); result = Serialize(plan); if (StateJson.IsTrue(config["staleHp"])) state["units"]![1]!["hp"] = 1; combat.ExecutePlan(state, plan, Emit); }
    }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
    { error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", ""), ["message"] = caught.Message }; }
    Check(name, "error", error, item["error"]); Check(name, "result", result, item["result"]);
    Check(name, "events", events, item["events"]); Check(name, "hashes", hashes, item["hashes"]); Check(name, "after", state, item["after"]);
}
var nativeChecks = HitEffectAdmissionChecks.Run(packs);
Console.WriteLine($"PASS {nativeChecks} handler-owned admission and composed save/replay checks");
Console.WriteLine($"PASS {input["pipelineCases"]!.AsArray().Count} pipeline + {registryChecks.Count} registry + {input["consumers"]!.AsArray().Count} canonical combat consumer cases, {assertions} assertions");
