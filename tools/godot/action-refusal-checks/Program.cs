using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Resources;
using Empire.Core.State;
var input = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var engine = new GrayFlagEngine(input["packs"]!.AsArray());
void Check(string name, JsonNode? actual, JsonNode? expected) { if (!EcmaJson.DeepEquals(actual, expected)) throw new Exception($"{name}: {EcmaJson.Stringify(actual)} != {EcmaJson.Stringify(expected)}"); }
foreach (var item in input["cases"]!.AsArray().OfType<JsonObject>()) {
    var name = StateJson.Text(item, "name"); var battle = engine.LoadSavedBattle(engine.SaveBattle(item["initial"]!.AsObject()));
    JsonObject? error = null; var events = new JsonArray();
    try { foreach (var ev in battle.Dispatch(item["action"]!.AsObject()).Events) events.Add(ev.DeepClone()); }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException) { error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", ""), ["message"] = caught.Message }; }
    Check(name + " error", error, item["error"]); Check(name + " events", events, item["events"]); Check(name + " after", battle.Snapshot(), item["after"]); Check(name + " hash", JsonValue.Create(battle.StateHash), item["hash"]);
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
