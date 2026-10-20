using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public readonly record struct CommandAura(double AttackMultiplier, double DefenseDelta, double MovementDelta);

public static class TacticalModifiers
{
    public static JsonObject? CommanderUnit(JsonObject state, JsonObject commander) =>
        TacticalQueries.UnitById(state, StateJson.Integer(commander, "unitId"))?.DeepClone().AsObject();

    public static JsonObject? ActiveCommanderFor(ITacticalGrid grid, JsonObject state, JsonObject unit) =>
        ActiveCommander(grid, state, unit)?.DeepClone().AsObject();

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

    public static CommandAura CommanderAura(ITacticalGrid grid, JsonObject state, JsonObject unit)
    {
        var commander = ActiveCommander(grid, state, unit);
        if (commander is null) return new(1, 0, 0);
        var aura = StateJson.Object(commander["aura"], "commander aura");
        return new(StateJson.Number(aura, "attackMultiplier", 1), StateJson.Number(aura, "defenseDelta", 0),
            StateJson.Number(aura, "movementDelta", 0));
    }

    internal static JsonObject? ActiveFormation(StateContent content, ITacticalGrid grid, JsonObject state, JsonObject unit) =>
        Empire.Core.Progression.FormationRules.InEffect(content, grid, state, unit, unit["formation"]?.GetValue<string>());
}
