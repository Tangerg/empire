using Godot;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

/// <summary>Shared campaign materials and native control states, alongside the tactical command rail.</summary>
public static class CampaignUiStyle
{
    public static Theme Create()
    {
        var theme = TacticalHudStyle.Create();
        theme.DefaultFontSize = 17;
        theme.SetStylebox("panel", "PopupPanel", TacticalHudStyle.Panel("#30291f", 16));
        return theme;
    }

    /// <summary>Existing notices and destructive confirmations share readable, bounded native chrome.</summary>
    public static void ConfigureDialog(AcceptDialog dialog, string closeText)
    {
        dialog.Theme = Create();
        dialog.OkButtonText = closeText;
        dialog.DialogAutowrap = true;
        dialog.GetLabel().AddThemeConstantOverride("line_spacing", 4);
        dialog.AddThemeConstantOverride("buttons_min_width", 104);
        dialog.AddThemeConstantOverride("buttons_min_height", 40);
        dialog.AddThemeConstantOverride("buttons_separation", 16);
    }

    public static void OpenDialog(AcceptDialog dialog)
    {
        var available = dialog.GetParent().GetViewport().GetVisibleRect().Size;
        dialog.PopupCentered(new Vector2I((int)Math.Min(440, available.X - 32), (int)Math.Min(140, available.Y - 48)));
        if (dialog is ConfirmationDialog confirmation)
            confirmation.GetCancelButton().GrabFocus();
    }

    public static void InitialFocus(Button button)
    {
        var pending = true;
        void Cancel() => pending = false;
        button.TreeExiting += Cancel;
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(button))
                return;
            button.TreeExiting -= Cancel;
            if (pending && GodotObject.IsInstanceValid(button) && button.IsInsideTree() && button.IsVisibleInTree()
                && !button.Disabled && button.CanProcess() && button.GetViewport().GuiGetFocusOwner() is null)
                button.GrabFocus();
        }).CallDeferred();
    }
}

/// <summary>Nine-slice painting: source detail density stays fixed at the corners, never stretched with the text area.</summary>
public partial class DispatchPanel : PanelContainer
{
    private readonly Texture2D painting = ResourceLoader.Load<Texture2D>("res://assets/native/hud/dispatch-board-v1.png");
    public DispatchPanel()
    {
        TextureFilter = TextureFilterEnum.Linear;
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty
        {
            ContentMarginLeft = 26,
            ContentMarginRight = 26,
            ContentMarginTop = 24,
            ContentMarginBottom = 24
        });
        Resized += QueueRedraw;
    }
    public override void _Draw()
    {
        const float edge = 24;
        float[] sourceX = [34, 154, 1382, 1502];
        float[] sourceY = [65, 185, 815, 935];
        float[] targetX = [0, edge, Size.X - edge, Size.X];
        float[] targetY = [0, edge, Size.Y - edge, Size.Y];
        if (Size.X < edge * 2 || Size.Y < edge * 2)
            return;
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                DrawTextureRectRegion(painting,
                    new Rect2(targetX[x], targetY[y], targetX[x + 1] - targetX[x], targetY[y + 1] - targetY[y]),
                    new Rect2(sourceX[x], sourceY[y], sourceX[x + 1] - sourceX[x], sourceY[y + 1] - sourceY[y]));
    }
}
