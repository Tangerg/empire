using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Tactics;
using Empire.Core.Progression;
using Empire.Core.Combat;

var fixture = Path.GetFullPath(args.Length == 0 ? "/tmp/empire-progression-oracle.json" : args[0]);
var input = JsonNode.Parse(File.ReadAllText(fixture))!.AsObject();
var diagnostics = Path.GetDirectoryName(fixture)!;
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var catalogs = input["catalogs"]!.AsArray();
// Catalog 2 deliberately bypasses the original installer to characterize raw
// career/formation helpers on cyclic and missing references. Production engine
// composition correctly rejects it; keep this test at the same raw boundary.
var engines = catalogs.Select((packs, index) => index == 2 ? null : new GrayFlagEngine(packs!.AsArray())).ToArray();
var rawProgression = new BattleProgression(catalogs[2]!.AsArray(), engines[0]!.Grids,
    engines[0]!.Resources, engines[0]!.Commands, new CombatEffects(catalogs[2]!.AsArray()));
var count = 0;
var kinds = new Dictionary<string, int>();
foreach (var entry in input["cases"]!.AsArray())
{
    var item = entry!.AsObject();
    var name = item["name"]!.GetValue<string>();
    var kind = item["operation"]!.GetValue<string>();
    var catalog = item["catalog"]!.GetValue<int>();
    var engine = engines[catalog] ?? engines[0]!;
    var progression = catalog == 2 ? rawProgression : engine.Progression;
    var parameters = item["params"]!.AsObject();
    var state = item["before"]!.DeepClone().AsObject();
    var unit = state["units"]![0]!.AsObject();
    var events = new JsonArray();
    JsonNode? value = null;
    string? error = null;
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
                    var emitted = new JsonArray(); string? failure = null;
                    try
                    {
                        var receipt = sequenceBattle.Dispatch(next!.AsObject());
                        foreach (var e in receipt.Events) { emitted.Add(e.DeepClone()); events.Add(e.DeepClone()); }
                    }
                    catch (Exception caught) { failure = caught.Message; }
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
    catch (Exception caught) { error = caught.Message; }
    var actual = new JsonObject { ["value"] = value, ["error"] = error, ["events"] = events, ["after"] = state };
    var expected = new JsonObject { ["value"] = item["value"]?.DeepClone(), ["error"] = item["error"]?.DeepClone(),
        ["events"] = item["events"]!.DeepClone(), ["after"] = item["after"]!.DeepClone() };
    // Compare full snapshots, not a digest with presentation fields omitted.
    if (StateDigest.Hash(new JsonArray(actual.DeepClone())) != StateDigest.Hash(new JsonArray(expected.DeepClone())))
    {
        File.WriteAllText(Path.Combine(diagnostics, "progression-actual.json"), actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(diagnostics, "progression-expected.json"), expected.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        throw new Exception($"Progression mismatch #{count}: {name}. Actual error: {error ?? "none"}. Expected: {item["error"]?.ToJsonString() ?? "none"}. Diagnostics: {diagnostics}");
    }
    count++;
    kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
}
Console.WriteLine($"PASS {count} source progression cases: exact values, ordered events, errors and full state snapshots");
Console.WriteLine(string.Join(", ", kinds.Select(pair => $"{pair.Key}={pair.Value}")));
JsonNode? Serialize<T>(T item) => JsonSerializer.SerializeToNode(item, options);
