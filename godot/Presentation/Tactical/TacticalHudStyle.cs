#nullable enable
using Godot;
using System.Linq;

namespace Empire.Presentation.Tactical;

/// <summary>Shared native battle chrome; terrain and team art keep their own palette.</summary>
public static class TacticalHudStyle
{
    public static readonly Color Ink = new("#efe2bf");
    public static readonly Color Muted = new("#b6aa8e");
    public static readonly Color Gold = new("#dec383");
    public static readonly Color Line = new("#69573d");

    public static StyleBoxFlat Panel(string fill, int padding = 12, string border = "#69573d") => new()
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

    public static TextureRect PaintedImage(string fileName, Rect2 region) => new()
    {
        Texture = new AtlasTexture
        {
            Atlas = ResourceLoader.Load<Texture2D>("res://assets/native/hud/" + fileName),
            Region = region
        },
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public static Control CommandTray()
    {
        const float density = .15f;
        var surface = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        var frame = new NinePatchRect
        {
            Texture = new AtlasTexture
            {
                Atlas = ResourceLoader.Load<Texture2D>("res://assets/native/hud/command-tray-v1.png"),
                Region = new Rect2(12, 136, 2148, 426)
            },
            PatchMarginLeft = 140,
            PatchMarginRight = 140,
            PatchMarginTop = 80,
            PatchMarginBottom = 80,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Scale = Vector2.One * density,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        surface.AddChild(frame);
        surface.Resized += () => frame.Size = surface.Size / density;
        return surface;
    }

    public static void OrderBadge(Button button, bool prominent = false)
    {
        var empty = new StyleBoxEmpty
        {
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 4,
            ContentMarginBottom = 8
        };
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" })
            button.AddThemeStyleboxOverride(state, empty);
        var plate = new TacticalOrderBadge { Prominent = prominent, MouseFilter = Control.MouseFilterEnum.Ignore, ShowBehindParent = true };
        button.AddChild(plate);
        plate.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        void Update()
        {
            plate.Disabled = button.Disabled;
            plate.Highlighted = button.IsHovered() || button.ButtonPressed;
            plate.QueueRedraw();
        }
        button.MouseEntered += Update;
        button.MouseExited += Update;
        button.ButtonDown += Update;
        button.ButtonUp += Update;
        button.Ready += Update;
    }

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 15 };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", new Color("#fff5d7"));
        theme.SetColor("font_pressed_color", "Button", new Color("#fff5d7"));
        theme.SetColor("font_disabled_color", "Button", new Color("#747d74"));
        theme.SetStylebox("normal", "Button", Panel("#493b29", 9));
        theme.SetStylebox("hover", "Button", Panel("#655033", 9, "#b4a477"));
        theme.SetStylebox("pressed", "Button", Panel("#78613c", 9, "#d5bb7b"));
        theme.SetStylebox("disabled", "Button", Panel("#29261e", 9, "#484133"));
        var focus = Panel("#00000000", 0, "#f1d89a");
        focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = focus.BorderWidthBottom = 2;
        theme.SetStylebox("focus", "Button", focus);
        theme.SetStylebox("background", "ProgressBar", Panel("#211d16", 0, "#211d16"));
        theme.SetStylebox("fill", "ProgressBar", Panel("#8b9b64", 0, "#8b9b64"));
        theme.SetStylebox("scroll", "VScrollBar", Panel("#29261e", 3, "#29261e"));
        theme.SetStylebox("grabber", "VScrollBar", Panel("#756344", 3, "#756344"));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Panel("#b59b6a", 3));
        theme.SetStylebox("grabber_pressed", "VScrollBar", Panel("#d5bb7b", 3));
        theme.SetConstant("separation", "VBoxContainer", 6);
        theme.SetConstant("separation", "HBoxContainer", 8);
        return theme;
    }
}

public partial class TacticalOrderBadge : Control
{
    public bool Prominent
    {
        get; init;
    }
    public bool Disabled
    {
        get; set;
    }
    public bool Highlighted
    {
        get; set;
    }
    public override void _Ready() => Resized += QueueRedraw;
    public override void _Draw()
    {
        var w = Size.X;
        var h = Size.Y;
        var edge = new Vector2[] { new(7, 1), new(w - 7, 1), new(w - 1, 7), new(w - 3, h - 9), new(w * .5f, h - 1), new(3, h - 9), new(1, 7) };
        DrawColoredPolygon(edge, new Color(Disabled ? "#302d25" : Highlighted ? "#716344" : Prominent ? "#5c633a" : "#474637"));
        DrawPolyline(edge.Append(edge[0]).ToArray(), new Color(Disabled ? "#58503c" : "#b59a62"), 2, true);
        DrawLine(new(10, 5), new(w - 10, 5), new Color(Disabled ? "#494434" : "#d4b878"), 1, true);
    }
}
