using System.Diagnostics;
using System.Text.Json.Nodes;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.State;

internal static class CatalogBenchmark
{
    internal static void Run(string root)
    {
        var packs = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
        var level = EcmaJson.Parse(File.ReadAllText(Path.Combine(root,
            "godot/data/grayflag-campaign-levels.json")))!.AsArray().OfType<JsonObject>()
            .Single(item => item["id"]!.GetValue<string>() == "c01-01");
        var catalog = new ReadOnlyContent(packs);
        var engine = BattleEngineFactory.Create(catalog);
        var state = engine.CreateState(level);
        var units = state["units"]!.AsArray().OfType<JsonObject>().ToArray();
        var actor = units.First(unit => unit["owner"]!.GetValue<int>() == state["currentPlayer"]!.GetValue<int>());
        var enemy = units.First(unit => unit["owner"]!.GetValue<int>() != actor["owner"]!.GetValue<int>());
        var battle = engine.CreateBattle(level);
        var recorder = engine.RecordBattle(battle.Snapshot());
        for (var step = 0; step < 12 && battle.Snapshot()["phase"]!.GetValue<string>() != "over"; step++)
        {
            var action = engine.Ai.ChooseAction(battle.Snapshot(), 0.58);
            battle.Dispatch(action);
            recorder.Record(action);
        }
        var replay = recorder.Replay(battle.Snapshot());

        Measure("compose-shared-catalog", 10, () => BattleEngineFactory.Create(catalog));
        Measure("create-engine-and-catalog", 10, () => BattleEngineFactory.Create(new ReadOnlyContent(packs)));
        Measure("move-field", 100, () => engine.Space.ComputeMoveField(state, actor));
        Measure("forecast", 100, () => engine.Combat.Forecasts.Forecast(state, actor, enemy));
        Measure("AI-choose", 5, () => engine.Ai.ChooseAction(state, 0.58));
        Measure("replay-12-actions", 5, () =>
        {
            var result = engine.ReplayBattle(level, replay);
            if (result.DivergedAt is not null) throw new InvalidOperationException("Benchmark replay diverged.");
        });
        Console.WriteLine($"initialHash={StateDigest.Hash(state)} finalHash={battle.StateHash} units={units.Length}");
    }

    private static void Measure(string name, int iterations, Action action)
    {
        for (var iteration = 0; iteration < 3; iteration++) action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++) action();
        watch.Stop();
        var bytesPerCall = (GC.GetAllocatedBytesForCurrentThread() - allocated) / iterations;
        Console.WriteLine($"{name}: iterations={iterations} totalMs={watch.Elapsed.TotalMilliseconds:F2} bytesPerCall={bytesPerCall}");
    }
}
