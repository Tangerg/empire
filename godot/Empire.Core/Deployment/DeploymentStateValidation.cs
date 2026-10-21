using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Deployment;

/// <summary>Stored deployment must describe the same already-owned unit arrangement as a live deployment.</summary>
public static class DeploymentStateValidation
{
    public static IEnumerable<string> Issues(JsonObject state)
    {
        var deploying = StateJson.Text(state, "phase") == "deployment";
        if (state["deployment"] is null)
        {
            if (deploying)
                yield return "部署阶段缺少部署状态";
            yield break;
        }
        if (!deploying)
            yield return "战斗开始后仍有部署状态";
        var deployment = StateJson.Object(state["deployment"], "deployment");
        var order = StateJson.Array(deployment["order"], "deployment order")
            .Select(id => StateJson.Integer(id, "deployment player")).ToArray();
        var players = StateJson.Objects(state["players"], "players").Select(player => StateJson.Integer(player, "id")).ToHashSet();
        if (order.Length == 0 || order.Distinct().Count() != order.Length || order.Any(id => !players.Contains(id)))
            yield return "部署顺序为空、重复或引用未知玩家";
        var index = StateJson.Integer(deployment, "currentIndex");
        if (index < 0 || index >= order.Length || order[index] != StateJson.Integer(state, "currentPlayer"))
            yield return "部署索引与当前玩家不一致";
        var zones = StateJson.Object(StateJson.Object(state["scenario"], "scenario")["zones"], "zones");
        var units = StateJson.Objects(state["units"], "units").ToDictionary(unit => StateJson.Integer(unit, "id"));
        var assigned = new HashSet<int>();
        var assignedPlayers = new HashSet<int>();
        var map = StateJson.Object(state["map"], "map");
        var width = StateJson.Integer(map, "width");
        var height = StateJson.Integer(map, "height");
        foreach (var assignment in StateJson.Objects(deployment["assignments"], "deployment assignments"))
        {
            var player = StateJson.Integer(assignment, "player");
            assignedPlayers.Add(player);
            if (!players.Contains(player))
                yield return "部署编组引用未知玩家";
            var zoneId = StateJson.Text(assignment, "zone");
            if (zones[zoneId] is not JsonArray zone)
                yield return $"部署区域「{zoneId}」缺失或损坏";
            else
                foreach (var cell in StateJson.Objects(zone, "deployment zone"))
                {
                    var x = StateJson.Integer(cell, "x");
                    var y = StateJson.Integer(cell, "y");
                    if (x < 0 || y < 0 || x >= width || y >= height)
                        yield return $"部署区域「{zoneId}」越界";
                }
            foreach (var id in StateJson.Array(assignment["unitIds"], "deployment unit IDs").Select(id => StateJson.Integer(id, "unit ID")))
                if (!assigned.Add(id) || !units.TryGetValue(id, out var unit) || StateJson.Integer(unit, "owner") != player)
                    yield return $"部署单位 {id} 重复、缺失或所属玩家不符";
        }
        if (order.Any(player => !assignedPlayers.Contains(player)))
            yield return "部署顺序中的玩家没有部署编组";
        // Empty assignments, overlapping zones and units initially outside their zones are all source-valid.
    }
}
