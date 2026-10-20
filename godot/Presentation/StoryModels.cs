#nullable enable
using System;
using System.Collections.Generic;

namespace Empire.Presentation;

/// <summary>Exact narrative export, not campaign state or transition rules.</summary>
public sealed class StoryDocument
{
    public Dictionary<string, string> Speakers { get; init; } = new(StringComparer.Ordinal);
    public List<StoryEntry> Stories { get; init; } = new();
    public List<StoryChoice> Choices { get; init; } = new();
    public static StoryDocument Load(string path = "res://data/grayflag-story.json") =>
        PresentationJson.Load<StoryDocument>(path);
}
public sealed class StoryEntry
{
    public string Id { get; init; } = "";
    public int Chapter { get; init; }
    public string Kicker { get; init; } = "";
    public string Title { get; init; } = "";
    public string Date { get; init; } = "";
    public string Location { get; init; } = "";
    public string Scene { get; init; } = "";
    public string Summary { get; init; } = "";
    public List<StoryBeat> Beats { get; init; } = new();
    public StoryMission? Mission { get; init; }
}
public sealed class StoryBeat
{
    public string Speaker { get; init; } = "";
    public string Text { get; init; } = "";
}
public sealed class StoryMission
{
    public string Objective { get; init; } = "";
    public string Danger { get; init; } = "";
    public string Lesson { get; init; } = "";
}
public sealed class StoryChoice
{
    public string Id { get; init; } = "";
    public int Chapter { get; init; }
    public string Prompt { get; init; } = "";
    public string Context { get; init; } = "";
    public List<StoryChoiceOption> Options { get; init; } = new();
}
public sealed class StoryChoiceOption
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Consequence { get; init; } = "";
}
