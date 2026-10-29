using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

/// <summary>The campaign bar, source backdrop and scrolling body. No campaign state transitions.</summary>
public partial class CampaignShell : Control
{
    private readonly List<string> diagnostics = new();
    private readonly List<Button> actions = new();
    private CampaignProgress progress = null!;
    private MarginContainer margins = null!;
    private VBoxContainer diagnosticsBox = null!;
    private bool built;
    private Button? exitButton;
    public HBoxContainer Footer { get; private set; } = null!;
    private int generation;
    public VBoxContainer Body { get; private set; } = null!;
    public ScrollContainer Scroll { get; private set; } = null!;
    public IReadOnlyList<string> RenderingDiagnostics => diagnostics.ToArray();
    public CampaignShellView? Presented
    {
        get; private set;
    }

    public void Show(CampaignShellView view, Action onExit)
    {
        var binding = ++generation;
        Theme = CampaignUiStyle.Create();
        Presented = view;
        diagnostics.Clear();
        actions.Clear();
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("#16110e"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var art = Texture(view.Art, "campaign backdrop");
        if (art is not null)
        {
            var backdrop = Picture(art);
            backdrop.Name = "CampaignBackdrop";
            backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(backdrop);
            var dim = new ColorRect { Color = new Color(0.035f, 0.028f, 0.02f, .62f), MouseFilter = MouseFilterEnum.Ignore };
            dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(dim);
        }
        var column = new VBoxContainer();
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(column);
        column.AddThemeConstantOverride("separation", 0);
        var topbar = new PanelContainer { Name = "CampaignTopbar" };
        topbar.AddThemeStyleboxOverride("panel", new StyleBoxEmpty { ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 8 });
        column.AddChild(topbar);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        topbar.AddChild(header);
        var pennant = new PanelContainer { CustomMinimumSize = new Vector2(300, 0) };
        pennant.AddThemeStyleboxOverride("panel", Empire.Presentation.Tactical.TacticalHudStyle.Panel("#29333c", 10, "#716247"));
        header.AddChild(pennant);
        var headingContent = new HBoxContainer();
        headingContent.AddThemeConstantOverride("separation", 12);
        pennant.AddChild(headingContent);
        var exit = new Button
        {
            Name = "CampaignExit",
            Text = "‹",
            TooltipText = view.ExitLabel,
            CustomMinimumSize = new Vector2(44, 44)
        };
        exitButton = exit;
        exit.AccessibilityName = view.ExitLabel;
        exit.Pressed += () =>
        {
            if (binding != generation || !IsInsideTree() || !exit.IsInsideTree() || exit.Disabled)
                return;
            exit.Disabled = true;
            onExit();
        };
        headingContent.AddChild(exit);
        actions.Add(exit);
        var headings = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingContent.AddChild(headings);
        headings.AddChild(Text(view.Title, 14, "#bba88b", "CampaignTitle"));
        headings.AddChild(Text(view.ChapterHeading, 18, "#f6ead2", "CampaignChapter"));
        progress = new CampaignProgress
        {
            Name = "CampaignProgress",
            CustomMinimumSize = new Vector2(180, 32),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Completed = view.CompletedBattles,
            Total = view.BattleTotal,
            Caption = view.ProgressLabel
        };
        progress.AccessibilityName = view.ProgressLabel;
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        header.AddChild(progress);
        Scroll = new CampaignScroll
        {
            Name = "NarrativeScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddChild(Scroll);
        margins = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Scroll.AddChild(margins);
        Body = new VBoxContainer { Name = "NarrativeBody", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Body.AddThemeConstantOverride("separation", 18);
        margins.AddChild(Body);
        column.AddChild(((CampaignScroll)Scroll).CreateReadingHint(view.ReadingHint));
        Footer = new HBoxContainer { Name = "CampaignFooter" };
        Footer.AddThemeConstantOverride("separation", 16);
        column.AddChild(Footer);
        diagnosticsBox = new VBoxContainer { Name = "NarrativeArtDiagnostics" };
        column.AddChild(diagnosticsBox);
        built = true;
        RefreshDiagnostics();
        ResizeLayout();
    }

    public override void _Ready() => Resized += ResizeLayout;
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }
            && exitButton is { Disabled: false } button && button.IsVisibleInTree())
        {
            GetViewport().SetInputAsHandled();
            button.EmitSignal(Button.SignalName.Pressed);
        }
    }
    public override void _ExitTree() => generation++;
    private void ResizeLayout()
    {
        if (!built)
            return;
        var narrow = Size.X <= 760;
        progress.Visible = !narrow;
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
            margins.AddThemeConstantOverride($"margin_{edge}", narrow ? 16 : 32);
    }
    public void DisableNavigation()
    {
        foreach (var action in actions)
            action.Disabled = true;
    }

    public Texture2D? Texture(NarrativeArt art, string purpose)
    {
        if (string.IsNullOrWhiteSpace(art.ResourcePath))
        {
            diagnostics.Add($"Original {purpose} is not exported: {art.Source}");
            RefreshDiagnostics();
            return null;
        }
        if (!ResourceLoader.Exists(art.ResourcePath))
        {
            diagnostics.Add($"Exported {purpose} is missing: {art.ResourcePath} (source {art.Source})");
            RefreshDiagnostics();
            return null;
        }
        var texture = GD.Load<Texture2D>(art.ResourcePath);
        if (texture is null)
            throw new InvalidOperationException($"Cannot load {purpose}: {art.ResourcePath}");
        return texture;
    }
    private void RefreshDiagnostics()
    {
        if (!IsInstanceValid(diagnosticsBox))
            return;
        foreach (var child in diagnosticsBox.GetChildren())
        {
            diagnosticsBox.RemoveChild(child);
            child.QueueFree();
        }
        if (diagnostics.Count > 0)
            diagnosticsBox.AddChild(Text(Presented!.ArtworkFailureLabel, 13, "#efb76c"));
        diagnosticsBox.Visible = diagnostics.Count > 0;
    }
    public static TextureRect Picture(Texture2D texture) => new()
    {
        Texture = texture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        MouseFilter = MouseFilterEnum.Ignore
    };
    public static Label Text(string value, int size, string color, string? name = null)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (name is not null)
            label.Name = name;
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }
    public static StyleBoxFlat Panel(Color color, int padding) => new()
    {
        BgColor = color,
        BorderColor = new Color(.85f, .71f, .44f, .3f),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        ContentMarginLeft = padding,
        ContentMarginRight = padding,
        ContentMarginTop = padding,
        ContentMarginBottom = padding,
    };
}

/// <summary>Compact source progress count; no invented mission or turn state.</summary>
public partial class CampaignProgress : Control
{
    private readonly StyleBox progressSurface = Empire.Presentation.Tactical.TacticalHudStyle.Panel("#30291f", 8);
    public string Caption { get; init; } = "";
    public int Completed
    {
        get; init;
    }
    public int Total
    {
        get; init;
    }
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var row = new HBoxContainer();
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", 12);
        AddChild(row);
        row.AddChild(CampaignShell.Text(Caption, 14, "#d8c6a4"));
        var label = CampaignShell.Text($"{Completed}/{Total}", 16, "#fff4dc", "ProgressCount");
        label.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(label);
        Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        DrawStyleBox(progressSurface, new Rect2(new Vector2(-8, -4), Size + new Vector2(16, 8)));
    }
}
