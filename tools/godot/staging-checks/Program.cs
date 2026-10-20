using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Presentation;

if (args.Length is < 1 or > 2) throw new ArgumentException("Usage: StagingChecks <oracle.json> [exported-staging.json]");
var oracle = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
int checks = 0;
string Text(JsonNode? value) => EcmaJson.ReadString(value);
string Name(string id) => Text(oracle["unitNames"]![id]);
void Check(string name, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (!EcmaJson.DeepEquals(actual, expected)) throw new Exception($"Mismatch {name}\nactual {EcmaJson.Stringify(actual)}\nexpected {EcmaJson.Stringify(expected)}");
}
foreach (var item in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var before = EcmaJson.Stringify(item);
    var view = CampaignStaging.Create(EcmaJson.ToElement(item["state"]), EcmaJson.ToElement(item["node"]), EcmaJson.ToElement(item["level"]),
        EcmaJson.ToElement(oracle["metadata"]), Name, item["pending"]!.GetValue<bool>());
    var actual = new JsonObject
    {
        ["campaignTitle"] = view.CampaignTitle, ["chapterHeading"] = view.ChapterHeading, ["progress"] = view.Progress,
        ["eyebrow"] = view.Eyebrow, ["title"] = view.Title, ["description"] = view.Description, ["actionNote"] = view.ActionNote, ["startLabel"] = view.StartLabel,
        ["roster"] = JsonSerializer.SerializeToNode(view.Roster.Select(unit => new { unit.Name, unit.Status }), serialization),
        ["resources"] = JsonSerializer.SerializeToNode(view.Resources.Select(resource => new { resource.Label, resource.Amount }), serialization),
        ["relations"] = JsonSerializer.SerializeToNode(view.Relations.Select(relation => new { relation.Label, relation.Amount, relation.Fill }), serialization),
        ["chapterSource"] = view.ChapterArt.Source, ["briefingSource"] = view.BriefingArt?.Source,
    };
    Check(Text(item["name"]), actual, item["expected"]);
    Check("projection did not mutate state", JsonValue.Create(EcmaJson.Stringify(item)), JsonValue.Create(before));
    foreach (var unit in view.Roster)
    {
        var bindings = item["node"]!["rosterBindings"] as JsonArray;
        var binding = bindings?.OfType<JsonObject>().FirstOrDefault(value => Text(value["campaignUnit"]) == unit.Id);
        Check("explicit actor binding", JsonValue.Create(unit.ActorKey), binding?["levelUnitKey"]);
    }
    try { ((IList<CampaignStagingUnit>)view.Roster).Clear(); throw new Exception("Roster remained mutable"); }
    catch (NotSupportedException) { checks++; }
}
var first = oracle["cases"]![0]!;
var node = first["node"]!.DeepClone().AsObject(); node["level"] = "wrong";
try
{
    CampaignStaging.Create(EcmaJson.ToElement(first["state"]), EcmaJson.ToElement(node), EcmaJson.ToElement(first["level"]), EcmaJson.ToElement(oracle["metadata"]), Name, false);
    throw new Exception("Mismatched staging identity accepted");
}
catch (InvalidOperationException) { checks++; }
if (args.Length == 2)
{
    var exported = EcmaJson.Parse(File.ReadAllText(args[1]))!.AsObject();
    var normalized = exported.DeepClone();
    void NormalizeArt(JsonNode? value)
    {
        if (value is not JsonObject obj) return;
        if (obj.ContainsKey("source") && obj.ContainsKey("resourcePath")) { obj["resourcePath"] = null; obj.Remove("sha256"); }
        foreach (var entry in obj.ToArray()) NormalizeArt(entry.Value);
    }
    NormalizeArt(normalized);
    Check("live adapter export metadata", normalized, oracle["metadata"]);
    var art = exported["portraits"]!.AsObject().Select(pair => pair.Value!.AsObject()).Concat(
        exported["levels"]!.AsObject().SelectMany(pair => new[] { pair.Value!["chapterArt"], pair.Value!["briefingArt"] }.OfType<JsonObject>()));
    var directory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
    foreach (var item in art)
    {
        var path = Text(item["resourcePath"]);
        if (!path.StartsWith("res://", StringComparison.Ordinal)) throw new Exception("Exported narrative art has no native resource path");
        var local = Path.GetFullPath(Path.Combine(directory, "..", path[6..]));
        var source = Path.GetFullPath(Path.Combine(directory, "../..", Text(item["source"]).TrimStart('/')));
        var targetHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(local))).ToLowerInvariant();
        var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
        Check("native narrative asset hash", JsonValue.Create(targetHash), item["sha256"]);
        Check("original narrative asset identity", JsonValue.Create(targetHash), JsonValue.Create(sourceHash));
    }
}
Console.WriteLine($"PASS {checks} comparisons across {oracle["cases"]!.AsArray().Count} live-source staging renders; immutable rows and explicit actor bindings");
