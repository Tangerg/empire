using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed partial class BattleCombat
{
    /// <summary>Public committed-area projection, preserving source last-cast overwrite order.</summary>
    public IReadOnlyDictionary<Coord, double> IncomingCasts(JsonObject state)
    {
        var result = new Dictionary<Coord, double>();
        var board = movement.Board(state);
        foreach (var cast in StateJson.Objects(state["pendingCasts"], "pending casts"))
        {
            if (TacticalQueries.UnitById(state, StateJson.Integer(cast, "caster")) is null) continue;
            var declared = StateJson.Number(cast, "declaredAt", 0);
            var resolve = StateJson.Number(cast, "resolveAt", 0);
            if (resolve <= declared) throw new DomainInvariantException($"a cast must resolve after it is declared, got {declared} -> {resolve}");
            var weapon = content.Get("weapons", StateJson.Text(cast, "weapon"));
            var remaining = Math.Max(0, resolve - StateJson.Number(state, "actorTurns", 0));
            foreach (var cell in AreaShapes.Coverage(board, TacticalQueries.Position(cast["origin"]!.AsObject()),
                TacticalQueries.Position(cast["target"]!.AsObject()), StateJson.Text(weapon, "area"))) result[cell] = remaining;
        }
        return result;
    }
}
