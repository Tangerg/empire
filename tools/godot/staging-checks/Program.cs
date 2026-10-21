using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Localization;
using System.Globalization;
using Empire.Presentation;
using Empire.Presentation.Feedback;
using Empire.Core.Engine;

if (args.Length != 3) throw new ArgumentException("Usage: StagingChecks <oracle.json> <native-staging.json> <catalog-check-projection.json>");
var oracle = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var exported = EcmaJson.Parse(File.ReadAllText(args[1]))!.AsObject();
var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[2]))!;
foreach (var item in oracle["testMessages"]!.AsObject()) catalog.Add(item.Key, item.Value!.GetValue<string>());
var messages = new GameText(key => catalog.GetValueOrDefault(key), CultureInfo.GetCultureInfo("zh-CN"));
var fixtureMetadata = (JsonObject)exported.DeepClone();
foreach (var (target, fixture) in new[] { ("resourceLabelKeys", "testResourceLabelKeys"), ("relationLabelKeys", "testRelationLabelKeys") })
    foreach (var item in oracle[fixture]!.AsObject()) fixtureMetadata[target]![item.Key] = item.Value!.DeepClone();
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
int checks = 0;
string Text(JsonNode? value) => EcmaJson.ReadString(value);
string Name(string id) => Text(oracle["unitNames"]![id]);
var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
var nativeLevels = EcmaJson.Parse(File.ReadAllText(Path.Combine(dataDirectory, "grayflag-campaign-levels.json")))!.AsArray();
var levelById = nativeLevels.OfType<JsonObject>().ToDictionary(level => Text(level["id"]), StringComparer.Ordinal);
var engine = new GrayFlagEngine(EcmaJson.Parse(File.ReadAllText(Path.Combine(dataDirectory, "content-packs.json")))!.AsArray());
var battleDocument = BattleTextDocument.Parse(new BattleTextDefinitions(nativeLevels, engine.Objectives, engine.ScenarioEffects),
    File.ReadAllText(Path.Combine(dataDirectory, "grayflag-battle-text.json")));
CampaignStagingText Presentation(GameText text) => new(text,
    new BattleText(text, battleDocument, ObjectiveText.CreateDefault(text).Seal()), Name);
var presentation = Presentation(messages);
JsonElement NativeLevel(JsonNode item) => EcmaJson.ToElement(levelById[Text(item["level"]!["id"])]);
void Check(string name, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (!EcmaJson.DeepEquals(actual, expected)) throw new Exception($"Mismatch {name}\nactual {EcmaJson.Stringify(actual)}\nexpected {EcmaJson.Stringify(expected)}");
}
foreach (var item in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var before = EcmaJson.Stringify(item);
    var view = CampaignStaging.Create(presentation, EcmaJson.ToElement(item["state"]), EcmaJson.ToElement(item["node"]), NativeLevel(item),
        EcmaJson.ToElement(fixtureMetadata), item["pending"]!.GetValue<bool>());
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
var alternate = new GameText(key => catalog.TryGetValue(key, out var value) ? value + " [fixture]" : null, CultureInfo.InvariantCulture);
var alternateView = CampaignStaging.Create(Presentation(alternate), EcmaJson.ToElement(first["state"]), EcmaJson.ToElement(first["node"]),
    NativeLevel(first), EcmaJson.ToElement(fixtureMetadata), false);
Check("staging injects shared campaign wording", JsonValue.Create(alternateView.CampaignTitle),
    JsonValue.Create(messages.Format(Text(exported["titleKey"])) + " [fixture]"));
Check("staging injects shell action wording", JsonValue.Create(alternateView.ExitLabel), JsonValue.Create("返回主菜单 [fixture]"));
var node = first["node"]!.DeepClone().AsObject(); node["level"] = "wrong";
try
{
    CampaignStaging.Create(presentation, EcmaJson.ToElement(first["state"]), EcmaJson.ToElement(node), NativeLevel(first), EcmaJson.ToElement(fixtureMetadata), false);
    throw new Exception("Mismatched staging identity accepted");
}
catch (InvalidOperationException) { checks++; }
{
    var normalized = exported.DeepClone();
    void NormalizeArt(JsonNode? value)
    {
        if (value is not JsonObject obj) return;
        if (obj.ContainsKey("source") && obj.ContainsKey("resourcePath")) { obj["resourcePath"] = null; obj.Remove("sha256"); }
        foreach (var entry in obj.ToArray()) NormalizeArt(entry.Value);
    }
    NormalizeArt(normalized);
    Check("native shared document schema", exported["schema"], JsonValue.Create(2));
    Check("single campaign title owner", JsonValue.Create(messages.Format(Text(exported["titleKey"]))), oracle["metadata"]!["title"]);
    foreach (var field in new[] { "battleTotal", "joinAfter", "portraits" })
        Check($"unchanged staging metadata {field}", normalized![field], oracle["metadata"]![field]);
    foreach (var kind in new[] { "resource", "relation" })
        foreach (var label in oracle["metadata"]![kind + "Labels"]!.AsObject())
            Check($"localized {kind} label {label.Key}", JsonValue.Create(messages.Format(Text(exported[kind + "LabelKeys"]![label.Key]))), label.Value);
    foreach (var level in exported["levels"]!.AsObject())
    {
        var previous = oracle["metadata"]!["levels"]![level.Key]!;
        Check("chapter title no longer duplicated per level", JsonValue.Create(level.Value!.AsObject().ContainsKey("chapterTitle")), JsonValue.Create(false));
        var chapter = exported["chapters"]![level.Value!["chapter"]!.GetValue<int>().ToString(CultureInfo.InvariantCulture)]!;
        Check("shared chapter owns staging label", JsonValue.Create(messages.Format(Text(chapter["titleKey"]))), previous["chapterTitle"]);
        foreach (var field in new[] { "chapter", "order", "briefingId", "chapterArt", "briefingArt" })
            Check($"unchanged level presentation {level.Key}.{field}", normalized!["levels"]![level.Key]![field], previous[field]);
    }
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
Console.WriteLine($"PASS {checks} comparisons across {oracle["cases"]!.AsArray().Count} frozen staging renders with native schema-2 metadata; immutable rows and explicit actor bindings");
