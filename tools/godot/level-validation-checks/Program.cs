using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Persistence;
using Empire.Core.Validation;

var oracle = JsonNode.Parse(File.ReadAllText(args[0]))!.AsObject();
var packs = oracle["packs"]!.AsArray();
var features = new BattleSaveFeatures();
foreach (var family in oracle["features"]!.AsObject())
    features.Register(family.Key, family.Value!.AsArray().Select(id => id!.GetValue<string>()));
var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid()).Seal();
var validator = new BattleLevelValidator(packs, features, grids);
var engine = new GrayFlagEngine(packs);
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
        catch (StoredDocumentException) { rejectedBeforeState++; }
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
var isolated = new BattleLevelValidator(isolatedPacks, features, grids);
var sample = oracle["cases"]![0]!["level"]!.AsObject();
var originalIssues = isolated.Issues(sample);
var plain = isolatedPacks.SelectMany(pack => pack!["terrains"]?.AsArray() ?? new JsonArray())
    .First(terrain => terrain!["id"]!.GetValue<string>() == "plain")!.AsObject();
plain["cost"] = new JsonObject();
if (!originalIssues.SequenceEqual(isolated.Issues(sample))) throw new Exception("Caller pack mutation changed composed validator");
var lateFeatures = new BattleSaveFeatures();
var featureSnapshot = new BattleLevelValidator(packs, lateFeatures, grids);
var learned = sample.DeepClone().AsObject(); learned["units"]![0]!["learnedAbilities"] = new JsonArray("late-capability");
var beforeRegistration = featureSnapshot.Issues(learned);
lateFeatures.Register("abilities", new[] { "late-capability" });
if (!beforeRegistration.SequenceEqual(featureSnapshot.Issues(learned))) throw new Exception("Late caller registration changed composed validator");
Console.WriteLine($"PASS {passed} ordered source differential level checks; {rejectedBeforeState} invalid definitions rejected before state creation; {accepted} valid definitions accepted.");
Console.WriteLine("PASS content and capability snapshot isolation.");
