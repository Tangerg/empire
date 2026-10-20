using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed record ValidatedMovement(
    int UnitId, Coord Origin, Coord Destination, IReadOnlyList<Coord> Path)
{
    public bool Moved => Origin != Destination;
}

public delegate void PartingShotResolver(
    JsonObject state, JsonObject unit, Coord from, Coord to, Action<JsonObject> emit);

public sealed class TacticalCommands
{
    private readonly MovementRules movement;
    private readonly Func<JsonObject, JsonObject, bool> mayAct;
    private readonly UnitDirectiveRegistry directives;

    public TacticalCommands(
        MovementRules movement,
        Func<JsonObject, JsonObject, bool> mayAct,
        UnitDirectiveRegistry directives)
    {
        this.movement = movement;
        this.mayAct = mayAct;
        this.directives = directives;
    }

    public JsonObject OwnUnit(JsonObject state, int unitId)
    {
        var unit = TacticalQueries.UnitById(state, unitId)
            ?? throw new IllegalActionException($"单位 {unitId} 不在场上");
        if (StateJson.Integer(unit, "owner") != StateJson.Integer(state, "currentPlayer"))
            throw new IllegalActionException("不是你的单位");
        return unit;
    }

    public JsonObject CommandableUnit(JsonObject state, int unitId, string order = "行动")
    {
        var unit = OwnUnit(state, unitId);
        if (StateJson.IsTrue(unit["done"]))
            throw new IllegalActionException($"已行动单位不能{order}");
        if (!mayAct(state, unit))
            throw new IllegalActionException($"该单位当前没有行动权，不能{order}");
        return unit;
    }

    public ValidatedMovement ValidateMovement(JsonObject state, JsonObject action)
    {
        var unitId = StateJson.Integer(action, "unit");
        var unit = CommandableUnit(state, unitId);
        var requested = StateJson.Array(action["path"], "action path");
        if (requested.Count == 0) throw new IllegalActionException("路径为空");
        var origin = TacticalQueries.Position(unit);
        if (TacticalQueries.Position(StateJson.Object(requested[0], "path origin")) != origin)
            throw new IllegalActionException("路径起点与单位位置不符");
        var destination = TacticalQueries.Position(StateJson.Object(requested[^1], "path destination"));
        if (!movement.Board(state).Contains(destination))
            throw new IllegalActionException($"目标格 {destination.X},{destination.Y} 不在战场内");
        if (requested.Count == 1) return new(unitId, origin, destination, new[] { origin });
        var field = movement.ComputeMoveField(state, unit);
        var canonical = movement.PathTo(field, state, destination)
            ?? throw new IllegalActionException($"目标格 {destination.X},{destination.Y} 不在移动范围内");
        if (!field.Stops.Contains(movement.Board(state).IndexOf(destination)))
            throw new IllegalActionException("目标格已被占据");
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
        var board = movement.Board(state);
        StateJson.Array(board.Map["captureProgress"], "captureProgress")[board.IndexOf(validated.Origin)] = 0;
        unit["x"] = validated.Destination.X;
        unit["y"] = validated.Destination.Y;
        unit["capture"] = 0;
        directives.ArriveAt(unit, validated.Destination);
        if (validated.Path.Count > 1)
            TurnToFace(unit, board.Grid.Toward(validated.Path[^2], validated.Destination), emit);
        emit(new JsonObject
        {
            ["type"] = "move",
            ["unit"] = validated.UnitId,
            ["path"] = TacticalQueries.PathJson(validated.Path),
        });
        partingShots?.Invoke(state, unit, validated.Origin, validated.Destination, emit);
        return TacticalQueries.UnitById(state, validated.UnitId);
    }

    public static void TurnToFace(JsonObject unit, string facing, Action<JsonObject> emit)
    {
        var previous = StateJson.Text(unit, "facing");
        unit["facing"] = facing;
        if (previous == facing) return;
        emit(new JsonObject
        {
            ["type"] = "facingChanged",
            ["unit"] = StateJson.Required(unit, "id").DeepClone(),
            ["from"] = previous,
            ["to"] = facing,
        });
    }

    public static void FinishCommand(JsonObject state, int unitId)
    {
        var unit = TacticalQueries.UnitById(state, unitId);
        if (unit is not null) unit["done"] = true;
    }

    public void ExecuteWait(
        JsonObject state, JsonObject action, Action<JsonObject> emit, PartingShotResolver? partingShots = null)
    {
        var validated = ValidateMovement(state, action);
        var unit = TacticalQueries.RequireUnit(state, validated.UnitId);
        var command = StateJson.Object(action["command"], "command");
        if (StateJson.Text(command, "ability") != "wait")
            throw new DomainInvariantException("wait handler received a different ability");
        var definition = movement.Content.Get("units", StateJson.Text(unit, "type"));
        var abilities = StateJson.Array(definition["abilities"], "unit abilities")
            .Concat(StateJson.Array(unit["learnedAbilities"], "learnedAbilities"));
        if (!abilities.Any(ability => StateJson.Text(ability, "ability") == "wait"))
            throw new IllegalActionException($"{StateJson.Text(definition, "name")} 没有「待机」");
        if (command["weapon"] is { } requestedWeapon && StateJson.Text(requestedWeapon, "weapon").Length > 0)
            throw new IllegalActionException($"「待机」无法使用「{StateJson.Text(requestedWeapon, "weapon")}」");
        var surviving = ApplyMovement(state, validated, emit, partingShots);
        if (surviving is null) return;
        if (command["target"] is JsonObject target)
            TurnToFace(surviving, movement.Grid(state).Toward(validated.Destination, TacticalQueries.Position(target)), emit);
        // The core wait ability has no effect or event; finishing the actor is the command's responsibility.
        FinishCommand(state, validated.UnitId);
    }
}
