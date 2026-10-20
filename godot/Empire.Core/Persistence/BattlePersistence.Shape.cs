using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Persistence;

public sealed partial class BattlePersistence
{
    private static void CheckShape(JsonObject save)
    {
        StoredSchema.Require(save, "存档",
            ("schema", StoredKind.Number), ("battle", StoredKind.Object), ("ruleset", StoredKind.Object), ("savedAt", StoredKind.String), ("state", StoredKind.Object));
        var state = State(save);
        StoredSchema.Require(state, "战斗",
            ("levelId", StoredKind.String), ("levelName", StoredKind.String), ("map", StoredKind.Object), ("units", StoredKind.Array),
            ("structures", StoredKind.Array), ("composites", StoredKind.Array), ("embarkedUnits", StoredKind.Array), ("markers", StoredKind.Array),
            ("commanders", StoredKind.Array), ("players", StoredKind.Array), ("rules", StoredKind.Object), ("turn", StoredKind.Number),
            ("currentPlayer", StoredKind.Number), ("phase", StoredKind.String), ("winnerTeam", StoredKind.NullableNumber), ("endReason", StoredKind.String),
            ("nextUnitId", StoredKind.Number), ("nextMarkerId", StoredKind.Number), ("deployment", StoredKind.NullableObject),
            ("scenario", StoredKind.Object), ("turnOrder", StoredKind.Object), ("actorTurns", StoredKind.Number), ("pendingCasts", StoredKind.Array), ("random", StoredKind.Object));
        StoredSchema.Require(state["map"], "地图",
            ("width", StoredKind.Number), ("height", StoredKind.Number), ("tiles", StoredKind.Array), ("owners", StoredKind.Array),
            ("captureProgress", StoredKind.Array), ("elevation", StoredKind.Array), ("cliffs", StoredKind.Array), ("directionalCover", StoredKind.Array));
        StoredSchema.Require(state["scenario"], "剧本",
            ("variables", StoredKind.Object), ("zones", StoredKind.Object), ("overlays", StoredKind.Array), ("triggers", StoredKind.Array),
            ("firedTriggerIds", StoredKind.Array), ("triggerRuntime", StoredKind.Object), ("eventCounts", StoredKind.Object),
            ("zoneTags", StoredKind.Object), ("engagementRules", StoredKind.Array));
        StoredSchema.Require(state["turnOrder"], "行动顺序", ("policy", StoredKind.String), ("activeUnit", StoredKind.NullableNumber), ("data", StoredKind.Object));
        StoredSchema.Require(state["random"], "随机流", ("seed", StoredKind.Number), ("counters", StoredKind.Object));
        if (state["deployment"] is not null)
            StoredSchema.Require(state["deployment"], "部署", ("order", StoredKind.Array), ("currentIndex", StoredKind.Number), ("assignments", StoredKind.Array));
    }

    private static void RequireUnitShape(JsonObject unit, string owner)
    {
        StoredSchema.Require(unit, owner,
            ("id", StoredKind.Integer), ("type", StoredKind.String), ("owner", StoredKind.Integer), ("x", StoredKind.Integer), ("y", StoredKind.Integer),
            ("hp", StoredKind.Number), ("done", StoredKind.Boolean), ("capture", StoredKind.Number), ("statuses", StoredKind.Array),
            ("weaponState", StoredKind.Object), ("rank", StoredKind.Number), ("rankProgress", StoredKind.Number), ("resources", StoredKind.Object),
            ("reaction", StoredKind.String), ("reactionUsedRound", StoredKind.Number), ("facing", StoredKind.String), ("morale", StoredKind.Object),
            ("directive", StoredKind.Object), ("career", StoredKind.Object), ("learnedAbilities", StoredKind.Array));
        StoredSchema.Require(unit["directive"], $"{owner}的常驻命令", ("mode", StoredKind.String), ("waypoints", StoredKind.Array), ("cursor", StoredKind.Integer));
        StoredSchema.Require(unit["career"], $"{owner}的职业", ("unlocked", StoredKind.Array), ("mastery", StoredKind.Object));
        if (!unit["career"]!.AsObject().ContainsKey("current") ||
            unit["career"]!["current"] is { } current && !StoredSchema.Matches(current, StoredKind.String))
            StoredSchema.Reject($"{owner}的职业损坏");
        StoredSchema.Require(unit["morale"], $"{owner}的士气", ("current", StoredKind.Number), ("maximum", StoredKind.Number), ("resilience", StoredKind.Number));
        foreach (var status in StateJson.Objects(unit["statuses"], "statuses"))
            StoredSchema.Require(status, $"{owner}的状态", ("id", StoredKind.String), ("stacks", StoredKind.Number), ("remaining", StoredKind.Number));
        foreach (var weapon in unit["weaponState"]!.AsObject())
            StoredSchema.Require(weapon.Value, $"{owner}的武器", ("cooldownRemaining", StoredKind.Number), ("resources", StoredKind.Object));
    }

    private void CheckNativeState(JsonObject save)
    {
        var state = State(save);
        var map = state["map"]!.AsObject();
        var width = StateJson.Integer(map, "width");
        var height = StateJson.Integer(map, "height");
        foreach (var field in new[] { "turn", "nextUnitId", "nextMarkerId", "actorTurns" })
            if (!StoredSchema.Matches(state[field], StoredKind.Integer) ||
                StateJson.Number(state[field], field) < (field == "actorTurns" ? 0 : 1))
                StoredSchema.Reject($"战斗的「{field}」数值不合法");
        var highestUnit = AllUnits(state).Select(entry => StateJson.Integer(entry.Unit, "id")).DefaultIfEmpty(0).Max();
        var highestMarker = StateJson.Objects(state["markers"], "markers")
            .Select(marker => StateJson.Integer(marker, "id")).DefaultIfEmpty(0).Max();
        if (StateJson.Number(state["nextUnitId"], "nextUnitId") <= highestUnit ||
            StateJson.Number(state["nextMarkerId"], "nextMarkerId") <= highestMarker)
            StoredSchema.Reject("战斗的后续标识会重复");
        var players = StateJson.Objects(state["players"], "players").ToArray();
        foreach (var player in players)
            StoredSchema.Require(player, "玩家", ("id", StoredKind.Integer), ("team", StoredKind.Integer), ("alive", StoredKind.Boolean),
                ("resources", StoredKind.Object), ("objectives", StoredKind.Array), ("objectiveStates", StoredKind.Object), ("ai", StoredKind.Object));
        var owners = players.Select(player => StateJson.Integer(player, "id")).ToHashSet();
        if (owners.Count != players.Length || !owners.Contains(StateJson.Integer(state, "currentPlayer")))
            StoredSchema.Reject("玩家标识或当前玩家不合法");
        if (StateJson.Text(state["rules"]!.AsObject(), "turnOrder") != StateJson.Text(state["turnOrder"]!.AsObject(), "policy"))
            StoredSchema.Reject("行动顺序与关卡规则不一致");
        var seed = StateJson.Number(state["random"]!["seed"], "seed");
        if (seed < 0 || seed > uint.MaxValue || seed != Math.Truncate(seed)) StoredSchema.Reject("随机流种子不合法");
        foreach (var counter in state["random"]!["counters"]!.AsObject())
            if (!StoredSchema.Matches(counter.Value, StoredKind.Integer) || StateJson.Number(counter.Value, "counter") < 0)
                StoredSchema.Reject("随机流计数器不合法");
        var ids = new HashSet<int>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var unit in StateJson.Objects(state["units"], "units"))
        {
            var x = StateJson.Integer(unit, "x");
            var y = StateJson.Integer(unit, "y");
            if (x < 0 || y < 0 || x >= width || y >= height || !occupied.Add((x, y)))
                StoredSchema.Reject("单位位置越界或重叠");
        }
        var livingUnits = StateJson.Objects(state["units"], "units").Concat(
            StateJson.Objects(state["embarkedUnits"], "embarkedUnits").Select(entry => StateJson.Object(entry["unit"], "embarked unit")));
        foreach (var unit in livingUnits)
        {
            if (StateJson.Integer(unit, "id") < 1 || !ids.Add(StateJson.Integer(unit, "id")) || !owners.Contains(StateJson.Integer(unit, "owner")))
                StoredSchema.Reject("单位标识或所属玩家不合法");
            if (unit["key"] is { } key && StateJson.Text(key, "unit key") is { Length: > 0 } text && !keys.Add(text))
                StoredSchema.Reject("存活单位的键重复");
            if (StateJson.Number(unit["hp"], "hp") <= 0) StoredSchema.Reject("存活单位生命值不合法");
        }
        foreach (var pending in StateJson.Objects(state["pendingCasts"], "pendingCasts"))
        {
            StoredSchema.Require(pending, "咏唱", ("caster", StoredKind.Integer), ("owner", StoredKind.Integer), ("ability", StoredKind.String),
                ("weapon", StoredKind.String), ("target", StoredKind.Object), ("origin", StoredKind.Object), ("declaredAt", StoredKind.Number), ("resolveAt", StoredKind.Number));
            if (StateJson.Number(pending["resolveAt"], "resolveAt") <= StateJson.Number(pending["declaredAt"], "declaredAt"))
                StoredSchema.Reject("咏唱结算时刻不合法");
        }
        var limitations = features.StateIssues(state).ToArray();
        if (limitations.Length > 0) StoredSchema.Reject(string.Join("；", limitations));
    }
}
