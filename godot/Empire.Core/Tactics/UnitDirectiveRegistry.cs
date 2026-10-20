using System.Text.Json.Nodes;
using Empire.Core.Geometry;
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
public sealed class UnitDirectiveRegistry
{
    private readonly Dictionary<string, UnitDirectiveBehavior> entries = new(StringComparer.Ordinal);

    public bool IsSealed { get; private set; }
    public int Count => entries.Count;

    public UnitDirectiveRegistry Register(UnitDirectiveBehavior behavior)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(behavior);
        if (!entries.TryAdd(behavior.Id, behavior))
            throw new DomainInvariantException($"unit directive already registered: \"{behavior.Id}\"");
        return this;
    }

    public UnitDirectiveRegistry Replace(UnitDirectiveBehavior behavior)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(behavior);
        entries[behavior.Id] = behavior;
        return this;
    }

    public UnitDirectiveBehavior Get(string id) => entries.TryGetValue(id, out var behavior) ? behavior :
        throw new DomainInvariantException($"unknown unit directive \"{id}\"");

    public UnitDirectiveBehavior? TryGet(string id) => entries.GetValueOrDefault(id);
    public bool Has(string id) => entries.ContainsKey(id);
    public IReadOnlyList<UnitDirectiveBehavior> All() => entries.Values.ToArray();
    public IReadOnlyList<string> Keys() => entries.Keys.ToArray();

    public UnitDirectiveRegistry Seal()
    {
        IsSealed = true;
        return this;
    }

    public UnitDirectiveRegistry Clone()
    {
        var clone = new UnitDirectiveRegistry();
        foreach (var behavior in entries.Values) clone.Register(behavior);
        return clone;
    }

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
        var board = new TacticalBoard(StateJson.Object(state["map"], "map"), grid);
        return Of(unit).Pull(new DirectiveSurvey(state, unit, at, zone, board));
    }

    public double Engagement(JsonObject unit) => Of(unit).Engagement;
    public double FightPenalty(JsonObject unit) => Of(unit).FightPenalty;
    public void ArriveAt(JsonObject unit, Coord at) => Of(unit).ArriveAt?.Invoke(unit, at);

    public static UnitDirectiveRegistry Core() => new UnitDirectiveRegistry()
        .Register(new("assault", _ => 0, 1, 0))
        .Register(new("guard", survey =>
        {
            if (survey.Zone.Count == 0) return 0;
            var distance = survey.Board.NearestDistance(survey.At, survey.Zone);
            return distance == 0 ? 260 : -distance * 55;
        }, 1, 0))
        .Register(new("patrol", survey =>
        {
            var directive = StateJson.Object(survey.Unit["directive"], "directive");
            var waypoints = StateJson.Array(directive["waypoints"], "waypoints");
            if (waypoints.Count == 0) return survey.Zone.Count == 0 ? 0 : HoldZone(survey);
            var cursor = StateJson.Integer(directive, "cursor");
            var target = TacticalQueries.Position(StateJson.Object(waypoints[cursor % waypoints.Count], "waypoint"));
            return 320d / (1 + survey.Board.Distance(survey.At, target));
        }, 1, 0, (unit, at) =>
        {
            var directive = StateJson.Object(unit["directive"], "directive");
            var waypoints = StateJson.Array(directive["waypoints"], "waypoints");
            if (waypoints.Count == 0) return;
            var cursor = StateJson.Integer(directive, "cursor");
            var waypoint = StateJson.Object(waypoints[cursor % waypoints.Count], "waypoint");
            if (TacticalQueries.Position(waypoint) == at) directive["cursor"] = (cursor + 1) % waypoints.Count;
        }))
        .Register(new("retreat", survey => survey.Zone.Count == 0 ? -100 : HoldZone(survey), 0.15, -5_000));

    private static double HoldZone(DirectiveSurvey survey)
    {
        var distance = survey.Board.NearestDistance(survey.At, survey.Zone);
        return distance == 0 ? 1_200 : -distance * 180;
    }

    private void RequireMutable()
    {
        if (IsSealed)
            throw new DomainInvariantException("unit directive registry is sealed after composition");
    }
}
