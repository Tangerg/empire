using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Determinism;

/// <summary>Projects battle-owned stream data onto the canonical numeric generator.</summary>
public sealed class DefaultBattleRandomSource : IBattleRandomSource
{
    private readonly SplitMixRandom random = new();
    public string Id => random.Id;
    public double Next(JsonObject state, string stream) => WithStream(state, value => random.Next(value, stream));
    public double Peek(JsonObject state, string stream) => WithStream(state, value => random.Peek(value, stream));
    public double Int(JsonObject state, string stream, double bound) => WithStream(state, value => random.Int(value, stream, bound));
    public bool Chance(JsonObject state, string stream, double percent) => WithStream(state, value => random.Chance(value, stream, percent));
    public T Pick<T>(JsonObject state, string stream, IReadOnlyList<T> items) => WithStream(state, value => random.Pick(value, stream, items));

    private static T WithStream<T>(JsonObject state, Func<RandomState, T> query)
    {
        var stored = StateJson.Object(state["random"], "random");
        var counters = StateJson.Object(stored["counters"], "random counters");
        var stream = new RandomState(EcmaNumber.ToUint32(StateJson.Number(stored["seed"], "seed")),
            counters.ToDictionary(pair => pair.Key, pair => StateJson.Number(pair.Value, "random counter"), StringComparer.Ordinal));
        var result = query(stream);
        stored["seed"] = stream.Seed;
        counters.Clear();
        foreach (var counter in stream.Counters) counters[counter.Key] = counter.Value;
        return result;
    }
}
