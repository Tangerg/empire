using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed record MovementRuleServices(ReadOnlyContent Content, GridRegistry Grids);

public sealed class MovementRules
{
    internal StateContent Content
    {
        get;
    }
    public GridRegistry Grids
    {
        get;
    }

    public MovementRules(MovementRuleServices rules)
    {
        ArgumentNullException.ThrowIfNull(rules.Content);
        ArgumentNullException.ThrowIfNull(rules.Grids);
        Content = rules.Content.Source;
        Grids = rules.Grids;
    }

    public ITacticalGrid Grid(JsonObject state) =>
        Grids.Get(StateJson.Text(StateJson.Object(state["rules"], "rules"), "grid"));

    public TacticalBoard Board(JsonObject state) =>
        new(Grid(state), StateJson.Object(state["map"], "map"));

    public TacticalBattlefield Battlefield(JsonObject state) => new(state, Content);

    public int MovementBudget(JsonObject state, JsonObject unit)
    {
        var definition = Content.Read("units", StateJson.Text(unit, "type"));
        var grid = Grid(state);
        var formation = TacticalModifiers.ActiveFormation(Content, grid, state, unit);
        var budget = Math.Max(0, StateJson.Number(definition, "movement", 0) +
            CombatEffects.CalculateModifiers(Content, unit).MovementDelta +
            TacticalModifiers.CommanderAura(grid, state, unit).MovementDelta +
            (formation is null ? 0 : StateJson.Number(formation, "movementDelta", 0)));
        return MovementInteger(budget, "movement budget");
    }

    public MoveField ComputeMoveField(JsonObject state, JsonObject unit)
    {
        var definition = Content.Read("units", StateJson.Text(unit, "type"));
        var movementClass = StateJson.Text(definition, "movementClass");
        var board = Board(state);
        var battlefield = Battlefield(state);
        var rules = StateJson.Object(state["rules"], "rules");
        var budget = MovementBudget(state, unit);
        var origin = TacticalQueries.Position(unit);
        var start = board.IndexOf(origin);
        var unitId = StateJson.Integer(unit, "id");
        var owner = StateJson.Integer(unit, "owner");
        var controlled = HostileControlZone(state, unit);
        var tiles = new Dictionary<int, ReachableTile>
        {
            [start] = new(start, origin.X, origin.Y, 0, -1, true),
        };
        var buckets = new List<int>[checked(budget + 1)];
        for (var index = 0; index < buckets.Length; index++)
            buckets[index] = new List<int>();
        buckets[0].Add(start);
        for (var cost = 0; cost <= budget; cost++)
        {
            for (var queueIndex = 0; queueIndex < buckets[cost].Count; queueIndex++)
            {
                if (!tiles.TryGetValue(buckets[cost][queueIndex], out var node) || node.Cost != cost)
                    continue;
                var from = board.CoordOf(node.Index);
                foreach (var next in board.Neighbours(from))
                {
                    var index = board.IndexOf(next);
                    var traversal = battlefield.TraversalCost(from, next, movementClass);
                    if (traversal is null)
                        continue;
                    var blocker = TacticalQueries.UnitAt(state, next);
                    if (blocker is not null && StateJson.Integer(blocker, "id") != unitId)
                    {
                        if (TacticalQueries.AreEnemies(state, StateJson.Integer(blocker, "owner"), owner))
                        {
                            if (StateJson.IsTrue(rules["enemiesBlockMovement"]))
                                continue;
                        }
                        else if (!StateJson.IsTrue(rules["friendlyPassThrough"]))
                            continue;
                    }
                    var nextCost = checked(cost + MovementInteger(traversal.Value, "traversal cost"));
                    if (nextCost > budget)
                        continue;
                    if (tiles.TryGetValue(index, out var previous) && previous.Cost <= nextCost)
                        continue;
                    tiles[index] = new(index, next.X, next.Y, nextCost, node.Index,
                        blocker is null || StateJson.Integer(blocker, "id") == unitId);
                    if (!controlled.Contains(index))
                        buckets[nextCost].Add(index);
                }
            }
        }
        return new MoveField(origin, unitId, tiles);
    }

    public IReadOnlyList<Coord>? PathTo(MoveField field, JsonObject state, Coord to)
    {
        var board = Board(state);
        if (!board.Contains(to))
            return null;
        return field.TracePath(board.IndexOf(to));
    }

    public int? MoveCostOf(MoveField field, JsonObject state, Coord to)
    {
        var board = Board(state);
        if (!board.Contains(to))
            return null;
        return field.Tiles.TryGetValue(board.IndexOf(to), out var tile) ? tile.Cost : null;
    }

    internal static int ControlRadius(StateContent content, JsonObject state, JsonObject unit)
    {
        if (!StateJson.IsTrue(StateJson.Object(state["rules"], "rules")["zoneOfControl"]))
            return 0;
        var definition = content.Read("units", StateJson.Text(unit, "type"));
        return MovementInteger(Math.Max(0, StateJson.Round(StateJson.Number(definition, "zoneOfControl", 1))), "control radius");
    }

    public IReadOnlySet<int> HostileControlZone(JsonObject state, JsonObject unit)
    {
        var controlled = new HashSet<int>();
        if (!StateJson.IsTrue(StateJson.Object(state["rules"], "rules")["zoneOfControl"]))
            return controlled;
        var board = Board(state);
        var owner = StateJson.Integer(unit, "owner");
        foreach (var other in StateJson.Objects(state["units"], "units"))
        {
            if (!TacticalQueries.AreEnemies(state, StateJson.Integer(other, "owner"), owner))
                continue;
            var radius = ControlRadius(Content, state, other);
            if (radius <= 0)
                continue;
            foreach (var at in board.Ring(TacticalQueries.Position(other), 1, radius))
                controlled.Add(board.IndexOf(at));
        }
        return controlled;
    }

    public bool HasDirectLineOfSight(JsonObject state, Coord from, Coord target)
    {
        var trace = Grid(state).Line(from, target);
        var battlefield = Battlefield(state);
        var fromEye = battlefield.Cell(from).Elevation + 1;
        var targetEye = battlefield.Cell(target).Elevation + 1;
        for (var index = 1; index < trace.Count - 1; index++)
        {
            var cell = battlefield.Cell(trace[index]);
            if (!cell.BlocksVision)
                continue;
            var progress = (double)index / (trace.Count - 1);
            var rayHeight = fromEye + (targetEye - fromEye) * progress;
            if (cell.ObstructionTop >= rayHeight)
                return false;
        }
        return true;
    }

    public IReadOnlyList<Coord> AttackTilesFrom(JsonObject state, JsonObject unit, Coord from, JsonObject? weapon = null) =>
        weapon is null ? AttackTilesFrom(state, from, UnitWeapons(unit).First()) :
            AttackTilesFrom(state, from, StateJson.Integer(weapon, "minRange"), StateJson.Integer(weapon, "maxRange"),
                StateJson.Text(weapon, "lineOfSight") == "direct");

    private IReadOnlyList<Coord> AttackTilesFrom(JsonObject state, Coord from, JsonElement weapon) =>
        AttackTilesFrom(state, from, StateJson.Integer(weapon, "minRange"), StateJson.Integer(weapon, "maxRange"),
            StateJson.Text(weapon, "lineOfSight") == "direct");

    private IReadOnlyList<Coord> AttackTilesFrom(JsonObject state, Coord from, int minimum, int maximum, bool direct)
    {
        if (maximum <= 0)
            return Array.Empty<Coord>();
        return Board(state).Ring(from, minimum, maximum)
            .Where(target => !direct || HasDirectLineOfSight(state, from, target)).ToArray();
    }

    public IReadOnlySet<int> ThreatTiles(JsonObject state, JsonObject unit, MoveField? field = null)
    {
        var result = new HashSet<int>();
        var board = Board(state);
        var weapons = UnitWeapons(unit);
        foreach (var weapon in weapons)
            foreach (var at in AttackTilesFrom(state, TacticalQueries.Position(unit), weapon))
                result.Add(board.IndexOf(at));
        var resolvedField = field ?? ComputeMoveField(state, unit);
        foreach (var weapon in weapons.Where(candidate => StateJson.IsTrue(candidate.GetProperty("moveAndAttack"))))
            foreach (var index in resolvedField.Stops)
                foreach (var at in AttackTilesFrom(state, board.CoordOf(index), weapon))
                    result.Add(board.IndexOf(at));
        return result;
    }

    public IReadOnlyList<Coord> AttackTargetCoords(JsonObject state, JsonObject unit, Coord from, JsonObject? weapon = null)
    {
        var result = new List<Coord>();
        var owner = StateJson.Integer(unit, "owner");
        foreach (var cell in AttackTilesFrom(state, unit, from, weapon))
        {
            var other = TacticalQueries.UnitAt(state, cell);
            if (other is not null && TacticalQueries.AreEnemies(state, StateJson.Integer(other, "owner"), owner))
            {
                result.Add(cell);
                continue;
            }
            var structure = TacticalQueries.StructureAt(state, cell);
            if (structure is null || !StateJson.IsTrue(Content.Read("structures", StateJson.Text(structure, "type")).GetProperty("targetable")))
                continue;
            var structureOwner = StateJson.Integer(structure, "owner");
            if (structureOwner == 0 || TacticalQueries.AreEnemies(state, structureOwner, owner))
                result.Add(cell);
        }
        return result;
    }

    public IReadOnlyList<JsonObject> HealTargetsFrom(JsonObject state, JsonObject unit, Coord from)
    {
        var result = new List<JsonObject>();
        var owner = StateJson.Integer(unit, "owner");
        var id = StateJson.Integer(unit, "id");
        foreach (var cell in Board(state).Ring(from, 1, 1))
        {
            var other = TacticalQueries.UnitAt(state, cell);
            if (other is null || StateJson.Integer(other, "id") == id ||
                !TacticalQueries.AreAllies(state, StateJson.Integer(other, "owner"), owner))
                continue;
            var maximum = StateJson.Number(Content.Read("units", StateJson.Text(other, "type")), "maxHp", 0);
            if (StateJson.Number(other, "hp", 0) < maximum)
                result.Add(other);
        }
        return result;
    }

    internal IReadOnlyList<JsonElement> UnitWeapons(JsonObject unit) =>
        Content.Read("units", StateJson.Text(unit, "type")).GetProperty("weapons").EnumerateArray()
            .Select(id => Content.Read("weapons", StateJson.Text(id))).ToArray();

    private static int MovementInteger(double value, string subject)
    {
        if (value != Math.Truncate(value) || value < 0 || value > int.MaxValue)
            throw new DomainInvariantException($"{subject} must be a non-negative integer");
        return (int)value;
    }
}
