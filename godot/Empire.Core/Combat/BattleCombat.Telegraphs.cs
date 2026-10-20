using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed record ActiveCast(int Caster, int Owner, string Weapon, string Name, Coord Origin, Coord Target,
    double DeclaredAt, double ResolveAt, double Remaining, double Progress);

public sealed partial class BattleCombat
{
    public IReadOnlyList<ActiveCast> ActiveCasts(JsonObject state)
    {
        var result = new List<ActiveCast>();
        foreach (var cast in StateJson.Objects(state["pendingCasts"], "pending casts"))
        {
            var caster = StateJson.Integer(cast, "caster");
            if (TacticalQueries.UnitById(state, caster) is null) continue;
            var declared = StateJson.Number(cast, "declaredAt", 0);
            var resolve = StateJson.Number(cast, "resolveAt", 0);
            if (resolve <= declared) throw new DomainInvariantException($"a cast must resolve after it is declared, got {declared} -> {resolve}");
            var weaponId = StateJson.Text(cast, "weapon");
            var weapon = content.Get("weapons", weaponId);
            var now = StateJson.Number(state, "actorTurns", 0);
            result.Add(new(caster, StateJson.Integer(cast, "owner"), weaponId, StateJson.Text(weapon, "name"),
                TacticalQueries.Position(cast["origin"]!.AsObject()), TacticalQueries.Position(cast["target"]!.AsObject()),
                declared, resolve, Math.Max(0, resolve - now), Math.Clamp((now - declared) / (resolve - declared), 0, 1)));
        }
        return result.AsReadOnly();
    }

    /// <summary>Public committed-area projection, preserving source last-cast overwrite order.</summary>
    public IReadOnlyDictionary<Coord, double> IncomingCasts(JsonObject state)
    {
        var result = new Dictionary<Coord, double>();
        var board = space.Board(state);
        foreach (var cast in ActiveCasts(state))
            foreach (var cell in AreaShapes.Coverage(board, cast.Origin, cast.Target,
                StateJson.Text(content.Get("weapons", cast.Weapon), "area"))) result[cell] = cast.Remaining;
        return result;
    }
}
