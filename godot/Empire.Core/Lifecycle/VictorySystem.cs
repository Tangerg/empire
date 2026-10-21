using System.Text.Json.Nodes;
using Empire.Core.Objectives;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Lifecycle;

public readonly record struct VictoryResult(int? Team, string Reason);
public sealed record ObjectiveProgress(string Id, string Description, string Status, string Progress, string Role);

/// <summary>Owns objective transitions and defeat ordering; handlers own objective policy.</summary>
public sealed class VictorySystem(ObjectiveRules rules, ObjectiveHandlerRegistry handlers)
{
    public ObjectiveRules Rules { get; } = rules;
    public ObjectiveHandlerRegistry Handlers { get; } = handlers;
    public VictoryResult Evaluate(JsonObject state, Action<JsonObject> emit)
    {
        RefreshObjectiveStates(state, emit);
        foreach (var owner in Players(state))
        {
            if (!IsTrue(owner["alive"])) continue;
            var id = Integer(owner, "id");
            if (!Units(state).Any(unit => Integer(unit, "owner") == id) && !OwnedTerrains(state, id).Any(terrain => Array(terrain["produces"], "produces").Count > 0))
                owner["alive"] = false;
            if (Objects(owner["objectives"], "objectives").Any(objective => Handlers.Role(objective) == "critical" && Status(state, id, objective) == "failed"))
                owner["alive"] = false;
        }
        var living = Players(state).Where(owner => IsTrue(owner["alive"])).ToArray();
        if (living.Length == 0) return new VictoryResult(null, "全员覆灭");
        if (living.Select(owner => Integer(owner, "team")).Distinct().Count() == 1)
            return new VictoryResult(Integer(living[0], "team"), "敌军已被全歼");
        foreach (var owner in living)
        {
            var completed = Objects(owner["objectives"], "objectives").FirstOrDefault(objective =>
                Handlers.Role(objective) != "optional" && Status(state, Integer(owner, "id"), objective) == "completed");
            if (completed is not null) return new VictoryResult(Integer(owner, "team"), $"{Text(owner, "name")} 完成目标：{Handlers.Describe(completed)}");
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
        var mode = Handlers.RefreshMode(objective);
        foreach (var child in mode == "self" ? [] : Handlers.Children(objective))
        {
            var status = Status(state, owner, child);
            if (mode == "sequence" && (status == "completed" || status == "cancelled")) continue;
            RefreshOne(state, owner, child, emit);
            if (mode == "sequence" && Status(state, owner, child) != "completed") break;
        }
        var outcome = Outcome(state, owner, objective);
        if (outcome == "pending") return;
        var id = Text(objective, "id");
        var runtime = Object(Object(Player(state, owner)["objectiveStates"], "objective states")[id], $"objective {id}");
        if (Text(runtime, "status") != "active") return;
        runtime["status"] = outcome == "success" ? "completed" : "failed";
        emit(new JsonObject
        {
            ["type"] = "objectiveChanged",
            ["player"] = owner,
            ["objective"] = id,
            ["status"] = runtime["status"]!.DeepClone(),
            ["hidden"] = runtime["hidden"]!.DeepClone()
        });
    }

    public string Outcome(JsonObject state, int owner, JsonObject objective) => Handlers.Evaluate(Rules, state, owner, objective);
    public string Describe(JsonObject objective) => Handlers.Describe(objective);
    public string Progress(JsonObject state, int owner, JsonObject objective) => Handlers.Progress(Rules, state, owner, objective);
    public static string Status(JsonObject state, int owner, JsonObject objective) => ObjectiveHandlerRegistry.Status(state, owner, objective);
    public IReadOnlyList<ObjectiveProgress> Objectives(JsonObject state, int owner) =>
        Objects(Player(state, owner)["objectives"], "objectives")
            .Where(objective => !IsTrue(Player(state, owner)["objectiveStates"]?[Text(objective, "id")]?["hidden"]))
            .Select(objective => new ObjectiveProgress(Text(objective, "id"), objective["label"] is { } label ? Text(label, "label") : Describe(objective),
                Status(state, owner, objective), Progress(state, owner, objective), Handlers.Role(objective))).ToArray();
    private IEnumerable<JsonObject> OwnedTerrains(JsonObject state, int owner)
    {
        var map = Object(state["map"], "map"); var owners = Array(map["owners"], "owners"); var tiles = Array(map["tiles"], "tiles");
        for (var index = 0; index < owners.Count; index++)
            if (Integer(owners[index], "owner") == owner) yield return Rules.Content.Source.Get("terrains", Text(tiles[index], "terrain"));
    }
}
