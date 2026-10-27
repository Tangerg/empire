#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

/// <summary>One local beat at a time. Only caller callbacks can advance or finish the campaign.</summary>
public partial class StoryScreen : Control
{
    private CampaignShell shell = null!;
    private StoryEntry? story;
    private IReadOnlyDictionary<string, string> speakers = null!;
    private NarrativeContext context = null!;
    private Action continueStory = null!;
    private Action finishStory = null!;
    private bool ending;
    private int generation;
    private bool submitted;
    private GridContainer? missionGrid;
    private GridContainer? actionGrid;
    private GridContainer? choiceGrid;
    private TextureRect? portrait;
    private readonly List<Button> buttons = new();
    public string? PresentedId
    {
        get; private set;
    }
    public int BeatIndex
    {
        get; private set;
    }
    public StoryView? PresentedStory
    {
        get; private set;
    }
    public ChoiceView? PresentedChoice
    {
        get; private set;
    }
    public IReadOnlyList<string> RenderingDiagnostics => IsInstanceValid(shell) ? shell.RenderingDiagnostics : Array.Empty<string>();

    public override void _Ready() => Resized += ResizeLayout;
    public override void _ExitTree()
    {
        generation++;
        submitted = true;
    }

    public void ShowStory(StoryEntry entry, IReadOnlyDictionary<string, string> identityNameKeys, NarrativeContext narrative,
        bool isEnding, Action onContinue, Action onFinish, Action onExit)
    {
        ArgumentNullException.ThrowIfNull(onContinue);
        ArgumentNullException.ThrowIfNull(onFinish);
        ArgumentNullException.ThrowIfNull(onExit);
        var first = NarrativeProjection.Story(entry, identityNameKeys, narrative, 0, isEnding);
        Reset(entry.Id, first.Shell, onExit);
        story = entry;
        speakers = identityNameKeys;
        context = narrative;
        ending = isEnding;
        continueStory = onContinue;
        finishStory = onFinish;
        RenderStory();
    }

    public void ShowChoice(StoryChoice choice, IReadOnlyCollection<string> availableOptionIds, NarrativeContext narrative,
        Action<string> onChoose, Action onExit)
    {
        ArgumentNullException.ThrowIfNull(onChoose);
        ArgumentNullException.ThrowIfNull(onExit);
        var view = NarrativeProjection.Choice(choice, availableOptionIds, narrative);
        Reset(choice.Id, view.Shell, onExit);
        PresentedChoice = view;
        var body = Card();
        body.AddChild(CampaignShell.Text(view.Eyebrow, 16, "#e2bb69", "ChoiceEyebrow"));
        body.AddChild(CampaignShell.Text(view.Prompt, 32, "#f6ead2", "ChoicePrompt"));
        body.AddChild(CampaignShell.Text(view.Context, 19, "#c7b8a3", "ChoiceContext"));
        choiceGrid = new GridContainer { Name = "ChoiceOptions", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        choiceGrid.AddThemeConstantOverride("h_separation", 16);
        choiceGrid.AddThemeConstantOverride("v_separation", 16);
        body.AddChild(choiceGrid);
        foreach (var option in view.Options)
        {
            var button = ActionButton("", "Choice_" + option.Id, () => Submit(() => onChoose(option.Id)));
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.CustomMinimumSize = new Vector2(0, 190);
            button.AccessibilityName = option.Label;
            var labels = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            labels.AddThemeConstantOverride("separation", 12);
            button.AddChild(labels);
            labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            labels.OffsetLeft = 20;
            labels.OffsetTop = 20;
            labels.OffsetRight = -20;
            labels.OffsetBottom = -20;
            labels.AddChild(CampaignShell.Text(option.Number, 16, "#e2bb69", "ChoiceNumber"));
            labels.AddChild(CampaignShell.Text(option.Label, 22, "#eee1cb", "ChoiceLabel"));
            labels.AddChild(CampaignShell.Text(option.Detail, 18, "#c7b8a3", "ChoiceDetail"));
            labels.AddChild(CampaignShell.Text(option.Consequence, 16, "#d7aa50", "ChoiceConsequence"));
            foreach (var child in labels.GetChildren())
                ((Control)child).MouseFilter = MouseFilterEnum.Ignore;
            labels.MinimumSizeChanged += () => button.CustomMinimumSize = new Vector2(0, Math.Max(190, labels.GetCombinedMinimumSize().Y + 40));
            choiceGrid.AddChild(button);
        }
        body.AddChild(CampaignShell.Text(view.Note, 16, "#c7b8a3", "ChoiceNote"));
        ResizeLayout();
    }

    private void RenderStory()
    {
        var view = NarrativeProjection.Story(story!, speakers, context, BeatIndex, ending);
        PresentedStory = view;
        var restorePrimaryFocus = buttons.Any(button => IsInstanceValid(button) && button.HasFocus());
        Clear(shell.Body);
        buttons.Clear();
        missionGrid = null;
        actionGrid = null;
        portrait = null;
        var body = Card();
        var meta = new HFlowContainer();
        meta.AddThemeConstantOverride("h_separation", 18);
        body.AddChild(meta);
        meta.AddChild(CampaignShell.Text(view.Kicker, 16, "#d6ba89", "StoryKicker"));
        meta.AddChild(CampaignShell.Text(view.Date, 16, "#d6ba89", "StoryDate"));
        meta.AddChild(CampaignShell.Text(view.Location, 16, "#d6ba89", "StoryLocation"));
        body.AddChild(CampaignShell.Text(view.Title, 36, "#f6ead2", "StoryTitle"));
        body.AddChild(CampaignShell.Text(view.Summary, 19, "#c9bba6", "StorySummary"));
        if (view.Mission is { } mission)
        {
            missionGrid = new GridContainer { Name = "MissionBrief", Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            missionGrid.AddThemeConstantOverride("h_separation", 18);
            missionGrid.AddThemeConstantOverride("v_separation", 12);
            body.AddChild(missionGrid);
            MissionLine(view.ObjectiveLabel, mission.Objective);
            MissionLine(view.DangerLabel, mission.Danger);
            MissionLine(view.LessonLabel, mission.Lesson);
        }
        var dialogue = new HBoxContainer { Name = view.Narration ? "Narration" : "Dialogue" };
        dialogue.AddThemeConstantOverride("separation", 18);
        body.AddChild(dialogue);
        if (view.Portrait is { } art && shell.Texture(art, $"portrait for {view.Speaker}") is { } texture)
        {
            portrait = CharacterPortrait.Create(texture);
            portrait.Name = "StoryPortrait";
            portrait.TooltipText = view.Speaker;
            portrait.AccessibilityName = view.Speaker;
            portrait.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            dialogue.AddChild(portrait);
        }
        var copyPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        copyPanel.AddThemeStyleboxOverride("panel", CampaignShell.Panel(new Color(.03f, .027f, .024f, .7f), 18));
        dialogue.AddChild(copyPanel);
        var copy = new VBoxContainer { CustomMinimumSize = new Vector2(0, 100) };
        copy.AddThemeConstantOverride("separation", 9);
        copyPanel.AddChild(copy);
        copy.AddChild(CampaignShell.Text(view.Speaker, 16, "#e7bd68", "StorySpeaker"));
        copy.AddChild(CampaignShell.Text(view.Text, 20, "#f4e8d2", "StoryBeat"));
        actionGrid = new GridContainer { Name = "StoryActions", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actionGrid.AddThemeConstantOverride("v_separation", 16);
        body.AddChild(actionGrid);
        var dots = new HBoxContainer { Name = "BeatDots", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        dots.AddThemeConstantOverride("separation", 6);
        actionGrid.AddChild(dots);
        foreach (var active in view.ActiveDots)
            dots.AddChild(new ColorRect
            {
                Color = active ? new Color("#d7aa50") : new Color(1, 1, 1, .15f),
                CustomMinimumSize = new Vector2(22, 3),
                MouseFilter = MouseFilterEnum.Ignore
            });
        var primary = ActionButton(view.PrimaryLabel, "StoryPrimary", AdvanceLocalBeat);
        actionGrid.AddChild(primary);
        ResizeLayout();
        if (!restorePrimaryFocus) CampaignUiStyle.InitialFocus(primary);
        if (restorePrimaryFocus)
        {
            var binding = generation;
            var beat = BeatIndex;
            Callable.From(() =>
            {
                if (IsInstanceValid(this) && IsInsideTree() && IsVisibleInTree()
                    && generation == binding && BeatIndex == beat && !submitted
                    && IsInstanceValid(primary) && primary.IsInsideTree() && !primary.Disabled
                    && GetViewport().GuiGetFocusOwner() is null)
                    primary.GrabFocus();
            }).CallDeferred();
        }
    }

    private void AdvanceLocalBeat()
    {
        // The source ending is intentionally not paged: its first beat exposes finish immediately.
        if (ending)
        {
            Submit(finishStory);
            return;
        }
        if (PresentedStory!.LastBeat)
        {
            Submit(continueStory);
            return;
        }
        BeatIndex++;
        RenderStory();
    }

    private void Reset(string id, CampaignShellView view, Action onExit)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Narrative identity is absent.");
        generation++;
        submitted = false;
        BeatIndex = 0;
        PresentedId = id;
        PresentedStory = null;
        PresentedChoice = null;
        story = null;
        missionGrid = null;
        actionGrid = null;
        choiceGrid = null;
        portrait = null;
        buttons.Clear();
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shell = new CampaignShell();
        AddChild(shell);
        var binding = generation;
        shell.Show(view, () => { if (binding == generation && IsInsideTree() && shell.IsInsideTree()) Submit(onExit); });
        shell.Scroll.ScrollVertical = 0;
    }
    private void Submit(Action callback)
    {
        if (submitted)
            return;
        submitted = true;
        shell.DisableNavigation();
        foreach (var button in buttons)
            button.Disabled = true;
        callback();
    }
    private Button ActionButton(string text, string name, Action callback)
    {
        var binding = generation;
        var button = new Button
        {
            Name = name,
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(160, 48)
        };
        button.Pressed += () =>
        {
            if (submitted || binding != generation || !IsInsideTree() || !button.IsInsideTree())
                return;
            callback();
        };
        buttons.Add(button);
        return button;
    }
    private VBoxContainer Card()
    {
        var panel = new DispatchPanel { Name = "NarrativeCard", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        shell.Body.AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 18);
        panel.AddChild(body);
        return body;
    }
    private void MissionLine(string label, string text)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(CampaignShell.Text(label, 15, "#d7aa50"));
        column.AddChild(CampaignShell.Text(text, 18, "#dbcdb8"));
        missionGrid!.AddChild(column);
    }
    private void ResizeLayout()
    {
        var narrow = Size.X <= 760;
        if (IsInstanceValid(missionGrid))
            missionGrid!.Columns = narrow ? 1 : 3;
        if (IsInstanceValid(actionGrid))
            actionGrid!.Columns = narrow ? 1 : 2;
        if (IsInstanceValid(choiceGrid))
            choiceGrid!.Columns = narrow ? 1 : 2;
        if (IsInstanceValid(portrait))
            portrait!.CustomMinimumSize = narrow ? new Vector2(64, 88) : new Vector2(92, 112);
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
