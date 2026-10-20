using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Resources;
using Empire.Core.Scenario;
using Empire.Core.State;
using Empire.Core.Tactics;

if (args.Length != 1) throw new ArgumentException("Usage: ScenarioTests <oracle.json>");
var input = JsonNode.Parse(File.ReadAllText(args[0]))!.AsObject();
var packs = input["packs"]!.AsArray();
var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid()).Seal();
var movement = new MovementRules(packs, grids);
var combat = new BattleCombat(packs, grids, new BattleResources(), movement, WeaponAreaRegistry.Core().Seal());
var extensions = new ScenarioExtensions(packs, movement, combat, (state, source, done) =>
    BattleStateFactory.SpawnUnit(packs, grids, state, StateJson.Text(source, "unit"), StateJson.Integer(source, "owner"),
        TacticalQueries.Position(source), done: done, source: source));
var scenarios = new ScenarioSystem(packs, extensions.ConditionMet, extensions.Apply);
var victory = new VictorySystem(packs, scenarios, CompositeRules.ObjectiveOutcome, CompositeRules.DescribeObjective);
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var count = 0;
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
    var events = new JsonArray(); var hashes = new JsonArray();
    void Emit(JsonObject ev) { events.Add(ev); hashes.Add(StateDigest.Hash(state)); }
    JsonNode? result = null, error = null;
    try
    {
        foreach (var effect in item["effects"]!.AsArray().OfType<JsonObject>())
        {
            if (StateJson.Text(effect, "type") == "emitSignal") Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = effect["signal"]!.DeepClone() });
            else extensions.Apply(state, effect, Emit);
        }
        if (item["condition"] is JsonObject condition) result = JsonValue.Create(scenarios.ConditionMet(state, condition));
        if (item["composite"] is { } composite) result = JsonSerializer.SerializeToNode(CompositeRules.Status(state, composite.GetValue<string>()), serialization);
        if (item["objective"] is JsonObject objective) result = new JsonObject
        {
            ["outcome"] = victory.Outcome(state, 1, objective),
            ["description"] = VictorySystem.Describe(objective, CompositeRules.DescribeObjective),
        };
        if (StateJson.IsTrue(item["victory"])) result = JsonSerializer.SerializeToNode(victory.Evaluate(state, Emit), serialization);
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
Console.WriteLine($"PASS {count} live TypeScript scenario cases: ordered events, every event-time digest, complete states, queries and typed failures");
