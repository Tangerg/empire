using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed partial class BattleCombat
{
    private void BeginCast(JsonObject state, JsonObject caster, JsonObject weapon, Coord target, Coord origin, Action<JsonObject> emit)
    {
        var casts = state["pendingCasts"]!.AsArray();
        var id = StateJson.Integer(caster, "id");
        if (casts.OfType<JsonObject>().Any(value => StateJson.Integer(value, "caster") == id)) throw new IllegalActionException("该单位已经在咏唱中");
        var declared = StateJson.Number(state, "actorTurns", 0);
        var duration = Math.Max(1, StateJson.Round(StateJson.Number(weapon, "castTurns", 0)));
        casts.Add(new JsonObject { ["caster"] = id, ["owner"] = caster["owner"]!.DeepClone(), ["ability"] = "attack",
            ["weapon"] = weapon["id"]!.DeepClone(), ["target"] = TacticalQueries.CoordinateJson(target),
            ["origin"] = TacticalQueries.CoordinateJson(origin), ["declaredAt"] = declared, ["resolveAt"] = declared + duration });
        emit(new JsonObject { ["type"] = "castBegan", ["unit"] = id, ["weapon"] = weapon["id"]!.DeepClone(),
            ["at"] = TacticalQueries.CoordinateJson(target), ["turns"] = duration });
    }
    public void ResolveDueCasts(JsonObject state, Action<JsonObject> emit)
    {
        var casts = state["pendingCasts"]!.AsArray();
        foreach (var cast in casts.OfType<JsonObject>())
            if (StateJson.Number(cast, "resolveAt", 0) <= StateJson.Number(cast, "declaredAt", 0))
                throw new DomainInvariantException($"a cast must resolve after it is declared, got {StateJson.Number(cast, "declaredAt", 0)} -> {StateJson.Number(cast, "resolveAt", 0)}");
        var due = casts.OfType<JsonObject>().Where(cast => StateJson.Number(cast, "resolveAt", 0) <= StateJson.Number(state, "actorTurns", 0)).ToArray();
        foreach (var cast in due) casts.Remove(cast);
        foreach (var cast in due.OrderBy(value => StateJson.Number(value, "declaredAt", 0)))
        {
            var target = TacticalQueries.Position(cast["target"]!.AsObject());
            var origin = TacticalQueries.Position(cast["origin"]!.AsObject());
            var id = StateJson.Integer(cast, "caster");
            var weaponId = StateJson.Text(cast, "weapon");
            void Cancel(string reason) => emit(new JsonObject { ["type"] = "castCancelled", ["unit"] = id,
                ["weapon"] = weaponId, ["at"] = TacticalQueries.CoordinateJson(target), ["reason"] = reason });
            var caster = TacticalQueries.UnitById(state, id);
            if (caster is null) { Cancel("casterLost"); continue; }
            var weapon = Forecasts.ReadyWeapon(state, caster, weaponId);
            if (weapon is null) { Cancel("weaponUnavailable"); continue; }
            if (!HostileActionAllowed(state, StateJson.Integer(caster, "owner"), origin, target)) { Cancel("targetProtected"); continue; }
            if (StateJson.Text(weapon, "area") == "single" && TacticalQueries.UnitAt(state, target) is null) { Cancel("targetVacated"); continue; }
            emit(new JsonObject { ["type"] = "castResolved", ["unit"] = id, ["weapon"] = weaponId, ["at"] = TacticalQueries.CoordinateJson(target) });
            ExecutePlan(state, Forecast(state, caster, target, origin, weaponId), emit);
        }
    }
}
