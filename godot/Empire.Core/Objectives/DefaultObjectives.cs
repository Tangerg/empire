using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.References;
using Empire.Core.Scenario;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Objectives;

public static class DefaultObjectives
{
    public static ObjectiveHandlerRegistry Create() => new ObjectiveHandlerRegistry()
        .Register(new("routEnemies", "歼灭所有敌军", (context, objective) =>
        {
            return Decide(Players(context.State).Where(candidate => Enemy(context, Integer(candidate, "id"))).All(candidate => !Units(context.State).Any(unit => Integer(unit, "owner") == Integer(candidate, "id"))));
        }, (context, objective) =>
        {
            return $"剩余敌军 {Players(context.State).Where(player => Enemy(context, Integer(player, "id"))).Sum(player => Units(context.State).Count(unit => Integer(unit, "owner") == Integer(player, "id")))}";
        }))
        .Register(new("captureHQ", "攻占敌方城堡", (context, objective) =>
        {
            var contenders = Players(context.State).Where(candidate => Enemy(context, Integer(candidate, "id")) && IsTrue(candidate["startedWithHQ"])).ToArray();
            return Decide(contenders.Length > 0 && contenders.All(candidate => !OwnedTerrains(context, Integer(candidate, "id")).Any(terrain => IsTrue(terrain["hq"]))));
        }, (context, objective) =>
        {
            return $"敌方城堡 {Players(context.State).Where(player => Enemy(context, Integer(player, "id"))).Sum(player => OwnedTerrains(context, Integer(player, "id")).Count(terrain => IsTrue(terrain["hq"])))}";
        }))
        .Register(new("holdAllVillages", "控制全部据点", (context, objective) =>
        {
            var map = Object(context.State["map"], "map");
            var tiles = Array(map["tiles"], "tiles");
            var sites = Enumerable.Range(0, tiles.Count).Where(index => IsTrue(context.Content.Source.Get("terrains", Text(tiles[index], "terrain"))["capturable"])).ToArray();
            return Decide(sites.Length > 0 && sites.All(index => Integer(Array(map["owners"], "owners")[index], "owner") == context.Owner));
        }, (context, objective) =>
        {
            var map = Object(context.State["map"], "map");
            var sites = Array(map["tiles"], "tiles").Select((tile, index) => (tile, index)).Where(entry => IsTrue(context.Content.Source.Get("terrains", Text(entry.tile, "terrain"))["capturable"])).ToArray();
            return $"{sites.Count(entry => Integer(map["owners"]![entry.index], "owner") == context.Owner)}/{sites.Length} 据点";
        }))
        .Register(new("surviveTurns", "坚守回合", (context, objective) =>
        {
            return Decide(Number(context.State["turn"], "turn") > Number(objective["turns"], "turns"));
        }, (context, objective) =>
        {
            return $"{Math.Min(Number(context.State["turn"], "turn"), Number(objective["turns"], "turns"))}/{Number(objective["turns"], "turns")} 回合";
        })
        { Describe = (objective, _) => $"坚守 {Number(objective["turns"], "turns")} 回合" })
        .Register(new("eliminate", "消灭指定目标", (context, objective) =>
        {
            return Decide(ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count == 0);
        }, (context, objective) =>
        {
            return $"剩余 {ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count}";
        })
        { References = objective => new PayloadReferences().Selector(Object(objective["selector"], "selector")) })
        .Register(new("destroy", "摧毁指定结构", (context, objective) =>
        {
            return Decide(Array(objective["structures"], "structures").All(id =>
                Objects(context.State["structures"], "structures").FirstOrDefault(s => Text(s, "id") == Text(id, "structure id")) is not { } structure || Number(structure["hp"], "hp") <= 0));
        }, (context, objective) =>
        {
            return $"剩余结构 {Array(objective["structures"], "structures").Count(id => Objects(context.State["structures"], "structures").Any(structure => Text(structure, "id") == Text(id, "id") && Number(structure["hp"], "hp") > 0))}";
        })
        { References = objective => Array(objective["structures"], "structures").Aggregate(new PayloadReferences(), (references, id) => references.Structure(Text(id, "id"))) })
        .Register(new("neutralizeComposite", "瘫痪复合战场目标",
            (context, objective) => Decide(CompositeRules.Status(context.State, Text(objective, "composite")).Neutralized >= CompositeThreshold(context.State, objective)),
            (context, objective) => $"{CompositeRules.Status(context.State, Text(objective, "composite")).Neutralized}/{CompositeThreshold(context.State, objective)} 部件")
        { References = objective => CompositeReferences(objective) })
        .Register(new("protect", "保护目标", (context, objective) =>
        {
            if (ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count < Number(objective["minimumAlive"], "minimumAlive")) return "failure";
            return Decide(Number(context.State["turn"], "turn") > Number(objective["untilTurn"], "untilTurn"));
        }, (context, objective) =>
        {
            return $"{ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count}/{Number(objective["minimumAlive"], "minimumAlive")} 存活";
        })
        { References = objective => ProtectReferences(objective), Describe = (objective, _) => $"保护目标至第 {Number(objective["untilTurn"], "untilTurn")} 回合" })
        .Register(new("escort", "护送目标抵达区域", (context, objective) =>
        {
            var zone = Zone(context.State, Text(objective, "zone")).ToArray();
            return Decide(ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count(unit => zone.Any(cell => ScenarioSelection.SameCell(cell, unit))) >= Number(objective["count"], "count"));
        }, (context, objective) =>
        {
            var zone = Zone(context.State, Text(objective, "zone")).ToArray();
            return $"{ScenarioSelection.SelectUnits(context.Content, context.State, Object(objective["selector"], "selector")).Count(unit => zone.Any(cell => ScenarioSelection.SameCell(cell, unit)))}/{Number(objective["count"], "count")} 抵达";
        })
        { References = objective => EscortReferences(objective) })
        .Register(new("control", "控制指定区域", (context, objective) =>
        {
            var cells = Zone(context.State, Text(objective, "zone")).ToArray();
            var field = Object(context.State["map"], "map");
            return Decide(cells.Length > 0 && cells.All(cell => Integer(Array(field["owners"], "owners")[Integer(cell, "y") * Integer(field, "width") + Integer(cell, "x")], "owner") == context.Owner));
        }, (context, objective) =>
        {
            var cells = Zone(context.State, Text(objective, "zone")).ToArray();
            var field = Object(context.State["map"], "map");
            return $"{cells.Count(cell => Integer(field["owners"]![Integer(cell, "y") * Integer(field, "width") + Integer(cell, "x")], "owner") == context.Owner)}/{cells.Length} 区域格";
        })
        { References = objective => new PayloadReferences().Zone(Text(objective, "zone")) })
        .Register(new("score", "达成计数", (context, objective) =>
        {
            return ScenarioSelection.Compare(Object(Object(context.State["scenario"], "scenario")["variables"], "variables")[Text(objective, "variable")], "gte", objective["atLeast"]) ? "success" : "pending";
        }, (context, objective) =>
        {
            return $"{ScoreNumber(context.State["scenario"]!["variables"]![Text(objective, "variable")])}/{Number(objective["atLeast"], "atLeast")}";
        })
        { Describe = (objective, _) => $"达成计数 {Number(objective["atLeast"], "atLeast")}" })
        .Register(new("interact", "完成战场交互", (context, objective) =>
        {
            return Decide(ScenarioSelection.Compare(Object(Object(context.State["scenario"], "scenario")["variables"], "variables")[Text(objective, "variable")], "eq", objective["equals"]));
        }, (context, objective) =>
        {
            return "进行中";
        }))
        .Register(new("all", "完成全部目标", (context, objective) => Fold(context, objective, false), StageProgress)
        { Refresh = "children", Children = Children })
        .Register(new("any", "完成任一目标", (context, objective) => Fold(context, objective, true), StageProgress)
        { Refresh = "children", Children = Children })
        .Register(new("sequence", "完成阶段目标", (context, objective) => Fold(context, objective, false), StageProgress)
        { Refresh = "sequence", Children = Children })
        .Register(new("optional", "可选目标", (context, objective) => context.Outcome(Object(objective["objective"], "objective")), NestedProgress)
        {
            Role = "optional",
            Refresh = "children",
            Children = Nested,
            Describe = (objective, handlers) => $"额外：{handlers.Describe(Object(objective["objective"], "objective"))}"
        })
        .Register(new("failOn", "失败条件", (context, objective) => context.ScenarioConditions.Evaluate(context.State,
            Object(objective["condition"], "condition"), context.Content) ? "failure" : context.Outcome(Object(objective["objective"], "objective")), NestedProgress)
        {
            Role = "critical",
            Refresh = "children",
            Children = Nested,
            Describe = (objective, handlers) => handlers.Describe(Object(objective["objective"], "objective")),
            References = objective => new PayloadReferences().Condition(Object(objective["condition"], "condition"))
        });

    private static string Decide(bool success) => success ? "success" : "pending";
    private static IReadOnlyList<JsonObject> Children(JsonObject objective) => Objects(objective["objectives"], "objectives").ToArray();
    private static IReadOnlyList<JsonObject> Nested(JsonObject objective) => [Object(objective["objective"], "objective")];
    private static string NestedProgress(ObjectiveEvaluationContext context, JsonObject objective) =>
        context.Handlers.Progress(context.Rules, context.State, context.Owner, Object(objective["objective"], "objective"));
    private static string StageProgress(ObjectiveEvaluationContext context, JsonObject objective) =>
        $"{Children(objective).Count(child => context.Status(child) == "completed")}/{Children(objective).Count} 阶段";
    private static string Fold(ObjectiveEvaluationContext context, JsonObject objective, bool any)
    {
        var outcomes = context.Handlers.Children(objective).Where(child => context.Handlers.Role(child) != "optional" &&
            context.Status(child) is not ("inactive" or "cancelled")).Select(context.Outcome).ToArray();
        if (outcomes.Length == 0) return "pending";
        return any ? outcomes.Contains("success") ? "success" : outcomes.All(value => value == "failure") ? "failure" : "pending"
            : outcomes.Contains("failure") ? "failure" : outcomes.All(value => value == "success") ? "success" : "pending";
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
    private static string ScoreNumber(JsonNode? value)
    {
        if (value is null) return "0";
        if (value is JsonValue scalar && scalar.TryGetValue<bool>(out var boolean)) return boolean ? "1" : "0";
        var text = value is JsonValue stringValue && stringValue.TryGetValue<string>(out var str) ? str.Trim() : value.ToJsonString();
        if (text.Length == 0) return "0";
        double number;
        if (text is "Infinity" or "+Infinity") return "Infinity";
        if (text == "-Infinity") return "-Infinity";
        if (text.Length > 2 && text[0] == '0' && "xXoObB".Contains(text[1]))
        {
            var radix = char.ToLowerInvariant(text[1]) switch { 'x' => 16, 'o' => 8, _ => 2 };
            number = 0;
            foreach (var digit in text[2..])
            {
                var valueOfDigit = "0123456789abcdef".IndexOf(char.ToLowerInvariant(digit));
                if (valueOfDigit < 0 || valueOfDigit >= radix) return "NaN";
                number = number * radix + valueOfDigit;
            }
        }
        else if (!Regex.IsMatch(text, @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$") ||
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return "NaN";
        return double.IsPositiveInfinity(number) ? "Infinity" : double.IsNegativeInfinity(number) ? "-Infinity" : EcmaNumber.Format(number);
    }
}
