using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Objectives;
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
// Old objective prose remains historical schema evidence. Native projection facts are specified below,
// not recovered from those strings or presented as source-v1 state/hash parity.
var historicalProseAssertions = 0;
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
JsonObject Finite(double value) => new() { ["kind"] = "finite", ["value"] = value };
var scoreFacts = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
{
    ["null"] = Finite(0), ["false"] = Finite(0), ["true"] = Finite(1),
    ["\"\""] = Finite(0), ["\"  \""] = Finite(0), ["\"bad\""] = new() { ["kind"] = "nan" },
    ["4"] = Finite(4), ["\"0xff\""] = Finite(255), ["\"0b11\""] = Finite(3), ["\"0o10\""] = Finite(8),
    ["\"Infinity\""] = new() { ["kind"] = "positiveInfinity" }, ["\"-Infinity\""] = new() { ["kind"] = "negativeInfinity" },
    ["\"infinity\""] = new() { ["kind"] = "nan" }, ["\"1e-7\""] = Finite(1e-7), ["\"1e21\""] = Finite(1e21),
    ["\"1e999\""] = new() { ["kind"] = "positiveInfinity" }, ["\"-0\""] = Finite(0), ["\"+0x1\""] = new() { ["kind"] = "nan" },
};
void AssertProjection(ObjectiveProgress progress, JsonObject definition, JsonObject owner, string name)
{
    Equal(progress.Id, definition["id"], name + " projected identity");
    Equal(progress.Kind, definition["type"], name + " projected kind");
    Equal(EcmaJson.Parse(progress.Definition.GetRawText()), definition, name + " detached definition");
    var runtime = owner["objectiveStates"]![progress.Id];
    Equal(progress.Status, runtime?["status"] ?? JsonValue.Create("active"), name + " projected status");
    if (progress.Status != "active") Equal(EcmaJson.Parse(progress.Facts.GetRawText()), new JsonObject(), name + " terminal facts");
    var children = engine.Objectives.Children(definition);
    Equal(progress.Children.Count, JsonValue.Create(children.Count), name + " projected child count");
    for (var index = 0; index < children.Count; index++) AssertProjection(progress.Children[index], children[index], owner, name + " child " + index);
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
    {
        var ownerId = objective["owner"]!.GetValue<int>();
        var definition = objective["objective"]!.AsObject();
        var owner = state["players"]!.AsArray().OfType<JsonObject>().Single(player => player["id"]!.GetValue<int>() == ownerId);
        var progress = engine.Objectives.Project(victory.Rules, state, ownerId, definition);
        AssertProjection(progress, definition, owner, name);
        historicalProseAssertions++;
        if (ownerId != 1 || progress.Status != "active") continue;
        JsonObject? expectedFacts = name switch
        {
            "objective kind surviveTurns" => new() { ["current"] = 1, ["target"] = 3 },
            "objective kind destroy" => new() { ["remaining"] = 0 },
            "objective kind score" => new() { ["current"] = Finite(3), ["target"] = 8 },
            "objective kind interact" or "nested optional" or "nested failOn" => new(),
            "nested all" or "nested any" or "nested sequence" => new() { ["completed"] = 1, ["total"] = 2 },
            _ => null,
        };
        const string coercion = "score coercion ";
        if (name.StartsWith(coercion, StringComparison.Ordinal))
            expectedFacts = new() { ["current"] = scoreFacts[name[coercion.Length..]].DeepClone(), ["target"] = 8 };
        if (expectedFacts is not null) Equal(EcmaJson.Parse(progress.Facts.GetRawText()), expectedFacts, name + " independently specified native facts");
    }
    var players = state["players"]!.AsArray().OfType<JsonObject>().ToArray();
    var expectedVisible = item["visibleObjectives"]!.AsArray();
    Equal(players.Length, JsonValue.Create(expectedVisible.Count), name + " visible player count");
    for (var playerIndex = 0; playerIndex < players.Length; playerIndex++)
    {
        var visible = victory.Objectives(state, players[playerIndex]["id"]!.GetValue<int>());
        var expected = expectedVisible[playerIndex]!.AsArray();
        Equal(visible.Count, JsonValue.Create(expected.Count), name + " visible objective count");
        for (var index = 0; index < visible.Count; index++)
        {
            Equal(visible[index].Id, expected[index]!["id"], name + " unchanged visible identity");
            Equal(visible[index].Status, expected[index]!["status"], name + " unchanged visible status");
            Equal(visible[index].Role, expected[index]!["role"], name + " unchanged visible role");
            historicalProseAssertions += 2;
        }
    }
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
if (historicalProseAssertions > 0)
    throw new InvalidOperationException($"PENDING historical schema migration: {historicalProseAssertions} source objective prose assertions are not native semantic facts. {checks} unchanged source and native projection assertions completed; this is not full suite acceptance.");
Console.WriteLine($"PASS {checks} tactical semantic query comparisons; detached and read-only guards");
