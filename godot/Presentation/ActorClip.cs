#nullable enable
using System;
using System.Collections.Generic;

namespace Empire.Presentation;

public enum ActorClipUnit
{
    Milliseconds, Cells
}

/// <summary>Immutable finite frame intervals. The caller owns progress and looping.</summary>
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
    public ActorClipUnit Unit
    {
        get;
    }
    public double Length => ends[^1];
    public double? ReleaseMs
    {
        get;
    }

    public double RequireRelease() => ReleaseMs
        ?? throw new InvalidOperationException($"Actor clip has no declared release: {Asset.Identity}.");

    internal ActorClip(RuntimeAsset asset, string unit, IReadOnlyList<ActorClipFrameRecord> sequence, double? releaseMs)
    {
        if (asset.Category is not ("combat-unit" or "mission-unit") || sequence.Count == 0)
            throw new InvalidOperationException("Actor clips require an actor sheet and at least one frame.");
        Unit = unit switch
        {
            "milliseconds" => ActorClipUnit.Milliseconds,
            "cells" => ActorClipUnit.Cells,
            _ => throw new InvalidOperationException($"Unknown actor clip sampling unit: {unit}.")
        };
        Asset = asset;
        frames = new int[sequence.Count];
        FrameIndices = Array.AsReadOnly(frames);
        ends = new double[sequence.Count];
        var position = 0.0;
        for (var index = 0; index < sequence.Count; index++)
        {
            var frame = sequence[index];
            if (frame is null || !double.IsFinite(frame.Span) || frame.Span <= 0
                || !double.IsFinite(position + frame.Span) || position + frame.Span <= position)
                throw new InvalidOperationException($"Actor clip frame has invalid span: {asset.Identity}/{index}.");
            frames[index] = AssetCatalog.FrameIndex(asset, frame.Frame);
            position += frame.Span;
            ends[index] = position;
        }
        if (releaseMs is { } release
            && (Unit != ActorClipUnit.Milliseconds || !double.IsFinite(release) || release < 0 || release > Length))
            throw new InvalidOperationException($"Actor clip has invalid release: {asset.Identity}.");
        ReleaseMs = releaseMs;
    }

    public int FrameAt(double offset)
    {
        if (!double.IsFinite(offset) || offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        for (var index = 0; index < ends.Length - 1; index++)
            if (offset < ends[index])
                return frames[index];
        return frames[^1];
    }
}
