using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Determinism;
var engine = new BattleEngine(JsonNode.Parse(File.ReadAllText("godot/data/content-packs.json"))!.AsArray());
var count = 0;
foreach (var item in JsonNode.Parse(File.ReadAllText(args[0]))!.AsArray().OfType<JsonObject>())
{
    var before = item["before"]!.AsObject();
    var battle = engine.LoadSavedBattle(engine.SaveBattle(before));
    var events = new JsonArray();
    ActionRefusal? refusal = null;
    try
    {
        foreach (var value in battle.Dispatch(item["action"]!.AsObject()).Events)
            events.Add(value.DeepClone());
    }
    catch (IllegalActionException error) { refusal = error.Refusal; }
    var expected = item["name"]!.GetValue<string>() switch
    {
        "off board" => new ActionRefusal("tactics.target_outside", new() { ["x"] = -1, ["y"] = 0 }),
        "wrong terrain" => new ActionRefusal("recruitment.terrain_incompatible", new() { ["terrainId"] = "plain", ["unitType"] = "soldier" }),
        "enemy owner" or "no owner" => new ActionRefusal("recruitment.not_owner"),
        "insufficient" => new ActionRefusal("recruitment.insufficient_resources"),
        "cap" => new ActionRefusal("recruitment.unit_limit", new() { ["limit"] = 1 }),
        "occupied" => new ActionRefusal("recruitment.occupied"),
        _ when item["error"] is null => null,
        _ => throw new Exception("Unspecified recruitment refusal case: " + item["name"]),
    };
    var at = item["action"]!["at"]!;
    var query = engine.Recruitment.Refusal(before, item["action"]!["unit"]!.GetValue<string>(),
        new(at["x"]!.GetValue<int>(), at["y"]!.GetValue<int>()));
    if (query?.Code != refusal?.Code || (query is not null && !EcmaJson.DeepEquals(
        EcmaJson.Parse(query.Details.GetRawText()), EcmaJson.Parse(refusal!.Details.GetRawText()))))
        throw new Exception($"{item["name"]}: query and command disagree on refusal facts");
    if ((item["error"] is null) != (expected is null) || refusal?.Code != expected?.Code ||
        (refusal is not null && !EcmaJson.DeepEquals(EcmaJson.Parse(refusal.Details.GetRawText()), EcmaJson.Parse(expected!.Details.GetRawText()))))
        throw new Exception($"{item["name"]}: semantic refusal differs");
    if (StateDigest.Hash(events) != StateDigest.Hash(item["events"]!))
        throw new Exception($"{item["name"]}: events differ");
    if (StateDigest.Hash(battle.Snapshot()) != StateDigest.Hash(item["after"]!))
        throw new Exception($"{item["name"]}: state differs");
    count++;
}
Console.WriteLine($"PASS {count} source recruitment action/event/state cases with semantic refusals");
