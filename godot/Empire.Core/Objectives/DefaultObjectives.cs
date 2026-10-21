using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Empire.Core.References;
using Empire.Core.Scenario;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Objectives;

public static class DefaultObjectives
{
    public static ObjectiveHandlerRegistry Create() => new ObjectiveHandlerRegistry()
        .Register(new("routEnemies", (context, _) =>
        {
            var remaining = Players(context.State).Where(player => Enemy(context, Integer(player, "id")))
                .Sum(player => Units(context.State).Count(unit => Integer(unit, "owner") == Integer(player, "id")));
            return Result(remaining == 0, ("remaining", remaining));
        }))
        .Register(new("captureHQ", (context, _) =>
        {
            var enemies = Players(context.State).Where(player => Enemy(context, Integer(player, "id"))).ToArray();
            var headquarters = enemies.ToDictionary(player => Integer(player, "id"), player =>
                OwnedTerrains(context, Integer(player, "id")).Count(terrain => IsTrue(terrain["hq"])));
            var contenders = enemies.Where(player => IsTrue(player["startedWithHQ"])).ToArray();
            return Result(contenders.Length > 0 && contenders.All(player => headquarters[Integer(player, "id")] == 0),
                ("remaining", headquarters.Values.Sum()));
        }))
        .Register(new("holdAllVillages", (context, _) =>
        {
            var map = Object(context.State["map"], "map");
            var sites = Array(map["tiles"], "tiles").Select((tile, index) => (tile, index))
                .Where(entry => IsTrue(context.Content.Source.Get("terrains", Text(entry.tile, "terrain"))["capturable"])).ToArray();
            var owned = sites.Count(entry => Integer(map["owners"]![entry.index], "owner") == context.Owner);
            return Result(sites.Length > 0 && owned == sites.Length, ("owned", owned), ("total", sites.Length));
        }))
        .Register(new("surviveTurns", (context, objective) =>
        {
            var turn = Number(context.State["turn"], "turn");
            var target = Number(objective["turns"], "turns");
            return Result(turn > target, ("current", Math.Min(turn, target)), ("target", target));
        }))
        .Register(new("eliminate", (context, objective) =>
        {
            var remaining = ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count;
            return Result(remaining == 0, ("remaining", remaining));
        }) { References = objective => new PayloadReferences().Selector(Object(objective["selector"], "selector")) })
        .Register(new("destroy", (context, objective) =>
        {
            var remaining = Array(objective["structures"], "structures").Count(id => Objects(context.State["structures"], "structures")
                .Any(structure => Text(structure, "id") == Text(id, "id") && Number(structure["hp"], "hp") > 0));
            return Result(remaining == 0, ("remaining", remaining));
        }) { References = objective => Array(objective["structures"], "structures").Aggregate(new PayloadReferences(), (references, id) => references.Structure(Text(id, "id"))) })
        .Register(new("neutralizeComposite", (context, objective) =>
        {
            var neutralized = CompositeRules.Status(context.State, Text(objective, "composite")).Neutralized;
            var target = CompositeThreshold(context.State, objective);
            return Result(neutralized >= target, ("neutralized", neutralized), ("target", target));
        }) { References = CompositeReferences })
        .Register(new("protect", (context, objective) =>
        {
            var alive = ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count;
            var minimum = Number(objective["minimumAlive"], "minimumAlive");
            var outcome = alive < minimum ? "failure" : Number(context.State["turn"], "turn") > Number(objective["untilTurn"], "untilTurn") ? "success" : "pending";
            return Assessment(outcome, ("alive", alive), ("minimum", minimum));
        }) { References = ProtectReferences })
        .Register(new("escort", (context, objective) =>
        {
            var zone = Zone(context.State, Text(objective, "zone")).ToArray();
            var arrived = ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector"))
                .Count(unit => zone.Any(cell => ScenarioSelection.SameCell(cell, unit)));
            var target = Number(objective["count"], "count");
            return Result(arrived >= target, ("arrived", arrived), ("target", target));
        }) { References = EscortReferences })
        .Register(new("control", (context, objective) =>
        {
            var cells = Zone(context.State, Text(objective, "zone")).ToArray();
            var map = Object(context.State["map"], "map");
            var owned = cells.Count(cell => Integer(map["owners"]![Integer(cell, "y") * Integer(map, "width") + Integer(cell, "x")], "owner") == context.Owner);
            return Result(cells.Length > 0 && owned == cells.Length, ("owned", owned), ("total", cells.Length));
        }) { References = objective => new PayloadReferences().Zone(Text(objective, "zone")) })
        .Register(new("score", (context, objective) =>
        {
            var value = context.State["scenario"]!["variables"]![Text(objective, "variable")];
            var facts = new JsonObject { ["current"] = NumericFact(ScoreNumber(value)), ["target"] = objective["atLeast"]!.DeepClone() };
            return new ObjectiveAssessment(ScenarioSelection.Compare(value, "gte", objective["atLeast"]) ? "success" : "pending", facts);
        }))
        .Register(new("interact", (context, objective) => new ObjectiveAssessment(
            ScenarioSelection.Compare(context.State["scenario"]!["variables"]![Text(objective, "variable")], "eq", objective["equals"]) ? "success" : "pending", new())))
        .Register(new("all", (context, objective) => Stage(context, objective, false)) { Refresh = "children", Children = Children })
        .Register(new("any", (context, objective) => Stage(context, objective, true)) { Refresh = "children", Children = Children })
        .Register(new("sequence", (context, objective) => Stage(context, objective, false)) { Refresh = "sequence", Children = Children })
        .Register(new("optional", (context, objective) => new ObjectiveAssessment(context.Outcome(Object(objective["objective"], "objective")), new()))
        { Role = "optional", Refresh = "children", Children = Nested })
        .Register(new("failOn", (context, objective) => new ObjectiveAssessment(context.ScenarioConditions.Evaluate(context.State,
            Object(objective["condition"], "condition"), context.Content) ? "failure" : context.Outcome(Object(objective["objective"], "objective")), new()))
        { Role = "critical", Refresh = "children", Children = Nested, References = objective => new PayloadReferences().Condition(Object(objective["condition"], "condition")) });

    private static ObjectiveAssessment Result(bool success, params (string Key, double Value)[] facts) => Assessment(success ? "success" : "pending", facts);
    private static ObjectiveAssessment Assessment(string outcome, params (string Key, double Value)[] facts) =>
        new(outcome, new JsonObject(facts.Select(fact => new KeyValuePair<string, JsonNode?>(fact.Key, JsonValue.Create(fact.Value)))));
    private static IReadOnlyList<JsonObject> Children(JsonObject objective) => Objects(objective["objectives"], "objectives").ToArray();
    private static IReadOnlyList<JsonObject> Nested(JsonObject objective) => [Object(objective["objective"], "objective")];
    private static ObjectiveAssessment Stage(ObjectiveEvaluationContext context, JsonObject objective, bool any)
    {
        var children = Children(objective);
        var outcomes = children.Where(child => context.Handlers.Role(child) != "optional" && context.Status(child) is not ("inactive" or "cancelled"))
            .Select(context.Outcome).ToArray();
        var outcome = outcomes.Length == 0 ? "pending" : any
            ? outcomes.Contains("success") ? "success" : outcomes.All(value => value == "failure") ? "failure" : "pending"
            : outcomes.Contains("failure") ? "failure" : outcomes.All(value => value == "success") ? "success" : "pending";
        return Assessment(outcome, ("completed", children.Count(child => context.Status(child) == "completed")), ("total", children.Count));
    }
    private static IEnumerable<JsonObject> Zone(JsonObject state, string id) => Objects(state["scenario"]!["zones"]![id] ?? new JsonArray(), "zone");
    private static IEnumerable<JsonObject> OwnedTerrains(ObjectiveEvaluationContext context, int owner)
    {
        var map = Object(context.State["map"], "map"); var owners = Array(map["owners"], "owners"); var tiles = Array(map["tiles"], "tiles");
        for (var index = 0; index < owners.Count; index++)
            if (Integer(owners[index], "owner") == owner) yield return context.Content.Source.Get("terrains", Text(tiles[index], "terrain"));
    }
    private static bool Enemy(ObjectiveEvaluationContext context, int id) => id != 0 && context.Owner != 0 && Team(context.State, id) != Team(context.State, context.Owner);
    private static int Team(JsonObject state, int id) => Players(state).FirstOrDefault(player => Integer(player, "id") == id) is { } found ? Integer(found, "team") : -id;
    private static PayloadReferences ProtectReferences(JsonObject objective)
    {
        var references = new PayloadReferences().Selector(Object(objective["selector"], "selector"));
        return Number(objective["minimumAlive"], "minimumAlive") >= 1 && Number(objective["untilTurn"], "untilTurn") >= 1 ? references : references.Fault("保护目标的人数和截止回合必须 >= 1");
    }
    private static PayloadReferences EscortReferences(JsonObject objective)
    {
        var references = new PayloadReferences().Zone(Text(objective, "zone")).Selector(Object(objective["selector"], "selector"));
        return Number(objective["count"], "count") >= 1 ? references : references.Fault("护送目标的抵达人数必须 >= 1");
    }
    private static double CompositeThreshold(JsonObject state, JsonObject objective) => Number(objective["minimumNeutralized"] ??
        CompositeRules.Require(state, Text(objective, "composite"))["minimumNeutralized"], "minimumNeutralized");
    private static PayloadReferences CompositeReferences(JsonObject objective)
    {
        var references = new PayloadReferences().Composite(Text(objective, "composite"));
        return !objective.ContainsKey("minimumNeutralized") || DefaultScenarioConditions.Whole(objective["minimumNeutralized"], 1) ? references : references.Fault("瘫痪数量必须 >= 1");
    }
    private static JsonObject NumericFact(double number) => double.IsFinite(number)
        ? new JsonObject { ["kind"] = "finite", ["value"] = number }
        : new JsonObject { ["kind"] = double.IsNaN(number) ? "nan" : number > 0 ? "positiveInfinity" : "negativeInfinity" };
    private static double ScoreNumber(JsonNode? value)
    {
        if (value is null) return 0;
        if (value is JsonValue scalar && scalar.TryGetValue<bool>(out var boolean)) return boolean ? 1 : 0;
        var text = value is JsonValue stringValue && stringValue.TryGetValue<string>(out var str) ? str.Trim() : value.ToJsonString();
        if (text.Length == 0) return 0;
        double number;
        if (text is "Infinity" or "+Infinity") return double.PositiveInfinity;
        if (text == "-Infinity") return double.NegativeInfinity;
        if (text.Length > 2 && text[0] == '0' && "xXoObB".Contains(text[1]))
        {
            var radix = char.ToLowerInvariant(text[1]) switch { 'x' => 16, 'o' => 8, _ => 2 };
            number = 0;
            foreach (var digit in text[2..])
            {
                var valueOfDigit = "0123456789abcdef".IndexOf(char.ToLowerInvariant(digit));
                if (valueOfDigit < 0 || valueOfDigit >= radix) return double.NaN;
                number = number * radix + valueOfDigit;
            }
        }
        else if (!Regex.IsMatch(text, @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$") ||
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return double.NaN;
        return number;
    }
}
