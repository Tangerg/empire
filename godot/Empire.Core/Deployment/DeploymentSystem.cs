using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Deployment;

public sealed record DeploymentRoster(int Player, IReadOnlyList<JsonObject> Units, IReadOnlyList<Coord> Zone);
public sealed record DeploymentSpot(Coord At, JsonObject? Swaps);

public sealed record DeploymentRules(ReadOnlyContent Content, Func<JsonObject, int, JsonObject> OwnUnit);

/// <summary>Arranges the already-created units before the first actor turn.</summary>
public sealed class DeploymentSystem
{
    public static IReadOnlyList<string> SupportedActions
    {
        get;
    } =
        Array.AsReadOnly(new[] { "deployUnit", "finishDeployment" });

    private readonly StateContent content;
    private readonly Func<JsonObject, int, JsonObject> ownUnit;

    public DeploymentSystem(DeploymentRules rules)
    {
        content = rules.Content.Source;
        ownUnit = rules.OwnUnit;
    }

    private static IReadOnlyList<Coord>? AssignedZone(JsonObject state, JsonObject unit)
    {
        if (state["deployment"] is not JsonObject deployment)
            return null;
        var player = StateJson.Integer(state, "currentPlayer");
        var id = StateJson.Integer(unit, "id");
        var assignment = StateJson.Objects(deployment["assignments"], "deployment assignments").FirstOrDefault(entry =>
            StateJson.Integer(entry, "player") == player &&
            StateJson.Array(entry["unitIds"], "deployment unit IDs").Any(value => StateJson.Integer(value, "unit ID") == id));
        return assignment is null ? null : Zone(state, StateJson.Text(assignment, "zone"));
    }

    private static IReadOnlyList<Coord> Zone(JsonObject state, string id) =>
        StateJson.Objects(StateJson.Object(StateJson.Object(state["scenario"], "scenario")["zones"], "zones")[id], "deployment zone")
            .Select(TacticalQueries.Position).ToArray();

    public DeploymentRoster? Roster(JsonObject state)
    {
        if (StateJson.Text(state, "phase") != "deployment" || state["deployment"] is not JsonObject deployment)
            return null;
        var player = StateJson.Integer(state, "currentPlayer");
        var mine = StateJson.Objects(deployment["assignments"], "deployment assignments")
            .Where(entry => StateJson.Integer(entry, "player") == player).ToArray();
        if (mine.Length == 0)
            return null;
        var byId = new Dictionary<int, JsonObject>();
        foreach (var unit in StateJson.Objects(state["units"], "units"))
            byId[StateJson.Integer(unit, "id")] = unit;
        var units = new List<JsonObject>();
        var zone = new List<Coord>();
        foreach (var assignment in mine)
        {
            foreach (var id in StateJson.Array(assignment["unitIds"], "deployment unit IDs"))
                if (byId.TryGetValue(StateJson.Integer(id, "unit ID"), out var unit))
                    units.Add(unit);
            // Source flatMap preserves declaration order, including overlapping-zone cells.
            zone.AddRange(Zone(state, StateJson.Text(assignment, "zone")));
        }
        return new(player, units.AsReadOnly(), zone.AsReadOnly());
    }

    public ActionRefusal? Refusal(JsonObject state, JsonObject unit, Coord at)
    {
        if (StateJson.Text(state, "phase") != "deployment" || state["deployment"] is not JsonObject)
            return new("deployment.wrong_phase");
        var zone = AssignedZone(state, unit);
        if (zone is null)
            return new("deployment.unassigned", new()
            {
                ["unitId"] = StateJson.Integer(unit, "id")
            });
        if (!zone.Contains(at))
            return new("deployment.outside_zone", new()
            {
                ["x"] = at.X,
                ["y"] = at.Y
            });
        var battlefield = new TacticalBattlefield(state, content);
        var occupant = TacticalQueries.UnitAt(state, at);
        var swapping = occupant is not null && StateJson.Integer(occupant, "id") != StateJson.Integer(unit, "id");
        if (swapping && AssignedZone(state, occupant!) is null)
            return new("deployment.occupied", new()
            {
                ["x"] = at.X,
                ["y"] = at.Y
            });
        var movementClass = StateJson.Text(content.Get("units", StateJson.Text(unit, "type")), "movementClass");
        if (!battlefield.Cell(at).Admits(movementClass))
            return new("deployment.impassable", new()
            {
                ["x"] = at.X,
                ["y"] = at.Y
            });
        if (!swapping)
            return null;
        var from = TacticalQueries.Position(unit);
        if (AssignedZone(state, occupant!)?.Contains(from) != true)
            return new("deployment.swap_outside_zone");
        var displacedMovement = StateJson.Text(content.Get("units", StateJson.Text(occupant!, "type")), "movementClass");
        return battlefield.Cell(from).Admits(displacedMovement) ? null : new("deployment.swap_impassable");
    }

    public IReadOnlyList<DeploymentSpot> Spots(JsonObject state, JsonObject unit)
    {
        var result = new List<DeploymentSpot>();
        foreach (var at in AssignedZone(state, unit) ?? Array.Empty<Coord>())
        {
            if (Refusal(state, unit, at) is not null)
                continue;
            var occupant = TacticalQueries.UnitAt(state, at);
            var swaps = occupant is not null && StateJson.Integer(occupant, "id") != StateJson.Integer(unit, "id") ? occupant : null;
            result.Add(new(at, swaps));
        }
        return result.AsReadOnly();
    }

    public void Deploy(JsonObject state, int unitId, Coord at, Action<JsonObject> emit)
    {
        var unit = ownUnit(state, unitId);
        var refusal = Refusal(state, unit, at);
        if (refusal is not null)
            throw new IllegalActionException(refusal);
        var from = TacticalQueries.Position(unit);
        if (from == at)
            return;
        new BattleRelocation(state).DeployUnit(unitId, at);
        emit(new JsonObject
        {
            ["type"] = "unitDeployed",
            ["unit"] = unitId,
            ["from"] = TacticalQueries.CoordinateJson(from),
            ["to"] = TacticalQueries.CoordinateJson(at),
        });
    }

    public void Confirm(Action beginPlaying, JsonObject state, Action<JsonObject> emit)
    {
        if (StateJson.Text(state, "phase") != "deployment" || state["deployment"] is not JsonObject deployment)
            throw new IllegalActionException(new ActionRefusal("deployment.wrong_phase"));
        emit(new JsonObject { ["type"] = "deploymentConfirmed", ["player"] = state["currentPlayer"]!.DeepClone() });
        var next = StateJson.Integer(deployment, "currentIndex") + 1;
        deployment["currentIndex"] = next;
        var order = StateJson.Array(deployment["order"], "deployment order");
        if (next < order.Count)
            state["currentPlayer"] = order[next]!.DeepClone();
        else
            beginPlaying();
    }

}
