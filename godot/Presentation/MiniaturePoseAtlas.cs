#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace Empire.Presentation;

/// <summary>Samples authored poses at the receipt owner's progress and release. Never advances time.</summary>
internal sealed class MiniaturePoseAtlas
{
    private sealed record Clip(Texture2D Sheet, Rect2[] Front, Rect2[] Back, double[] Ends, double Release)
    {
        public Dictionary<(bool Backward, int Frame), Texture2D> Textures { get; } = new();
    }
    private readonly JsonObject _document;
    private readonly Dictionary<(string Role, string Clip), Clip> _clips = new();

    internal MiniaturePoseAtlas() => _document = PresentationJson.Load<JsonObject>("res://assets/c01-miniature-roles/action-sheets.json");

    internal Texture2D? Sample(string role, bool backward, ActorPoseSample sample)
    {
        if (_document["roles"]?[role] is not JsonObject record
            || record["sourceActions"]?[sample.Action]?.GetValue<string>() is not { } clipKey)
            return null;
        if (!_clips.TryGetValue((role, clipKey), out var clip))
        {
            var source = record["clips"]?[clipKey]?.AsObject() ?? throw new InvalidOperationException($"Missing miniature clip {role}/{clipKey}.");
            var sheet = GD.Load<Texture2D>(record["texture"]!.GetValue<string>())
                ?? throw new InvalidOperationException($"Missing miniature action sheet for {role}.");
            var durations = source["durationsMs"]!.AsArray().Select(n => n!.GetValue<double>()).ToArray();
            if (durations.Length == 0 || durations.Any(duration => !double.IsFinite(duration) || duration <= 0))
                throw new InvalidOperationException($"Invalid miniature pose durations for {role}/{clipKey}.");
            var ends = new double[durations.Length];
            var sum = 0d;
            for (var index = 0; index < ends.Length; index++)
                ends[index] = sum += durations[index];
            var releaseFrame = source["releaseFrame"]!.GetValue<int>();
            if (releaseFrame < 0 || releaseFrame >= ends.Length)
                throw new InvalidOperationException($"Invalid miniature release frame for {role}/{clipKey}.");
            var release = releaseFrame == 0 ? 0 : ends[releaseFrame - 1];
            if (release != source["referenceReleaseMs"]!.GetValue<double>())
                throw new InvalidOperationException($"Miniature release anchor differs from its authored frames: {role}/{clipKey}.");
            Rect2[] Regions(string key)
            {
                var regions = source[key]!.AsArray().Select(node =>
                {
                    var values = node!.AsArray();
                    if (values.Count != 4)
                        throw new InvalidOperationException("A pose region needs x/y/width/height.");
                    var region = new Rect2(values[0]!.GetValue<int>(), values[1]!.GetValue<int>(), values[2]!.GetValue<int>(), values[3]!.GetValue<int>());
                    if (region.Size != new Vector2(64, 64) || !new Rect2(Vector2.Zero, sheet.GetSize()).Encloses(region))
                        throw new InvalidOperationException("Miniature poses must preserve their 64-pixel foot-anchored frame inside the sheet.");
                    return region;
                }).ToArray();
                if (regions.Length != ends.Length)
                    throw new InvalidOperationException("Miniature frame regions and intervals differ.");
                return regions;
            }
            clip = new(sheet, Regions("frontFrames"), Regions("backFrames"), ends, release);
            _clips.Add((role, clipKey), clip);
        }
        var elapsed = VisualTime(sample.ElapsedMs, sample.DurationMs, sample.ReleaseMs
            ?? throw new InvalidOperationException("An attack/cast pose needs the owner's release."), clip.Ends[^1], clip.Release);
        var frame = Array.FindIndex(clip.Ends, end => elapsed < end);
        if (frame < 0)
            frame = clip.Ends.Length - 1;
        if (!clip.Textures.TryGetValue((backward, frame), out var texture))
        {
            texture = new AtlasTexture { Atlas = clip.Sheet, Region = (backward ? clip.Back : clip.Front)[frame] };
            clip.Textures.Add((backward, frame), texture);
        }
        return texture;
    }

    internal static double VisualTime(double elapsed, double ownerDuration, double ownerRelease, double visualDuration, double visualRelease)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0 || !double.IsFinite(ownerDuration) || ownerDuration <= 0
            || !double.IsFinite(ownerRelease) || ownerRelease < 0 || ownerRelease > ownerDuration
            || !double.IsFinite(visualDuration) || visualDuration <= 0 || !double.IsFinite(visualRelease)
            || visualRelease < 0 || visualRelease > visualDuration)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Pose sampling requires finite owner and visual intervals.");
        if (elapsed < ownerRelease)
            return elapsed / ownerRelease * visualRelease;
        if (ownerRelease == ownerDuration)
            return visualDuration;
        return visualRelease + Math.Min(1, (elapsed - ownerRelease) / (ownerDuration - ownerRelease)) * (visualDuration - visualRelease);
    }
}
