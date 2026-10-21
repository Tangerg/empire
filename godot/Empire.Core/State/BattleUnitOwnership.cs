using System.Text.Json.Nodes;
using Empire.Core.Tactics;

namespace Empire.Core.State;

public sealed record UnitOwnershipChange(int Unit, int PreviousOwner, int Owner);

/// <summary>Owns live unit allegiance and the attached commander's ownership projection.</summary>
public sealed class BattleUnitOwnership(JsonObject state)
{
    public void Validate()
    {
        var units = LiveUnits();
        foreach (var unit in units)
            _ = TacticalQueries.Player(state, StateJson.Integer(unit, "owner"));
        foreach (var commander in Commanders().Values)
        {
            var owner = StateJson.Integer(commander, "owner");
            _ = TacticalQueries.Player(state, owner);
            var unitId = StateJson.Integer(commander, "unitId");
            var unit = units.FirstOrDefault(candidate => StateJson.Integer(candidate, "id") == unitId);
            if (unit is not null && StateJson.Integer(unit, "owner") != owner)
                throw new DomainInvariantException("attached commander ownership disagrees with its unit");
        }
    }

    public IReadOnlyList<UnitOwnershipChange> Assign(IReadOnlyList<JsonObject> units, int owner)
    {
        _ = TacticalQueries.Player(state, owner);
        var commanders = Commanders();
        var liveUnits = LiveUnits();
        var assignments = units.Select(unit =>
        {
            var id = StateJson.Integer(unit, "id");
            var existing = liveUnits.Where(value => StateJson.Integer(value, "id") == id).ToArray();
            if (existing.Length > 1 || existing.Length == 1 && !ReferenceEquals(existing[0], unit))
                throw new DomainInvariantException($"ownership subject {id} is not the canonical live unit");
            return (Unit: unit, Id: id, Previous: StateJson.Integer(unit, "owner"), Commander: commanders.GetValueOrDefault(id));
        }).ToArray();
        if (assignments.Select(item => item.Id).Distinct().Count() != assignments.Length)
            throw new DomainInvariantException("an ownership transfer contains duplicate unit identities");

        var changes = new List<UnitOwnershipChange>();
        foreach (var assignment in assignments)
        {
            assignment.Unit["owner"] = owner;
            if (assignment.Commander is { } commander)
                commander["owner"] = owner;
            if (assignment.Previous != owner)
                changes.Add(new(assignment.Id, assignment.Previous, owner));
        }
        return changes;
    }

    private JsonObject[] LiveUnits() => StateJson.Objects(state["units"], "units")
        .Concat(StateJson.Objects(state["embarkedUnits"], "embarkedUnits")
            .Select(entry => StateJson.Object(entry["unit"], "embarked unit")))
        .ToArray();

    private Dictionary<int, JsonObject> Commanders()
    {
        var result = new Dictionary<int, JsonObject>();
        foreach (var commander in StateJson.Objects(state["commanders"], "commanders"))
        {
            var unitId = StateJson.Integer(commander, "unitId");
            if (!result.TryAdd(unitId, commander))
                throw new DomainInvariantException($"unit {unitId} has multiple attached commanders");
        }
        return result;
    }

}
