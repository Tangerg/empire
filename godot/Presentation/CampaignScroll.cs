using Godot;

namespace Empire.Presentation;

/// <summary>Keyboard reading for campaign prose. Focused child actions retain their own key handling.</summary>
public partial class CampaignScroll : ScrollContainer
{
    private Label? readingHint;
    private PanelContainer? hintSurface;
    public PanelContainer CreateReadingHint(string text)
    {
        readingHint = new Label
        {
            Name = "ReadingHint",
            Text = text,
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = MouseFilterEnum.Ignore
        };
        readingHint.AddThemeFontSizeOverride("font_size", 13);
        readingHint.AddThemeColorOverride("font_color", new Color("#c6b89c"));
        hintSurface = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        hintSurface.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("#29251ef2"),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        });
        hintSurface.AddChild(readingHint);
        return hintSurface;
    }
    public override void _Ready()
    {
        var bar = GetVScrollBar();
        bar.CustomMinimumSize = new Vector2(10, 0);
        foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
            bar.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = new Color(state == "grabber" ? "#a18d64" : "#e2bd73"),
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3
            });
        bar.Changed += () => { RefreshHint(); RevealFocusedChild(); };
        Resized += () => { RefreshHint(); RevealFocusedChild(); };
        Callable.From(RefreshHint).CallDeferred();
    }
    private void RefreshHint()
    {
        if (!IsInsideTree() || !IsInstanceValid(readingHint) || !IsInstanceValid(hintSurface))
            return;
        var hint = readingHint!;
        var unreservedHeight = GetVScrollBar().Page + (hintSurface!.Visible ? hintSurface.Size.Y +
            (hintSurface.GetParent() is BoxContainer parent ? parent.GetThemeConstant("separation") : 0) : 0);
        var visible = hint.Text.Length > 0 && GetVScrollBar().MaxValue > unreservedHeight + 1;
        if (hint.Visible == visible)
            return;
        hint.Visible = hintSurface.Visible = visible;
        RevealFocusedChild();
    }
    private void RevealFocusedChild()
    {
        var focused = GetViewport()?.GuiGetFocusOwner();
        if (focused is null || !IsAncestorOf(focused))
            return;
        Callable.From(() =>
        {
            if (IsInstanceValid(this) && IsInsideTree() && IsInstanceValid(focused) && focused.HasFocus() && IsAncestorOf(focused))
                EnsureControlVisible(focused);
        }).CallDeferred();
    }
    public CampaignScroll()
    {
        FollowFocus = true;
        FocusMode = FocusModeEnum.All;
        HorizontalScrollMode = ScrollMode.Disabled;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }
    public override void _GuiInput(InputEvent input)
    {
        if (!HasFocus() || input is not InputEventKey { Pressed: true } key || key.AltPressed || key.CtrlPressed || key.MetaPressed)
            return;
        var maximum = Math.Max(0, GetVScrollBar().MaxValue - GetVScrollBar().Page);
        var target = key.Keycode switch
        {
            Key.Down => ScrollVertical + 48,
            Key.Up => ScrollVertical - 48,
            Key.Pagedown => ScrollVertical + Size.Y * .85,
            Key.Pageup => ScrollVertical - Size.Y * .85,
            Key.Home => 0,
            Key.End => maximum,
            _ => double.NaN
        };
        if (double.IsNaN(target))
            return;
        ScrollVertical = (int)Math.Clamp(target, 0, maximum);
        AcceptEvent();
    }
    public override void _Draw()
    {
        if (HasFocus())
            DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), new Color("#d5bc7f"), false, 2);
    }
}
