using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Tactics;

/// <summary>Canonical fog queries. Terrain remains known; only actors can be concealed.</summary>
public sealed class BattleVision(MovementRules movement)
{
    public IReadOnlySet<int> VisibleTiles(JsonObject state, int viewer)
    {
        var seen = new HashSet<int>();
        var map = Object(state["map"], "map");
        if (!IsTrue(state["rules"]?["fog"]))
        {
            for (var i = 0; i < Array(map["tiles"], "tiles").Count; i++) seen.Add(i);
            return seen;
        }
        var board = movement.Board(state);
        var battlefield = movement.Battlefield(state);
        foreach (var unit in Objects(state["units"], "units"))
        {
            var owner = Integer(unit, "owner");
            if (owner != viewer && !TacticalQueries.AreAllies(state, owner, viewer)) continue;
            var at = TacticalQueries.Position(unit);
            var radius = Number(movement.Content.Get("units", Text(unit, "type"))["vision"], "vision") + battlefield.Cell(at).Vision;
            if (radius != Math.Truncate(radius) || radius < int.MinValue || radius > int.MaxValue)
                throw new DomainInvariantException("vision radius must be an integer");
            foreach (var cell in board.Ring(at, 0, (int)radius)) seen.Add(board.IndexOf(cell));
        }
        var owners = Array(map["owners"], "owners");
        for (var i = 0; i < owners.Count; i++) if (owners[i] is not null && Integer(owners[i], "owner") == viewer) seen.Add(i);
        return seen;
    }

    public bool IsUnitVisible(JsonObject state, int viewer, JsonObject unit, IReadOnlySet<int>? seen = null)
    {
        if (!IsTrue(state["rules"]?["fog"])) return true;
        var owner = Integer(unit, "owner");
        if (owner == viewer || TacticalQueries.AreAllies(state, owner, viewer)) return true;
        var board = movement.Board(state);
        var at = TacticalQueries.Position(unit);
        if (!(seen ?? VisibleTiles(state, viewer)).Contains(board.IndexOf(at))) return false;
        // Source cover concealment uses terrain opacity, not structures' line-of-sight blocking.
        if (!IsTrue(movement.Battlefield(state).Cell(at).Terrain["opaque"])) return true;
        return Objects(state["units"], "units").Any(other =>
            (Integer(other, "owner") == viewer || TacticalQueries.AreAllies(state, Integer(other, "owner"), viewer)) &&
            board.Distance(TacticalQueries.Position(other), at) == 1);
    }

    public IReadOnlyList<JsonObject> VisibleUnits(JsonObject state, int viewer)
    {
        var seen = VisibleTiles(state, viewer);
        return Objects(state["units"], "units").Where(unit => IsUnitVisible(state, viewer, unit, seen)).ToArray();
    }
}
