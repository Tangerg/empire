using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Checks;
using Empire.Core.Campaign;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Presentation.Feedback;

if (args.Length != 3) throw new ArgumentException("Usage: FeedbackChecks <oracle.json> <repository-root> <catalog.json>");
var oracle = EcmaJson.Parse(File.ReadAllText(args[0]))!.AsObject();
var root = args[1];
var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
int checkedValues = 0;
void Check(string name, JsonNode? actual, JsonNode? expected)
{
    checkedValues++;
    if (!EcmaJson.DeepEquals(actual, expected))
        throw new Exception($"Mismatch {name}\nactual: {EcmaJson.Stringify(actual)}\nexpected: {EcmaJson.Stringify(expected)}");
}
string Text(JsonNode? value) => EcmaJson.ReadString(value);
JsonNode? Serialize<T>(T value) => JsonSerializer.SerializeToNode(value, serialization);
// Historical prose-bearing fixtures are not converted into current native state.
var packs = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
var engine = new Empire.Core.Engine.BattleEngine(packs);
var text = FixtureText.Load(args[2]);
var presentationText = FixtureBattleText.Session(root, engine, text);
var battleText = presentationText.Battle;
string UnitName(string id) => presentationText.Content.UnitName(id);
BattleLogNames Names(JsonNode state) => new(unit => presentationText.Content.UnitName(unit.GetProperty("type").GetString()!), presentationText.Content.UnitName, presentationText.Content.CareerName,
    presentationText.Content.ResourceName, PlayerNames(state));
Func<int, string> PlayerNames(JsonNode state) => player => battleText.PlayerName(Text(state["levelId"]), player);
var presenters = BattleEventText.CreateDefault(new(text, battleText));
Check("registered presenter keys", Serialize(presenters.Keys()), oracle["presenterKeys"]);
var priorCulture = CultureInfo.CurrentCulture;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
foreach (var item in oracle["formatCases"]!.AsArray().OfType<JsonObject>())
{
    var context = new BattleLogContext(Names(item["state"]!), EcmaJson.ToElement(item["state"]), EcmaJson.ToElement(item["state"]));
    var before = EcmaJson.Stringify(item);
    Check(Text(item["name"]), JsonValue.Create(presenters.Describe(context, EcmaJson.ToElement(item["event"]))), item["expected"]);
    if (EcmaJson.Stringify(item) != before)
        throw new Exception("Formatter mutated its input");
}
CultureInfo.CurrentCulture = priorCulture;
foreach (var item in oracle["logCases"]!.AsArray().OfType<JsonObject>())
{
    var log = new BattleLog(item["depth"]!.GetValue<int>());
    var checkpoints = item["checkpoints"]!.AsArray();
    for (var index = 0; index < item["lines"]!.AsArray().Count; index++)
    {
        if (item["clearAt"]?.GetValue<int>() == index)
            log.Clear();
        log.Add(Text(item["lines"]![index]));
        var actual = log.Recent();
        Check($"{Text(item["name"])} step {index}", Serialize(actual), checkpoints[index]);
        if (actual.Length > 0)
            actual[0] = "reader changed snapshot";
        Check($"{Text(item["name"])} detached {index}", Serialize(log.Recent()), checkpoints[index]);
    }
    log.Clear();
    Check("clear log", Serialize(log.Recent()), new JsonArray());
}
var strikeEvents = oracle["strikes"]!["events"]!.AsArray().OfType<JsonObject>().ToArray();
Check("canonical strike payload shape", JsonValue.Create(BattleEventFacts.StrikeCount(strikeEvents)), oracle["strikes"]!["expected"]);
var level = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/tests/oracle/source-v1/grayflag-level.json")))!.AsObject();
foreach (var pair in oracle["unitNames"]!.AsObject()) Check("canonical unit label " + pair.Key, JsonValue.Create(presentationText.Content.UnitName(pair.Key)), pair.Value);
foreach (var pair in oracle["careerNames"]!.AsObject()) Check("canonical career label " + pair.Key, JsonValue.Create(presentationText.Content.CareerName(pair.Key)), pair.Value);
foreach (var pair in oracle["resourceNames"]!.AsObject()) Check("canonical resource label " + pair.Key, JsonValue.Create(presentationText.Content.ResourceName(pair.Key)), pair.Value);
var missingResourceRejected = false;
try
{
    presentationText.Content.ResourceName("plugin.custom");
}
catch (Empire.Localization.LocalizationException) { missingResourceRejected = true; }
Check("unknown resource presentation rejects missing metadata", JsonValue.Create(missingResourceRejected), JsonValue.Create(true));
var bridge = new CampaignBattleBridge(new CampaignBattleBridgeRules(_ => level, new Empire.Core.State.ReadOnlyContent(packs), Empire.Core.Engine.LevelNormalizer.Normalize, GrayFlagCampaignPolicy.MarkerDispositions));
foreach (var item in oracle["summaries"]!.AsArray().OfType<JsonObject>())
{
    var state = item["state"]!.AsObject();
    var events = item["events"]!.AsArray().OfType<JsonObject>().ToArray();
    var request = new JsonObject { ["id"] = "feedback-test", ["perspectivePlayer"] = item["perspectivePlayer"]!.DeepClone(), ["rosterBindings"] = new JsonArray() };
    var result = bridge.Result(request, state, events);
    Check($"{Text(item["name"])} canonical signals", result["signals"], item["signals"]);
    var count = BattleEventFacts.StrikeCount(events);
    Check($"{Text(item["name"])} canonical strikes", JsonValue.Create(count), item["strikeCount"]);
    var signals = result["signals"]!.AsArray().Select(Text).ToArray();
    var before = EcmaJson.Stringify(item);
    // Historical expected signal IDs/title fields remain source evidence, not a native localized-summary oracle.
    var summary = CampaignBattleSummary.Create(new CampaignSummaryText(text, battleText, unit => UnitName(unit.GetProperty("type").GetString()!)), item["chapter"]!.GetValue<int>(), item["perspectivePlayer"]!.GetValue<int>(),
        EcmaJson.ToElement(state), signals, count);
    var actual = new JsonObject
    {
        ["title"] = summary.Title,
        ["chapter"] = summary.Chapter,
        ["outcome"] = summary.Outcome,
        ["turns"] = summary.Turns,
        ["alliesRemaining"] = summary.AlliesRemaining,
        ["enemiesRemaining"] = summary.EnemiesRemaining,
        ["fallen"] = Serialize(summary.Fallen),
        ["signals"] = Serialize(summary.Signals),
        ["strikeCount"] = summary.StrikeCount,
        ["metrics"] = Serialize(summary.Metrics),
        ["heading"] = summary.Heading,
        ["fallenLabel"] = summary.FallenLabel,
        ["signalsLabel"] = summary.SignalsLabel,
        ["fallenText"] = summary.FallenText,
        ["signalsText"] = summary.SignalsText,
        ["continueLabel"] = summary.ContinueLabel,
    };
    Check(Text(item["name"]), actual, item["expected"]);
    if (EcmaJson.Stringify(item) != before)
        throw new Exception("Summary mutated canonical state or events");
    if (signals.Length > 0)
    {
        signals[0] = "modified input";
        Check("summary detached signals", Serialize(summary.Signals), item["signals"]);
    }
}
var fixture = oracle["formatCases"]![0]!;
var customContext = new BattleLogContext(Names(fixture["state"]!), EcmaJson.ToElement(fixture["state"]), EcmaJson.ToElement(fixture["state"]));
var cloned = presenters.Clone().Register("plugin.custom", (_, _) => "custom line").Replace("attack", (_, _) => "replaced").Seal();
Check("registry custom", JsonValue.Create(cloned.Describe(customContext, EcmaJson.ToElement(new JsonObject { ["type"] = "plugin.custom" }))), JsonValue.Create("custom line"));
Check("registry replacement", JsonValue.Create(cloned.Describe(customContext, EcmaJson.ToElement(fixture["event"]))), JsonValue.Create("replaced"));
Check("registry independent clone", JsonValue.Create(presenters.Describe(customContext, EcmaJson.ToElement(fixture["event"]))), fixture["expected"]);
try
{
    presenters.Register("illegal", (_, _) => "");
    throw new Exception("Sealed registry accepted mutation");
}
catch (InvalidOperationException) { }
try
{
    presenters.Clone().Register("attack", (_, _) => "");
    throw new Exception("Duplicate presenter accepted");
}
catch (InvalidOperationException) { }
using (var document = JsonDocument.Parse(EcmaJson.Stringify(fixture["state"])))
{
    customContext = new BattleLogContext(Names(fixture["state"]!), document.RootElement, document.RootElement);
}
Check("context survives input document disposal", JsonValue.Create(presenters.Describe(customContext, EcmaJson.ToElement(fixture["event"]))), fixture["expected"]);
Console.WriteLine($"PASS {checkedValues} source comparisons: {oracle["formatCases"]!.AsArray().Count} event descriptions, {oracle["logCases"]!.AsArray().Count} log sequences, {oracle["summaries"]!.AsArray().Count} summaries, canonical strike/signal facts; {oracle["replaySteps"]} real replay actions");
