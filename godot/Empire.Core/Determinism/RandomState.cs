using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Empire.Core.Determinism;

public sealed class RandomState
{
    [JsonPropertyName("seed")]
    public uint Seed { get; set; }

    // JavaScript counters are doubles, not uints: wrapping a saved counter at
    // 2^32 would preserve the next draw but change the structural state digest.
    [JsonPropertyName("counters")]
    public Dictionary<string, double> Counters { get; }

    [JsonConstructor]
    public RandomState(uint seed, Dictionary<string, double>? counters = null)
    {
        Seed = seed;
        Counters = counters is null
            ? new Dictionary<string, double>(StringComparer.Ordinal)
            : new Dictionary<string, double>(counters, StringComparer.Ordinal);
    }

    public static RandomState Create(double seed) => new(EcmaNumber.ToUint32(seed));
}
