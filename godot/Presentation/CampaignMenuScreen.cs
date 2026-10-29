using Godot;

namespace Empire.Presentation;

/// <summary>Signals menu intent once per binding. The caller owns confirmation, persistence and campaign transitions.</summary>
public partial class CampaignMenuScreen : Control
{
    private DispatchPanel? menuCard;
    public override void _Ready() => Resized += FitCard;
    private void FitCard()
    {
        if (IsInstanceValid(menuCard))
            menuCard!.CustomMinimumSize = new Vector2(Math.Min(570, Math.Max(280, Size.X - 72)), 0);
    }
    private int generation;
    private bool submitted;
    private readonly List<string> diagnostics = new();
    public CampaignMenuView? Presented
    {
        get; private set;
    }
    public IReadOnlyList<string> RenderingDiagnostics => diagnostics.ToArray();
    public override void _ExitTree()
    {
        generation++;
        submitted = true;
    }

    public void ShowMenu(CampaignMenuView view, Action continueCampaign, Action newCampaignRequested)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(continueCampaign);
        ArgumentNullException.ThrowIfNull(newCampaignRequested);
        Theme = CampaignUiStyle.Create();
        generation++;
        var binding = generation;
        submitted = false;
        Presented = view;
        diagnostics.Clear();
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("#111713"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        if (view.ArtResourcePath is { } path && ResourceLoader.Exists(path))
        {
            var texture = GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Cannot load title art: {path}");
            var picture = new TextureRect
            {
                Name = "MenuArt",
                Texture = texture,
                TooltipText = view.ArtAlt,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(picture);
        }
        else
            diagnostics.Add($"Original title art is unavailable: {view.ArtResourcePath ?? view.ArtSource}");
        var shade = new ColorRect { Color = new Color(0.025f, 0.028f, 0.02f, 0.38f), MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{edge}", 36);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 16);
        var card = menuCard = new DispatchPanel { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        scroll.AddChild(card);
        card.AddChild(column);
        FitCard();
        column.AddChild(Text(view.Title, 40));
        column.AddChild(Text(view.Description, 18));
        var buttons = new List<Button>();
        if (view.CanContinue)
            AddButton("ContinueCampaign", view.ContinueLabel, view.Progress, true, continueCampaign);
        AddButton("NewCampaign", view.NewLabel, null, view.NewPrimary, newCampaignRequested);
        var subtitle = Text(view.Kicker, 13);
        subtitle.AddThemeColorOverride("font_color", new Color("#c6b89c"));
        column.AddChild(subtitle);
        if (view.RejectedNotice is { } notice)
            column.AddChild(Text(notice, 18));
        if (diagnostics.Count > 0)
            column.AddChild(Text(view.ArtworkFailureLabel, 18));

        void AddButton(string name, string label, string? note, bool primary, Action callback)
        {
            var row = new VBoxContainer();
            row.AddThemeConstantOverride("separation", 16);
            column.AddChild(row);
            var button = new Button { Name = name, Text = label, CustomMinimumSize = new Vector2(240, 54) };
            button.AddThemeColorOverride("font_color", new Color(primary ? "#f1daa0" : "#d9dfd6"));
            buttons.Add(button);
            row.AddChild(button);
            if (primary)
                CampaignUiStyle.InitialFocus(button);
            if (note is not null)
                row.AddChild(Text(note, 20));
            button.Pressed += () =>
            {
                if (submitted || binding != generation || !IsInsideTree() || !button.IsInsideTree())
                    return;
                submitted = true;
                foreach (var current in buttons)
                    current.Disabled = true;
                callback();
            };
        }
    }

    private static Label Text(string value, int size)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("#e4e6dc"));
        return label;
    }
}
