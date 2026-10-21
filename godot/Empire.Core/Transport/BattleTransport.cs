using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Transport;

public sealed record CarrierOption(JsonObject Carrier, bool Eligible, IReadOnlyList<ActionRefusal> Reasons);
public sealed record PassengerOption(JsonObject Unit, IReadOnlyList<Coord> Spots);

/// <summary>Owns boarding and landing. Action entitlement remains the dispatcher's responsibility.</summary>
public sealed class BattleTransport
{
    private readonly StateContent content;
    private readonly GridRegistry grids;
    private TacticalBoard Board(JsonObject state) => new(StateJson.Object(state["map"], "map"),
        grids.Get(StateJson.Text(state["rules"]!["grid"], "grid")));

    public BattleTransport(ReadOnlyContent catalog, GridRegistry grids)
    {
        content = catalog.Source;
        this.grids = grids;
    }

    public static IReadOnlyList<JsonObject> PassengersOf(JsonObject state, int carrierId) =>
        StateJson.Objects(state["embarkedUnits"], "embarkedUnits")
            .Where(entry => StateJson.Integer(entry, "carrier") == carrierId)
            .Select(entry => StateJson.Object(entry["unit"], "embarked unit")).ToArray();

    /// <summary>
    /// Relational checks for a shape-checked saved state. This does not rerun boarding
    /// eligibility: ownership, capacity and tag rules may change after a unit boards.
    /// Catalog-reference errors and global unit identity uniqueness belong to persistence.
    /// </summary>
    public IReadOnlyList<string> StoredStateIssues(JsonObject state)
    {
        var issues = new List<string>();
        var index = 0;
        foreach (var entry in StateJson.Objects(state["embarkedUnits"], "embarkedUnits"))
        {
            var path = $"state.embarkedUnits[{index++}]";
            var carrierId = StateJson.Integer(entry, "carrier");
            var carrier = TacticalQueries.UnitById(state, carrierId);
            if (carrier is null) issues.Add($"{path}.carrier references missing field unit {carrierId}");
            else if (content.TryGet("units", StateJson.Text(carrier, "type")) is { } definition && definition["transport"] is null)
                issues.Add($"{path}.carrier references a unit without a transport profile");
            var passenger = StateJson.Object(entry["unit"], "embarked unit");
            if (content.TryGet("units", StateJson.Text(passenger, "type")) is { } passengerDefinition && passengerDefinition["transport"] is not null)
                issues.Add($"{path}.unit cannot itself be a transport carrier");
        }
        return issues.AsReadOnly();
    }

    public ActionRefusal? EmbarkRefusal(JsonObject state, JsonObject unit, JsonObject carrier)
    {
        var unitId = StateJson.Integer(unit, "id");
        var carrierId = StateJson.Integer(carrier, "id");
        if (unitId == carrierId) return new("transport.self");
        var carrierDefinition = content.Get("units", StateJson.Text(carrier, "type"));
        if (carrierDefinition["transport"] is not JsonObject profile)
            return new("transport.not_carrier", new() { ["unitType"] = StateJson.Text(carrier, "type") });
        if (!TacticalQueries.AreAllies(state, StateJson.Integer(unit, "owner"), StateJson.Integer(carrier, "owner")))
            return new("transport.not_allies");
        if (Board(state).Grid.Distance(TacticalQueries.Position(unit), TacticalQueries.Position(carrier)) != 1)
            return new("transport.not_adjacent");
        if (PassengersOf(state, carrierId).Count >= StateJson.Number(profile["capacity"], "capacity")) return new("transport.full");
        var passengerDefinition = content.Get("units", StateJson.Text(unit, "type"));
        if (passengerDefinition["transport"] is not null) return new("transport.carrier_passenger");
        var tags = StateJson.Array(passengerDefinition["tags"], "unit tags");
        bool Matches(JsonNode? candidate) => tags.Any(tag => StateJson.Text(tag, "unit tag") == StateJson.Text(candidate, "transport tag"));
        if (profile["allowedTags"] is JsonArray allowed && allowed.Count > 0 && !allowed.Any(Matches)) return new("transport.incompatible_tags");
        if (profile["forbiddenTags"] is JsonArray forbidden && forbidden.Any(Matches)) return new("transport.forbidden_tags");
        return null;
    }

    public IReadOnlyList<CarrierOption> CarrierOptions(JsonObject state, JsonObject unit)
    {
        var result = new List<CarrierOption>();
        foreach (var carrier in StateJson.Objects(state["units"], "units"))
        {
            if (StateJson.Integer(carrier, "id") == StateJson.Integer(unit, "id")) continue;
            if (content.Get("units", StateJson.Text(carrier, "type"))["transport"] is null) continue;
            if (!TacticalQueries.AreAllies(state, StateJson.Integer(unit, "owner"), StateJson.Integer(carrier, "owner"))) continue;
            if (Board(state).Grid.Distance(TacticalQueries.Position(unit), TacticalQueries.Position(carrier)) != 1) continue;
            var refusal = EmbarkRefusal(state, unit, carrier);
            result.Add(new(carrier, refusal is null, refusal is null ? Array.Empty<ActionRefusal>() : Array.AsReadOnly(new[] { refusal })));
        }
        return result.AsReadOnly();
    }

    public void Embark(JsonObject state, int unitId, int carrierId, Action<JsonObject> emit)
    {
        var unit = TacticalQueries.RequireUnit(state, unitId);
        var carrier = TacticalQueries.RequireUnit(state, carrierId);
        if (EmbarkRefusal(state, unit, carrier) is { } refusal) throw new IllegalActionException(refusal);
        var snapshot = new BattleRelocation(state).RemoveForEmbark(unitId);
        snapshot["done"] = true;
        StateJson.Array(state["embarkedUnits"], "embarkedUnits").Add(new JsonObject { ["carrier"] = carrierId, ["unit"] = snapshot });
        emit(new JsonObject { ["type"] = "unitEmbarked", ["unit"] = unitId, ["carrier"] = carrierId });
    }

    public ActionRefusal? DisembarkRefusal(JsonObject state, int carrierId, int unitId, Coord at)
    {
        var carrier = TacticalQueries.UnitById(state, carrierId);
        if (carrier is null) return new("transport.carrier_missing", new() { ["carrierId"] = carrierId });
        var definition = content.Get("units", StateJson.Text(carrier, "type"));
        if (definition["transport"] is null) return new("transport.not_carrier", new() { ["unitType"] = StateJson.Text(carrier, "type") });
        var entry = StateJson.Objects(state["embarkedUnits"], "embarkedUnits").FirstOrDefault(candidate =>
            StateJson.Integer(candidate, "carrier") == carrierId && StateJson.Integer(StateJson.Object(candidate["unit"], "embarked unit"), "id") == unitId);
        if (entry is null) return new("transport.not_aboard");
        var board = Board(state);
        if (!board.Contains(at) || board.Distance(TacticalQueries.Position(carrier), at) != 1) return new("transport.disembark_not_adjacent");
        if (TacticalQueries.UnitAt(state, at) is not null) return new("transport.disembark_occupied");
        var unit = StateJson.Object(entry["unit"], "embarked unit");
        var movementClass = StateJson.Text(content.Get("units", StateJson.Text(unit, "type")), "movementClass");
        if (!new TacticalBattlefield(state, content).Cell(at).Admits(movementClass)) return new("transport.disembark_impassable");
        return null;
    }

    public IReadOnlyList<PassengerOption> PassengerOptions(JsonObject state, JsonObject carrier) =>
        PassengersOf(state, StateJson.Integer(carrier, "id")).Select(unit => new PassengerOption(unit,
            Board(state).Neighbours(TacticalQueries.Position(carrier)).Where(at =>
                DisembarkRefusal(state, StateJson.Integer(carrier, "id"), StateJson.Integer(unit, "id"), at) is null).ToArray())).ToArray();

    public void Disembark(JsonObject state, int carrierId, int unitId, Coord at, Action<JsonObject> emit)
    {
        if (DisembarkRefusal(state, carrierId, unitId, at) is { } refusal) throw new IllegalActionException(refusal);
        var passengers = StateJson.Array(state["embarkedUnits"], "embarkedUnits");
        var entry = passengers.OfType<JsonObject>().First(candidate => StateJson.Integer(candidate, "carrier") == carrierId &&
            StateJson.Integer(StateJson.Object(candidate["unit"], "embarked unit"), "id") == unitId);
        var unit = StateJson.Object(entry["unit"], "embarked unit");
        new BattleRelocation(state).PlaceSnapshot(unit, at);
        unit["done"] = true;
        passengers.Remove(entry);
        entry.Remove("unit");
        StateJson.Array(state["units"], "units").Add(unit);
        emit(new JsonObject { ["type"] = "unitDisembarked", ["unit"] = unitId, ["carrier"] = carrierId, ["at"] = TacticalQueries.CoordinateJson(at) });
    }
}
