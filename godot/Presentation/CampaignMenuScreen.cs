using Godot;

namespace Empire.Presentation;

/// <summary>Signals menu intent once per binding. The caller owns confirmation, persistence and campaign transitions.</summary>
public partial class CampaignMenuScreen : Control
{
    private int generation;
    private bool submitted;
    private readonly List<string> diagnostics = new();
    public CampaignMenuView? Presented { get; private set; }
    public IReadOnlyList<string> RenderingDiagnostics => diagnostics.ToArray();
    public override void _ExitTree() { generation++; submitted = true; }

    public void ShowMenu(CampaignMenuView view, Action continueCampaign, Action newCampaignRequested)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(continueCampaign);
        ArgumentNullException.ThrowIfNull(newCampaignRequested);
        generation++;
        var binding = generation;
        submitted = false; Presented = view; diagnostics.Clear();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("#111713"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        if (view.ArtResourcePath is { } path && Godot.FileAccess.FileExists(path))
        {
            var texture = GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Cannot load title art: {path}");
            var picture = new TextureRect { Name = "MenuArt", Texture = texture, TooltipText = view.ArtAlt,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore };
            picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(picture);
        }
        else diagnostics.Add($"Original title art is unavailable: {view.ArtResourcePath ?? view.ArtSource}");
        var shade = new ColorRect { Color = new Color(0.025f, 0.04f, 0.025f, 0.65f), MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(margin);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{edge}", 36);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 20); scroll.AddChild(column);
        column.AddChild(Text(view.Kicker, 18)); column.AddChild(Text(view.Title, 48)); column.AddChild(Text(view.Description, 22));
        var buttons = new List<Button>();
        if (view.CanContinue) AddButton("ContinueCampaign", view.ContinueLabel, view.Progress, true, continueCampaign);
        AddButton("NewCampaign", view.NewLabel, null, view.NewPrimary, newCampaignRequested);
        if (view.RejectedNotice is { } notice) column.AddChild(Text(notice, 18));

        void AddButton(string name, string label, string? note, bool primary, Action callback)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 20); column.AddChild(row);
            var button = new Button { Name = name, Text = label, CustomMinimumSize = new Vector2(240, 54) };
            button.AddThemeColorOverride("font_color", new Color(primary ? "#f1daa0" : "#d9dfd6"));
            buttons.Add(button); row.AddChild(button);
            if (note is not null) row.AddChild(Text(note, 20));
            button.Pressed += () =>
            {
                if (submitted || binding != generation || !IsInsideTree() || !button.IsInsideTree()) return;
                submitted = true;
                foreach (var current in buttons) current.Disabled = true;
                callback();
            };
        }
    }

    private static Label Text(string value, int size)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color("#e4e6dc"));
        return label;
    }
}
