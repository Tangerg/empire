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
    foreach (var item in oracle[fixture]!.AsObject())
        fixtureMetadata[target]![item.Key] = item.Value!.DeepClone();
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
int checks = 0;
string Text(JsonNode? value) => EcmaJson.ReadString(value);
var dataDirectory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
var portraits = PortraitCatalog.Parse(File.ReadAllText(Path.Combine(dataDirectory, "portrait-catalog.json")));
var nativeLevels = EcmaJson.Parse(File.ReadAllText(Path.Combine(dataDirectory, "grayflag-campaign-levels.json")))!.AsArray();
var levelById = nativeLevels.OfType<JsonObject>().ToDictionary(level => Text(level["id"]), StringComparer.Ordinal);
var engine = new BattleEngine(EcmaJson.Parse(File.ReadAllText(Path.Combine(dataDirectory, "content-packs.json")))!.AsArray());
var battleDocument = BattleTextDocument.Parse(new BattleTextDefinitions(nativeLevels, engine.Objectives, engine.ScenarioEffects),
    File.ReadAllText(Path.Combine(dataDirectory, "grayflag-battle-text.json")));
var story = EcmaJson.Parse(File.ReadAllText(Path.Combine(dataDirectory, "grayflag-story.json")))!;
var nameKeys = story["identityNameKeys"]!.AsObject().ToDictionary(item => item.Key, item => Text(item.Value), StringComparer.Ordinal);
CampaignStagingText Presentation(GameText text) => new(text,
    new BattleText(new(text, ObjectiveText.CreateDefault(text).Seal()), battleDocument), id => IdentityNames.Resolve(text, nameKeys, id));
var presentation = Presentation(messages);
JsonElement NativeLevel(JsonNode item) => EcmaJson.ToElement(levelById[Text(item["level"]!["id"])]);
void Check(string name, JsonNode? actual, JsonNode? expected)
{
    checks++;
    if (!EcmaJson.DeepEquals(actual, expected))
        throw new Exception($"Mismatch {name}\nactual {EcmaJson.Stringify(actual)}\nexpected {EcmaJson.Stringify(expected)}");
}
foreach (var item in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var before = EcmaJson.Stringify(item);
    var digest = StateDigest.Hash(item["state"]!);
    var view = CampaignStaging.Create(presentation, portraits, EcmaJson.ToElement(item["state"]), EcmaJson.ToElement(item["node"]), NativeLevel(item),
        EcmaJson.ToElement(fixtureMetadata), item["pending"]!.GetValue<bool>());
    var actual = new JsonObject
    {
        ["campaignTitle"] = view.CampaignTitle,
        ["chapterHeading"] = view.ChapterHeading,
        ["progress"] = view.Progress,
        ["eyebrow"] = view.Eyebrow,
        ["title"] = view.Title,
        ["description"] = view.Description,
        ["actionNote"] = view.ActionNote,
        ["startLabel"] = view.StartLabel,
        ["roster"] = JsonSerializer.SerializeToNode(view.Roster.Select(unit => new { unit.Name, unit.Status }), serialization),
        ["resources"] = JsonSerializer.SerializeToNode(view.Resources.Select(resource => new { resource.Label, resource.Amount }), serialization),
        ["relations"] = JsonSerializer.SerializeToNode(view.Relations.Select(relation => new { relation.Label, relation.Amount, relation.Fill }), serialization),
        ["chapterSource"] = view.ChapterArt.Source,
        ["briefingSource"] = view.BriefingArt?.Source,
    };
    var expected = item["expected"]!.DeepClone();
    // The native identity delta is limited to Torren and Elin; retain every other frozen assertion.
    for (var index = 0; index < view.Roster.Count; index++)
    {
        var unit = view.Roster[index];
        if (unit.Id is "torren" or "elin")
        {
            Check("historical class-only roster label", expected["roster"]![index]!["name"], oracle["unitNames"]![unit.UnitType]);
            expected["roster"]![index]!["name"] = unit.Id == "torren" ? "托伦" : "艾琳";
        }
    }
    Check(Text(item["name"]), actual, expected);
    Check("projection did not change digest", JsonValue.Create(StateDigest.Hash(item["state"]!)), JsonValue.Create(digest));
    Check("projection did not mutate state", JsonValue.Create(EcmaJson.Stringify(item)), JsonValue.Create(before));
    foreach (var unit in view.Roster)
    {
        var bindings = item["node"]!["rosterBindings"] as JsonArray;
        var binding = bindings?.OfType<JsonObject>().FirstOrDefault(value => Text(value["campaignUnit"]) == unit.Id);
        Check("explicit actor binding", JsonValue.Create(unit.ActorKey), binding?["levelUnitKey"]);
    }
    try
    {
        ((IList<CampaignStagingUnit>)view.Roster).Clear();
        throw new Exception("Roster remained mutable");
    }
    catch (NotSupportedException) { checks++; }
}
// A catalog change is observed by every binding without editing presentation metadata.
var renamedPortraits = new PortraitCatalog(new Dictionary<string, PortraitAsset>
{
    ["fixture-card"] = portraits.Portraits["laiya"]
});
var explicitBindings = fixtureMetadata.DeepClone();
explicitBindings["portraits"] = new JsonObject { ["laiya"] = "fixture-card" };
var explicitView = CampaignStaging.Create(presentation, renamedPortraits,
    EcmaJson.ToElement(oracle["cases"]![0]!["state"]), EcmaJson.ToElement(oracle["cases"]![0]!["node"]),
    NativeLevel(oracle["cases"]![0]!), EcmaJson.ToElement(explicitBindings), false);
Check("portrait IDs are explicit, not derived from identity names", JsonValue.Create(explicitView.Roster.Single(unit => unit.Id == "laiya").Portrait!.ResourcePath),
    JsonValue.Create(renamedPortraits.Resolve("fixture-card").ResourcePath));
try
{
    portraits.Resolve("missing-card");
    throw new Exception("Unknown portrait silently accepted");
}
catch (InvalidOperationException error) when (error.Message.Contains("Unknown portrait ID", StringComparison.Ordinal)) { checks++; }
var invalidCatalog = "{\"schema\":1,\"portraits\":{\"id\":{\"resourcePath\":\"res://assets/card.png\",\"sha256\":\"" + new string('a', 64) + "\",\"source\":\"competing-owner\"}}}";
try
{
    PortraitCatalog.Parse(invalidCatalog);
    throw new Exception("Competing source declaration accepted");
}
catch (InvalidOperationException error) when (error.Message.Contains("Unexpected portrait asset fields", StringComparison.Ordinal)) { checks++; }
var first = oracle["cases"]![0]!;
using (var stream = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(dataDirectory, "../tests/oracle/narrative.json.gz")), System.IO.Compression.CompressionMode.Decompress))
using (var reader = new StreamReader(stream))
{
    var narrativeOracle = EcmaJson.Parse(reader.ReadToEnd())!;
    foreach (var speaker in narrativeOracle["sourceDocuments"]!["speakers"]!.AsObject())
        Check("existing dialogue speaker label unchanged", JsonValue.Create(IdentityNames.Resolve(messages, nameKeys, speaker.Key)), speaker.Value);
}
var campaign = EcmaJson.Parse(File.ReadAllText(Path.Combine(dataDirectory, "grayflag-campaign.json")))!;
foreach (var unit in campaign["initial"]!["roster"]!.AsArray())
    Check("declared roster identity has a name", JsonValue.Create(string.IsNullOrWhiteSpace(IdentityNames.Resolve(messages, nameKeys, Text(unit!["id"])))), JsonValue.Create(false));
Check("Laiya keeps personal name", JsonValue.Create(IdentityNames.Resolve(messages, nameKeys, "laiya")), JsonValue.Create("莱娅"));
var changedState = first["state"]!.DeepClone();
var reordered = new JsonObject();
foreach (var unit in changedState["roster"]!.AsObject().Reverse())
    reordered.Add(unit.Key, unit.Value!.DeepClone());
changedState["roster"] = reordered;
reordered["elin"]!["unitType"] = "c01.swordsman";
reordered["torren"]!["disposition"] = "fallen";
var changedBefore = EcmaJson.Stringify(changedState);
var changedDigest = StateDigest.Hash(changedState);
var changedView = CampaignStaging.Create(presentation, portraits, EcmaJson.ToElement(changedState), EcmaJson.ToElement(first["node"]), NativeLevel(first), EcmaJson.ToElement(fixtureMetadata), false);
Check("reordered people sharing a class retain distinct names", JsonSerializer.SerializeToNode(changedView.Roster.Select(unit => new { unit.Id, unit.Name }), serialization),
    JsonNode.Parse("[{\"id\":\"elin\",\"name\":\"艾琳\"},{\"id\":\"torren\",\"name\":\"托伦\"},{\"id\":\"laiya\",\"name\":\"莱娅\"}]"));
Check("fallen member keeps status", JsonValue.Create(changedView.Roster.Single(unit => unit.Id == "torren").Status), JsonValue.Create(messages.Format("ui.staging.disposition.fallen")));
Check("reordered projection preserves state", JsonValue.Create(EcmaJson.Stringify(changedState)), JsonValue.Create(changedBefore));
Check("reordered projection preserves digest", JsonValue.Create(StateDigest.Hash(changedState)), JsonValue.Create(changedDigest));
void Reject<TException>(Action action, string diagnostic) where TException : InvalidOperationException
{
    try
    {
        action();
    }
    catch (TException error) when (error.Message.Contains(diagnostic, StringComparison.Ordinal)) { checks++; return; }
    throw new Exception($"Expected {typeof(TException).Name}: {diagnostic}");
}
var unknownState = first["state"]!.DeepClone();
unknownState["roster"]!["torren"]!["id"] = "undeclared-person";
Reject<InvalidOperationException>(() => CampaignStaging.Create(presentation, portraits, EcmaJson.ToElement(unknownState), EcmaJson.ToElement(first["node"]), NativeLevel(first), EcmaJson.ToElement(fixtureMetadata), false), "undeclared-person");
var missingName = new GameText(key => key == nameKeys["elin"] ? null : catalog.GetValueOrDefault(key), CultureInfo.InvariantCulture);
Reject<LocalizationException>(() => CampaignStaging.Create(Presentation(missingName), portraits, EcmaJson.ToElement(first["state"]), EcmaJson.ToElement(first["node"]), NativeLevel(first), EcmaJson.ToElement(fixtureMetadata), false), nameKeys["elin"]);
var contentText = EcmaJson.Parse(File.ReadAllText(Path.Combine(dataDirectory, "grayflag-content-text.json")))!;
foreach (var (id, expectedName) in new[] { ("c01.swordsman", "边境剑士"), ("c01.archer", "边境弓手") })
    Check("generic class name unchanged", JsonValue.Create(messages.Format(Text(contentText["content"]!["units"]![id]!["name"]))), JsonValue.Create(expectedName));
var alternate = new GameText(key => catalog.TryGetValue(key, out var value) ? value + " [fixture]" : null, CultureInfo.InvariantCulture);
var alternateView = CampaignStaging.Create(Presentation(alternate), portraits, EcmaJson.ToElement(first["state"]), EcmaJson.ToElement(first["node"]),
    NativeLevel(first), EcmaJson.ToElement(fixtureMetadata), false);
Check("identity names use injected locale", JsonValue.Create(alternateView.Roster.Single(unit => unit.Id == "torren").Name), JsonValue.Create("托伦 [fixture]"));
Check("staging injects shared campaign wording", JsonValue.Create(alternateView.CampaignTitle),
    JsonValue.Create(messages.Format(Text(exported["titleKey"])) + " [fixture]"));
Check("staging injects shell action wording", JsonValue.Create(alternateView.ExitLabel), JsonValue.Create("返回主菜单 [fixture]"));
Console.WriteLine($"PASS {checks} staging projection/identity comparisons across 45 frozen views; shared-class, reordered/fallen, missing-name, dialogue/class-label and state/digest guards.");
var node = first["node"]!.DeepClone().AsObject();
node["level"] = "wrong";
try
{
    CampaignStaging.Create(presentation, portraits, EcmaJson.ToElement(first["state"]), EcmaJson.ToElement(node), NativeLevel(first), EcmaJson.ToElement(fixtureMetadata), false);
    throw new Exception("Mismatched staging identity accepted");
}
catch (InvalidOperationException) { checks++; }
{
    var normalized = exported.DeepClone();
    void NormalizeArt(JsonNode? value)
    {
        if (value is not JsonObject obj)
            return;
        if (obj.ContainsKey("source") && obj.ContainsKey("resourcePath"))
        {
            obj["resourcePath"] = null;
            obj.Remove("sha256");
        }
        foreach (var entry in obj.ToArray())
            NormalizeArt(entry.Value);
    }
    NormalizeArt(normalized);
    Check("native shared document schema", exported["schema"], JsonValue.Create(3));
    Check("single campaign title owner", JsonValue.Create(messages.Format(Text(exported["titleKey"]))), oracle["metadata"]!["title"]);
    var expectedPortraits = oracle["metadata"]!["portraits"]!.AsObject();
    Check("native portrait binding identities", JsonSerializer.SerializeToNode(exported["portraits"]!.AsObject().Select(pair => pair.Key).Order()),
        JsonSerializer.SerializeToNode(expectedPortraits.Select(pair => pair.Key).Order()));
    foreach (var (identity, previous) in expectedPortraits)
    {
        var portraitId = Text(exported["portraits"]![identity]);
        Check("explicit shared portrait identity", JsonValue.Create(portraitId), JsonValue.Create(identity));
        var portrait = portraits.Resolve(portraitId);
        var historicalFilename = Path.GetFileName(Text(previous!["source"]));
        Check("native portrait keeps resource identity", JsonValue.Create(portrait.ResourcePath),
            JsonValue.Create("res://assets/runtime/narrative-static/" + historicalFilename));
        Check("native portrait source is derived", JsonValue.Create(portrait.Source),
            JsonValue.Create("/godot/assets/runtime/narrative-static/" + historicalFilename));
    }
    foreach (var field in new[] { "battleTotal", "joinAfter" })
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
    var art = portraits.Portraits.Keys.Select(id => JsonSerializer.SerializeToNode(portraits.Resolve(id), serialization)!.AsObject()).Concat(
        exported["levels"]!.AsObject().SelectMany(pair => new[] { pair.Value!["chapterArt"], pair.Value!["briefingArt"] }.OfType<JsonObject>()));
    var directory = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
    foreach (var item in art)
    {
        var path = Text(item["resourcePath"]);
        if (!path.StartsWith("res://", StringComparison.Ordinal))
            throw new Exception("Exported narrative art has no native resource path");
        var local = Path.GetFullPath(Path.Combine(directory, "..", path[6..]));
        var source = Path.GetFullPath(Path.Combine(directory, "../..", Text(item["source"]).TrimStart('/')));
        var targetHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(local))).ToLowerInvariant();
        var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
        Check("native narrative asset hash", JsonValue.Create(targetHash), item["sha256"]);
        Check("original narrative asset identity", JsonValue.Create(targetHash), JsonValue.Create(sourceHash));
    }
}
Console.WriteLine($"PASS {checks} comparisons across {oracle["cases"]!.AsArray().Count} frozen staging renders with native schema-3 metadata; immutable rows and explicit actor bindings");
