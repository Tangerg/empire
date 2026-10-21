using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Deployment;

public sealed record DeploymentRoster(int Player, IReadOnlyList<JsonObject> Units, IReadOnlyList<Coord> Zone);
public sealed record DeploymentSpot(Coord At, JsonObject? Swaps);

/// <summary>Arranges the already-created units before the first actor turn.</summary>
public sealed class DeploymentSystem
{
    public static IReadOnlyList<string> SupportedActions { get; } =
        Array.AsReadOnly(new[] { "deployUnit", "finishDeployment" });

    private readonly StateContent content;
    private readonly Func<JsonObject, int, JsonObject> ownUnit;

    public DeploymentSystem(ReadOnlyContent catalog, Func<JsonObject, int, JsonObject> ownUnit)
    {
        content = catalog.Source;
        this.ownUnit = ownUnit;
    }

    private static IReadOnlyList<Coord>? AssignedZone(JsonObject state, JsonObject unit)
    {
        if (state["deployment"] is not JsonObject deployment) return null;
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
        if (StateJson.Text(state, "phase") != "deployment" || state["deployment"] is not JsonObject deployment) return null;
        var player = StateJson.Integer(state, "currentPlayer");
        var mine = StateJson.Objects(deployment["assignments"], "deployment assignments")
            .Where(entry => StateJson.Integer(entry, "player") == player).ToArray();
        if (mine.Length == 0) return null;
        var byId = new Dictionary<int, JsonObject>();
        foreach (var unit in StateJson.Objects(state["units"], "units")) byId[StateJson.Integer(unit, "id")] = unit;
        var units = new List<JsonObject>();
        var zone = new List<Coord>();
        foreach (var assignment in mine)
        {
            foreach (var id in StateJson.Array(assignment["unitIds"], "deployment unit IDs"))
                if (byId.TryGetValue(StateJson.Integer(id, "unit ID"), out var unit)) units.Add(unit);
            // Source flatMap preserves declaration order, including overlapping-zone cells.
            zone.AddRange(Zone(state, StateJson.Text(assignment, "zone")));
        }
        return new(player, units.AsReadOnly(), zone.AsReadOnly());
    }

    public string? Refusal(JsonObject state, JsonObject unit, Coord at)
    {
        if (StateJson.Text(state, "phase") != "deployment" || state["deployment"] is not JsonObject)
            return "当前不在战前部署阶段";
        var zone = AssignedZone(state, unit);
        if (zone is null) return "该单位不在当前部署编组中";
        if (!zone.Contains(at)) return "目标格不在该单位的部署区域内";
        var battlefield = new TacticalBattlefield(state, content);
        var occupant = TacticalQueries.UnitAt(state, at);
        var swapping = occupant is not null && StateJson.Integer(occupant, "id") != StateJson.Integer(unit, "id");
        if (swapping && AssignedZone(state, occupant!) is null) return "目标格已被其他单位占据";
        var movementClass = StateJson.Text(content.Get("units", StateJson.Text(unit, "type")), "movementClass");
        if (!battlefield.Cell(at).Admits(movementClass)) return "该单位无法部署到目标格";
        if (!swapping) return null;
        var from = TacticalQueries.Position(unit);
        if (AssignedZone(state, occupant!)?.Contains(from) != true) return "交换后会使另一单位离开部署区域";
        var displacedMovement = StateJson.Text(content.Get("units", StateJson.Text(occupant!, "type")), "movementClass");
        return battlefield.Cell(from).Admits(displacedMovement) ? null : "交换后另一单位无法站在腾出的格子上";
    }

    public IReadOnlyList<DeploymentSpot> Spots(JsonObject state, JsonObject unit)
    {
        var result = new List<DeploymentSpot>();
        foreach (var at in AssignedZone(state, unit) ?? Array.Empty<Coord>())
        {
            if (Refusal(state, unit, at) is not null) continue;
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
        if (refusal is not null) throw new IllegalActionException(refusal);
        var from = TacticalQueries.Position(unit);
        if (from == at) return;
        new BattleRelocation(state).DeployUnit(unitId, at);
        emit(new JsonObject
        {
            ["type"] = "unitDeployed", ["unit"] = unitId,
            ["from"] = TacticalQueries.CoordinateJson(from), ["to"] = TacticalQueries.CoordinateJson(at),
        });
    }

    public void Confirm(JsonObject state, Action beginPlaying, Action<JsonObject> emit)
    {
        if (StateJson.Text(state, "phase") != "deployment" || state["deployment"] is not JsonObject deployment)
            throw new IllegalActionException("当前不在战前部署阶段");
        emit(new JsonObject { ["type"] = "deploymentConfirmed", ["player"] = state["currentPlayer"]!.DeepClone() });
        var next = StateJson.Integer(deployment, "currentIndex") + 1;
        deployment["currentIndex"] = next;
        var order = StateJson.Array(deployment["order"], "deployment order");
        if (next < order.Count) state["currentPlayer"] = order[next]!.DeepClone();
        else beginPlaying();
    }

}
