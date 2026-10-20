using System.Text.Json.Nodes;

namespace Empire.Core.State;

internal static class StateObjectives
{
    internal static JsonArray AssignIds(JsonArray objectives, string prefix)
    {
        var result = new JsonArray();
        for (var index = 0; index < objectives.Count; index++)
        {
            var objective = StateJson.Object(objectives[index], "objective");
            var id = objective["id"] is { } authored ? StateJson.Text(authored, "objective id") : $"{prefix}.{index + 1}";
            result.Add(AssignChildren(objective, id));
        }
        return result;
    }

    internal static JsonObject RuntimeStates(JsonArray objectives)
    {
        var result = new JsonObject();
        void Visit(JsonObject objective)
        {
            var id = StateJson.Text(objective, "id");
            result[id] = new JsonObject
            {
                ["id"] = id,
                ["status"] = StateJson.IsFalse(objective["active"]) ? "inactive" : "active",
                ["hidden"] = objective["hidden"]?.DeepClone() ?? JsonValue.Create(false),
            };
            if (objective["objectives"] is JsonArray children)
            {
                foreach (var nested in children) Visit(StateJson.Object(nested, "child objective"));
            }
            else if (objective["objective"] is JsonObject child)
                Visit(child);
        }
        foreach (var objective in objectives) Visit(StateJson.Object(objective, "objective"));
        return result;
    }

    private static JsonObject AssignChildren(JsonObject objective, string id)
    {
        var copy = (JsonObject)objective.DeepClone();
        if (objective["objectives"] is JsonArray children)
            copy["objectives"] = AssignIds(children, id);
        else if (objective["objective"] is JsonObject child)
            copy["objective"] = AssignChildren(child,
                child["id"] is { } authored ? StateJson.Text(authored, "objective id") : $"{id}.1");
        copy["id"] = id;
        return copy;
    }
}
