using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

internal readonly record struct CommandAura(double AttackMultiplier, double DefenseDelta, double MovementDelta);

internal static class TacticalModifiers
{
    internal static JsonObject? ActiveCommander(ITacticalGrid grid, JsonObject state, JsonObject unit)
    {
        if (unit["commanderId"] is not { } rawId) return null;
        var id = StateJson.Text(rawId, "commanderId");
        if (id.Length == 0) return null;
        var owner = StateJson.Integer(unit, "owner");
        var commander = StateJson.Objects(state["commanders"], "commanders")
            .FirstOrDefault(candidate => StateJson.Text(candidate, "id") == id);
        if (commander is null || StateJson.Integer(commander, "owner") != owner) return null;
        var leader = TacticalQueries.UnitById(state, StateJson.Integer(commander, "unitId"));
        if (leader is null || StateJson.Integer(leader, "owner") != owner) return null;
        return grid.Distance(TacticalQueries.Position(unit), TacticalQueries.Position(leader)) <=
            StateJson.Number(commander, "radius", 0) ? commander : null;
    }

    internal static CommandAura CommanderAura(ITacticalGrid grid, JsonObject state, JsonObject unit)
    {
        var commander = ActiveCommander(grid, state, unit);
        if (commander is null) return new(1, 0, 0);
        var aura = StateJson.Object(commander["aura"], "commander aura");
        return new(StateJson.Number(aura, "attackMultiplier", 1), StateJson.Number(aura, "defenseDelta", 0),
            StateJson.Number(aura, "movementDelta", 0));
    }

    internal static JsonObject? ActiveFormation(StateContent content, ITacticalGrid grid, JsonObject state, JsonObject unit)
    {
        if (unit["formation"] is not { } rawFormation) return null;
        var id = StateJson.Text(rawFormation, "formation");
        if (id.Length == 0) return null;
        var definition = content.TryGet("formations", id);
        if (definition is null) return null;
        var owner = StateJson.Integer(unit, "owner");
        var unitId = StateJson.Integer(unit, "id");
        var at = TacticalQueries.Position(unit);
        var adjacentAllies = StateJson.Objects(state["units"], "units").Count(candidate =>
            StateJson.Integer(candidate, "id") != unitId &&
            TacticalQueries.AreAllies(state, StateJson.Integer(candidate, "owner"), owner) &&
            grid.Distance(TacticalQueries.Position(candidate), at) == 1);
        return adjacentAllies >= StateJson.Number(definition, "minimumAdjacentAllies", 0) ? definition : null;
    }
}
