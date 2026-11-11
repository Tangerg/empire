using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;

if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] is not ("--calculate" or "--negative-control"))
    throw new ArgumentException("Usage: C14TargetMetadataChecks ROOT [--calculate|--negative-control]");
var root = Path.GetFullPath(args[0]);
var calculate = args.Length == 2 && args[1] == "--calculate";
var negative = args.Length == 2 && args[1] == "--negative-control";
JsonNode Read(string path) => EcmaJson.Parse(File.ReadAllText(Path.Combine(root, path)))!;
// Fixed witness anchors from base 088c0bf4; never regenerate from the file under verification.
var witnesses = new[]
{
    (File: "recorded-status-feedback.json", Count: 130, ActionsHash: "dddf8206098fa3fbb4e952aeb77301dc5b2e2762b0c901097d1d51063a7366c6", Anchors: new Dictionary<int, (string Before, string After, string Events)>
        { [56] = ("dd871f64", "7c78b141", "984c94ebccc269dc6fa113626eed5960b8e8dc73f163fb115080bae7c87d313a"), [129] = ("cb85a026", "2eeda816", "fc7d54011479b06e7f569c237015fe7aa12e08a6d59f6a3a21512dc60176e94c") }),
    (File: "recorded-displacement.json", Count: 59, ActionsHash: "499b6dafe624d4fabc8cf81efcec4377b55a46110c7811468246a02fd6f9967c", Anchors: new Dictionary<int, (string Before, string After, string Events)>
        { [58] = ("1fc73c28", "60fee9f7", "c20e94a13e3089397908d7760d11259d2652097cb115055a0cbc91bb0ce84833") })
};
foreach (var witness in witnesses)
{
    try
    {
        Verify(witness.File, witness.Count, witness.ActionsHash, witness.Anchors);
        if (negative)
            throw new InvalidOperationException("Negative control was not rejected.");
    }
    catch (InvalidOperationException error) when (negative && error.Message == "Nonmetadata canonical state divergence at initial.")
    {
        Console.WriteLine($"NEGATIVE CONTROL REJECTED {witness.File}: turnLimit rule change: {error.Message}");
    }
}
void Verify(string file, int expectedCount, string expectedActionsHash, Dictionary<int, (string Before, string After, string Events)> oldCheckpoints)
{
    Console.WriteLine($"WITNESS {file}");
    var fixtures = Read("godot/tests/native/" + file).AsArray();
    var fixture = fixtures.OfType<JsonObject>().Single(value => value["levelId"]!.GetValue<string>() == "c01-14");
    var level = Read("godot/data/grayflag-campaign-levels.json").AsArray().OfType<JsonObject>()
        .Single(value => value["id"]!.GetValue<string>() == "c01-14");
    void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
    void StripMetadata(JsonObject document)
    {
        var score = document["players"]!.AsArray().OfType<JsonObject>().Single(player => player["id"]!.GetValue<int>() == 1)
            ["objectives"]![0]!["objective"]!["objectives"]![0]!.AsObject();
        Require(score["id"]!.GetValue<string>() == "score-valves"
            && score["targetZones"] is JsonArray zones
            && zones.Select(value => value!.GetValue<string>()).SequenceEqual(new[] { "valve-a", "valve-b" }),
            "Only the confirmed score-valves targetZones declaration may be removed.");
        score.Remove("targetZones");
    }
    var oldLevel = level.DeepClone().AsObject();
    StripMetadata(oldLevel);
    var newLevel = level.DeepClone().AsObject();
    if (negative)
        newLevel["rules"]!["turnLimit"] = newLevel["rules"]!["turnLimit"]!.GetValue<int>() + 1;
    var engine = new BattleEngine(Read("godot/data/content-packs.json").AsArray());
    var oldBattle = engine.CreateBattle(oldLevel);
    var newBattle = engine.CreateBattle(newLevel);
    void EqualState(string stage)
    {
        var current = newBattle.Snapshot();
        StripMetadata(current);
        Require(EcmaJson.Stringify(current) == EcmaJson.Stringify(oldBattle.Snapshot()),
            $"Nonmetadata canonical state divergence at {stage}.");
    }
    var checkpoints = fixture["checkpoints"]!.AsArray().OfType<JsonObject>().ToDictionary(value => value["index"]!.GetValue<int>());
    Require(checkpoints.Keys.Order().SequenceEqual(oldCheckpoints.Keys.Order()), "Original checkpoint identities must remain intact.");
    var actions = fixture["actions"]!.AsArray().OfType<JsonObject>().ToArray();
    Require(actions.Length == expectedCount, "Each witness must replay its own complete original action sequence.");
    EqualState("initial");
    Require(oldBattle.StateHash == "10b86025", "Legacy initial hash mismatch.");
    if (!calculate)
        Require(newBattle.StateHash == fixture["initialStateHash"]!.GetValue<string>(), "Current initial fixture hash mismatch.");
    Console.WriteLine($"initialStateHash: legacy={oldBattle.StateHash}, current={newBattle.StateHash}");
    Require(EcmaJson.Sha256(fixture["actions"]) == expectedActionsHash, "Original baseline action sequence SHA changed.");
    Console.WriteLine($"actionsSha256={expectedActionsHash}");
    for (var index = 0; index < actions.Length; index++)
    {
        if (checkpoints.TryGetValue(index, out var checkpoint))
        {
            Require(oldBattle.StateHash == oldCheckpoints[index].Before, $"Legacy before hash mismatch at {index}.");
            if (!calculate)
                Require(newBattle.StateHash == checkpoint["beforeStateHash"]!.GetValue<string>(), $"Current before fixture hash mismatch at {index}.");
            Console.WriteLine($"checkpoint {index} beforeStateHash: legacy={oldBattle.StateHash}, current={newBattle.StateHash}");
        }
        // Dispatch validates the exact recorded order independently on each canonical battle.
        var oldReceipt = oldBattle.Dispatch(actions[index]);
        var newReceipt = newBattle.Dispatch(actions[index]);
        Require(oldReceipt.Events.Select(EcmaJson.Stringify).SequenceEqual(newReceipt.Events.Select(EcmaJson.Stringify)),
            $"Event divergence at action {index}.");
        EqualState($"action {index}");
        if (checkpoint is not null)
        {
            Require(oldBattle.StateHash == oldCheckpoints[index].After, $"Legacy after hash mismatch at {index}.");
            var events = new JsonArray(newReceipt.Events.Select(value => (JsonNode)value.DeepClone()).ToArray());
            var eventsHash = EcmaJson.Sha256(events);
            Require(eventsHash == oldCheckpoints[index].Events && eventsHash == checkpoint["eventsSha256"]!.GetValue<string>(), $"Original eventsSha256 mismatch at {index}.");
            if (!calculate)
                Require(newBattle.StateHash == checkpoint["afterStateHash"]!.GetValue<string>(), $"Current after fixture hash mismatch at {index}.");
            Console.WriteLine($"checkpoint {index} afterStateHash: legacy={oldBattle.StateHash}, current={newBattle.StateHash}; eventsSha256={eventsHash}");
        }
    }
    Console.WriteLine($"PASS {file}: {expectedCount} independently accepted actions; every complete event sequence and canonical state identical after removing only targetZones; all legacy checkpoint hashes and event hashes verified.");
}
