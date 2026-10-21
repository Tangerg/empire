#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Empire.Localization;

namespace Empire.Presentation;

/// <summary>Native authored structure and message references, never campaign state or translated prose.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StoryDocument
{
    public int Schema
    {
        get; init;
    }
    public Dictionary<string, string> SpeakerNameKeys { get; init; } = new(StringComparer.Ordinal);
    public List<StoryEntry> Stories { get; init; } = new();
    public List<StoryChoice> Choices { get; init; } = new();

    public static StoryDocument Load(GameText text, string path = "res://data/grayflag-story.json")
    {
        var document = PresentationJson.Load<StoryDocument>(path);
        document.Validate(text);
        return document;
    }

    public void Validate(GameText text)
    {
        if (Schema != 2)
            throw new InvalidOperationException("Unsupported story document schema.");
        Unique(Stories.Select(story => story.Id).Concat(Choices.Select(choice => choice.Id)), "narrative");
        foreach (var nameKey in SpeakerNameKeys.Values)
            text.Format(nameKey);
        foreach (var story in Stories)
        {
            if (story.Beats.Count == 0)
                throw new InvalidOperationException($"Narrative has no beats: {story.Id}.");
            Unique(story.Beats.Select(beat => beat.Id), $"beat in {story.Id}");
            foreach (var key in new[] { story.KickerKey, story.TitleKey, story.DateKey, story.LocationKey, story.SummaryKey })
                text.Format(key);
            foreach (var beat in story.Beats)
            {
                if (!SpeakerNameKeys.ContainsKey(beat.Speaker))
                    throw new InvalidOperationException($"Unknown narrative speaker {beat.Speaker} in {story.Id}/{beat.Id}.");
                text.Format(beat.TextKey);
            }
            if (story.Mission is { } mission)
                foreach (var key in new[] { mission.ObjectiveKey, mission.DangerKey, mission.LessonKey })
                    text.Format(key);
        }
        foreach (var choice in Choices)
        {
            Unique(choice.Options.Select(option => option.Id), $"option in {choice.Id}");
            text.Format(choice.PromptKey);
            text.Format(choice.ContextKey);
            foreach (var option in choice.Options)
                foreach (var key in new[] { option.LabelKey, option.DetailKey, option.ConsequenceKey })
                    text.Format(key);
        }
    }

    private static void Unique(IEnumerable<string> ids, string kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                throw new InvalidOperationException($"Missing or duplicate {kind} identity: {id}.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StoryEntry
{
    public string Id { get; init; } = "";
    public int Chapter
    {
        get; init;
    }
    public string KickerKey { get; init; } = "";
    public string TitleKey { get; init; } = "";
    public string DateKey { get; init; } = "";
    public string LocationKey { get; init; } = "";
    public string? Scene
    {
        get; init;
    }
    public string SummaryKey { get; init; } = "";
    public List<StoryBeat> Beats { get; init; } = new();
    public StoryMission? Mission
    {
        get; init;
    }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StoryBeat
{
    public string Id { get; init; } = "";
    public string Speaker { get; init; } = "";
    public string TextKey { get; init; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StoryMission
{
    public string ObjectiveKey { get; init; } = "";
    public string DangerKey { get; init; } = "";
    public string LessonKey { get; init; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StoryChoice
{
    public string Id { get; init; } = "";
    public int Chapter
    {
        get; init;
    }
    public string PromptKey { get; init; } = "";
    public string ContextKey { get; init; } = "";
    public List<StoryChoiceOption> Options { get; init; } = new();
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StoryChoiceOption
{
    public string Id { get; init; } = "";
    public string LabelKey { get; init; } = "";
    public string DetailKey { get; init; } = "";
    public string ConsequenceKey { get; init; } = "";
}
