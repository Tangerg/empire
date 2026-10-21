using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;

namespace Empire.Core.State;

/// <summary>Owns reaction/facing admission and live transitions against the composed rules.</summary>
internal static class UnitDisposition
{
    internal static bool AdmitsFacing(ITacticalGrid grid, string facing) =>
        grid.Directions.Any(direction => direction.Id == facing);

    internal static void RequireFacing(ITacticalGrid grid, string facing)
    {
        if (!AdmitsFacing(grid, facing))
            throw new DomainInvariantException($"grid \"{grid.Id}\" has no direction \"{facing}\"");
    }

    internal static void React(ReactionRegistry reactions, JsonObject unit, string stance, Action<JsonObject> emit)
    {
        reactions.Get(stance);
        unit["reaction"] = stance;
        emit(new JsonObject
        {
            ["type"] = "reactionChanged",
            ["unit"] = unit["id"]!.DeepClone(),
            ["stance"] = stance,
        });
    }

    internal static void Face(ITacticalGrid grid, JsonObject unit, string facing, Action<JsonObject>? emit = null)
    {
        RequireFacing(grid, facing);
        var previous = StateJson.Text(unit, "facing");
        unit["facing"] = facing;
        if (previous == facing)
            return;
        emit?.Invoke(new JsonObject
        {
            ["type"] = "facingChanged",
            ["unit"] = unit["id"]!.DeepClone(),
            ["from"] = previous,
            ["to"] = facing,
        });
    }
}
