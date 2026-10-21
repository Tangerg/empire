using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Combat;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed record ValidatedMovement(
    int UnitId, Coord Origin, Coord Destination, IReadOnlyList<Coord> Path)
{
    public bool Moved => Origin != Destination;
}

public delegate void PartingShotResolver(
    JsonObject state, JsonObject unit, Coord from, Coord to, Action<JsonObject> emit);

public sealed record TacticalCommandRules(GridRegistry Grids, ITacticalSpace Space, ReactionRegistry Reactions);

public sealed class TacticalCommands
{
    private readonly TacticalCommandRules rules;
    private readonly Func<JsonObject, JsonObject, bool> mayAct;
    private readonly UnitDirectiveRegistry directives;

    public TacticalCommands(
        TacticalCommandRules rules,
        Func<JsonObject, JsonObject, bool> mayAct,
        UnitDirectiveRegistry directives)
    {
        this.rules = rules;
        this.mayAct = mayAct;
        this.directives = directives;
    }

    private TacticalBoard Board(JsonObject state) => new(StateJson.Object(state["map"], "map"),
        rules.Grids.Get(StateJson.Text(state["rules"]!["grid"], "grid")));

    public void TurnToward(JsonObject state, JsonObject unit, Coord from, Coord target, Action<JsonObject> emit)
    {
        var grid = Board(state).Grid;
        UnitDisposition.Face(grid, unit, grid.Toward(from, target), emit);
    }

    public JsonObject OwnUnit(JsonObject state, int unitId)
    {
        var unit = TacticalQueries.UnitById(state, unitId)
            ?? throw new IllegalActionException(new ActionRefusal("tactics.unit_missing", new() { ["unitId"] = unitId }));
        if (StateJson.Integer(unit, "owner") != StateJson.Integer(state, "currentPlayer"))
            throw new IllegalActionException(new ActionRefusal("tactics.not_owner", new() { ["unitId"] = unitId }));
        return unit;
    }

    public JsonObject CommandableUnit(JsonObject state, int unitId)
    {
        var unit = OwnUnit(state, unitId);
        if (StateJson.IsTrue(unit["done"]))
            throw new IllegalActionException(new ActionRefusal("tactics.already_acted", new() { ["unitId"] = unitId }));
        if (!mayAct(state, unit))
            throw new IllegalActionException(new ActionRefusal("tactics.not_actor", new() { ["unitId"] = unitId }));
        return unit;
    }

    public ValidatedMovement ValidateMovement(JsonObject state, JsonObject action)
    {
        var unitId = StateJson.Integer(action, "unit");
        var unit = CommandableUnit(state, unitId);
        var requested = StateJson.Array(action["path"], "action path");
        if (requested.Count == 0) throw new IllegalActionException(new ActionRefusal("tactics.path_empty"));
        var origin = TacticalQueries.Position(unit);
        if (TacticalQueries.Position(StateJson.Object(requested[0], "path origin")) != origin)
            throw new IllegalActionException(new ActionRefusal("tactics.path_origin_mismatch", new() { ["unitId"] = unitId }));
        var destination = TacticalQueries.Position(StateJson.Object(requested[^1], "path destination"));
        if (!Board(state).Contains(destination))
            throw new IllegalActionException(new ActionRefusal("tactics.target_outside", new() { ["x"] = destination.X, ["y"] = destination.Y }));
        if (requested.Count == 1) return new(unitId, origin, destination, new[] { origin });
        var field = rules.Space.ComputeMoveField(state, unit);
        var canonical = rules.Space.PathTo(field, state, destination)
            ?? throw new IllegalActionException(new ActionRefusal("tactics.target_unreachable", new() { ["x"] = destination.X, ["y"] = destination.Y }));
        if (!field.Stops.Contains(Board(state).IndexOf(destination)))
            throw new IllegalActionException(new ActionRefusal("tactics.target_occupied", new() { ["x"] = destination.X, ["y"] = destination.Y }));
        return new(unitId, origin, destination, canonical);
    }

    public JsonObject? ApplyMovement(
        JsonObject state, JsonObject action, Action<JsonObject> emit, PartingShotResolver? partingShots = null) =>
        ApplyMovement(state, ValidateMovement(state, action), emit, partingShots);

    public JsonObject? ApplyMovement(
        JsonObject state, ValidatedMovement validated, Action<JsonObject> emit, PartingShotResolver? partingShots = null)
    {
        var unit = TacticalQueries.RequireUnit(state, validated.UnitId);
        if (TacticalQueries.Position(unit) != validated.Origin)
            throw new DomainInvariantException("unit moved after its command path was validated");
        if (!validated.Moved) return unit;
        if (StateJson.IsTrue(StateJson.Object(state["rules"], "rules")["zoneOfControl"]) && partingShots is null)
            throw new DomainInvariantException("voluntary movement with zone of control requires a parting-shot resolver");
        var board = Board(state);
        new BattleRelocation(state).MoveUnit(validated.UnitId, validated.Destination);
        directives.ArriveAt(unit, validated.Destination);
        if (validated.Path.Count > 1)
            UnitDisposition.Face(board.Grid, unit, board.Grid.Toward(validated.Path[^2], validated.Destination), emit);
        emit(new JsonObject
        {
            ["type"] = "move",
            ["unit"] = validated.UnitId,
            ["path"] = TacticalQueries.PathJson(validated.Path),
        });
        partingShots?.Invoke(state, unit, validated.Origin, validated.Destination, emit);
        return TacticalQueries.UnitById(state, validated.UnitId);
    }

    public void ChangeReaction(JsonObject unit, string stance, Action<JsonObject> emit)
    {
        if (!rules.Reactions.Has(stance))
            throw new IllegalActionException(new ActionRefusal("tactics.reaction_unknown", new() { ["stance"] = stance }));
        UnitDisposition.React(rules.Reactions, unit, stance, emit);
    }

    public void TurnToFace(JsonObject state, JsonObject unit, string facing, Action<JsonObject> emit)
    {
        var grid = Board(state).Grid;
        if (!UnitDisposition.AdmitsFacing(grid, facing))
            throw new IllegalActionException(new ActionRefusal("tactics.facing_invalid", new() { ["grid"] = grid.Id, ["facing"] = facing }));
        UnitDisposition.Face(grid, unit, facing, emit);
    }

    public static void FinishCommand(JsonObject state, int unitId)
    {
        var unit = TacticalQueries.UnitById(state, unitId);
        if (unit is not null) FinishCommand(state, unit);
    }

    public static void FinishCommand(JsonObject state, JsonObject actor)
    {
        // An ability can replace the stored object; the accepted order still owns its captured actor.
        if (TacticalQueries.UnitById(state, StateJson.Integer(actor, "id")) is not null) actor["done"] = true;
    }
}
