using System;
using System.Collections.Generic;
using System.Linq;

namespace Empire.Presentation;

/// <summary>Source adapter metadata; neither a save nor a second campaign definition.</summary>
public sealed class NarrativePresentationDocument
{
    public int Schema { get; init; }
    public string Title { get; init; } = "";
    public string CompletionLabel { get; init; } = "";
    public int BattleTotal { get; init; }
    public Dictionary<int, NarrativeChapter> Chapters { get; init; } = new();
    public Dictionary<string, NarrativeArt> StoryScenes { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, NarrativeArt> Portraits { get; init; } = new(StringComparer.Ordinal);
    public static NarrativePresentationDocument Load(string path = "res://data/grayflag-staging.json") =>
        PresentationJson.Load<NarrativePresentationDocument>(path);
}

public sealed class NarrativeChapter
{
    public string Title { get; init; } = "";
    public NarrativeArt Art { get; init; } = new();
}

public sealed class NarrativeArt
{
    public string Source { get; init; } = "";
    public string? ResourcePath { get; init; }
    public string? Sha256 { get; init; }
}

/// <summary>Caller-owned campaign facts, passed explicitly to a read-only presentation.</summary>
public sealed record NarrativeContext(NarrativePresentationDocument Presentation, int CompletedBattles, bool CampaignActive);

public sealed record CampaignShellView(string Title, string ChapterHeading, int CompletedBattles, int BattleTotal, NarrativeArt Art)
{
    public string Progress => $"{CompletedBattles}/{BattleTotal}";
    public double ProgressFraction => BattleTotal == 0 ? 0 : (double)CompletedBattles / BattleTotal;
    public int ProgressSegments => Math.Max(1, BattleTotal);
    public const string ExitLabel = "返回主菜单";
    public const string ProgressLabel = "战役进度";

    public static CampaignShellView Create(NarrativeContext context, int chapter, string? sceneTopic = null)
    {
        var metadata = context.Presentation;
        if (metadata.Schema != 1) throw new InvalidOperationException("Unsupported narrative presentation metadata schema.");
        if (!metadata.Chapters.TryGetValue(chapter, out var entry))
            throw new InvalidOperationException($"Narrative chapter metadata is not exported: {chapter}.");
        var art = sceneTopic is null ? entry.Art : metadata.StoryScenes.TryGetValue(sceneTopic, out var scene)
            ? scene : new NarrativeArt { Source = sceneTopic };
        return new CampaignShellView(metadata.Title, $"第 {chapter} 章 · {entry.Title}", context.CompletedBattles, metadata.BattleTotal, art);
    }
}

public sealed record StoryView(CampaignShellView Shell, string Kicker, string Date, string Location,
    string Title, string Summary, StoryMission? Mission, string Speaker, string Text, NarrativeArt? Portrait,
    int BeatIndex, int BeatCount, string PrimaryLabel, bool Ending)
{
    public bool Narration => Portrait is null;
    public bool LastBeat => BeatIndex >= BeatCount - 1;
    public bool[] ActiveDots => Enumerable.Range(0, BeatCount).Select(index => index <= BeatIndex).ToArray();
    public const string ObjectiveLabel = "胜利目标";
    public const string DangerLabel = "战场风险";
    public const string LessonLabel = "机制主题";
}

public sealed record NarrativeChoiceOption(string Id, string Number, string Label, string Detail, string Consequence);
public sealed record ChoiceView(CampaignShellView Shell, string Prompt, string Context, IReadOnlyList<NarrativeChoiceOption> Options)
{
    public const string Eyebrow = "需要作出决定";
    public const string Note = "选择会写入战役存档，并在后续关卡、关系或资源中兑现。";
}

public static class NarrativeProjection
{
    public static StoryView Story(StoryEntry story, IReadOnlyDictionary<string, string> speakers,
        NarrativeContext context, int beatIndex, bool ending)
    {
        if (story.Beats.Count == 0) throw new InvalidOperationException($"Narrative has no beats: {story.Id}.");
        if (beatIndex < 0) throw new ArgumentOutOfRangeException(nameof(beatIndex));
        var beat = story.Beats[Math.Min(beatIndex, story.Beats.Count - 1)];
        var lastBeat = beatIndex >= story.Beats.Count - 1;
        return new StoryView(CampaignShellView.Create(context, story.Chapter, story.Scene),
            story.Kicker, story.Date, story.Location, story.Title, story.Summary, story.Mission,
            speakers.TryGetValue(beat.Speaker, out var speaker) ? speaker : beat.Speaker, beat.Text,
            context.Presentation.Portraits.GetValueOrDefault(beat.Speaker), beatIndex, story.Beats.Count,
            ending ? context.CampaignActive ? context.Presentation.CompletionLabel : CampaignShellView.ExitLabel
                : (lastBeat ? "继续" : "下一段") + " →", ending);
    }

    public static ChoiceView Choice(StoryChoice choice, IReadOnlyCollection<string> availableOptionIds, NarrativeContext context)
    {
        var available = availableOptionIds.ToHashSet(StringComparer.Ordinal);
        if (available.Count != availableOptionIds.Count || choice.Options.Select(option => option.Id).Distinct().Count() != choice.Options.Count
            || available.Any(id => !choice.Options.Any(option => option.Id == id)))
            throw new InvalidOperationException($"Campaign supplied duplicate or unknown choice options for {choice.Id}.");
        var options = choice.Options.Select((option, index) => (option, index)).Where(row => available.Contains(row.option.Id))
            .Select(row => new NarrativeChoiceOption(row.option.Id, $"0{row.index + 1}", row.option.Label, row.option.Detail, row.option.Consequence)).ToArray();
        return new ChoiceView(CampaignShellView.Create(context, choice.Chapter), choice.Prompt, choice.Context, Array.AsReadOnly(options));
    }
}
