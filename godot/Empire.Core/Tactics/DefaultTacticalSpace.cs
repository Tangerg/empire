using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;

namespace Empire.Core.Tactics;

public sealed record TacticalSpaceRules(ReadOnlyContent Content, GridRegistry Grids);

/// <summary>Default spatial policy, composed from the canonical movement and vision owners.</summary>
public class DefaultTacticalSpace : ITacticalSpace
{
    private readonly MovementRules movement;
    private readonly BattleVision vision;
    public ReadOnlyContent Content
    {
        get;
    }
    public GridRegistry Grids
    {
        get;
    }

    public DefaultTacticalSpace(TacticalSpaceRules rules)
    {
        movement = new MovementRules(new MovementRuleServices(rules.Content, rules.Grids));
        vision = new BattleVision(movement);
        Content = rules.Content;
        Grids = rules.Grids;
    }

    // Source defaults call their underlying rule functions, not virtual sibling methods.
    // A move-field override must not silently change inherited threat or visibility policy.
    public virtual MoveField ComputeMoveField(JsonObject state, JsonObject unit) => movement.ComputeMoveField(state, unit);
    public virtual IReadOnlyList<Coord>? PathTo(MoveField field, JsonObject state, Coord destination) => movement.PathTo(field, state, destination);
    public virtual IReadOnlySet<int> ThreatTiles(JsonObject state, JsonObject unit, MoveField? field = null) => movement.ThreatTiles(state, unit, field);
    public virtual IReadOnlySet<int> HostileControlZone(JsonObject state, JsonObject unit) => movement.HostileControlZone(state, unit);
    public virtual IReadOnlyList<Coord> AttackTargetCoords(JsonObject state, JsonObject unit, Coord from, JsonObject weapon) => movement.AttackTargetCoords(state, unit, from, weapon);
    public virtual IReadOnlyList<JsonObject> HealTargetsFrom(JsonObject state, JsonObject unit, Coord from) => movement.HealTargetsFrom(state, unit, from);
    public virtual TacticalBoard Board(JsonObject state) => movement.Board(state);
    public virtual IReadOnlySet<int> VisibleTiles(JsonObject state, int viewer) => vision.VisibleTiles(state, viewer);
    public virtual bool IsUnitVisible(JsonObject state, int viewer, JsonObject unit, IReadOnlySet<int>? seen = null) => vision.IsUnitVisible(state, viewer, unit, seen);
    public virtual IReadOnlyList<JsonObject> VisibleUnits(JsonObject state, int viewer) => vision.VisibleUnits(state, viewer);
}
