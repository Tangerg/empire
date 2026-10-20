using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Terrain;

/// <summary>Owns overlay insertion/removal and its round clock, shared by scenarios and lifecycle.</summary>
public sealed class BattleOverlays
{
    private readonly StateContent content;
    public BattleOverlays(JsonArray packs) => content = new StateContent(packs);

    public void Add(JsonObject state, JsonObject overlay, Action<JsonObject> emit)
    {
        var overlays = Array(Object(state["scenario"], "scenario")["overlays"], "overlays");
        var id = Text(overlay, "id");
        if (Objects(overlays, "overlays").Any(candidate => Text(candidate, "id") == id))
            throw new DomainInvariantException($"duplicate terrain overlay id \"{id}\"");
        content.Get("terrainOverlays", Text(overlay, "type"));
        overlays.Add(overlay.DeepClone());
        emit(new JsonObject { ["type"] = "overlayAdded", ["overlay"] = id, ["overlayType"] = overlay["type"]!.DeepClone(),
            ["cells"] = overlay["cells"]!.DeepClone() });
    }

    public static bool Remove(JsonObject state, string id, Action<JsonObject> emit)
    {
        var overlays = Array(Object(state["scenario"], "scenario")["overlays"], "overlays");
        var found = Objects(overlays, "overlays").FirstOrDefault(overlay => Text(overlay, "id") == id);
        if (found is null) return false;
        overlays.Remove(found);
        emit(new JsonObject { ["type"] = "overlayRemoved", ["overlay"] = id });
        return true;
    }

    public static void AdvanceRound(JsonObject state, Action<JsonObject> emit)
    {
        foreach (var overlay in Objects(Object(state["scenario"], "scenario")["overlays"], "overlays").ToArray())
        {
            if (overlay["remainingRounds"] is null) continue;
            var remaining = Number(overlay["remainingRounds"], "remainingRounds") - 1;
            overlay["remainingRounds"] = remaining;
            if (remaining <= 0) Remove(state, Text(overlay, "id"), emit);
        }
    }
}
