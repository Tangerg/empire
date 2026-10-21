using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Resources;
using Empire.Core.State;
var input = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var engine = new GrayFlagEngine(input["packs"]!.AsArray());
void Check(string name, JsonNode? actual, JsonNode? expected) { if (!EcmaJson.DeepEquals(actual, expected)) throw new Exception($"{name}: {EcmaJson.Stringify(actual)} != {EcmaJson.Stringify(expected)}"); }
JsonNode? ExpectedError(JsonObject item)
{
    if (item["error"]?["kind"]?.GetValue<string>() != "IllegalAction") return item["error"];
    var name = StateJson.Text(item, "name");
    var command = item["action"]!["command"]!.AsObject();
    var code = name switch
    {
        "unit lacks attack before weapon validation" or "unit lacks healing" or "unit lacks capture" => "ability.not_owned",
        "attack rejects unoffered weapon" or "heal rejects unoffered weapon" or "capture rejects unoffered weapon" or "wait rejects unoffered weapon" => "ability.weapon_unavailable",
        "attack missing target with no eligible target" or "heal missing target without wounded allies" or "attack empty ammo" => "ability.target_required",
        "attack wrong target" or "attack target outside map" or "attack allied target" or "heal illegal target" or "heal healthy ally" => "ability.target_invalid",
        "attack empty weapon is unusable" or "attack cooling before target requirement" or
        "attack status blocked" or "attack cannot move then fire" or "capture noncapturable tile" or "capture status blocked" or "capture owned tile" => "ability.unusable",
        _ => throw new Exception("Unspecified semantic refusal expectation: " + name),
    };
    var details = new JsonObject { ["abilityId"] = command["ability"]!.DeepClone() };
    if (code == "ability.not_owned") details["unitType"] = item["initial"]!["units"]![0]!["type"]!.DeepClone();
    if (code == "ability.weapon_unavailable") details["weaponId"] = command["weapon"]!.DeepClone();
    return new JsonObject { ["kind"] = "IllegalAction", ["refusal"] = new JsonObject { ["code"] = code, ["details"] = details } };
}
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>()) {
    var name = StateJson.Text(item, "name"); var battle = engine.LoadSavedBattle(engine.SaveBattle(item["initial"]!.AsObject()));
    JsonObject? error = null; var events = new JsonArray();
    try { foreach (var ev in battle.Dispatch(item["action"]!.AsObject()).Events) events.Add(ev.DeepClone()); }
    catch (DomainInvariantException caught) { error = new JsonObject { ["kind"] = "DomainInvariant", ["message"] = caught.Message }; }
    catch (IllegalActionException caught) { error = new JsonObject { ["kind"] = "IllegalAction", ["refusal"] = JsonSerializer.SerializeToNode(caught.Refusal) }; }
    Check(name + " error", error, ExpectedError(item)); Check(name + " events", events, item["events"]); Check(name + " after", battle.Snapshot(), item["after"]); Check(name + " hash", JsonValue.Create(battle.StateHash), item["hash"]);
}
foreach (var item in input["resources"]!.AsArray().OfType<JsonObject>()) {
    var holder = item["holder"]!.DeepClone().AsObject(); var before = holder.DeepClone(); var kind = StateJson.Text(item, "kind"); var resource = StateJson.Text(item, "resource");
    var requested = item["requested"]!.GetValueKind() == System.Text.Json.JsonValueKind.String ? StateJson.Text(item, "requested") switch { "NaN" => double.NaN, "Infinity" => double.PositiveInfinity, _ => double.NegativeInfinity } : StateJson.Number(item["requested"], "requested");
    JsonObject? error = null; JsonNode? result = null;
    try { result = JsonValue.Create(engine.Resources.CanSpend(new BattleResources.Subject(kind, holder, kind == "weapon" ? "w" : null), resource, requested)); }
    catch (DomainInvariantException caught) { error = new JsonObject { ["kind"] = "DomainInvariant", ["message"] = caught.Message }; }
    Check(resource + " error", error, item["error"]); Check(resource + " result", result, item["result"]); Check(resource + " unchanged", holder, before);
}
foreach (var item in input["announcements"]!.AsArray().OfType<JsonObject>()) {
    var holder = item["holder"]!.DeepClone().AsObject(); var before = holder.DeepClone(); var kind = StateJson.Text(item, "kind"); var resource = StateJson.Text(item, "resource");
    JsonObject? error = null; var events = new JsonArray();
    try { engine.Resources.Announce(new BattleResources.Subject(kind, holder, kind == "weapon" ? "w" : null), resource, StateJson.Number(item["amount"], "amount"), ev => events.Add(ev)); }
    catch (DomainInvariantException caught) { error = new JsonObject { ["kind"] = "DomainInvariant", ["message"] = caught.Message }; }
    Check(resource + " announce error", error, item["error"]); Check(resource + " announce events", events, item["events"]); Check(resource + " announce unchanged", holder, before);
}
Console.WriteLine($"PASS {input["cases"]!.AsArray().Count} source action outcomes/refusal distinctions with exact rollback; {input["resources"]!.AsArray().Count} source resource queries including unlimited non-finite rejection; {input["announcements"]!.AsArray().Count} announcement account-validation cases");
