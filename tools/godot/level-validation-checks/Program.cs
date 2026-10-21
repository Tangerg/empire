using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Persistence;
using Empire.Core.Validation;
using Empire.Core.Abilities;
using Empire.Core.References;
using Empire.Core.State;

using var input = File.OpenRead(args[0]);
using var compressed = args[0].EndsWith(".gz", StringComparison.Ordinal) ? new GZipStream(input, CompressionMode.Decompress) : null;
var oracle = JsonNode.Parse((Stream?)compressed ?? input)!.AsObject();
var packs = oracle["packs"]!.AsArray();
var engine = new GrayFlagEngine(packs);
var referenceRules = new RuleReferenceRules
{
    Content = engine.Catalog, Grids = engine.Grids, Abilities = engine.Abilities, HitEffects = engine.Combat.HitEffects,
    Objectives = engine.Objectives, ScenarioConditions = engine.ScenarioConditions, ScenarioEffects = engine.ScenarioEffects,
    Reactions = engine.Reactions, TurnOrders = engine.TurnOrders, AreaShapes = engine.AreaShapes, Directives = engine.Directives,
};
var validator = new BattleLevelValidator(referenceRules, engine.ReferenceChecks, engine.Grids);
var passed = 0;
var rejectedBeforeState = 0;
var accepted = 0;
foreach (var node in oracle["cases"]!.AsArray())
{
    var test = node!.AsObject();
    var name = test["name"]!.GetValue<string>();
    var level = test["level"]!.AsObject();
    var before = level.ToJsonString();
    var expected = test["issues"]!.AsArray().Select(issue => issue!.GetValue<string>()).ToArray();
    var actual = validator.Issues(level);
    if (!expected.SequenceEqual(actual))
        throw new Exception($"{name}: level issues differ\nexpected: {string.Join("\n", expected)}\nactual: {string.Join("\n", actual)}");
    if (before != level.ToJsonString()) throw new Exception($"{name}: validator mutated input");
    if (expected.Length > 0)
    {
        try { engine.CreateState(level); throw new Exception($"{name}: engine accepted invalid level"); }
        catch (BattleLevelException error)
        {
            if (!expected.SequenceEqual(error.Issues) || error.LevelId != level["id"]?.GetValue<string>())
                throw new Exception($"{name}: typed level failure lost its identity or ordered issues");
            rejectedBeforeState++;
        }
        if (before != level.ToJsonString()) throw new Exception($"{name}: rejection mutated input");
    }
    else
    {
        engine.CreateState(level);
        if (before != level.ToJsonString()) throw new Exception($"{name}: state creation mutated input");
        accepted++;
    }
    passed++;
}
foreach (var node in oracle["failureCases"]!.AsArray())
{
    var test = node!.AsObject(); var level = test["level"]!.AsObject(); var before = level.ToJsonString();
    try { validator.Issues(level); throw new Exception($"{test["name"]}: invalid document was accepted"); }
    catch (DomainInvariantException error)
    {
        if (error.Message != test["message"]!.GetValue<string>()) throw new Exception($"{test["name"]}: wrong failure: {error.Message}");
    }
    if (before != level.ToJsonString()) throw new Exception($"{test["name"]}: invalid document was mutated");
    passed++;
}
var isolatedPacks = packs.DeepClone().AsArray();
var isolated = new BattleLevelValidator(referenceRules with { Content = new ReadOnlyContent(isolatedPacks) }, engine.ReferenceChecks, engine.Grids);
var sample = oracle["cases"]![0]!["level"]!.AsObject();
var originalIssues = isolated.Issues(sample);
var plain = isolatedPacks.SelectMany(pack => pack!["terrains"]?.AsArray() ?? new JsonArray())
    .First(terrain => terrain!["id"]!.GetValue<string>() == "plain")!.AsObject();
plain["cost"] = new JsonObject();
if (!originalIssues.SequenceEqual(isolated.Issues(sample))) throw new Exception("Caller pack mutation changed composed validator");
var lateAbilities = engine.Abilities.Clone();
var liveCapabilities = new BattleLevelValidator(referenceRules with { Abilities = lateAbilities }, engine.ReferenceChecks, engine.Grids);
var learned = sample.DeepClone().AsObject(); learned["units"]![0]!["learnedAbilities"] = new JsonArray("late-capability");
var beforeRegistration = liveCapabilities.Issues(learned);
if (!beforeRegistration.Any(issue => issue.Contains("late-capability", StringComparison.Ordinal)))
    throw new Exception("Unregistered learned ability was not rejected");
lateAbilities.Register(new AbilityDefinition("late-capability", "Late capability"));
if (!liveCapabilities.Issues(sample).SequenceEqual(liveCapabilities.Issues(learned)))
    throw new Exception("Registered ability owner was not consulted by the validator");
if (engine.Abilities.Has("late-capability")) throw new Exception("Mutable registry clone leaked into composed engine");
if (oracle["referenceCases"] is JsonArray referenceCases)
{
    var registries = new Dictionary<string, IRulePayloadRegistry>
    {
        ["effect"] = engine.ScenarioEffects, ["condition"] = engine.ScenarioConditions, ["objective"] = engine.Objectives,
    };
    var serialization = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    foreach (var pair in registries)
    {
        var expectedKinds = oracle["referenceKinds"]![pair.Key]!.AsArray().Select(kind => kind!.GetValue<string>());
        if (!expectedKinds.SequenceEqual(pair.Value.Keys())) throw new Exception($"{pair.Key}: registered reference owners differ");
    }
    foreach (var node in referenceCases)
    {
        var test = node!.AsObject(); var family = test["family"]!.GetValue<string>();
        var payload = test["payload"]!.AsObject(); var before = payload.ToJsonString();
        var registry = registries[family]; var kind = payload["type"]!.GetValue<string>();
        var actual = JsonSerializer.SerializeToNode(registry.References(payload), serialization);
        if (!JsonNode.DeepEquals(test["references"], actual))
            throw new Exception($"{family} {kind}: ordered references differ\nexpected: {test["references"]}\nactual: {actual}");
        if (test["children"] is { } children)
        {
            var actualChildren = registry.Has(kind) ? ((IRuleTreeRegistry)registry).Children(payload) : [];
            if (!JsonNode.DeepEquals(children, JsonSerializer.SerializeToNode(actualChildren, serialization)))
                throw new Exception($"{family} {kind}: ordered children differ");
        }
        if (before != payload.ToJsonString()) throw new Exception($"{family} {kind}: metadata lookup mutated payload");
    }
    var coveredEffects = referenceCases.Where(test => test!["family"]!.GetValue<string>() == "effect")
        .Select(test => test!["payload"]!["type"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
    if (engine.ScenarioEffects.Keys().Any(kind => !coveredEffects.Contains(kind)))
        throw new Exception("Not all resolved scenario effect reference owners have source vectors");
    Console.WriteLine($"PASS {referenceCases.Count} exact ordered metadata vectors covering all {engine.ScenarioEffects.Count} effect owners, including compound child order and repeated references.");
}
Console.WriteLine($"PASS {passed} ordered source differential level checks; {rejectedBeforeState} invalid definitions rejected before state creation; {accepted} valid definitions accepted.");
Console.WriteLine("PASS content snapshot isolation and live registered capability ownership.");
