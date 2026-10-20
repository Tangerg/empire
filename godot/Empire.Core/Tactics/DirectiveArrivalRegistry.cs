using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed class DirectiveArrivalRegistry
{
    private readonly Dictionary<string, Action<JsonObject, Coord>> handlers = new(StringComparer.Ordinal);

    public DirectiveArrivalRegistry Register(string id, Action<JsonObject, Coord> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!handlers.TryAdd(id, handler))
            throw new DomainInvariantException($"unit directive already registered: \"{id}\"");
        return this;
    }

    public DirectiveArrivalRegistry Replace(string id, Action<JsonObject, Coord> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handlers[id] = handler;
        return this;
    }

    public IReadOnlyList<string> Keys() => handlers.Keys.ToArray();

    public DirectiveArrivalRegistry Clone()
    {
        var clone = new DirectiveArrivalRegistry();
        foreach (var pair in handlers) clone.Register(pair.Key, pair.Value);
        return clone;
    }

    public void ArriveAt(JsonObject unit, Coord at)
    {
        var directive = StateJson.Object(unit["directive"], "directive");
        var mode = StateJson.Text(directive, "mode");
        if (!handlers.TryGetValue(mode, out var handler))
            throw new DomainInvariantException($"unknown unit directive \"{mode}\"");
        handler(unit, at);
    }

    public static DirectiveArrivalRegistry Core() => new DirectiveArrivalRegistry()
        .Register("assault", (_, _) => { })
        .Register("guard", (_, _) => { })
        .Register("retreat", (_, _) => { })
        .Register("patrol", (unit, at) =>
        {
            var directive = StateJson.Object(unit["directive"], "directive");
            var waypoints = StateJson.Array(directive["waypoints"], "waypoints");
            if (waypoints.Count == 0) return;
            var cursor = StateJson.Integer(directive, "cursor");
            var waypoint = StateJson.Object(waypoints[cursor % waypoints.Count], "waypoint");
            if (TacticalQueries.Position(waypoint) == at) directive["cursor"] = (cursor + 1) % waypoints.Count;
        });
}
