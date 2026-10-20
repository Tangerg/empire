using System.Text.Json.Nodes;
using Empire.Core.Geometry;

namespace Empire.Core.Tactics;

/// <summary>One replaceable policy for movement, targeting, threat, and visibility.</summary>
public interface ITacticalSpace
{
    MoveField ComputeMoveField(JsonObject state, JsonObject unit);
    IReadOnlyList<Coord>? PathTo(MoveField field, JsonObject state, Coord destination);
    IReadOnlySet<int> ThreatTiles(JsonObject state, JsonObject unit, MoveField? field = null);
    IReadOnlySet<int> HostileControlZone(JsonObject state, JsonObject unit);
    IReadOnlyList<Coord> AttackTargetCoords(JsonObject state, JsonObject unit, Coord from, JsonObject weapon);
    IReadOnlyList<JsonObject> HealTargetsFrom(JsonObject state, JsonObject unit, Coord from);
    TacticalBoard Board(JsonObject state);
    IReadOnlySet<int> VisibleTiles(JsonObject state, int viewer);
    bool IsUnitVisible(JsonObject state, int viewer, JsonObject unit, IReadOnlySet<int>? seen = null);
    IReadOnlyList<JsonObject> VisibleUnits(JsonObject state, int viewer);
}
