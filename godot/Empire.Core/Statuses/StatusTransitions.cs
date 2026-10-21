using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Statuses;

public static class StatusTransitions
{
    internal static void RequireDuration(double duration)
    {
        if (!double.IsFinite(duration) || duration < 1 || duration != Math.Truncate(duration))
            throw new DomainInvariantException("status duration must be >= 1");
    }

    public static void Add(ReadOnlyContent content, JsonObject unit, string id, double duration, int? sourceUnitId, Action<JsonObject>? emit)
    {
        RequireDuration(duration);
        var definition = content.Source.Get("statuses", id);
        var statuses = unit["statuses"]!.AsArray();
        var carried = statuses.OfType<JsonObject>().FirstOrDefault(value => StateJson.Text(value, "id") == id);
        if (carried is null)
        {
            carried = new JsonObject { ["id"] = id, ["remaining"] = duration, ["stacks"] = 1 };
            if (sourceUnitId is not null)
                carried["sourceUnitId"] = sourceUnitId.Value;
            statuses.Add(carried);
        }
        else
        {
            var remaining = StateJson.Number(carried, "remaining", 0);
            switch (StateJson.Text(definition, "stackMode"))
            {
                case "refresh":
                    carried["remaining"] = Math.Max(remaining, duration);
                    break;
                case "extend":
                    carried["remaining"] = remaining + duration;
                    break;
                case "stack":
                    carried["stacks"] = Math.Min(StateJson.Number(definition, "maxStacks", 1),
                        StateJson.Number(carried, "stacks", 1) + 1);
                    carried["remaining"] = Math.Max(remaining, duration);
                    break;
                default:
                    throw new DomainInvariantException($"unknown status stack mode for \"{id}\"");
            }
            if (sourceUnitId is not null)
                carried["sourceUnitId"] = sourceUnitId.Value;
        }
        emit?.Invoke(new JsonObject
        {
            ["type"] = "statusApplied",
            ["unit"] = unit["id"]!.DeepClone(),
            ["status"] = id,
            ["remaining"] = carried["remaining"]!.DeepClone(),
            ["stacks"] = carried["stacks"]!.DeepClone(),
        });
    }

    public static bool Remove(JsonObject unit, string id, Action<JsonObject>? emit)
    {
        var statuses = unit["statuses"]!.AsArray();
        var instance = statuses.OfType<JsonObject>().FirstOrDefault(value => StateJson.Text(value, "id") == id);
        if (instance is null)
            return false;
        statuses.Remove(instance);
        emit?.Invoke(new JsonObject { ["type"] = "statusRemoved", ["unit"] = unit["id"]!.DeepClone(), ["status"] = id });
        return true;
    }

}
