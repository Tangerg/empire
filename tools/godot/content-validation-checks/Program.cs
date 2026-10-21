using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;

var fixture = Path.GetFullPath(args.Length > 0 ? args[0] : "/tmp/empire-content-validation.json");
var cases = JsonNode.Parse(File.ReadAllText(fixture))!.AsArray();
var passed = 0;
var failed = 0;
foreach (var entry in cases.OfType<JsonObject>())
{
    var name = entry["name"]!.GetValue<string>();
    var packs = entry["packs"]!.AsArray();
    var before = packs.ToJsonString();
    var expected = entry["error"]?.GetValue<string>();
    string? actual = null;
    try
    {
        _ = new BattleEngine(packs);
    }
    catch (DomainInvariantException error) { actual = error.Message; }
    if (packs.ToJsonString() != before)
        throw new Exception($"{name}: input mutated");
    var matches = actual == expected;
    if (!matches)
    {
        Console.Error.WriteLine($"FAIL {name}\nExpected: {expected ?? "<accepted>"}\nActual: {actual ?? "<accepted>"}");
        failed++;
    }
    else
        passed++;
}
if (failed > 0) throw new Exception($"{failed} content validation mismatches");
Console.WriteLine($"PASS {passed} exact ordered source content-admission checks; inputs unchanged");
