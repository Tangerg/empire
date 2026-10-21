using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.References;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Scenario;

public static class DefaultScenarioConditions
{
    public static ScenarioConditionHandlerRegistry Create(IBattleRandomSource random) => new ScenarioConditionHandlerRegistry(random)
        .Register(new("turnAtLeast", (context, condition) => Number(context.State["turn"], "turn") >= Number(condition["turn"], "turn")))
        .Register(new("turnCycle", (context, condition) =>
        {
            if (!Whole(condition["every"], 1)) return false;
            var every = Number(condition["every"], "every"); var offset = Round(Number(condition, "offset", 1)); var turn = Number(context.State["turn"], "turn");
            return turn >= offset && (turn - offset) % every == 0;
        })
        { References = condition => Whole(condition["every"], 1) ? new() : new PayloadReferences().Fault("循环回合条件的间隔必须是正整数") })
        .Register(new("chance", (context, condition) => context.Random.Chance(context.State,
            condition["stream"] is { } stream ? Text(stream, "stream") : "scenario.chance", Number(condition["percent"], "percent"))))
        .Register(new("currentPlayer", (context, condition) => Integer(context.State, "currentPlayer") == Integer(condition, "player"))
        { References = condition => new PayloadReferences().Player(Number(condition["player"], "player")) })
        .Register(new("variable", (context, condition) => ScenarioSelection.Compare(context.State["scenario"]!["variables"]![Text(condition, "key")], Text(condition, "op"), condition["value"])))
        .Register(new("unitInZone", (context, condition) =>
        {
            var selector = new JsonObject(); foreach (var field in new[] { "owner", "zone", "anyTags" }) CopyIfPresent(condition, selector, field);
            return ScenarioSelection.SelectUnits(context.Content, context.State, selector).Count > 0;
        })
        { References = condition => new PayloadReferences().Zone(Text(condition, "zone")) })
        .Register(new("unitCount", (context, condition) => ScenarioSelection.Compare(JsonValue.Create(ScenarioSelection.SelectUnits(context.Content, context.State, Object(condition["selector"], "selector")).Count), Text(condition, "op"), condition["value"]))
        { References = condition => new PayloadReferences().Selector(Object(condition["selector"], "selector")) })
        .Register(new("unitHealth", (context, condition) =>
        {
            var ratios = ScenarioSelection.SelectUnits(context.Content, context.State, Object(condition["selector"], "selector")).Select(unit =>
            {
                var maximum = Number(context.Content.Source.Get("units", Text(unit, "type"))["maxHp"], "maxHp");
                return maximum > 0 ? Math.Clamp(Number(unit["hp"], "hp") / maximum, 0, 1) : 1;
            }).ToArray();
            if (ratios.Length == 0) return false;
            bool Match(double value) => ScenarioSelection.Compare(JsonValue.Create(value), Text(condition, "op"), condition["value"]);
            return Text(condition, "aggregate") switch { "any" => ratios.Any(Match), "all" => ratios.All(Match), _ => Match(ratios.Average()) };
        })
        { References = condition => new PayloadReferences().Selector(Object(condition["selector"], "selector")) })
        .Register(new("markerCount", (context, condition) => ScenarioSelection.Compare(JsonValue.Create(ScenarioSelection.SelectMarkers(context.State, Object(condition["selector"], "selector")).Count), Text(condition, "op"), condition["value"]))
        { References = condition => new PayloadReferences().Selector(Object(condition["selector"], "selector")) })
        .Register(new("eventCount", (context, condition) => ScenarioSelection.Compare(context.State["scenario"]!["eventCounts"]![Text(condition, "event")] ?? JsonValue.Create(0), Text(condition, "op"), condition["value"])))
        .Register(new("structure", (context, condition) =>
        {
            var structure = Objects(context.State["structures"], "structures").FirstOrDefault(value => Text(value, "id") == Text(condition, "id"));
            var requested = Text(condition, "state"); if (structure is null) return requested == "destroyed";
            var hp = Number(structure["hp"], "hp");
            return requested == "destroyed" ? hp <= 0 : requested == "disabled" ? hp > 0 && IsTrue(structure["disabled"]) : hp > 0 && !IsTrue(structure["disabled"]);
        })
        { References = condition => new PayloadReferences().Structure(Text(condition, "id")) })
        .Register(new("composite", (context, condition) => CompositeRules.Status(context.State, Text(condition, "id")).State == Text(condition, "state"))
        { References = condition => new PayloadReferences().Composite(Text(condition, "id")) })
        .Register(new("objective", (context, condition) => Player(context.State, Integer(condition, "player"))["objectiveStates"]![Text(condition, "id")]?["status"]?.GetValue<string>() == Text(condition, "status"))
        { References = condition => new PayloadReferences().Player(Number(condition["player"], "player")).Objective(Number(condition["player"], "player"), Text(condition, "id")) })
        .Register(new("all", (context, condition) => Objects(condition["conditions"], "conditions").All(context.Evaluate))
        { Children = condition => Objects(condition["conditions"], "conditions").ToArray() })
        .Register(new("any", (context, condition) => Objects(condition["conditions"], "conditions").Any(context.Evaluate))
        { Children = condition => Objects(condition["conditions"], "conditions").ToArray() })
        .Register(new("not", (context, condition) => !context.Evaluate(Object(condition["condition"], "condition")))
        { Children = condition => [Object(condition["condition"], "condition")] });

    internal static bool Whole(JsonNode? value, double minimum = double.NegativeInfinity)
    {
        if (value is not JsonValue scalar || scalar.GetValueKind() != System.Text.Json.JsonValueKind.Number) return false;
        var number = Number(value, "number");
        return double.IsFinite(number) && number == Math.Truncate(number) && number >= minimum;
    }
}
