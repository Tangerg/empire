using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;

var fixture = Path.GetFullPath(args.Length > 0 ? args[0] : "/tmp/empire-content-validation.json");
var cases = JsonNode.Parse(File.ReadAllText(fixture))!.AsArray();
var passed = 0; var guards = 0; var failed = 0;
foreach (var entry in cases.OfType<JsonObject>())
{
    var name = entry["name"]!.GetValue<string>(); var packs = entry["packs"]!.AsArray();
    var before = packs.ToJsonString();
    var expected = entry["error"]?.GetValue<string>(); var guard = entry["nativeGuard"]?.GetValue<string>();
    string? actual = null;
    try { _ = new GrayFlagEngine(packs); }
    catch (DomainInvariantException error) { actual = error.Message; }
    if (packs.ToJsonString() != before) throw new Exception($"{name}: input mutated");
    var matches = guard is null ? actual == expected : expected is null && actual?.Contains(guard, StringComparison.Ordinal) == true;
    if (!matches)
    {
        Console.Error.WriteLine($"FAIL {name}\nExpected: {expected ?? "<accepted>"}\nActual: {actual ?? "<accepted>"}\nNative guard: {guard ?? "none"}");
        failed++;
    }
    else if (guard is null) passed++; else guards++;
}
if (failed > 0) throw new Exception($"{failed} content validation mismatches");
Console.WriteLine($"PASS {passed} ordered source content-admission checks and {guards} explicit native capability guards; inputs unchanged");
