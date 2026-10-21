#nullable enable
using System;
using System.Linq;

namespace Empire.Presentation;

public sealed class PaintedBattleDefinition
{
    public required string LevelPath
    {
        get; init;
    }
    public required string GroundPath
    {
        get; init;
    }
    public required string LevelTextPath
    {
        get; init;
    }
    public required string[] AdditionalContentPaths
    {
        get; init;
    }
    public required string ContentTextAdditionsPath
    {
        get; init;
    }
    public required int[] OpeningFocus
    {
        get; init;
    }
    public required string SavePath
    {
        get; init;
    }

    public void Validate()
    {
        if (AdditionalContentPaths is null || OpeningFocus is not { Length: 2 }
            || OpeningFocus.Any(value => value < 0))
            throw new InvalidOperationException("Painted battle content and opening focus must be explicit.");
        foreach (var path in AdditionalContentPaths.Concat(new[] { LevelPath, GroundPath, LevelTextPath, ContentTextAdditionsPath }))
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("res://", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException("Painted battle sources must be explicit project resources.");
        if (string.IsNullOrWhiteSpace(SavePath) || !SavePath.StartsWith("user://", StringComparison.Ordinal)
            || SavePath.Contains("..", StringComparison.Ordinal) || !SavePath.EndsWith(".json", StringComparison.Ordinal))
            throw new InvalidOperationException("Painted battle saves require an explicit isolated user JSON path.");
    }
}
