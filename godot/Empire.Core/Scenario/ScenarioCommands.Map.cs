using System.Text.Json.Nodes;
using Empire.Core.Lifecycle;
using Empire.Core.Tactics;
using Empire.Core.Terrain;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Scenario;

public sealed partial class ScenarioCommands
{
    public void AddOverlay(JsonObject state, JsonObject effect, Action<JsonObject> emit) => overlays.Add(state, new JsonObject
    {
        ["id"] = effect["id"]!.DeepClone(),
        ["type"] = effect["overlay"]!.DeepClone(),
        ["cells"] = new JsonArray(ScenarioSelection.Zone(state, Text(effect, "zone")).Select(cell => cell.DeepClone()).ToArray()),
        ["remainingRounds"] = effect["rounds"]?.DeepClone(),
    }, emit);

    public void ReplaceTerrain(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var layers = new MapLayers(Object(state["map"], "map"));
        var terrain = Text(effect, "terrain");
        if (content.TryGet("terrains", terrain) is null)
            throw new DomainInvariantException($"unknown terrain \"{terrain}\"");
        foreach (var cell in ScenarioSelection.Zone(state, Text(effect, "zone")))
        {
            var from = layers.ChangeTerrain(TacticalQueries.Position(cell), terrain);
            if (from is not null)
                emit(new JsonObject { ["type"] = "terrainChanged", ["at"] = cell.DeepClone(), ["from"] = from, ["to"] = terrain });
        }
    }
    public void SetElevation(JsonObject state, JsonObject effect, Action<JsonObject> emit) => ChangeElevation(state, effect, true, emit);
    public void AddElevation(JsonObject state, JsonObject effect, Action<JsonObject> emit) => ChangeElevation(state, effect, false, emit);
    private static void ChangeElevation(JsonObject state, JsonObject effect, bool setting, Action<JsonObject> emit)
    {
        var layers = new MapLayers(Object(state["map"], "map"));
        var value = Number(effect[setting ? "value" : "amount"], "elevation change");
        foreach (var cell in ScenarioSelection.Zone(state, Text(effect, "zone")))
        {
            var at = TacticalQueries.Position(cell);
            var step = setting ? layers.ChangeElevation(at, value) : layers.RaiseElevation(at, value);
            if (step is not null)
                emit(new JsonObject { ["type"] = "elevationChanged", ["at"] = cell.DeepClone(), ["from"] = step.From, ["to"] = step.To });
        }
    }
    public void SetCliffs(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var layers = new MapLayers(Object(state["map"], "map"));
        var blocked = IsTrue(effect["blocked"]);
        foreach (var edge in Objects(effect["edges"], "edges"))
        {
            var from = TacticalQueries.Position(Object(edge["from"], "edge from"));
            var to = TacticalQueries.Position(Object(edge["to"], "edge to"));
            if (!layers.IsEdge(from, to))
                throw new DomainInvariantException($"invalid cliff edge {from.X},{from.Y} -> {to.X},{to.Y}");
            if (layers.BlockEdge(from, to, blocked))
                emit(new JsonObject
                {
                    ["type"] = "cliffChanged",
                    ["from"] = edge["from"]!.DeepClone(),
                    ["to"] = edge["to"]!.DeepClone(),
                    ["blocked"] = blocked
                });
        }
    }
    public void SetDirectionalCover(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var layers = new MapLayers(Object(state["map"], "map"));
        foreach (var cover in Objects(effect["covers"], "covers"))
        {
            var at = TacticalQueries.Position(Object(cover["at"], "cover at"));
            if (!layers.Contains(at))
                throw new DomainInvariantException($"directional cover out of bounds: {at.X},{at.Y}");
            layers.ChangeCover(at, Object(cover["sides"], "cover sides"));
            emit(new JsonObject { ["type"] = "directionalCoverChanged", ["at"] = cover["at"]!.DeepClone(), ["sides"] = cover["sides"]!.DeepClone() });
        }
    }
}
