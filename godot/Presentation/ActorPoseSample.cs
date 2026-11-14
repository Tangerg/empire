#nullable enable
using Godot;

namespace Empire.Presentation;

/// <summary>Read-only sample from the existing receipt timeline, not a command or an independent clock.</summary>
public readonly record struct ActorPoseSample(int UnitId, string Action, double ElapsedMs,
    double DurationMs, double? ReleaseMs, Vector2? AimDirection = null);
