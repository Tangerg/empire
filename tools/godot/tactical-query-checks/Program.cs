using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Resources;
using Empire.Core.Scenario;
using Empire.Core.State;
using Empire.Core.Tactics;

var oracle = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var packs = oracle["packs"]!.AsArray();
var engine = new GrayFlagEngine(packs);
var victory = engine.Victory;
var serializer = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var checks = 0;
void Equal(object? actual, JsonNode? expected, string name)
{
    var node = actual is JsonNode json ? json : JsonSerializer.SerializeToNode(actual, serializer);
    if (!JsonNode.DeepEquals(node, expected)) throw new Exception($"{name}:\nactual {node}\nexpected {expected}");
    checks++;
}
JsonObject Capture(Func<object?> query)
{
    try { return new JsonObject { ["value"] = JsonSerializer.SerializeToNode(query(), serializer) }; }
    catch (DomainInvariantException error) { return new JsonObject { ["error"] = error.Message }; }
}
foreach (var item in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var state = item["state"]!.DeepClone().AsObject();
    var name = item["name"]!.GetValue<string>();
    var before = state.ToJsonString();
    var firstUnit = state["units"]![0]!.AsObject();
    var subjects = new List<BattleResources.Subject> { new("player", state["players"]![0]!.AsObject()), new("unit", firstUnit) };
    subjects.AddRange(firstUnit["weaponState"]!.AsObject().Select(pair => new BattleResources.Subject("weapon", firstUnit, pair.Key)));
    Equal(subjects.Select(subject => engine.Resources.Accounts(subject)).ToArray(), item["accounts"], name + " accounts");
    Equal(Capture(() => engine.Recruitment.Cards(state, new Coord(0, 0))), item["cards"], name + " cards");
    foreach (var objective in item["objectives"]!.AsArray().OfType<JsonObject>())
        Equal(Capture(() => victory.Progress(state, objective["owner"]!.GetValue<int>(), objective["objective"]!.AsObject())), objective["value"], name + " progress");
    Equal(state["players"]!.AsArray().OfType<JsonObject>().Select(player => victory.Objectives(state, player["id"]!.GetValue<int>())).ToArray(), item["visibleObjectives"], name + " visible objectives");
    Equal(Capture(() => engine.Combat.ActiveCasts(state)), item["casts"], name + " casts");
    Equal(Capture(() => engine.Combat.IncomingCasts(state).Select(pair => new { pair.Key.X, pair.Key.Y, Remaining = pair.Value }).ToArray()), item["incoming"], name + " incoming");
    var grid = engine.Grids.Get(state["rules"]!["grid"]!.GetValue<string>());
    var commanders = state["commanders"]!.AsArray().OfType<JsonObject>().Select(commander => new
    {
        Leader = TacticalModifiers.CommanderUnit(state, commander),
        Tactics = engine.Support.Tactics(state, commander["id"]!.GetValue<string>()),
    }).ToArray();
    Equal(commanders, item["commanders"], name + " commanders");
    Equal(state["units"]!.AsArray().OfType<JsonObject>().Select(unit => new
    {
        Commander = TacticalModifiers.ActiveCommanderFor(grid, state, unit),
        Aura = TacticalModifiers.CommanderAura(grid, state, unit),
    }).ToArray(), item["auras"], name + " auras");
    if (state.ToJsonString() != before) throw new Exception(name + " query mutated state");
    // Query projections are detached even when a caller modifies a returned JSON copy.
    foreach (var commander in commanders) if (commander.Leader is { } leader) leader["hp"] = -999;
    foreach (var unit in state["units"]!.AsArray().OfType<JsonObject>())
        if (TacticalModifiers.ActiveCommanderFor(grid, state, unit) is { } commander) commander["radius"] = -999;
    if (state.ToJsonString() != before) throw new Exception(name + " commander projection aliases state");
}
foreach (var item in oracle["thresholds"]!.AsArray()) Equal(engine.Combat.Effects.NextRankThreshold(item!["rank"]!.GetValue<int>()), item["next"], "threshold");
foreach (var item in oracle["ranks"]!.AsArray()) Equal(engine.Combat.Effects.RankFor(item!["progress"]!.GetValue<double>()), item["rank"], "rank");
var duplicate = oracle["duplicate"]!;
var duplicateEngine = new GrayFlagEngine(duplicate["packs"]!.AsArray());
var duplicateState = duplicate["state"]!.AsObject();
Equal(duplicateEngine.Recruitment.Cards(duplicateState, new Coord(0, 0)), duplicate["cards"], "duplicate cost cards preserve individual affordability");
if (duplicateEngine.Recruitment.Refusal(duplicateState, "soldier", new Coord(0, 0))?.Code != "recruitment.insufficient_resources") throw new Exception("duplicate cost dispatch must aggregate costs");
foreach (var item in oracle["inspectErrors"]!.AsArray())
{
    var kind = item!["kind"]!.GetValue<string>();
    var state = oracle["cases"]![0]!["state"]!;
    var holder = state[kind == "unit" ? "units" : "players"]![0]!.AsObject();
    Equal(Capture(() => engine.Resources.Inspect(new(kind, holder), item["resource"]!.GetValue<string>())), item["result"], "invalid inspect");
}
foreach (var family in oracle["catalog"]!.AsObject())
{
    var definitions = engine.Catalog.Definitions(family.Key);
    if (!EcmaJson.DeepEquals(new JsonArray(definitions.Select(element => EcmaJson.Parse(element.GetRawText())).ToArray()), family.Value))
        throw new Exception("catalog family differs: " + family.Key);
    foreach (var entry in family.Value!.AsArray())
    {
        var id = EcmaJson.ReadString(entry!["id"]);
        if (!EcmaJson.DeepEquals(EcmaJson.Parse(engine.Catalog.Definition(family.Key, id)!.Value.GetRawText()), entry))
            throw new Exception("catalog definition differs: " + id);
        checks++;
    }
}
var unicodeEngine = new GrayFlagEngine(oracle["unicode"]!["packs"]!.AsArray());
var unicodeDefinition = unicodeEngine.Catalog.Definition("units", "soldier")!.Value;
if (!EcmaJson.DeepEquals(EcmaJson.Parse(unicodeDefinition.GetRawText()), oracle["unicode"]!["definition"])) throw new Exception("UTF-16 catalog projection lost code units");
var detached = EcmaJson.Parse(unicodeDefinition.GetRawText())!; detached["name"] = "mutated";
if (!EcmaJson.DeepEquals(EcmaJson.Parse(unicodeEngine.Catalog.Definition("units", "soldier")!.Value.GetRawText()), oracle["unicode"]!["definition"])) throw new Exception("catalog alias");
Equal(oracle["reactions"]!.AsArray().Select(reaction => engine.Combat.Forecasts.Reaction(reaction!["id"]!.GetValue<string>())).ToArray(), oracle["reactions"], "reaction definitions including hints");
var seenAbilities = new HashSet<string>();
foreach (var row in oracle["commandLabels"]!.AsArray())
{
    var state = row!["state"]!.AsObject();
    var actor = state["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == row["actor"]!.GetValue<int>());
    var at = new Coord(row["at"]!["x"]!.GetValue<int>(), row["at"]!["y"]!.GetValue<int>());
    var options = engine.CommandsAt(state, actor, at);
    foreach (var option in options) seenAbilities.Add(option.Ability);
    Equal(options.Select(option => new { option.Key, option.Name, option.Hint }).ToArray(), row["options"], "command labels " + actor["type"]);
}
if (!seenAbilities.SetEquals(new[] { "attack", "capture", "heal", "wait" })) throw new Exception("command label fixture failed to exercise every ability");
Console.WriteLine($"PASS {checks} live source tactical query comparisons; detached and read-only guards");
