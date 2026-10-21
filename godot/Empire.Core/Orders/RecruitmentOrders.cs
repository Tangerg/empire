using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Orders;

public sealed record RecruitCard(string Unit, IReadOnlyList<BattleResources.Cost> Costs, bool Affordable);

/// <summary>Recruitment cards and authoritative command validation.</summary>
public sealed record RecruitmentRules(
    ReadOnlyContent Content,
    GridRegistry Grids,
    BattleResources Resources,
    Func<JsonObject, string, int, Coord, bool, JsonObject> Spawn);

public sealed class RecruitmentOrders
{
    private readonly StateContent content;
    private readonly GridRegistry grids;
    private TacticalBoard Board(JsonObject state) => new(StateJson.Object(state["map"], "map"),
        grids.Get(StateJson.Text(state["rules"]!["grid"], "grid")));
    private readonly BattleResources resources;
    private readonly Func<JsonObject, string, int, Coord, bool, JsonObject> spawn;
    public RecruitmentOrders(RecruitmentRules rules)
    {
        content = rules.Content.Source;
        grids = rules.Grids;
        resources = rules.Resources;
        spawn = rules.Spawn;
    }

    public ActionRefusal? Refusal(JsonObject state, string type, Coord at)
    {
        var board = Board(state);
        if (!board.Contains(at)) return new("tactics.target_outside", new() { ["x"] = at.X, ["y"] = at.Y });
        var index = board.IndexOf(at);
        var terrain = content.Get("terrains", StateJson.Text(state["map"]!["tiles"]![index], "terrain"));
        if (!StateJson.Array(terrain["produces"], "produces").Any(value => StateJson.Text(value,"unit") == type))
            return new("recruitment.terrain_incompatible", new() { ["terrainId"] = StateJson.Text(terrain, "id"), ["unitType"] = type });
        var owner = StateJson.Integer(state,"currentPlayer");
        if (state["map"]!["owners"]![index] is not { } tileOwner || StateJson.Integer(tileOwner,"owner") != owner) return new("recruitment.not_owner");
        if (TacticalQueries.UnitAt(state,at) is not null) return new("recruitment.occupied");
        var definition = content.Get("units",type);
        var subject = new BattleResources.Subject("player",TacticalQueries.Player(state,owner));
        if (!resources.CanAfford(subject,StateJson.Array(definition["recruitCosts"],"recruitCosts"))) return new("recruitment.insufficient_resources");
        var cap = state["rules"]!["maxUnitsPerPlayer"];
        if (cap is not null && StateJson.Objects(state["units"],"units").Count(unit => StateJson.Integer(unit,"owner") == owner) >= StateJson.Number(cap,"unit cap"))
            return new("recruitment.unit_limit", new() { ["limit"] = StateJson.Number(cap, "unit cap") });
        return null;
    }

    public IReadOnlyList<string> Options(JsonObject state, Coord at)
    {
        var board = Board(state);
        if (!board.Contains(at)) return [];
        var terrain = content.Get("terrains",StateJson.Text(state["map"]!["tiles"]![board.IndexOf(at)],"terrain"));
        return StateJson.Array(terrain["produces"],"produces").Select(value => StateJson.Text(value,"unit"))
            .Where(type => Refusal(state,type,at) is null).ToArray();
    }

    public IReadOnlyList<RecruitCard> Cards(JsonObject state, Coord at)
    {
        var board = Board(state);
        if (!board.Contains(at)) return [];
        var index = board.IndexOf(at);
        var terrain = content.Get("terrains", StateJson.Text(state["map"]!["tiles"]![index], "terrain"));
        var owner = StateJson.Integer(state, "currentPlayer");
        if (state["map"]!["owners"]![index] is not { } tileOwner || StateJson.Integer(tileOwner, "owner") != owner) return [];
        var subject = new BattleResources.Subject("player", TacticalQueries.Player(state, owner));
        return StateJson.Array(terrain["produces"], "produces").Select(value =>
        {
            var type = StateJson.Text(value, "unit");
            var costs = BattleResources.CopyCosts(StateJson.Array(content.Get("units", type)["recruitCosts"], "recruitCosts"));
            // Source cards check each authored cost, while dispatch aggregates duplicate resources.
            return new RecruitCard(type, costs, costs.All(cost => resources.CanSpend(subject, cost.Resource, cost.Amount)));
        }).ToArray();
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
