using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public static class TacticalQueries
{
    public static Coord Position(JsonObject entity) =>
        new(StateJson.Integer(entity, "x"), StateJson.Integer(entity, "y"));

    public static JsonObject? UnitAt(JsonObject state, Coord at) =>
        StateJson.Objects(state["units"], "units").FirstOrDefault(unit => Position(unit) == at);

    public static JsonObject? UnitById(JsonObject state, int id) =>
        StateJson.Objects(state["units"], "units").FirstOrDefault(unit => StateJson.Integer(unit, "id") == id);

    public static JsonObject RequireUnit(JsonObject state, int id) =>
        UnitById(state, id) ?? throw new DomainInvariantException($"no unit with id {id}");

    public static JsonObject Player(JsonObject state, int id) =>
        StateJson.Objects(state["players"], "players").FirstOrDefault(player => StateJson.Integer(player, "id") == id)
        ?? throw new DomainInvariantException($"no player with id {id}");

    public static int TeamOf(JsonObject state, int owner)
    {
        var player = StateJson.Objects(state["players"], "players")
            .FirstOrDefault(candidate => StateJson.Integer(candidate, "id") == owner);
        return player is null ? -owner : StateJson.Integer(player, "team");
    }

    public static bool AreAllies(JsonObject state, int left, int right) =>
        left != 0 && right != 0 && TeamOf(state, left) == TeamOf(state, right);

    public static bool AreEnemies(JsonObject state, int left, int right) =>
        left != 0 && right != 0 && TeamOf(state, left) != TeamOf(state, right);

    public static IReadOnlyList<JsonObject> UnitsOf(JsonObject state, int owner) =>
        StateJson.Objects(state["units"], "units").Where(unit => StateJson.Integer(unit, "owner") == owner).ToArray();

    public static JsonObject? StructureAt(JsonObject state, Coord at) =>
        StateJson.Objects(state["structures"], "structures")
            .FirstOrDefault(structure => StateJson.Number(structure, "hp", 0) > 0 && Position(structure) == at);

    public static JsonObject CoordinateJson(Coord at) => new() { ["x"] = at.X, ["y"] = at.Y };

    public static JsonArray PathJson(IEnumerable<Coord> path)
    {
        var result = new JsonArray();
        foreach (var at in path)
            result.Add(CoordinateJson(at));
        return result;
    }
}
