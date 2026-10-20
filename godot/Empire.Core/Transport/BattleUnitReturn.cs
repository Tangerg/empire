using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Transport;

public sealed record UnitReturnRequest(Coord At, int? Owner = null, double? Hp = null);

/// <summary>Returns a fallen or withdrawn snapshot. Ordinary disembarking must not use these reset rules.</summary>
public static class BattleUnitReturn
{
    public static JsonObject? Restore(JsonObject state, JsonObject marker, UnitReturnRequest request, Action<JsonObject> emit)
    {
        if (marker["fallenUnit"] is not JsonObject fallen) return null;
        if (TacticalQueries.UnitAt(state, request.At) is not null) return null;
        var id = StateJson.Integer(fallen, "id");
        var key = fallen["key"]?.GetValue<string>();
        if (StateJson.Objects(state["units"], "units").Any(unit => StateJson.Integer(unit, "id") == id ||
            !string.IsNullOrEmpty(key) && unit["key"]?.GetValue<string>() == key)) return null;
        var unit = fallen.DeepClone().AsObject();
        if (request.Owner is { } owner) unit["owner"] = owner;
        unit["x"] = request.At.X;
        unit["y"] = request.At.Y;
        unit["capture"] = 0;
        if (request.Hp is { } hp) unit["hp"] = Math.Max(1, StateJson.Round(hp));
        unit["done"] = true;
        unit["statuses"] = new JsonArray();
        unit["morale"]!["current"] = unit["morale"]!["maximum"]!.DeepClone();
        unit["reactionUsedRound"] = -1;
        StateJson.Array(state["units"], "units").Add(unit);
        state["nextUnitId"] = Math.Max(StateJson.Integer(state, "nextUnitId"), id + 1);
        var commander = StateJson.Objects(state["commanders"], "commanders").FirstOrDefault(value => StateJson.Integer(value, "unitId") == id);
        if (commander is not null) commander["owner"] = unit["owner"]!.DeepClone();
        StateJson.Array(state["markers"], "markers").Remove(marker);
        emit(new JsonObject { ["type"] = "markerRemoved", ["marker"] = marker["id"]!.DeepClone(),
            ["kind"] = marker["kind"]!.DeepClone(), ["at"] = marker["at"]!.DeepClone() });
        emit(new JsonObject { ["type"] = "unitRevived", ["unit"] = id, ["marker"] = marker["id"]!.DeepClone(),
            ["at"] = TacticalQueries.CoordinateJson(request.At), ["hp"] = unit["hp"]!.DeepClone() });
        return unit;
    }
}
