using System.Collections.Generic;

namespace Empire.Core.Determinism;

public sealed class DeterministicOnlyRandom : IRandomSource
{
    public string Id => "deterministic-only";

    public double Next(RandomState state, string stream) => throw Refused();
    public double Peek(RandomState state, string stream) => throw Refused();
    public int Int(RandomState state, string stream, int bound) => throw Refused();
    public double Int(RandomState state, string stream, double bound) => throw Refused();
    public bool Chance(RandomState state, string stream, double percent) => throw Refused();
    public T Pick<T>(RandomState state, string stream, IReadOnlyList<T> items) => throw Refused();

    private static DomainInvariantException Refused() => new(
        "this ruleset is configured for exact forecasts; install a seeded RandomSource to use variance");
}
