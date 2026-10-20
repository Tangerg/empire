using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

public sealed record AiDestination(Coord At, double Weight, IReadOnlySet<int>? UnitIds, bool CaptureOnly, string Reason);
public sealed record AiMissionIntent(IReadOnlyList<AiDestination> Destinations, IReadOnlyDictionary<int, double> PriorityUnits,
    IReadOnlyDictionary<string, double> PriorityStructures, IReadOnlyDictionary<int, double> ProtectedUnits);
public sealed record CaptureTarget(Coord At, double Weight);
public sealed record AiAgenda(IReadOnlyList<CaptureTarget> CaptureTargets, IReadOnlyList<Coord> EnemyHqs,
    IReadOnlyList<Coord> MyHqs, AiMissionIntent Mission)
{
    internal static AiAgenda Read(AiTurnContext context)
    {
        var targets = new List<CaptureTarget>(); var enemyHqs = new List<Coord>(); var myHqs = new List<Coord>();
        var map = Object(context.State["map"], "map"); var tiles = Array(map["tiles"], "tiles"); var owners = Array(map["owners"], "owners");
        for (var index = 0; index < tiles.Count; index++)
        {
            var terrain = context.Content.Get("terrains", Text(tiles[index], "terrain"));
            if (!IsTrue(terrain["capturable"])) continue;
            var at = context.Board.CoordOf(index); var owner = Integer(owners[index], "owner");
            if (IsTrue(terrain["hq"]))
            { if (owner == context.Player) myHqs.Add(at); else if (AreEnemies(context.State, owner, context.Player)) enemyHqs.Add(at); }
            if (owner != context.Player) targets.Add(new CaptureTarget(at, IsTrue(terrain["hq"]) ? 6 : Array(terrain["produces"], "produces").Count > 0 ? 3 : 2));
        }
        return new AiAgenda(targets, enemyHqs, myHqs, BuildMission(context));
    }

    private static AiMissionIntent BuildMission(AiTurnContext context)
    {
        var destinations = new List<AiDestination>(); var priorities = new Dictionary<int, double>();
        var structures = new Dictionary<string, double>(StringComparer.Ordinal); var protectedUnits = new Dictionary<int, double>();
        void Visit(JsonObject objective, double inheritedWeight)
        {
            if (VictorySystem.Status(context.State, context.Player, objective) != "active") return;
            var weight = inheritedWeight * (VictorySystem.Role(objective) switch { "critical" => 1.4, "optional" => 0.55, _ => 1 });
            var pending = VictorySystem.Children(objective).Where(child => VictorySystem.Status(context.State, context.Player, child) is not ("completed" or "failed" or "cancelled" or "inactive"));
            var children = (VictorySystem.RefreshMode(objective) == "sequence" ? pending.Take(1) : pending).ToArray();
            if (children.Length > 0) { foreach (var child in children) Visit(child, weight); return; }
            var advisor = context.ObjectiveAdvisors.TryGet(Text(objective, "type"));
            if (advisor is null) return;
            var advice = new AiObjectiveAdviceContext(context, weight); advisor.Advise(advice, objective);
            destinations.AddRange(advice.Destinations);
            foreach (var pair in advice.PriorityUnits) priorities[pair.Key] = Math.Max(priorities.GetValueOrDefault(pair.Key), pair.Value);
            foreach (var pair in advice.PriorityStructures) structures[pair.Key] = Math.Max(structures.GetValueOrDefault(pair.Key), pair.Value);
            foreach (var pair in advice.ProtectedUnits) protectedUnits[pair.Key] = Math.Max(protectedUnits.GetValueOrDefault(pair.Key), pair.Value);
        }
        foreach (var objective in Objects(Player(context.State, context.Player)["objectives"], "objectives")) Visit(objective, 1);
        return new AiMissionIntent(destinations, priorities, structures, protectedUnits);
    }
}

public sealed class AiObjectiveAdviceContext
{
    public AiTurnContext Turn { get; }
    public double Weight { get; }
    public JsonObject State => Turn.State;
    public int Owner => Turn.Player;
    public List<AiDestination> Destinations { get; } = new();
    public Dictionary<int, double> PriorityUnits { get; } = new();
    public Dictionary<string, double> PriorityStructures { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, double> ProtectedUnits { get; } = new();
    public AiObjectiveAdviceContext(AiTurnContext turn, double weight) { Turn = turn; Weight = weight; }
    public void Destination(Coord at, double weight, string reason, IEnumerable<int>? unitIds = null, bool captureOnly = false) =>
        Destinations.Add(new AiDestination(at, weight * Weight, unitIds?.ToHashSet(), captureOnly, reason));
    public void PriorityUnit(int id, double weight) => PriorityUnits[id] = Math.Max(PriorityUnits.GetValueOrDefault(id), weight * Weight);
    public void PriorityStructure(string id, double weight) => PriorityStructures[id] = Math.Max(PriorityStructures.GetValueOrDefault(id), weight * Weight);
    public void Protect(int id, double weight) => ProtectedUnits[id] = Math.Max(ProtectedUnits.GetValueOrDefault(id), weight * Weight);
    internal IEnumerable<Coord> Zone(string id) => Objects(Object(Object(State["scenario"], "scenario")["zones"], "zones")[id], "zone").Select(Position);
    internal IReadOnlyList<JsonObject> Select(JsonObject selector) => new ScenarioSystem(Turn.Content).SelectUnits(State, selector);
}

public static class DefaultAiObjectiveAdvisors
{
    public static AiObjectiveAdvisorRegistry Create()
    {
        var registry = new AiObjectiveAdvisorRegistry();
        registry.Register(new("routEnemies", (context, _) =>
        { foreach (var enemy in context.Turn.Enemies(context.Owner)) context.PriorityUnit(Integer(enemy, "id"), 0.3); }));
        registry.Register(new("captureHQ", (context, _) => Sites(context, true)));
        registry.Register(new("holdAllVillages", (context, _) => Sites(context, false)));
        registry.Register(new("surviveTurns", (context, _) =>
        { foreach (var unit in UnitsOf(context.State, context.Owner)) context.Protect(Integer(unit, "id"), 1); }));
        registry.Register(new("eliminate", (context, objective) =>
        {
            foreach (var target in context.Select(Object(objective["selector"], "selector")))
            {
                if (!AreEnemies(context.State, Integer(target, "owner"), context.Owner)) continue;
                context.PriorityUnit(Integer(target, "id"), 5); context.Destination(Position(target), 3, "追击指定目标");
            }
        }));
        registry.Register(new("destroy", (context, objective) =>
        {
            foreach (var id in Array(objective["structures"], "structures"))
            {
                var key = Text(id, "structure id");
                var structure = Objects(context.State["structures"], "structures").FirstOrDefault(candidate => Text(candidate, "id") == key && Number(candidate["hp"], "hp") > 0);
                if (structure is null) continue;
                context.PriorityStructure(key, 5); context.Destination(Position(structure), 5, "摧毁任务结构");
            }
        }));
        registry.Register(new("neutralizeComposite", (context, objective) =>
        {
            var composite = Objects(context.State["composites"], "composites").FirstOrDefault(candidate => Text(candidate, "id") == Text(objective, "composite"));
            if (composite is null) return;
            foreach (var id in Array(composite["parts"], "parts"))
            {
                var key = Text(id, "part id");
                var structure = Objects(context.State["structures"], "structures").FirstOrDefault(candidate => Text(candidate, "id") == key && Number(candidate["hp"], "hp") > 0 && !IsTrue(candidate["disabled"]));
                if (structure is null) continue;
                context.PriorityStructure(key, 6); context.Destination(Position(structure), 6, "瘫痪复合目标部件");
            }
        }));
        registry.Register(new("protect", (context, objective) =>
        {
            foreach (var target in context.Select(Object(objective["selector"], "selector")))
            {
                if (Integer(target, "owner") != context.Owner) continue;
                var id = Integer(target, "id"); context.Protect(id, 5);
                context.Destination(Position(target), 2.5, "护卫关键单位", UnitsOf(context.State, context.Owner).Where(unit => Integer(unit, "id") != id).Select(unit => Integer(unit, "id")));
            }
        }));
        registry.Register(new("escort", (context, objective) =>
        {
            var cells = context.Zone(Text(objective, "zone")).ToArray();
            var escorted = context.Select(Object(objective["selector"], "selector")).Where(unit => Integer(unit, "owner") == context.Owner).ToArray();
            var unarrived = escorted.Where(unit => !cells.Contains(Position(unit))).ToArray();
            foreach (var unit in unarrived) context.Protect(Integer(unit, "id"), 6);
            foreach (var cell in cells)
            {
                context.Destination(cell, 12, "护送目标撤离", unarrived.Select(unit => Integer(unit, "id")));
                context.Destination(cell, 1.5, "封锁护送终点", UnitsOf(context.State, context.Owner).Where(unit => !escorted.Any(target => Integer(target, "id") == Integer(unit, "id"))).Select(unit => Integer(unit, "id")));
            }
        }));
        registry.Register(new("control", (context, objective) =>
        {
            var owners = Array(Object(context.State["map"], "map")["owners"], "owners");
            foreach (var cell in context.Zone(Text(objective, "zone")))
                if (Integer(owners[context.Turn.Board.IndexOf(cell)], "owner") != context.Owner) context.Destination(cell, 8, "控制任务区域", captureOnly: true);
        }));
        registry.Seal(); return registry;
    }

    private static void Sites(AiObjectiveAdviceContext context, bool hqOnly)
    {
        var map = Object(context.State["map"], "map"); var tiles = Array(map["tiles"], "tiles"); var owners = Array(map["owners"], "owners");
        for (var index = 0; index < tiles.Count; index++)
        {
            var terrain = context.Turn.Content.Get("terrains", Text(tiles[index], "terrain")); var owner = Integer(owners[index], "owner");
            if (hqOnly ? !IsTrue(terrain["hq"]) || !AreEnemies(context.State, owner, context.Owner) : !IsTrue(terrain["capturable"]) || owner == context.Owner) continue;
            context.Destination(context.Turn.Board.CoordOf(index), hqOnly ? 8 : IsTrue(terrain["hq"]) ? 7 : 4, hqOnly ? "攻占敌方总部" : "控制据点", captureOnly: true);
        }
    }
}
