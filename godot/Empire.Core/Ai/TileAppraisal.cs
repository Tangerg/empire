using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

/// <summary>Original ordered one-ply position utility; legality belongs to the engine.</summary>
public sealed class TileAppraisal
{
    private readonly AiTurnContext context;
    private readonly JsonObject unit;
    private readonly Coord at;
    private readonly JsonElement definition;
    private readonly JsonElement terrain;
    private readonly TacticalCell cell;
    private IReadOnlyList<JsonObject>? foes;

    public TileAppraisal(AiTurnContext context, JsonObject unit, Coord at)
    {
        this.context = context;
        this.unit = unit;
        this.at = at;
        definition = context.Content.Read("units", Text(unit, "type"));
        cell = context.Battlefield.Cell(at);
        terrain = cell.Terrain;
    }

    private int Tile => context.Board.IndexOf(at);
    private int UnitId => Integer(unit, "id");
    private AiAgenda Agenda => context.Agenda;
    private double Aggression => context.Aggression;
    private bool CanCapture => AiMeasures.Includes(definition.GetProperty("abilities"), "capture");
    private double ProtectedWeight => Agenda.Mission.ProtectedUnits.GetValueOrDefault(UnitId);
    private IReadOnlyList<JsonObject> Foes => foes ??= context.Enemies(Integer(unit, "owner"));

    public double Value
    {
        get
        {
            // Addition order is observable when utility scores nearly tie.
            var total = 0.0;
            total += context.Rules.Directives.Pull(context.State, unit, at, context.Board.Grid);
            total += Number(terrain.GetProperty("defense"), "defense") * 90;
            total += cell.Elevation * 3;
            total += CaptureProspect();
            total += MissionPull();
            total += EscortPull();
            total += EngagementRange();
            total += CoverFromNearestFoe();
            total += EnemyKeepPull();
            total += HomeDefencePull();
            total += Exposure();
            total += Recovery();
            total += CommandLink();
            return total;
        }
    }

    private double CaptureProspect()
    {
        if (!CanCapture || Agenda.CaptureTargets.Count == 0)
            return 0;
        var best = 0.0;
        foreach (var target in Agenda.CaptureTargets)
            best = Math.Max(best, target.Weight * 120 / (1 + context.Distance(at, target.At)));
        return best * (0.6 + 0.4 * Aggression);
    }

    private double MissionPull()
    {
        var strongest = 0.0;
        foreach (var destination in Agenda.Mission.Destinations)
        {
            if (destination.UnitIds is not null && !destination.UnitIds.Contains(UnitId))
                continue;
            if (destination.CaptureOnly && !CanCapture)
                continue;
            strongest = Math.Max(strongest, destination.Weight * 180 / (1 + context.Distance(at, destination.At)));
        }
        return strongest;
    }

    private double EscortPull()
    {
        if (ProtectedWeight != 0 || Agenda.Mission.ProtectedUnits.Count == 0)
            return 0;
        var strongest = 0.0;
        foreach (var (id, weight) in Agenda.Mission.ProtectedUnits)
            if (UnitById(context.State, id) is { } charge)
                strongest = Math.Max(strongest, weight * 100 / (1 + context.Distance(at, Position(charge))));
        return strongest;
    }

    private double EngagementRange()
    {
        if (Foes.Count == 0)
            return 0;
        var nearest = context.NearestDistance(at, Foes.Select(Position));
        var ideal = AiMeasures.PreferredEngagementRange(context.Content, unit);
        var wants = context.Rules.Directives.Engagement(unit);
        return -Math.Abs(nearest - ideal) * 16 * (0.5 + Aggression) * wants;
    }

    private double CoverFromNearestFoe()
    {
        if (Foes.Count == 0)
            return 0;
        var nearest = Foes.OrderBy(foe => context.Distance(at, Position(foe))).First();
        var facing = cell.DirectionalCoverFrom(context.Board.Grid, Position(nearest));
        if (facing == "full")
            return 36;
        if (facing == "half")
            return 18;
        if (cell.Cover == "full")
            return 28;
        if (cell.Cover == "half")
            return 14;
        return 0;
    }

    private double EnemyKeepPull() => Agenda.EnemyHqs.Count == 0 ? 0 :
        -context.NearestDistance(at, Agenda.EnemyHqs) * 8 * Aggression;

    private double HomeDefencePull()
    {
        if (Agenda.MyHqs.Count == 0)
            return 0;
        var threatened = Agenda.MyHqs.Any(keep => Foes.Any(foe => context.Distance(Position(foe), keep) <= 5));
        return threatened ? -context.NearestDistance(at, Agenda.MyHqs) * 14 * (1 - Aggression * 0.5) : 0;
    }

    private double Exposure() => -context.Threat.GetValueOrDefault(Tile) * (0.55 - 0.25 * Aggression) *
        (1 - Number(definition.GetProperty("defense"), "defense")) * (1 + ProtectedWeight * 0.15);

    private double Recovery()
    {
        var ours = Integer(context.Board.Map["owners"]![Tile], "owner") == Integer(unit, "owner");
        var rate = ours ? Number(terrain.GetProperty("heal"), "heal") : 0;
        return rate <= 0 || AiMeasures.HpRatio(context.Content, unit) >= 0.6 ? 0 : rate * 4;
    }

    private double CommandLink()
    {
        if (unit["commanderId"] is not JsonValue id || string.IsNullOrEmpty(id.GetValue<string>()))
            return 0;
        var commander = Objects(context.State["commanders"], "commanders").FirstOrDefault(candidate => Text(candidate, "id") == id.GetValue<string>());
        if (commander is null || UnitById(context.State, Integer(commander, "unitId")) is not { } leader)
            return 0;
        var distance = context.Distance(at, Position(leader));
        var radius = Number(commander["radius"], "radius");
        return distance <= radius ? 45 : -Math.Max(0, distance - radius) * 18;
    }
}
