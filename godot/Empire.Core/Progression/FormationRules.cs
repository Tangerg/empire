using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Progression;

public sealed record FormationOption(JsonObject Formation, bool Current, bool Eligible, IReadOnlyList<string> Reasons);

/// <summary>The spatial invariant shared by formation menus, orders, movement and combat.</summary>
public sealed class FormationRules
{
    private readonly StateContent content;
    private readonly GridRegistry grids;
    public FormationRules(JsonArray packs, GridRegistry grids) { content = new StateContent(packs); this.grids = grids; }
    private ITacticalGrid Grid(JsonObject state) => grids.Get(Text(Object(state["rules"], "rules"), "grid"));
    public JsonObject? InEffect(JsonObject state, JsonObject unit, string? formation) => InEffect(content, Grid(state), state, unit, formation);
    public JsonObject? Active(JsonObject state, JsonObject unit) => InEffect(state, unit, unit["formation"]?.GetValue<string>());
    internal static JsonObject? InEffect(StateContent content, ITacticalGrid grid, JsonObject state, JsonObject unit, string? formation)
    {
        if (string.IsNullOrEmpty(formation)) return null;
        var definition = content.TryGet("formations", formation);
        if (definition is null) return null;
        var owner = Integer(unit, "owner"); var id = Integer(unit, "id"); var at = TacticalQueries.Position(unit);
        var adjacentAllies = Objects(state["units"], "units").Count(candidate => Integer(candidate, "id") != id &&
            TacticalQueries.AreAllies(state, Integer(candidate, "owner"), owner) && grid.Distance(TacticalQueries.Position(candidate), at) == 1);
        return adjacentAllies >= Number(definition["minimumAdjacentAllies"], "minimumAdjacentAllies") ? definition : null;
    }
    public IReadOnlyList<FormationOption> Options(JsonObject state, JsonObject unit)
    {
        var result = new List<FormationOption>();
        foreach (var node in content.Get("units", Text(unit, "type"))["formations"] as JsonArray ?? new JsonArray())
        {
            var id = Text(node, "formation");
            if (content.TryGet("formations", id) is not { } definition) continue;
            var eligible = InEffect(state, unit, id) is not null;
            result.Add(new FormationOption((JsonObject)definition.DeepClone(), unit["formation"]?.GetValue<string>() == id, eligible,
                eligible ? System.Array.Empty<string>() : new[] { $"需要 {Empire.Core.Determinism.EcmaNumber.Format(Number(definition["minimumAdjacentAllies"], "minimumAdjacentAllies"))} 名相邻友军" }));
        }
        return result;
    }
    public void ValidateChange(JsonObject state, JsonObject unit, string? formation)
    {
        if (formation is null) return;
        var allowed = content.Get("units", Text(unit, "type"))["formations"] as JsonArray ?? new JsonArray();
        if (!allowed.Any(value => Text(value, "formation") == formation))
            throw new IllegalActionException($"unit {Integer(unit, "id")} cannot use formation \"{formation}\"");
        if (InEffect(state, unit, formation) is null) throw new IllegalActionException($"formation \"{formation}\" lacks adjacent allied units");
    }
    public double MovementDelta(JsonObject state, JsonObject unit) => Active(state, unit) is { } active ? Number(active["movementDelta"], "movementDelta") : 0;
}
