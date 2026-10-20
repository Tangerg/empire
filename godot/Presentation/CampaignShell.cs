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
    private int generation;
    public VBoxContainer Body { get; private set; } = null!;
    public ScrollContainer Scroll { get; private set; } = null!;
    public IReadOnlyList<string> RenderingDiagnostics => diagnostics.ToArray();
    public CampaignShellView? Presented { get; private set; }

    public void Show(CampaignShellView view, Action onExit)
    {
        var binding = ++generation;
        Presented = view; diagnostics.Clear(); actions.Clear();
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("#16110e"), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var art = Texture(view.Art, "campaign backdrop");
        if (art is not null)
        {
            var backdrop = Picture(art); backdrop.Name = "CampaignBackdrop";
            backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(backdrop);
            var dim = new ColorRect { Color = new Color(0.035f, 0.028f, 0.02f, .62f), MouseFilter = MouseFilterEnum.Ignore };
            dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(dim);
        }
        var column = new VBoxContainer(); column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(column);
        column.AddThemeConstantOverride("separation", 0);
        var topbar = new PanelContainer { Name = "CampaignTopbar" }; topbar.AddThemeStyleboxOverride("panel", Panel(new Color("#18120e"), 10)); column.AddChild(topbar);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 12); topbar.AddChild(header);
        var exit = new Button { Name = "CampaignExit", Text = "‹", TooltipText = CampaignShellView.ExitLabel,
            CustomMinimumSize = new Vector2(44, 44) };
        exit.AccessibilityName = CampaignShellView.ExitLabel;
        exit.Pressed += () =>
        {
            if (binding != generation || !IsInsideTree() || !exit.IsInsideTree() || exit.Disabled) return;
            exit.Disabled = true; onExit();
        };
        header.AddChild(exit); actions.Add(exit);
        var headings = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; header.AddChild(headings);
        headings.AddChild(Text(view.Title, 14, "#bba88b", "CampaignTitle"));
        headings.AddChild(Text(view.ChapterHeading, 18, "#f6ead2", "CampaignChapter"));
        progress = new CampaignProgress { Name = "CampaignProgress", CustomMinimumSize = new Vector2(180, 32),
            SizeFlagsVertical = SizeFlags.ShrinkCenter, Completed = view.CompletedBattles, Total = view.BattleTotal };
        progress.AccessibilityName = CampaignShellView.ProgressLabel; header.AddChild(progress);
        Scroll = new ScrollContainer { Name = "NarrativeScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(Scroll);
        margins = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; Scroll.AddChild(margins);
        Body = new VBoxContainer { Name = "NarrativeBody", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        Body.AddThemeConstantOverride("separation", 18); margins.AddChild(Body);
        diagnosticsBox = new VBoxContainer { Name = "NarrativeArtDiagnostics" }; column.AddChild(diagnosticsBox);
        built = true; RefreshDiagnostics(); ResizeLayout();
    }

    public override void _Ready() => Resized += ResizeLayout;
    public override void _ExitTree() => generation++;
    private void ResizeLayout()
    {
        if (!built) return;
        var narrow = Size.X <= 760;
        progress.Visible = !narrow;
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margins.AddThemeConstantOverride($"margin_{edge}", narrow ? 16 : 32);
    }
    public void DisableNavigation() { foreach (var action in actions) action.Disabled = true; }

    public Texture2D? Texture(NarrativeArt art, string purpose)
    {
        if (string.IsNullOrWhiteSpace(art.ResourcePath))
        {
            diagnostics.Add($"Original {purpose} is not exported: {art.Source}"); RefreshDiagnostics(); return null;
        }
        if (!Godot.FileAccess.FileExists(art.ResourcePath))
        {
            diagnostics.Add($"Exported {purpose} is missing: {art.ResourcePath} (source {art.Source})"); RefreshDiagnostics(); return null;
        }
        var texture = GD.Load<Texture2D>(art.ResourcePath);
        if (texture is null) throw new InvalidOperationException($"Cannot load {purpose}: {art.ResourcePath}");
        return texture;
    }
    private void RefreshDiagnostics()
    {
        if (!IsInstanceValid(diagnosticsBox)) return;
        foreach (var child in diagnosticsBox.GetChildren()) { diagnosticsBox.RemoveChild(child); child.QueueFree(); }
        foreach (var message in diagnostics) diagnosticsBox.AddChild(Text(message, 13, "#efb76c"));
        diagnosticsBox.Visible = diagnostics.Count > 0;
    }
    public static TextureRect Picture(Texture2D texture) => new() { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
    public static Label Text(string value, int size, string color, string? name = null)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (name is not null) label.Name = name;
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color)); return label;
    }
    public static StyleBoxFlat Panel(Color color, int padding) => new()
    {
        BgColor = color, BorderColor = new Color(.85f, .71f, .44f, .3f), BorderWidthLeft = 1, BorderWidthRight = 1,
        BorderWidthTop = 1, BorderWidthBottom = 1, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
        CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding, ContentMarginBottom = padding,
    };
}

/// <summary>Source segmented progress, including the zero-battle denominator contract.</summary>
public partial class CampaignProgress : Control
{
    public int Completed { get; init; }
    public int Total { get; init; }
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var label = CampaignShell.Text($"{Completed}/{Total}", 14, "#fff4dc", "ProgressCount");
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center; AddChild(label); Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(1, 1, 1, .05f));
        var fraction = Total == 0 ? 0 : (double)Completed / Total;
        DrawRect(new Rect2(Vector2.Zero, new Vector2((float)(Math.Clamp(fraction, 0, 1) * Size.X), Size.Y)), new Color("#4d86b8"));
        for (var index = 1; index < Math.Max(1, Total); index++)
        {
            var x = Size.X * index / Math.Max(1, Total);
            DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), new Color(.91f, .77f, .51f, .2f));
        }
    }
}
