#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Native, keyboard-focusable controls over revision-bound immutable rows.</summary>
public partial class TacticalHud : VBoxContainer
{
    private TacticalHudProjection? current;
    private long generation;
    private bool submitted;
    private TacticalHudArt? art;
    private readonly HashSet<string> expanded = new();
    public void ConfigureArt(TacticalHudArt value)
    {
        if (ReferenceEquals(art, value))
            return;
        ReleaseTextureReferences(this);
        art = value;
    }
    private static void ReleaseTextureReferences(Node node)
    {
        if (node is TextureRect texture)
            texture.Texture = null;
        if (node is Button button)
            button.Icon = null;
        foreach (var child in node.GetChildren())
            ReleaseTextureReferences(child);
    }
    public override void _ExitTree()
    {
        generation++;
        current = null;
        ReleaseTextureReferences(this);
    }
    public event Action<TacticalHudIntent>? IntentRequested;
    public TacticalHudProjection? Projection => current;
    public bool ShowHint { get; set; } = true;

    public void Apply(TacticalHudProjection projection)
    {
        var focused = GetViewport()?.GuiGetFocusOwner();
        var focusKey = focused?.GetMeta("hud_key", "").AsString();
        current = projection;
        var renderedGeneration = ++generation;
        submitted = false;
        foreach (var child in GetChildren())
        {
            ReleaseTextureReferences(child);
            RemoveChild(child);
            child.QueueFree();
        }
        AddThemeConstantOverride("separation", 14);
        foreach (var section in projection.Sections.OrderBy(section => Priority(section.Key)))
        {
            var group = new VBoxContainer { Name = section.Key, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            group.AddThemeConstantOverride("separation", 6);
            var heading = Label(section.Title, section.Key == "crown" ? 21 : section.Key == "unit" ? 19 : 14,
                section.Key is "crown" or "unit" or "forecast" or "commands" ? TacticalHudStyle.Gold : TacticalHudStyle.Muted);
            group.AddChild(heading);
            VBoxContainer rows = group;
            var collapsible = section.Key is "forecast-details" or "unit-details" or "roster" or "tile" or "stances" or "formations" or "careers";
            if (collapsible)
            {
                heading.Visible = false;
                var toggle = new Button
                {
                    Text = (expanded.Contains(section.Key) ? "−  " : "+  ") + section.Title,
                    Alignment = HorizontalAlignment.Left,
                    ToggleMode = true,
                    ButtonPressed = expanded.Contains(section.Key)
                };
                toggle.SetMeta("hud_key", section.Key + "/disclosure");
                group.AddChild(toggle);
                if (focusKey == section.Key + "/disclosure")
                    toggle.CallDeferred(Control.MethodName.GrabFocus);
                rows = new VBoxContainer { Visible = expanded.Contains(section.Key), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                group.AddChild(rows);
                var content = rows;
                toggle.Toggled += open =>
                {
                    if (open)
                        expanded.Add(section.Key);
                    else
                        expanded.Remove(section.Key);
                    content.Visible = open;
                    toggle.Text = (open ? "−  " : "+  ") + section.Title;
                };
            }
            var unitSummary = rows;
            if (section.Key == "unit" && projection.Portrait is { } portrait && art is not null)
            {
                var summary = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                group.AddChild(summary);
                var plate = new PanelContainer { Name = "InspectedPortrait", CustomMinimumSize = new Vector2(62, 76) };
                plate.AddThemeStyleboxOverride("panel", TacticalHudStyle.Panel("#18241d", 2));
                plate.AddChild(new TextureRect
                {
                    Texture = art.Portrait(portrait),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(58, 72)
                });
                summary.AddChild(plate);
                unitSummary = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                summary.AddChild(unitSummary);
            }
            foreach (var row in section.Rows)
            {
                var destination = section.Key == "unit" && row.Key is "owner" or "hp" or "done" or "selection" ? unitSummary : rows;
                if (row.Intent is { } intent)
                {
                    var button = new Button
                    {
                        Text = row.Label,
                        AutowrapMode = TextServer.AutowrapMode.WordSmart,
                        Alignment = HorizontalAlignment.Left,
                        CustomMinimumSize = new Vector2(0, 36),
                        TooltipText = row.Hint,
                        Disabled = !row.Enabled || projection.Busy,
                        ToggleMode = row.Selected,
                        ButtonPressed = row.Selected,
                        SizeFlagsHorizontal = SizeFlags.ExpandFill
                    };
                    if (row.Icon is { } icon && art is not null)
                    {
                        button.Icon = art.Icon(icon);
                        button.ExpandIcon = true;
                        button.AddThemeConstantOverride("icon_max_width", 24);
                    }
                    if (row.UnitIcon is { } unitIcon && art is not null)
                    {
                        button.Icon = art.UnitIcon(unitIcon);
                        button.ExpandIcon = true;
                        button.AddThemeConstantOverride("icon_max_width", 32);
                    }
                    button.SetMeta("hud_key", section.Key + "/" + row.Key);
                    button.Pressed += () =>
                    {
                        if (IsInsideTree() && button.IsInsideTree() && renderedGeneration == generation && !submitted && !projection.Busy && !button.Disabled && ReferenceEquals(current, projection) && row.Enabled && intent.Stamp == current.Stamp)
                        {
                            submitted = true;
                            IntentRequested?.Invoke(intent);
                        }
                    };
                    destination.AddChild(button);
                    if (focusKey == section.Key + "/" + row.Key && !button.Disabled)
                        button.CallDeferred(Control.MethodName.GrabFocus);
                }
                else
                {
                    var line = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    var rowTexture = row.UnitIcon is { } unitIcon && art is not null ? art.UnitIcon(unitIcon) : row.Icon is { } icon ? art?.Icon(icon) : null;
                    if (rowTexture is { } texture)
                        line.AddChild(new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(24, 24) });
                    var label = Label(row.Label, row.Key is "strike" or "counter" or "hp" ? 17 : 15,
                        row.Key is "recipient-dies" or "attacker-dies" ? new Color("#e5a08b") : TacticalHudStyle.Ink);
                    label.TooltipText = row.Hint;
                    line.AddChild(label);
                    destination.AddChild(line);
                    if (row.Selected)
                        line.Modulate = new Color("#e4bf69");
                    if (row.Gauge is { } gauge)
                    {
                        var bar = new ProgressBar
                        {
                            MinValue = 0,
                            MaxValue = gauge.Maximum,
                            Value = gauge.Value,
                            ShowPercentage = false,
                            CustomMinimumSize = new Vector2(0, 8)
                        };
                        if (gauge.Maximum > 0 && gauge.Value / gauge.Maximum <= .3)
                            bar.AddThemeStyleboxOverride("fill", TacticalHudStyle.Panel("#cc926f", 0, "#cc926f"));
                        destination.AddChild(bar);
                    }
                }
            }
            AddChild(group);
        }
        var hint = Label(projection.Hint, 14, TacticalHudStyle.Gold);
        hint.Name = "SelectionHint";
        hint.Visible = ShowHint;
        AddChild(hint);
    }
    private static Label Label(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static int Priority(string key) => key switch
    {
        "crown" => 0,
        "objectives" => 1,
        "forecast" => 2,
        "target" or "commands" => 3,
        "unit" => 4,
        "site" => 5,
        "roster" => 90,
        "tile" => 91,
        _ => 10
    };

}
