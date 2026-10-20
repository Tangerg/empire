using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;

internal static class ReplayChecks
{
    public static void Run(string root, string aggression)
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(root, $"grayflag-{aggression}.json")))!.AsObject();
        var packs = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "../../data/content-packs.json")))!.AsArray();
        var engine = new GrayFlagEngine(packs);
        var battle = engine.CreateBattle(fixture["level"]!.AsObject());
        RequireEqual(fixture["initialState"]!, battle.Snapshot(), "initial state");
        var index = 0;
        foreach (var step in fixture["steps"]!.AsArray())
        {
            try
            {
                var receipt = battle.Dispatch(step!["action"]!.AsObject());
                var events = new JsonArray(receipt.Events.Select(value => value.DeepClone()).ToArray());
                RequireEqual(step["events"]!, events, $"events at step {index}");
                var expected = step["stateHash"]!.GetValue<string>();
                if (battle.StateHash != expected)
                {
                    File.WriteAllText($"/tmp/empire-divergence-{aggression}-{index}.json", battle.Snapshot().ToJsonString());
                    throw new Exception($"state hash at step {index}: expected {expected}, actual {battle.StateHash}");
                }
            }
            catch (Exception error) { throw new Exception($"Replay {aggression}, action {index}: {step!["action"]}: {error.Message}", error); }
            index++;
        }
        RequireEqual(fixture["finalSave"]!["state"]!, battle.Snapshot(), "final state");
        Console.WriteLine($"  Verified {index} native actions and ordered event sequences, hash {battle.StateHash}");
    }

    private static void RequireEqual(JsonNode expected, JsonNode actual, string subject)
    {
        if (StateDigest.Hash(expected) == StateDigest.Hash(actual)) return;
        throw new Exception($"{subject} differs at {Difference(expected, actual, "$")}");
    }

    internal static string Difference(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is null || actual is null) return expected is null && actual is null ? "" : $"{path}: {expected} != {actual}";
        if (expected is JsonObject left && actual is JsonObject right)
        {
            foreach (var key in left.Select(pair => pair.Key).Union(right.Select(pair => pair.Key)).Order(StringComparer.Ordinal))
            {
                if (!left.ContainsKey(key) || !right.ContainsKey(key)) return $"{path}.{key}: missing field";
                var difference = Difference(left[key], right[key], $"{path}.{key}");
                if (difference.Length > 0) return difference;
            }
            return "";
        }
        if (expected is JsonArray a && actual is JsonArray b)
        {
            if (a.Count != b.Count) return $"{path}: array length {a.Count} != {b.Count}";
            for (var i = 0; i < a.Count; i++)
            {
                var difference = Difference(a[i], b[i], $"{path}[{i}]");
                if (difference.Length > 0) return difference;
            }
            return "";
        }
        return StateDigest.Hash(expected) == StateDigest.Hash(actual) ? "" : $"{path}: {expected} != {actual}";
    }
}
