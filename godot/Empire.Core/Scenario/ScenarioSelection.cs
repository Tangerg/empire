using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Scenario;

public static class ScenarioSelection
{
    public static IReadOnlyList<JsonObject> SelectUnits(ReadOnlyContent catalog, JsonObject state, JsonObject selector) => Units(state).Where(unit =>
        (selector["ids"] is not JsonArray ids || ids.Any(id => Integer(id, "unit id") == Integer(unit, "id"))) &&
        (selector["keys"] is not JsonArray keys || unit["key"] is { } key && keys.Any(candidate => Text(candidate, "unit key") == Text(key, "unit key"))) &&
        (selector["owner"] is null || Integer(selector, "owner") == Integer(unit, "owner")) &&
        (selector["zone"] is null || Zone(state, Text(selector, "zone")).Any(cell => SameCell(unit, cell))) &&
        (selector["anyTags"] is not JsonArray tags || tags.Count == 0 ||
            Array(catalog.Source.Get("units", Text(unit, "type"))["tags"], "unit tags").Any(tag => tags.Any(candidate => Empire.Core.Determinism.EcmaJson.DeepEquals(candidate, tag)))))
        .ToArray();

    public static IReadOnlyList<JsonObject> SelectMarkers(JsonObject state, JsonObject selector) =>
        Objects(state["markers"], "markers").Where(marker =>
            (selector["ids"] is not JsonArray ids || ids.Any(id => Empire.Core.Determinism.EcmaJson.DeepEquals(id, marker["id"]))) &&
            (selector["kind"] is null || Text(selector, "kind") == Text(marker, "kind")) &&
            (selector["owner"] is null || Empire.Core.Determinism.EcmaJson.DeepEquals(selector["owner"], marker["owner"])) &&
            (selector["zone"] is null || Zone(state, Text(selector, "zone")).Any(cell => SameCell(Object(marker["at"], "marker position"), cell))))
        .ToArray();

    internal static bool Compare(JsonNode? left, string operation, JsonNode? right) => operation switch
    {
        "eq" => Empire.Core.Determinism.EcmaJson.DeepEquals(left, right),
        "neq" => !Empire.Core.Determinism.EcmaJson.DeepEquals(left, right),
        "gte" => left?.GetValueKind() == JsonValueKind.Number && right?.GetValueKind() == JsonValueKind.Number && Number(left, "left") >= Number(right, "right"),
        "lte" => left?.GetValueKind() == JsonValueKind.Number && right?.GetValueKind() == JsonValueKind.Number && Number(left, "left") <= Number(right, "right"),
        _ => throw new DomainInvariantException($"unknown scenario comparison \"{operation}\""),
    };
    internal static IEnumerable<JsonObject> Zone(JsonObject state, string id) => Objects(
        Object(Object(state["scenario"], "scenario")["zones"], "zones")[id] ?? throw new DomainInvariantException($"unknown scenario zone \"{id}\""), "zone");
    internal static bool SameCell(JsonObject left, JsonObject right) => Integer(left, "x") == Integer(right, "x") && Integer(left, "y") == Integer(right, "y");
}
