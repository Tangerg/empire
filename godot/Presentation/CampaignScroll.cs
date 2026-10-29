using Godot;

namespace Empire.Presentation;

/// <summary>Keyboard reading for campaign prose. Focused child actions retain their own key handling.</summary>
public partial class CampaignScroll : ScrollContainer
{
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
