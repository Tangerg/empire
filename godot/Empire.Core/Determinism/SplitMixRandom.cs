using System;
using System.Collections.Generic;

namespace Empire.Core.Determinism;

public sealed class SplitMixRandom : IRandomSource
{
    public string Id => "splitmix32";

    public double Next(RandomState state, string stream)
    {
        var counter = state.Counters.GetValueOrDefault(stream);
        state.Counters[stream] = counter + 1;
        return Draw(state.Seed, stream, counter);
    }

    public double Peek(RandomState state, string stream) =>
        Draw(state.Seed, stream, state.Counters.GetValueOrDefault(stream));

    public int Int(RandomState state, string stream, int bound) =>
        (int)Int(state, stream, (double)bound);

    public double Int(RandomState state, string stream, double bound)
    {
        if (!double.IsFinite(bound) || Math.Truncate(bound) != bound || bound < 1)
            throw new DomainInvariantException("random bound must be a positive integer");

        return Math.Floor(Next(state, stream) * bound);
    }

    public bool Chance(RandomState state, string stream, double percent)
    {
        if (percent <= 0) return false;
        if (percent >= 100) return true;
        return Next(state, stream) * 100 < percent;
    }

    public T Pick<T>(RandomState state, string stream, IReadOnlyList<T> items)
    {
        if (items.Count == 0)
            throw new DomainInvariantException("cannot pick from an empty list");

        return items[Int(state, stream, items.Count)];
    }

    private static double Draw(uint seed, string stream, double counter)
    {
        var input = seed ^ Utf16Hash.Hash(stream)
            ^ unchecked(EcmaNumber.ToUint32(counter) * 0x85ebca6bu);
        return Mix32(input) / 4294967296.0;
    }

    private static uint Mix32(uint value)
    {
        var z = unchecked(value + 0x9e3779b9u);
        z ^= z >> 16;
        z = unchecked(z * 0x21f0aaadu);
        z ^= z >> 15;
        z = unchecked(z * 0x735a2d97u);
        z ^= z >> 15;
        return z;
    }
}
