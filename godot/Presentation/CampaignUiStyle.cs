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
        var theme = Create();
        var frame = TacticalHudStyle.Panel("#30291f", 8, "#ad9160");
        frame.ExpandMarginTop = 34;
        frame.ShadowColor = new Color("#080a0866");
        frame.ShadowSize = 8;
        frame.ShadowOffset = new Vector2(0, 3);
        theme.SetStylebox("embedded_border", "Window", frame);
        var unfocused = (StyleBoxFlat)frame.Duplicate();
        unfocused.BorderColor = TacticalHudStyle.Line;
        theme.SetStylebox("embedded_unfocused_border", "Window", unfocused);
        theme.SetConstant("title_height", "Window", 34);
        theme.SetConstant("close_h_offset", "Window", 32);
        theme.SetConstant("close_v_offset", "Window", 29);
        theme.SetFontSize("title_font_size", "Window", 18);
        theme.SetColor("title_color", "Window", TacticalHudStyle.Gold);
        theme.SetIcon("close", "Window", CloseGlyph(false));
        theme.SetIcon("close_pressed", "Window", CloseGlyph(true));
        theme.SetStylebox("panel", "AcceptDialog", new StyleBoxEmpty
        {
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 22,
            ContentMarginBottom = 22
        });
        dialog.Theme = theme;
        dialog.TransparentBg = true;
        var surfaceLayer = new CanvasLayer { Name = "DialogSurfaceLayer", Layer = -1 };
        var surface = new DispatchPanel { Name = "DialogSurface", MouseFilter = Control.MouseFilterEnum.Ignore };
        dialog.AddChild(surfaceLayer);
        surfaceLayer.AddChild(surface);
        void FitSurface()
        {
            if (GodotObject.IsInstanceValid(surface) && surface.IsInsideTree())
                surface.Size = dialog.GetVisibleRect().Size;
        }
        dialog.SizeChanged += FitSurface;
        surface.Ready += FitSurface;
        InstallBackdrop(dialog);
        if (dialog is ConfirmationDialog confirmation)
        {
            var destructive = confirmation.GetOkButton();
            destructive.AddThemeStyleboxOverride("normal", TacticalHudStyle.Panel("#382923", 9, "#795844"));
            destructive.AddThemeStyleboxOverride("hover", TacticalHudStyle.Panel("#50352b", 9, "#b58160"));
            destructive.AddThemeStyleboxOverride("pressed", TacticalHudStyle.Panel("#634335", 9, "#dfb49a"));
            destructive.AddThemeColorOverride("font_color", new Color("#dfb49a"));
        }
        dialog.OkButtonText = closeText;
        dialog.DialogAutowrap = true;
        dialog.GetLabel().AddThemeConstantOverride("line_spacing", 4);
        dialog.AddThemeConstantOverride("buttons_min_width", 104);
        dialog.AddThemeConstantOverride("buttons_min_height", 40);
        dialog.AddThemeConstantOverride("buttons_separation", 16);
    }

    private static Texture2D CloseGlyph(bool pressed)
    {
        var fill = pressed ? "#78613c" : "#30291f";
        var line = pressed ? "#fff5d7" : "#dec383";
        using var image = new Image();
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'><rect x='1' y='1' width='22' height='22' rx='3' fill='{fill}' stroke='{line}'/><path d='M7 7L17 17M17 7L7 17' stroke='{line}' stroke-width='2' stroke-linecap='round'/></svg>";
        if (image.LoadSvgFromString(svg) != Error.Ok)
            throw new InvalidOperationException("Native close glyph could not be rendered.");
        return ImageTexture.CreateFromImage(image);
    }

    private static void InstallBackdrop(AcceptDialog dialog)
    {
        CanvasLayer? layer = null;
        dialog.AboutToPopup += () =>
        {
            if (!GodotObject.IsInstanceValid(layer))
            {
                layer = new CanvasLayer { Name = "DialogBackdrop", Layer = 100, Visible = false };
                var shade = new ColorRect { Name = "DialogShade", Color = new Color("#10120e4d"), MouseFilter = Control.MouseFilterEnum.Ignore };
                dialog.GetParent().AddChild(layer);
                layer.AddChild(shade);
                shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            layer!.Visible = true;
        };
        dialog.VisibilityChanged += () =>
        {
            if (GodotObject.IsInstanceValid(layer))
                layer!.Visible = GodotObject.IsInstanceValid(dialog) && dialog.Visible;
        };
        dialog.TreeExiting += () =>
        {
            if (GodotObject.IsInstanceValid(layer))
            {
                layer!.Visible = false;
                layer.QueueFree();
            }
            layer = null;
        };
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
