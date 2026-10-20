using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Engine;

internal static class CanonicalChecks
{
    internal static void Run(string oraclePath, string root)
    {
        var input = EcmaJson.Parse(File.ReadAllText(oraclePath))!.AsObject();
        var count = 0;
        foreach (var vector in input["vectors"]!.AsArray().OfType<JsonObject>())
        {
            var stream = EcmaJson.ReadString(vector["stream"]);
            var state = EcmaJson.Parse(EcmaJson.ReadString(vector["initial"]))!.DeepClone().AsObject();
            Require(EcmaJson.ReadString(state["payload"]![stream]) == stream, "String/property key changed during parse/clone.");
            var random = new RandomState(state["random"]!["seed"]!.GetValue<uint>());
            var actualDraws = new SplitMixRandom();
            foreach (var expected in vector["draws"]!.AsArray())
                Require(actualDraws.Next(random, stream) == expected!.GetValue<double>(), "Source RNG draw diverged.");
            state["random"]!["counters"]![stream] = random.Counters[stream];
            var saved = EcmaJson.Stringify(state);
            Require(saved == EcmaJson.ReadString(vector["after"]), "JSON.stringify or counter key parity differs.");
            var loaded = EcmaJson.Parse(saved)!.AsObject();
            Require(loaded["random"]!["counters"]![stream]!.GetValue<int>() == 8, "Saved counter key/value did not survive.");
            Require(EcmaJson.DeepEquals(state, loaded), "Saved state differs under code-unit equality.");
            Require(StateDigest.Hash(loaded) == EcmaJson.ReadString(vector["hash"]), "Source state digest differs.");
            Require(EcmaJson.DeepEquals(loaded, EcmaJson.Parse(EcmaJson.ToElement(loaded).GetRawText())), "Immutable element transfer changed state.");
            var literal = "\"" + string.Concat(stream.Select(unit => unit switch
            {
                '\\' => "\\\\", '"' => "\\\"", < ' ' => "\\u" + ((int)unit).ToString("x4"), _ => unit.ToString(),
            })) + "\"";
            Require(EcmaJson.ReadString(EcmaJson.Parse(literal)) == stream,
                "Direct UTF-16 JSON source text changed code units.");
            count++;
        }
        foreach (var vector in input["json"]!.AsArray().OfType<JsonObject>())
        {
            var node = EcmaJson.Parse(EcmaJson.ReadString(vector["input"]));
            Require(EcmaJson.Stringify(node) == EcmaJson.ReadString(vector["output"]), "Number/property ordering parity differs.");
            Require(StateDigest.Hash(node!) == EcmaJson.ReadString(vector["hash"]), "Number/property digest differs.");
            count++;
        }
        Require(!EcmaJson.DeepEquals(JsonValue.Create("\ud800"), JsonValue.Create("\ufffd")), "Lone high unit equals replacement character.");
        Require(!EcmaJson.DeepEquals(JsonValue.Create("\ud800"), JsonValue.Create("\udfff")), "Distinct lone units compare equal.");
        Require(!EcmaJson.DeepEquals(EcmaJson.Parse("{\"\\ud800\":1}"), EcmaJson.Parse("{\"\\udfff\":1}")), "Distinct lone property names compare equal.");
        Require(EcmaJson.DeepEquals(EcmaJson.Parse("{\"b\":1,\"a\":2}"), EcmaJson.Parse("{\"a\":2.0,\"b\":1e0}")), "Object order or numeric representation changed equality.");
        Require(EcmaJson.DeepEquals(JsonValue.Create(9007199254740993L), JsonValue.Create(9007199254740992d)), "Numbers were not compared as binary64.");
        Require(EcmaJson.ReadString(EcmaJson.Normalize(JsonNode.Parse("\"\\ud800\""))) == "\ud800", "Lazy scalar normalization lost raw escape.");
        try
        {
            EcmaJson.Normalize(JsonNode.Parse("{\"\\ud800\":1}"));
            throw new Exception("Inaccessible lazy property name was not rejected.");
        }
        catch (DomainInvariantException error) when (error.Message.Contains("EcmaJson.Parse", StringComparison.Ordinal)) { }
        foreach (var bad in new[] { "", "1 2", "{\"a\":}", "[1,]", "\"\\q\"", "{}{}", "\"\\" + '\ud800' + "\"", "\"\\" + '\udfff' + "\"" })
        {
            try { EcmaJson.Parse(bad); throw new Exception("Malformed JSON accepted: " + bad); }
            catch (JsonException) { }
        }
        var packs = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
        var level = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/grayflag-level.json")))!.AsObject();
        var engine = new GrayFlagEngine(packs);
        var original = engine.CreateState(level);
        foreach (var stream in new[] { "\ud800", "\udfff", "script.骰子💥", "\ud800X\udfff" })
        {
            original["random"]!["counters"]![stream] = 8;
            original["scenario"]!["variables"]![stream] = JsonValue.Create(stream);
        }
        var before = StateDigest.Hash(original);
        var save = engine.SaveBattle(original, "2026-10-20T20:20:00.000Z");
        var restored = engine.LoadBattle(EcmaJson.Parse(EcmaJson.Stringify(save)));
        Require(StateDigest.Hash(restored) == before && EcmaJson.DeepEquals(original, restored), "Real battle save/load did not preserve lone strings and counter keys.");
        Console.WriteLine($"PASS {count} live-source UTF-16/number vectors: parse, DeepClone, RNG writes, stringify, parse, digest; immutable snapshots, explicit lazy-key rejection and real battle save/load.");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
