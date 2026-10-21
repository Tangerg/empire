using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

/// <summary>Owns unit placement and capture invalidation, not movement or action policy.</summary>
public sealed class BattleRelocation(JsonObject state)
{
    public JsonObject MoveUnit(int unitId, Coord destination)
    {
        var unit = TacticalQueries.RequireUnit(state, unitId);
        var origin = TacticalQueries.Position(unit);
        var capture = CaptureAt(origin);
        ValidateVacant(destination, unitId);
        if (origin == destination)
            return unit;
        capture.Progress[capture.Index] = 0;
        Place(unit, destination);
        return unit;
    }

    public void RemoveUnit(int unitId)
    {
        var unit = TacticalQueries.UnitById(state, unitId);
        if (unit is null)
            return;
        var capture = CaptureAt(TacticalQueries.Position(unit));
        capture.Progress[capture.Index] = 0;
        StateJson.Array(state["units"], "units").Remove(unit);
    }

    public JsonObject RemoveForEmbark(int unitId)
    {
        var unit = TacticalQueries.RequireUnit(state, unitId);
        var snapshot = unit.DeepClone().AsObject();
        ClearUnitCapture(snapshot);
        RemoveUnit(unitId);
        return snapshot;
    }

    internal void DeployUnit(int unitId, Coord destination)
    {
        var unit = TacticalQueries.RequireUnit(state, unitId);
        var origin = TacticalQueries.Position(unit);
        CaptureAt(origin);
        CaptureAt(destination);
        if (origin == destination)
            return;
        var displaced = TacticalQueries.UnitAt(state, destination);
        if (displaced is not null)
            Place(displaced, origin);
        Place(unit, destination);
    }

    internal void PlaceSnapshot(JsonObject unit, Coord destination)
    {
        if (TacticalQueries.UnitById(state, StateJson.Integer(unit, "id")) is not null)
            throw new DomainInvariantException("a placement snapshot must not already be on the field");
        ValidateVacant(destination);
        Place(unit, destination);
    }

    // Deployment and entering snapshots do not leave a live cell. They must not
    // erase capture belonging to another occupant at the snapshot's old position.
    private static void Place(JsonObject unit, Coord destination)
    {
        unit["x"] = destination.X;
        unit["y"] = destination.Y;
        ClearUnitCapture(unit);
    }

    private static void ClearUnitCapture(JsonObject unit) => unit["capture"] = 0;

    private void ValidateVacant(Coord destination, int? movingUnit = null)
    {
        CaptureAt(destination);
        if (TacticalQueries.UnitAt(state, destination) is { } occupant &&
            StateJson.Integer(occupant, "id") != movingUnit)
            throw new DomainInvariantException("unit destination is already occupied");
    }

    private int ValidateCell(Coord at)
    {
        var map = StateJson.Object(state["map"], "map");
        var extent = new GridExtent(StateJson.Integer(map, "width"), StateJson.Integer(map, "height"));
        if (!GridStorage.InBounds(extent, at.X, at.Y))
            throw new DomainInvariantException($"cell ({at.X}, {at.Y}) is outside the map");
        return GridStorage.Index(extent, at.X, at.Y);
    }

    private (JsonArray Progress, int Index) CaptureAt(Coord at)
    {
        var index = ValidateCell(at);
        var progress = StateJson.Array(StateJson.Object(state["map"], "map")["captureProgress"], "captureProgress");
        if (index >= progress.Count)
            throw new DomainInvariantException("capture progress does not cover the unit's origin");
        return (progress, index);
    }
}
