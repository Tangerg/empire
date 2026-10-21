using Empire.Core;
using Empire.Core.State;
using Empire.Core.Lifecycle;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Tactics;
using Empire.Core.Progression;
using Empire.Core.Combat;

var fixture = Path.GetFullPath(args.Length == 0 ? "/tmp/empire-progression-oracle.json" : args[0]);
var input = JsonNode.Parse(File.ReadAllText(fixture))!.AsObject();
using var nativeStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("NativeRefusals")
    ?? throw new Exception("Missing explicit native refusal expectations");
var nativeRefusals = JsonNode.Parse(nativeStream)!.AsObject();
var nativeCases = nativeRefusals["cases"]!.AsObject();
if (!input["cases"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).SequenceEqual(nativeCases.Select(pair => pair.Key)))
    throw new Exception("Native refusal expectation cases are missing or stale");
var diagnostics = Path.GetDirectoryName(fixture)!;
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var catalogs = input["catalogs"]!.AsArray();
// Catalog 2 deliberately bypasses the original installer to characterize raw
// career/formation helpers on cyclic and missing references. Production engine
// composition correctly rejects it; keep this test at the same raw boundary.
var engines = catalogs.Select((packs, index) => index == 2 ? null : new GrayFlagEngine(packs!.AsArray())).ToArray();
var rawCatalog = new ReadOnlyContent(catalogs[2]!.AsArray());
var rawProgression = new BattleProgression(new BattleProgressionRules(rawCatalog, engines[0]!.Grids,
    engines[0]!.Resources, engines[0]!.Commands, new CombatEffects(rawCatalog, new ThresholdRankProgressionPolicy())));
var count = 0;
var nativeRejections = 0;
var kinds = new Dictionary<string, int>();
foreach (var entry in input["cases"]!.AsArray())
{
    var item = entry!.AsObject();
    var name = item["name"]!.GetValue<string>();
    var native = nativeCases[name]!.AsObject();
    if (!native.ContainsKey("error")) throw new Exception("Missing native error expectation: " + name);
    var kind = item["operation"]!.GetValue<string>();
    var catalog = item["catalog"]!.GetValue<int>();
    var engine = engines[catalog] ?? engines[0]!;
    var progression = catalog == 2 ? rawProgression : engine.Progression;
    var parameters = item["params"]!.AsObject();
    var state = item["before"]!.DeepClone().AsObject();
    var unit = state["units"]![0]!.AsObject();
    var events = new JsonArray();
    JsonNode? value = null;
    JsonNode? error = null;
    Exception? commandFailure = null;
    void Emit(JsonObject e) => events.Add(e);
    void CountedEmit(JsonObject e)
    {
        Emit(e);
        var counts = state["scenario"]!["eventCounts"]!.AsObject();
        var type = e["type"]!.GetValue<string>();
        counts[type] = (counts[type]?.GetValue<int>() ?? 0) + 1;
    }
    try
    {
        var formation = parameters["formation"]?.GetValue<string>();
        switch (kind)
        {
            case "careerOptions": value = Serialize(progression.Careers.Options(state, unit)); break;
            case "abilities": value = Serialize(progression.Careers.UnitAbilityIds(unit)); break;
            case "mastery": value = Serialize(progression.Careers.Mastery(unit, parameters["career"]!.GetValue<string>())); break;
            case "changeCareer": progression.Careers.Change(state, unit, parameters["career"]!.GetValue<string>(), Emit); break;
            case "awardCareer": progression.Careers.AwardProgress(unit, parameters["amount"]!.GetValue<double>(), Emit); break;
            case "awardRank": engine.Combat.Effects.AwardRankProgress(unit, parameters["amount"]!.GetValue<double>(), Emit); break;
            case "formationOptions": value = Serialize(progression.Formations.Options(state, unit)); break;
            case "formation": value = progression.Formations.InEffect(state, unit, formation)?.DeepClone(); break;
            case "validateFormation": progression.Formations.ValidateChange(state, unit, formation); break;
            case "delta": value = Serialize(progression.Formations.MovementDelta(state, unit)); break;
            case "rawAction":
                var action = parameters["action"]!.AsObject();
                var unitId = action["unit"]!.GetValue<int>();
                if (action["kind"]!.GetValue<string>() == "changeCareer")
                    progression.ChangeCareer(state, unitId, action["career"]!.GetValue<string>(), CountedEmit);
                else progression.ChangeFormation(state, unitId, action["formation"]?.GetValue<string>(), CountedEmit);
                break;
            case "dispatch":
                var battle = engine.LoadSavedBattle(engine.SaveBattle(state));
                try
                {
                    var receipt = battle.Dispatch(parameters["action"]!.AsObject());
                    foreach (var e in receipt.Events) events.Add(e.DeepClone());
                }
                finally { state = battle.Snapshot(); }
                break;
            case "sequence":
                var sequenceBattle = engine.LoadSavedBattle(engine.SaveBattle(state));
                var steps = new JsonArray();
                foreach (var next in parameters["actions"]!.AsArray())
                {
                    var emitted = new JsonArray(); JsonNode? failure = null;
                    try
                    {
                        var receipt = sequenceBattle.Dispatch(next!.AsObject());
                        foreach (var e in receipt.Events) { emitted.Add(e.DeepClone()); events.Add(e.DeepClone()); }
                    }
                    catch (IllegalActionException caught) { failure = Serialize(caught.Refusal); }
                    catch (DomainInvariantException caught) { failure = JsonValue.Create(caught.Message); }
                    steps.Add(new JsonObject { ["action"] = next!.DeepClone(), ["events"] = emitted,
                        ["error"] = failure, ["after"] = sequenceBattle.Snapshot() });
                }
                state = sequenceBattle.Snapshot(); value = steps;
                break;
            case "derived":
                var field = engine.Space.ComputeMoveField(state, unit);
                value = new JsonObject
                {
                    ["abilities"] = Serialize(progression.Careers.UnitAbilityIds(unit)),
                    ["formation"] = progression.Formations.Active(state, unit)?.DeepClone(),
                    ["movementDelta"] = Serialize(progression.Formations.MovementDelta(state, unit)),
                    ["move"] = new JsonObject
                    {
                        ["origin"] = Serialize(field.Origin), ["unit"] = field.Unit,
                        ["tiles"] = new JsonArray(field.Tiles.Select(pair => (JsonNode)new JsonArray(JsonValue.Create(pair.Key), Serialize(pair.Value))).ToArray()),
                        ["stops"] = Serialize(field.Stops),
                    },
                    ["damage"] = Serialize(engine.Combat.Forecasts.ComputeDamage(state, unit,
                        state["units"]!.AsArray().Select(n => n!.AsObject()).First(candidate => candidate["owner"]!.GetValue<int>() == 2))),
                };
                break;
            default: throw new Exception($"Unknown progression check {kind}");
        }
    }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
    {
        commandFailure = caught;
        error = caught is IllegalActionException illegal ? Serialize(illegal.Refusal) : JsonValue.Create(caught.Message);
    }
    if (name is "health ratio 0" or "health ratio 200" or "health invariant direct mutation order")
    {
        if (commandFailure is not DomainInvariantException || events.Count != 0 ||
            !JsonNode.DeepEquals(state, item["before"]))
            throw new Exception($"Invalid career health must fail before resource costs, events or state mutations: {name}");
        nativeRejections++;
        continue;
    }
    var actual = new JsonObject { ["value"] = value, ["error"] = error, ["events"] = events, ["after"] = state };
    if ((item["error"] is null) != (native["error"] is null))
        throw new Exception("Native progression expectation changed refusal acceptance: " + name);
    var expectedValue = item["value"]?.DeepClone();
    if ((kind is "careerOptions" or "formationOptions") && expectedValue is JsonArray optionValues)
    {
        var reasons = native["optionReasons"]!.AsArray();
        if (reasons.Count != optionValues.Count) throw new Exception("Missing or stale native option expectations: " + name);
        var family = kind == "careerOptions" ? "career" : "formation";
        for (var index = 0; index < optionValues.Count; index++)
        {
            var option = optionValues[index]!.AsObject(); var expectedReasons = reasons[index]!.AsObject();
            if (option[family]!["id"]!.GetValue<string>() != expectedReasons["id"]!.GetValue<string>() ||
                option["reasons"]!.AsArray().Count != expectedReasons["reasons"]!.AsArray().Count)
                throw new Exception("Native option reason order/coverage changed: " + name);
            option["reasons"] = expectedReasons["reasons"]!.DeepClone();
        }
    }
    if (kind == "sequence")
    {
        var steps = expectedValue!.AsArray(); var errors = native["stepErrors"]!.AsArray();
        if (steps.Count != errors.Count) throw new Exception("Missing or stale native sequence expectations: " + name);
        for (var index = 0; index < steps.Count; index++)
        {
            if ((steps[index]!["error"] is null) != (errors[index] is null))
                throw new Exception("Native sequence changed refusal acceptance: " + name);
            steps[index]!["error"] = errors[index]?.DeepClone();
        }
    }
    var expected = new JsonObject { ["value"] = expectedValue, ["error"] = native["error"]?.DeepClone(),
        ["events"] = item["events"]!.DeepClone(), ["after"] = item["after"]!.DeepClone() };
    // Compare full snapshots, not a digest with presentation fields omitted.
    if (StateDigest.Hash(new JsonArray(actual.DeepClone())) != StateDigest.Hash(new JsonArray(expected.DeepClone())))
    {
        File.WriteAllText(Path.Combine(diagnostics, "progression-actual.json"), actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(diagnostics, "progression-expected.json"), expected.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        throw new Exception($"Progression mismatch #{count}: {name}. Actual error: {error?.ToJsonString() ?? "none"}. Expected: {native["error"]?.ToJsonString() ?? "none"}. Diagnostics: {diagnostics}");
    }
    count++;
    kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
}
Console.WriteLine($"PASS {count} source progression cases: exact values, ordered events and full state snapshots; explicit native refusal codes/details");
Console.WriteLine($"PASS {nativeRejections} explicit native invalid-career-health atomic rejections");
if (nativeRejections != 3) throw new Exception("Expected three deliberately hardened career-health cases");
Console.WriteLine(string.Join(", ", kinds.Select(pair => $"{pair.Key}={pair.Value}")));
JsonNode? Serialize<T>(T item) => JsonSerializer.SerializeToNode(item, options);
