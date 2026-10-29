using System;
using System.Collections.Generic;
using System.Linq;
using Empire.Localization;

namespace Empire.Presentation;

/// <summary>Native presentation metadata; neither a save nor a second campaign definition.</summary>
public sealed class NarrativePresentationDocument
{
    public int Schema
    {
        get; init;
    }
    public string TitleKey { get; init; } = "";
    public string CompletionLabelKey { get; init; } = "";
    public int BattleTotal
    {
        get; init;
    }
    public Dictionary<int, NarrativeChapter> Chapters { get; init; } = new();
    public Dictionary<string, NarrativeArt> StoryScenes { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Portraits { get; init; } = new(StringComparer.Ordinal);
    public static NarrativePresentationDocument Load(GameText text, PortraitCatalog portraits, string path = "res://data/grayflag-staging.json")
    {
        var document = PresentationJson.Load<NarrativePresentationDocument>(path);
        if (document.Schema != 3)
            throw new InvalidOperationException("Unsupported narrative presentation metadata schema.");
        foreach (var id in document.Portraits.Values)
            portraits.Resolve(id);
        text.Format(document.TitleKey);
        text.Format(document.CompletionLabelKey);
        foreach (var chapter in document.Chapters.Values)
            text.Format(chapter.TitleKey);
        return document;
    }
}

public sealed class NarrativeChapter
{
    public string TitleKey { get; init; } = "";
    public NarrativeArt Art { get; init; } = new();
}

/// <summary>Caller-owned campaign facts and text service, passed explicitly to a read-only presentation.</summary>
public sealed record NarrativeContext(GameText Text, PortraitCatalog PortraitCatalog, NarrativePresentationDocument Presentation, int CompletedBattles, bool CampaignActive);

public sealed record CampaignShellView(string Title, string ChapterHeading, int CompletedBattles, int BattleTotal,
    NarrativeArt Art, string ExitLabel, string ProgressLabel, string ArtworkFailureLabel, string ReadingHint = "")
{
    public string Progress => FormattableString.Invariant($"{CompletedBattles}/{BattleTotal}");
    public double ProgressFraction => BattleTotal == 0 ? 0 : (double)CompletedBattles / BattleTotal;
    public int ProgressSegments => Math.Max(1, BattleTotal);

    public static CampaignShellView Create(NarrativeContext context, int chapter, string? sceneTopic = null)
    {
        var metadata = context.Presentation;
        if (metadata.Schema != 3)
            throw new InvalidOperationException("Unsupported narrative presentation metadata schema.");
        if (!metadata.Chapters.TryGetValue(chapter, out var entry))
            throw new InvalidOperationException($"Narrative chapter metadata is not authored: {chapter}.");
        var art = sceneTopic is null ? entry.Art : metadata.StoryScenes.TryGetValue(sceneTopic, out var scene)
            ? scene : new NarrativeArt { Source = sceneTopic };
        var text = context.Text;
        return new CampaignShellView(text.Format(metadata.TitleKey), text.Format("ui.campaign.chapter_heading", chapter, text.Format(entry.TitleKey)),
            context.CompletedBattles, metadata.BattleTotal, art, text.Format("ui.campaign.exit"), text.Format("ui.campaign.progress"), text.Format("error.presentation.artwork_unavailable"), text.Format("ui.campaign.reading_hint"));
    }
}

public sealed record StoryMissionView(string Objective, string Danger, string Lesson);
public sealed record StoryView(CampaignShellView Shell, string Kicker, string Date, string Location,
    string Title, string Summary, StoryMissionView? Mission, string Speaker, string Text, NarrativeArt? Portrait,
    int BeatIndex, int BeatCount, string PrimaryLabel, bool Ending, string ObjectiveLabel, string DangerLabel, string LessonLabel)
{
    public bool Narration => Portrait is null;
    public bool LastBeat => BeatIndex >= BeatCount - 1;
    public bool[] ActiveDots => Enumerable.Range(0, BeatCount).Select(index => index <= BeatIndex).ToArray();
}

public sealed record NarrativeChoiceOption(string Id, string Number, string Label, string Detail, string Consequence);
public sealed record ChoiceView(CampaignShellView Shell, string Prompt, string Context, IReadOnlyList<NarrativeChoiceOption> Options,
    string Eyebrow, string Note);

public static class NarrativeProjection
{
    public static StoryView Story(StoryEntry story, IReadOnlyDictionary<string, string> identityNameKeys,
        NarrativeContext context, int beatIndex, bool ending)
    {
        if (story.Beats.Count == 0)
            throw new InvalidOperationException($"Narrative has no beats: {story.Id}.");
        if (beatIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(beatIndex));
        var beat = story.Beats[Math.Min(beatIndex, story.Beats.Count - 1)];
        var lastBeat = beatIndex >= story.Beats.Count - 1;
        var text = context.Text;
        var mission = story.Mission is { } source ? new StoryMissionView(text.Format(source.ObjectiveKey), text.Format(source.DangerKey), text.Format(source.LessonKey)) : null;
        return new StoryView(CampaignShellView.Create(context, story.Chapter, story.Scene),
            text.Format(story.KickerKey), text.Format(story.DateKey), text.Format(story.LocationKey), text.Format(story.TitleKey), text.Format(story.SummaryKey), mission,
            IdentityNames.Resolve(text, identityNameKeys, beat.Speaker), text.Format(beat.TextKey), context.Presentation.Portraits.TryGetValue(beat.Speaker, out var portraitId) ? context.PortraitCatalog.Resolve(portraitId) : null, beatIndex, story.Beats.Count,
            text.Format(ending ? context.CampaignActive ? context.Presentation.CompletionLabelKey : "ui.campaign.exit"
                : lastBeat ? "ui.story.continue" : "ui.story.next_beat"), ending,
            text.Format("ui.story.objective"), text.Format("ui.story.danger"), text.Format("ui.story.lesson"));
    }

    public static ChoiceView Choice(StoryChoice choice, IReadOnlyCollection<string> availableOptionIds, NarrativeContext context)
    {
        var available = availableOptionIds.ToHashSet(StringComparer.Ordinal);
        if (available.Count != availableOptionIds.Count || choice.Options.Select(option => option.Id).Distinct().Count() != choice.Options.Count
            || available.Any(id => !choice.Options.Any(option => option.Id == id)))
            throw new InvalidOperationException($"Campaign supplied duplicate or unknown choice options for {choice.Id}.");
        var text = context.Text;
        var options = choice.Options.Select((option, index) => (option, index)).Where(row => available.Contains(row.option.Id))
            .Select(row => new NarrativeChoiceOption(row.option.Id, FormattableString.Invariant($"0{row.index + 1}"), text.Format(row.option.LabelKey),
                text.Format(row.option.DetailKey), text.Format(row.option.ConsequenceKey))).ToArray();
        return new ChoiceView(CampaignShellView.Create(context, choice.Chapter), text.Format(choice.PromptKey), text.Format(choice.ContextKey),
            Array.AsReadOnly(options), text.Format("ui.choice.eyebrow"), text.Format("ui.choice.note"));
    }
}
