using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

public sealed class AiTurnContext
{
    public GrayFlagEngine Engine { get; }
    public JsonObject State { get; }
    public int Player { get; }
    public double Aggression { get; }
    public AbilityAiEvaluatorRegistry AbilityEvaluators { get; }
    public AiObjectiveAdvisorRegistry ObjectiveAdvisors { get; }
    internal StateContent Content => Engine.Content;
    private AiAgenda? agenda;
    private Dictionary<int, double>? threat;
    private TacticalBattlefield? battlefield;
    private TacticalBoard? board;
    public AiAgenda Agenda => agenda ??= AiAgenda.Read(this);
    public IReadOnlyDictionary<int, double> Threat => threat ??= AiMeasures.ThreatMap(this);
    public TacticalBattlefield Battlefield => battlefield ??= Engine.Movement.Battlefield(State);
    public TacticalBoard Board => board ??= Engine.Movement.Board(State);

    public AiTurnContext(GrayFlagEngine engine, JsonObject state, int player, double aggression,
        AbilityAiEvaluatorRegistry abilities, AiObjectiveAdvisorRegistry objectives)
    { Engine = engine; State = state; Player = player; Aggression = aggression; AbilityEvaluators = abilities; ObjectiveAdvisors = objectives; }
    public IReadOnlyList<JsonObject> Actors() => Engine.Turns.Actors(State).Where(unit => !IsTrue(unit["done"])).ToArray();
    public int Distance(Coord left, Coord right) => Board.Grid.Distance(left, right);
    public double NearestDistance(Coord from, IEnumerable<Coord> places)
    { var best = double.PositiveInfinity; foreach (var place in places) best = Math.Min(best, Distance(from, place)); return best; }
    public IReadOnlyList<JsonObject> Enemies(int owner) => Objects(State["units"], "units").Where(unit => AreEnemies(State, Integer(unit, "owner"), owner)).ToArray();
}

internal static class AiMeasures
{
    internal static IReadOnlyList<JsonObject> WeaponsForType(StateContent content, string type) =>
        Array(content.Get("units", type)["weapons"], "weapons").Select(id => content.Get("weapons", Text(id, "weapon id"))).ToArray();
    internal static double MaximumWeaponPower(StateContent content, string type) => WeaponsForType(content, type).Select(weapon => Number(weapon["power"], "power")).Prepend(0).Max();
    internal static double MaximumWeaponRange(StateContent content, string type) => WeaponsForType(content, type).Select(weapon => Number(weapon["maxRange"], "maxRange")).Prepend(0).Max();
    internal static double PreferredEngagementRange(StateContent content, JsonObject unit)
    {
        var weapons = WeaponsForType(content, Text(unit, "type"));
        return weapons.Any(weapon => Number(weapon["minRange"], "minRange") <= 1) ? 1 : weapons.Select(weapon => Number(weapon["maxRange"], "maxRange")).Prepend(1).Max();
    }
    internal static double HpRatio(StateContent content, JsonObject unit)
    { var maximum = Number(content.Get("units", Text(unit, "type"))["maxHp"], "maxHp"); return maximum > 0 ? Math.Clamp(Number(unit["hp"], "hp") / maximum, 0, 1) : 1; }
    internal static double UnitWorth(StateContent content, JsonObject unit) => Number(content.Get("units", Text(unit, "type"))["value"], "value") *
        (0.4 + 0.6 * HpRatio(content, unit)) * (1 + Number(unit["rank"], "rank") * 0.08);
    internal static bool Includes(JsonNode? value, string text) => Array(value, "values").Any(node => Text(node, "value") == text);
    internal static bool HasStatus(JsonObject unit, string id) => Objects(unit["statuses"], "statuses").Any(status => Text(status, "id") == id);

    internal static Dictionary<int, double> ThreatMap(AiTurnContext context)
    {
        var threat = new Dictionary<int, double>();
        foreach (var foe in context.Enemies(context.Player))
        {
            var weight = MaximumWeaponPower(context.Content, Text(foe, "type")) * (0.5 + 0.5 * HpRatio(context.Content, foe));
            foreach (var tile in context.Engine.Movement.ThreatTiles(context.State, foe)) threat[tile] = threat.GetValueOrDefault(tile) + weight;
        }
        foreach (var cast in Objects(context.State["pendingCasts"], "pendingCasts"))
        {
            if (Integer(cast, "owner") == context.Player || UnitById(context.State, Integer(cast, "caster")) is null) continue;
            var weapon = context.Content.Get("weapons", Text(cast, "weapon"));
            foreach (var cell in context.Engine.Combat.Coverage(context.State, Position(Object(cast["origin"], "origin")), Position(Object(cast["target"], "target")), Text(weapon, "area")))
            {
                var tile = context.Board.IndexOf(cell);
                threat[tile] = threat.GetValueOrDefault(tile) + Number(weapon["power"], "power") * 1.5;
            }
        }
        return threat;
    }
}
