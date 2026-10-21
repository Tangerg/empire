using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Registries;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed record DirectiveSurvey(
    JsonObject State, JsonObject Unit, Coord At, IReadOnlyList<Coord> Zone, TacticalBoard Board);

public sealed record UnitDirectiveBehavior(
    string Id,
    Func<DirectiveSurvey, double> Pull,
    double Engagement,
    double FightPenalty,
    Action<JsonObject, Coord>? ArriveAt = null);

/// <summary>One mode declaration owns both AI appraisal and movement arrival.</summary>
public sealed class UnitDirectiveRegistry : KeyedRegistry<UnitDirectiveBehavior, UnitDirectiveRegistry>
{
    public UnitDirectiveRegistry() : base("unit directive") { }
    protected override string KeyOf(UnitDirectiveBehavior entry) => entry.Id;
    public UnitDirectiveRegistry Clone() => CopyInto(new UnitDirectiveRegistry());

    public UnitDirectiveBehavior Of(JsonObject unit) =>
        Get(StateJson.Text(StateJson.Object(unit["directive"], "directive"), "mode"));

    public double Pull(JsonObject state, JsonObject unit, Coord at, ITacticalGrid grid)
    {
        var directive = StateJson.Object(unit["directive"], "directive");
        var zoneId = directive["zone"]?.GetValue<string>();
        var zones = StateJson.Object(StateJson.Object(state["scenario"], "scenario")["zones"], "zones");
        IReadOnlyList<Coord> zone = string.IsNullOrEmpty(zoneId) || zones[zoneId] is null
            ? Array.Empty<Coord>()
            : StateJson.Objects(zones[zoneId], "directive zone").Select(TacticalQueries.Position).ToArray();
        var board = new TacticalBoard(grid, StateJson.Object(state["map"], "map"));
        return Of(unit).Pull(new DirectiveSurvey(state, unit, at, zone, board));
    }

    public double Engagement(JsonObject unit) => Of(unit).Engagement;
    public double FightPenalty(JsonObject unit) => Of(unit).FightPenalty;
    public void ArriveAt(JsonObject unit, Coord at) => Of(unit).ArriveAt?.Invoke(unit, at);

    public static UnitDirectiveRegistry Core() => new UnitDirectiveRegistry()
        .Register(new("assault", _ => 0, 1, 0))
        .Register(new("guard", survey =>
        {
            if (survey.Zone.Count == 0)
                return 0;
            var distance = survey.Board.NearestDistance(survey.At, survey.Zone);
            return distance == 0 ? 260 : -distance * 55;
        }, 1, 0))
        .Register(new("patrol", survey =>
        {
            var directive = StateJson.Object(survey.Unit["directive"], "directive");
            var waypoints = StateJson.Array(directive["waypoints"], "waypoints");
            if (waypoints.Count == 0)
                return survey.Zone.Count == 0 ? 0 : HoldZone(survey);
            var cursor = StateJson.Integer(directive, "cursor");
            var target = TacticalQueries.Position(StateJson.Object(waypoints[cursor % waypoints.Count], "waypoint"));
            return 320d / (1 + survey.Board.Distance(survey.At, target));
        }, 1, 0, (unit, at) =>
        {
            var directive = StateJson.Object(unit["directive"], "directive");
            var waypoints = StateJson.Array(directive["waypoints"], "waypoints");
            if (waypoints.Count == 0)
                return;
            var cursor = StateJson.Integer(directive, "cursor");
            var waypoint = StateJson.Object(waypoints[cursor % waypoints.Count], "waypoint");
            if (TacticalQueries.Position(waypoint) == at)
                directive["cursor"] = (cursor + 1) % waypoints.Count;
        }))
        .Register(new("retreat", survey => survey.Zone.Count == 0 ? -100 : HoldZone(survey), 0.15, -5_000));

    private static double HoldZone(DirectiveSurvey survey)
    {
        var distance = survey.Board.NearestDistance(survey.At, survey.Zone);
        return distance == 0 ? 1_200 : -distance * 180;
    }

}
