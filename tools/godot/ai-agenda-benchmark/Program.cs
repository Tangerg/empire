using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Ai;
using Empire.Core.Determinism;
if (args.Length != 3) throw new ArgumentException("Usage: AiAgendaBenchmark <repository> <label> <round>");
var repo = args[0];
var variant = args[1];
var round = int.Parse(args[2]);
var fixtures = EcmaJson.Parse(File.ReadAllText(Path.Combine(repo, "godot/tests/native/ai-agenda-read.json")))!["cases"]!.AsArray();
var packs = EcmaJson.Parse(File.ReadAllText(repo + "/godot/data/content-packs.json"))!.AsArray();
var engine = new BattleEngine(packs);
var levels = EcmaJson.Parse(File.ReadAllText(repo + "/godot/data/grayflag-campaign-levels.json"))!.AsArray();
string Catalog() => EcmaJson.Sha256(JsonSerializer.SerializeToNode(engine.Catalog.Families.ToDictionary(f => f, f => engine.Catalog.Definitions(f))));
var catalog = Catalog();
var cases = fixtures.OfType<JsonObject>().ToArray();
if (round % 2 == 1) Array.Reverse(cases);
foreach (var fixture in cases)
{
    var name = fixture["name"]!.GetValue<string>();
    var level = levels.OfType<JsonObject>().Single(l => l["id"]!.GetValue<string>() == fixture["levelId"]!.GetValue<string>());
    var battle = engine.CreateBattle(level);
    foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
        battle.Dispatch(action);
    var state = battle.Snapshot();
    var hash = StateDigest.Hash(state);
    if (hash != fixture["stateHash"]!.GetValue<string>())
        throw new Exception("Formal witness no longer replays: " + name);
    var player = state["currentPlayer"]!.GetValue<int>();
    var playerRecord = state["players"]!.AsArray().OfType<JsonObject>().Single(p => p["id"]!.GetValue<int>() == player);
    if (playerRecord["controller"]!.GetValue<string>() != "ai")
        throw new Exception("Witness must be a real AI turn");
    var aggression = playerRecord["ai"]!["aggression"]!.GetValue<double>();
    var expected = EcmaJson.Stringify(fixture["action"]);
    foreach (var mode in new[] { "agenda", "decision" })
    {
        object Query() => mode == "agenda" ? new AiTurnContext(engine.Ai.Planning, state, player, aggression).Agenda : engine.Ai.ChooseAction(state);
        string Describe(object value) => value is JsonObject action ? EcmaJson.Stringify(action) : EcmaJson.Stringify(JsonSerializer.SerializeToNode(value));
        string? answer = null;
        for (int i = 0; i < 4; i++)
            answer = Describe(Query());
        if (mode == "decision" && answer != expected)
            throw new Exception("Chosen action changed: " + name);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var samples = new List<object>();
        for (int i = 0; i < 11; i++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            var value = Query();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (Describe(value) != answer || StateDigest.Hash(state) != hash)
                throw new Exception("Nondeterministic or mutating query: " + name);
            samples.Add(new
            {
                ms = elapsed,
                bytes
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            variant,
            round,
            name,
            mode,
            hash,
            tiles = state["map"]!["tiles"]!.AsArray().Count,
            answer,
            samples
        }));
    }
    if (Catalog() != catalog)
        throw new Exception("Query mutated canonical content");
}
