using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

public sealed class AiTurnContext
{
    public AiRules Rules
    {
        get;
    }
    public JsonObject State
    {
        get;
    }
    public int Player
    {
        get;
    }
    public double Aggression
    {
        get;
    }
    public AbilityAiEvaluatorRegistry AbilityEvaluators
    {
        get;
    }
    public AiObjectiveAdvisorRegistry ObjectiveAdvisors
    {
        get;
    }
    internal StateContent Content => Rules.Content.Source;
    private AiAgenda? agenda;
    private Dictionary<int, double>? threat;
    private TacticalBattlefield? battlefield;
    private TacticalBoard? board;
    public AiAgenda Agenda => agenda ??= AiAgenda.Read(this);
    public IReadOnlyDictionary<int, double> Threat => threat ??= AiMeasures.ThreatMap(this);
    public TacticalBattlefield Battlefield => battlefield ??= new TacticalBattlefield(State, Content);
    public TacticalBoard Board => board ??= new TacticalBoard(Rules.Grids.Get(Text(State["rules"]!["grid"], "grid")), Object(State["map"], "map"));

    public AiTurnContext(AiPlanningDependencies planning, JsonObject state, int player, double aggression)
    {
        Rules = planning.Rules;
        State = state;
        Player = player;
        Aggression = aggression;
        AbilityEvaluators = planning.AbilityEvaluators;
        ObjectiveAdvisors = planning.ObjectiveAdvisors;
    }
    public IReadOnlyList<JsonObject> Actors() => Rules.Turns.Actors(State).Where(unit => !IsTrue(unit["done"])).ToArray();
    public int Distance(Coord left, Coord right) => Board.Grid.Distance(left, right);
    public double NearestDistance(Coord from, IEnumerable<Coord> places)
    {
        var best = double.PositiveInfinity;
        foreach (var place in places)
            best = Math.Min(best, Distance(from, place));
        return best;
    }
    public IReadOnlyList<JsonObject> Enemies(int owner) => Objects(State["units"], "units").Where(unit => AreEnemies(State, Integer(unit, "owner"), owner)).ToArray();
}

internal static class AiMeasures
{
    internal static IReadOnlyList<JsonElement> WeaponsForType(StateContent content, string type) =>
        content.Read("units", type).GetProperty("weapons").EnumerateArray()
            .Select(id => content.Read("weapons", Text(id))).ToArray();
    internal static double MaximumWeaponPower(StateContent content, string type) => WeaponsForType(content, type).Select(weapon => Number(weapon.GetProperty("power"), "power")).Prepend(0).Max();
    internal static double MaximumWeaponRange(StateContent content, string type) => WeaponsForType(content, type).Select(weapon => Number(weapon.GetProperty("maxRange"), "maxRange")).Prepend(0).Max();
    internal static double PreferredEngagementRange(StateContent content, JsonObject unit)
    {
        var weapons = WeaponsForType(content, Text(unit, "type"));
        return weapons.Any(weapon => Number(weapon.GetProperty("minRange"), "minRange") <= 1) ? 1 : weapons.Select(weapon => Number(weapon.GetProperty("maxRange"), "maxRange")).Prepend(1).Max();
    }
    internal static double HpRatio(StateContent content, JsonObject unit)
    {
        var maximum = Number(content.Read("units", Text(unit, "type")).GetProperty("maxHp"), "maxHp");
        return maximum > 0 ? Math.Clamp(Number(unit["hp"], "hp") / maximum, 0, 1) : 1;
    }
    internal static double UnitWorth(StateContent content, JsonObject unit) => Number(content.Read("units", Text(unit, "type")).GetProperty("value"), "value") *
        (0.4 + 0.6 * HpRatio(content, unit)) * (1 + Number(unit["rank"], "rank") * 0.08);
    internal static bool Includes(JsonElement value, string text) => value.EnumerateArray().Any(node => Text(node) == text);
    internal static bool Includes(JsonNode? value, string text) => Array(value, "values").Any(node => Text(node, "value") == text);
    internal static bool HasStatus(JsonObject unit, string id) => Objects(unit["statuses"], "statuses").Any(status => Text(status, "id") == id);

    internal static Dictionary<int, double> ThreatMap(AiTurnContext context)
    {
        var threat = new Dictionary<int, double>();
        foreach (var foe in context.Enemies(context.Player))
        {
            var weight = MaximumWeaponPower(context.Content, Text(foe, "type")) * (0.5 + 0.5 * HpRatio(context.Content, foe));
            foreach (var tile in context.Rules.Space.ThreatTiles(context.State, foe))
                threat[tile] = threat.GetValueOrDefault(tile) + weight;
        }
        foreach (var cast in Objects(context.State["pendingCasts"], "pendingCasts"))
        {
            if (Integer(cast, "owner") == context.Player || UnitById(context.State, Integer(cast, "caster")) is null)
                continue;
            var weapon = context.Content.Read("weapons", Text(cast, "weapon"));
            foreach (var cell in context.Rules.Combat.Coverage(context.State, Position(Object(cast["origin"], "origin")), Position(Object(cast["target"], "target")), Text(weapon, "area")))
            {
                var tile = context.Board.IndexOf(cell);
                threat[tile] = threat.GetValueOrDefault(tile) + Number(weapon.GetProperty("power"), "power") * 1.5;
            }
        }
        return threat;
    }
}
