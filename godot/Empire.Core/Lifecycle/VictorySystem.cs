using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

public readonly record struct VictoryResult(int? Team, string Reason);

/// <summary>Source objective resolution and defeat ordering, including GrayFlag's critical survivor objective.</summary>
public sealed class VictorySystem
{
    private readonly StateContent content;
    private readonly ScenarioSystem scenarios;
    public VictorySystem(JsonArray packs, ScenarioSystem? scenarios = null)
    { content = new StateContent(packs); this.scenarios = scenarios ?? new ScenarioSystem(content); }
    internal VictorySystem(StateContent content, ScenarioSystem scenarios) { this.content = content; this.scenarios = scenarios; }

    public VictoryResult Evaluate(JsonObject state, Action<JsonObject> emit)
    {
        RefreshObjectiveStates(state, emit);
        foreach (var owner in Players(state))
        {
            if (!IsTrue(owner["alive"])) continue;
            var id = Integer(owner, "id");
            if (!Units(state).Any(unit => Integer(unit, "owner") == id) && !OwnedTerrains(state, id).Any(terrain => Array(terrain["produces"], "produces").Count > 0))
                owner["alive"] = false;
            if (Objects(owner["objectives"], "objectives").Any(objective => Role(objective) == "critical" && Status(state, id, objective) == "failed"))
                owner["alive"] = false;
        }
        var living = Players(state).Where(owner => IsTrue(owner["alive"])).ToArray();
        if (living.Length == 0) return new VictoryResult(null, "全员覆灭");
        if (living.Select(owner => Integer(owner, "team")).Distinct().Count() == 1)
            return new VictoryResult(Integer(living[0], "team"), "敌军已被全歼");
        foreach (var owner in living)
        {
            var completed = Objects(owner["objectives"], "objectives").FirstOrDefault(objective =>
                Role(objective) != "optional" && Status(state, Integer(owner, "id"), objective) == "completed");
            if (completed is not null) return new VictoryResult(Integer(owner, "team"), $"{Text(owner, "name")} 完成目标：{Describe(completed)}");
        }
        var limit = Object(state["rules"], "rules")["turnLimit"];
        return limit is not null && Number(state["turn"], "turn") > Number(limit, "turn limit")
            ? new VictoryResult(null, "回合数耗尽，平局") : new VictoryResult(null, "");
    }

    public void RefreshObjectiveStates(JsonObject state, Action<JsonObject> emit)
    {
        foreach (var owner in Players(state))
            foreach (var objective in Objects(owner["objectives"], "objectives")) RefreshOne(state, Integer(owner, "id"), objective, emit);
    }

    private void RefreshOne(JsonObject state, int owner, JsonObject objective, Action<JsonObject> emit)
    {
        if (Status(state, owner, objective) != "active") return;
        var type = Text(objective, "type");
        foreach (var child in Children(objective))
        {
            var status = Status(state, owner, child);
            if (type == "sequence" && (status == "completed" || status == "cancelled")) continue;
            RefreshOne(state, owner, child, emit);
            if (type == "sequence" && Status(state, owner, child) != "completed") break;
        }
        var outcome = Outcome(state, owner, objective);
        if (outcome == "pending") return;
        var id = Text(objective, "id");
        var runtime = Object(Object(Player(state, owner)["objectiveStates"], "objective states")[id], $"objective {id}");
        if (Text(runtime, "status") != "active") return;
        runtime["status"] = outcome == "success" ? "completed" : "failed";
        emit(new JsonObject { ["type"] = "objectiveChanged", ["player"] = owner, ["objective"] = id,
            ["status"] = runtime["status"]!.DeepClone(), ["hidden"] = runtime["hidden"]!.DeepClone() });
    }

    public string Outcome(JsonObject state, int owner, JsonObject objective)
    {
        var terminal = Status(state, owner, objective);
        if (terminal == "completed") return "success";
        if (terminal == "failed") return "failure";
        if (terminal is "inactive" or "cancelled") return "pending";
        bool Enemy(int id) => id != 0 && owner != 0 && Team(state, id) != Team(state, owner);
        string Decide(bool success) => success ? "success" : "pending";
        switch (Text(objective, "type"))
        {
            case "routEnemies": return Decide(Players(state).Where(candidate => Enemy(Integer(candidate, "id"))).All(candidate => !Units(state).Any(unit => Integer(unit, "owner") == Integer(candidate, "id"))));
            case "failOn": return scenarios.ConditionMet(state, Object(objective["condition"], "condition")) ? "failure" : Outcome(state, owner, Object(objective["objective"], "objective"));
            case "optional": return Outcome(state, owner, Object(objective["objective"], "objective"));
            case "all": case "any": case "sequence":
                var children = Children(objective).Where(child => Role(child) != "optional" && Status(state, owner, child) is not ("inactive" or "cancelled")).ToArray();
                if (children.Length == 0) return "pending";
                var outcomes = children.Select(child => Outcome(state, owner, child)).ToArray();
                if (Text(objective, "type") == "any") return outcomes.Contains("success") ? "success" : outcomes.All(value => value == "failure") ? "failure" : "pending";
                return outcomes.Contains("failure") ? "failure" : outcomes.All(value => value == "success") ? "success" : "pending";
            case "captureHQ":
                var contenders = Players(state).Where(candidate => Enemy(Integer(candidate, "id")) && IsTrue(candidate["startedWithHQ"])).ToArray();
                return Decide(contenders.Length > 0 && contenders.All(candidate => !OwnedTerrains(state, Integer(candidate, "id")).Any(terrain => IsTrue(terrain["hq"]))));
            case "holdAllVillages":
                var map = Object(state["map"], "map");
                var tiles = Array(map["tiles"], "tiles");
                var sites = Enumerable.Range(0, tiles.Count).Where(index => IsTrue(content.Get("terrains", Text(tiles[index], "terrain"))["capturable"])).ToArray();
                return Decide(sites.Length > 0 && sites.All(index => Integer(Array(map["owners"], "owners")[index], "owner") == owner));
            case "surviveTurns": return Decide(Number(state["turn"], "turn") > Number(objective["turns"], "turns"));
            case "eliminate": return Decide(scenarios.SelectUnits(state, Object(objective["selector"], "selector")).Count == 0);
            case "protect":
                if (scenarios.SelectUnits(state, Object(objective["selector"], "selector")).Count < Number(objective["minimumAlive"], "minimumAlive")) return "failure";
                return Decide(Number(state["turn"], "turn") > Number(objective["untilTurn"], "untilTurn"));
            case "destroy": return Decide(Array(objective["structures"], "structures").All(id =>
                Objects(state["structures"], "structures").FirstOrDefault(s => Text(s, "id") == Text(id, "structure id")) is not { } structure || Number(structure["hp"], "hp") <= 0));
            case "escort":
                var zone = ObjectiveZone(state, Text(objective, "zone")).ToArray();
                return Decide(scenarios.SelectUnits(state, Object(objective["selector"], "selector")).Count(unit => zone.Any(cell => ScenarioSystem.SameCell(cell, unit))) >= Number(objective["count"], "count"));
            case "control":
                var cells = ObjectiveZone(state, Text(objective, "zone")).ToArray();
                var field = Object(state["map"], "map");
                return Decide(cells.Length > 0 && cells.All(cell => Integer(Array(field["owners"], "owners")[Integer(cell, "y") * Integer(field, "width") + Integer(cell, "x")], "owner") == owner));
            case "score": return ScenarioSystem.Compare(Object(Object(state["scenario"], "scenario")["variables"], "variables")[Text(objective, "variable")], "gte", objective["atLeast"]) ? "success" : "pending";
            case "interact": return Decide(ScenarioSystem.Compare(Object(Object(state["scenario"], "scenario")["variables"], "variables")[Text(objective, "variable")], "eq", objective["equals"]));
            default: throw new DomainInvariantException($"Unsupported objective \"{Text(objective, "type")}\"");
        }
    }

    public static string Status(JsonObject state, int owner, JsonObject objective) => objective["id"] is { } id &&
        Object(Player(state, owner)["objectiveStates"], "objective states")[Text(id, "objective id")] is JsonObject runtime ? Text(runtime, "status") : "active";

    public static string Describe(JsonObject objective)
    {
        if (objective["label"] is JsonValue label && label.TryGetValue<string>(out var text) && text.Length > 0) return text;
        return Text(objective, "type") switch
        {
            "routEnemies" => "歼灭所有敌军", "captureHQ" => "攻占敌方城堡", "holdAllVillages" => "控制全部据点",
            "surviveTurns" => $"坚守 {Number(objective["turns"], "turns")} 回合", "eliminate" => "消灭指定目标", "destroy" => "摧毁指定结构",
            "protect" => $"保护目标至第 {Number(objective["untilTurn"], "untilTurn")} 回合", "escort" => "护送目标抵达区域", "control" => "控制指定区域",
            "score" => $"达成计数 {Number(objective["atLeast"], "atLeast")}", "interact" => "完成战场交互", "all" => "完成全部目标", "any" => "完成任一目标", "sequence" => "完成阶段目标",
            "optional" => $"额外：{Describe(Object(objective["objective"], "objective"))}", "failOn" => Describe(Object(objective["objective"], "objective")),
            var type => throw new DomainInvariantException($"Unsupported objective \"{type}\""),
        };
    }
    public static string Role(JsonObject objective) => Text(objective, "type") switch { "optional" => "optional", "failOn" => "critical", _ => "primary" };
    public static IEnumerable<JsonObject> Children(JsonObject objective) => Text(objective, "type") switch
    {
        "all" or "any" or "sequence" => Objects(objective["objectives"], "objectives"),
        "optional" or "failOn" => new[] { Object(objective["objective"], "objective") },
        _ => Enumerable.Empty<JsonObject>(),
    };
    private IEnumerable<JsonObject> OwnedTerrains(JsonObject state, int owner)
    {
        var map = Object(state["map"], "map");
        var owners = Array(map["owners"], "owners");
        var tiles = Array(map["tiles"], "tiles");
        for (var index = 0; index < owners.Count; index++)
            if (Integer(owners[index], "owner") == owner) yield return content.Get("terrains", Text(tiles[index], "terrain"));
    }
    private static IEnumerable<JsonObject> ObjectiveZone(JsonObject state, string id) => Objects(Object(Object(state["scenario"], "scenario")["zones"], "zones")[id], "zone");
    private static int Team(JsonObject state, int id) => Players(state).FirstOrDefault(player => Integer(player, "id") == id) is { } found ? Integer(found, "team") : -id;
}
