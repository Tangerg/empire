using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Orders;

/// <summary>One recruitment validator shared by menus and committed orders.</summary>
public sealed class RecruitmentOrders
{
    private readonly StateContent content;
    private readonly MovementRules movement;
    private readonly BattleResources resources;
    private readonly Func<JsonObject, string, int, Coord, bool, JsonObject> spawn;
    public RecruitmentOrders(JsonArray packs, MovementRules movement, BattleResources resources,
        Func<JsonObject, string, int, Coord, bool, JsonObject> spawn)
    { content = new StateContent(packs); this.movement = movement; this.resources = resources; this.spawn = spawn; }

    public string? Refusal(JsonObject state, string type, Coord at)
    {
        var board = movement.Board(state);
        if (!board.Contains(at)) return $"目标格 {at.X},{at.Y} 不在战场内";
        var index = board.IndexOf(at);
        var terrain = content.Get("terrains", StateJson.Text(state["map"]!["tiles"]![index], "terrain"));
        if (!StateJson.Array(terrain["produces"], "produces").Any(value => StateJson.Text(value,"unit") == type))
            return $"{StateJson.Text(terrain,"name")} 无法生产该兵种";
        var owner = StateJson.Integer(state,"currentPlayer");
        if (state["map"]!["owners"]![index] is not { } tileOwner || StateJson.Integer(tileOwner,"owner") != owner) return "该建筑不属于你";
        if (TacticalQueries.UnitAt(state,at) is not null) return "建筑上已有单位";
        var definition = content.Get("units",type);
        var subject = new BattleResources.Subject("player",TacticalQueries.Player(state,owner));
        if (!resources.CanAfford(subject,StateJson.Array(definition["recruitCosts"],"recruitCosts"))) return "招募资源不足";
        var cap = state["rules"]!["maxUnitsPerPlayer"];
        if (cap is not null && StateJson.Objects(state["units"],"units").Count(unit => StateJson.Integer(unit,"owner") == owner) >= StateJson.Number(cap,"unit cap"))
            return $"单位数量已达上限 {cap}";
        return null;
    }

    public IReadOnlyList<string> Options(JsonObject state, Coord at)
    {
        var board = movement.Board(state);
        if (!board.Contains(at)) return [];
        var terrain = content.Get("terrains",StateJson.Text(state["map"]!["tiles"]![board.IndexOf(at)],"terrain"));
        return StateJson.Array(terrain["produces"],"produces").Select(value => StateJson.Text(value,"unit"))
            .Where(type => Refusal(state,type,at) is null).ToArray();
    }

    public void Recruit(JsonObject state, string type, Coord at, Action<JsonObject> emit)
    {
        if (Refusal(state,type,at) is { } reason) throw new IllegalActionException(reason);
        var owner = StateJson.Integer(state,"currentPlayer");
        var subject = new BattleResources.Subject("player",TacticalQueries.Player(state,owner));
        foreach (var cost in resources.SpendAll(subject,StateJson.Array(content.Get("units",type)["recruitCosts"],"recruitCosts")))
            resources.Announce(subject,cost.Resource,-cost.Value,emit);
        var unit = spawn(state,type,owner,at,!StateJson.IsTrue(state["rules"]!["recruitsActImmediately"]));
        emit(new JsonObject { ["type"]="recruit",["unit"]=unit["id"]!.DeepClone(),["at"]=TacticalQueries.CoordinateJson(at) });
    }
}
