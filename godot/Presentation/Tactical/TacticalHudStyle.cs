#nullable enable
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Shared native battle chrome; terrain and team art keep their own palette.</summary>
public static class TacticalHudStyle
{
    public static readonly Color Ink = new("#ece5d5");
    public static readonly Color Muted = new("#a9b0a5");
    public static readonly Color Gold = new("#d5bb7b");
    public static readonly Color Line = new("#475049");

    public static StyleBoxFlat Panel(string fill, int padding = 12, string border = "#475049") => new()
    {
        BgColor = new Color(fill),
        BorderColor = new Color(border),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ContentMarginLeft = padding,
        ContentMarginRight = padding,
        ContentMarginTop = padding,
        ContentMarginBottom = padding
    };

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 15 };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", new Color("#fff5d7"));
        theme.SetColor("font_pressed_color", "Button", new Color("#fff5d7"));
        theme.SetColor("font_disabled_color", "Button", new Color("#747d74"));
        theme.SetStylebox("normal", "Button", Panel("#29332e", 9));
        theme.SetStylebox("hover", "Button", Panel("#35453b", 9, "#b4a477"));
        theme.SetStylebox("pressed", "Button", Panel("#43503a", 9, "#d5bb7b"));
        theme.SetStylebox("disabled", "Button", Panel("#202723", 9, "#303a33"));
        var focus = Panel("#00000000", 0, "#f1d89a");
        focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = focus.BorderWidthBottom = 2;
        theme.SetStylebox("focus", "Button", focus);
        theme.SetStylebox("background", "ProgressBar", Panel("#101b17", 0, "#101b17"));
        theme.SetStylebox("fill", "ProgressBar", Panel("#8aa67c", 0, "#8aa67c"));
        theme.SetStylebox("scroll", "VScrollBar", Panel("#18221d", 3, "#18221d"));
        theme.SetStylebox("grabber", "VScrollBar", Panel("#657265", 3, "#657265"));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Panel("#a4b096", 3));
        theme.SetStylebox("grabber_pressed", "VScrollBar", Panel("#d5bb7b", 3));
        theme.SetConstant("separation", "VBoxContainer", 6);
        theme.SetConstant("separation", "HBoxContainer", 8);
        return theme;
    }
}
