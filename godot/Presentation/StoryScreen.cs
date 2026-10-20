#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Displays supplied narrative and choices; callbacks own all campaign transitions.</summary>
public partial class StoryScreen : Control
{
    private VBoxContainer _content = null!;
    private HBoxContainer _footer = null!;
    private ScrollContainer _scroll = null!;
    private bool _built;
    private bool _submitted;
    public string? PresentedId { get; private set; }

    public override void _Ready() => Build();

    public void ShowStory(StoryEntry story, IReadOnlyDictionary<string, string> speakers, Action onContinue)
    {
        ArgumentNullException.ThrowIfNull(onContinue);
        foreach (var beat in story.Beats)
            if (!speakers.ContainsKey(beat.Speaker))
                throw new InvalidOperationException($"Narrative speaker is not exported: {beat.Speaker}");
        Reset(story.Id);
        _content.AddChild(Label(story.Kicker, 16, "#d5bc7f"));
        _content.AddChild(Label(story.Title, 32, "#f0e2bd"));
        _content.AddChild(Label($"{story.Date}  ·  {story.Location}", 16, "#b1bdb1"));
        _content.AddChild(Label(story.Summary, 19, "#d4d9cd"));
        _content.AddChild(new HSeparator());
        foreach (var beat in story.Beats)
        {
            var paragraph = new VBoxContainer();
            paragraph.AddThemeConstantOverride("separation", 6);
            paragraph.AddChild(Label(speakers[beat.Speaker], 16, "#d5bc7f"));
            paragraph.AddChild(Label(beat.Text, 20, "#eee9d7"));
            _content.AddChild(paragraph);
        }
        if (story.Mission is { } mission)
        {
            _content.AddChild(new HSeparator());
            MissionLine("目标", mission.Objective);
            MissionLine("危险", mission.Danger);
            MissionLine("要点", mission.Lesson);
        }
        _footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _footer.AddChild(Button("继续", () => Submit(onContinue)));
    }

    public void ShowChoice(StoryChoice choice, IReadOnlyCollection<string> availableOptionIds, Action<string> onChoose)
    {
        ArgumentNullException.ThrowIfNull(onChoose);
        var available = availableOptionIds.ToHashSet(StringComparer.Ordinal);
        if (available.Count != availableOptionIds.Count || choice.Options.Select(option => option.Id).Distinct().Count() != choice.Options.Count
            || available.Any(id => !choice.Options.Any(option => option.Id == id)))
            throw new InvalidOperationException($"Campaign supplied duplicate or unknown choice options for {choice.Id}.");
        Reset(choice.Id);
        _content.AddChild(Label($"第 {choice.Chapter} 章", 16, "#d5bc7f"));
        _content.AddChild(Label(choice.Prompt, 30, "#f0e2bd"));
        _content.AddChild(Label(choice.Context, 20, "#d4d9cd"));
        _content.AddChild(new HSeparator());
        foreach (var option in choice.Options.Where(option => available.Contains(option.Id)))
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 8);
            box.AddChild(Button(option.Label, () => Submit(() => onChoose(option.Id))));
            box.AddChild(Label(option.Detail, 19, "#eee9d7"));
            box.AddChild(Label(option.Consequence, 16, "#b4c7b4"));
            _content.AddChild(box);
        }
        if (available.Count == 0)
            _content.AddChild(Label("当前没有可选项。", 19, "#e0bc88"));
    }

    private void Build()
    {
        if (_built) return;
        _built = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("#091712"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margins = new MarginContainer();
        margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margins.AddThemeConstantOverride($"margin_{side}", 32);
        AddChild(margins);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        margins.AddChild(column);
        column.AddChild(Label("EMPIRE  /  断冠纪事", 18, "#b1bdb1"));
        _scroll = new ScrollContainer { Name = "NarrativeScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(_scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 22);
        _scroll.AddChild(_content);
        _footer = new HBoxContainer();
        column.AddChild(_footer);
    }

    private void Reset(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Narrative identity is absent.");
        Build();
        PresentedId = id;
        _submitted = false;
        Clear(_content);
        Clear(_footer);
        _scroll.ScrollVertical = 0;
    }
    private void Submit(Action callback)
    {
        if (_submitted) return;
        _submitted = true;
        callback();
    }
    private void MissionLine(string label, string text)
    {
        if (text.Length != 0) _content.AddChild(Label($"{label}：{text}", 18, "#d4d9cd"));
    }
    private static Label Label(string text, int size, string color)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }
    private static Button Button(string text, Action callback)
    {
        var button = new Button { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(160, 48) };
        button.Pressed += callback;
        return button;
    }
    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
