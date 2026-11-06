#nullable enable
using System;
using System.Collections.Generic;

namespace Empire.Presentation;

/// <summary>Immutable finite frame sequence. The caller owns elapsed presentation time.</summary>
public sealed class ActorClip
{
    private readonly int[] frames;
    private readonly double[] ends;
    internal IReadOnlyList<int> FrameIndices
    {
        get;
    }
    public RuntimeAsset Asset
    {
        get;
    }
    public double DurationMs => ends[^1];

    internal ActorClip(RuntimeAsset asset, IReadOnlyList<ActorClipFrameRecord> sequence)
    {
        if (asset.Category is not ("combat-unit" or "mission-unit") || sequence.Count == 0)
            throw new InvalidOperationException("Actor clips require an actor sheet and at least one frame.");
        Asset = asset;
        frames = new int[sequence.Count];
        FrameIndices = Array.AsReadOnly(frames);
        ends = new double[sequence.Count];
        var elapsed = 0.0;
        for (var index = 0; index < sequence.Count; index++)
        {
            var frame = sequence[index];
            if (frame is null || !double.IsFinite(frame.DurationMs) || frame.DurationMs <= 0
                || !double.IsFinite(elapsed + frame.DurationMs) || elapsed + frame.DurationMs <= elapsed)
                throw new InvalidOperationException($"Actor clip frame has invalid duration: {asset.Identity}/{index}.");
            frames[index] = AssetCatalog.FrameIndex(asset, frame.Frame);
            elapsed += frame.DurationMs;
            ends[index] = elapsed;
        }
    }

    public int FrameAt(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        for (var index = 0; index < ends.Length - 1; index++)
            if (milliseconds < ends[index])
                return frames[index];
        return frames[^1];
    }
}
