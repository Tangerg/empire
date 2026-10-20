#nullable enable
using System;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Native, keyboard-focusable controls over revision-bound immutable rows.</summary>
public partial class TacticalHud : VBoxContainer
{
    private TacticalHudProjection? current;
    private long generation;
    private bool submitted;
    private TacticalHudArt? art;
    public void ConfigureArt(TacticalHudArt value)
    {
        if (ReferenceEquals(art,value)) return;
        ReleaseTextureReferences(this);
        art?.ReleaseOwnedTextures();
        art = value;
    }
    private static void ReleaseTextureReferences(Node node)
    {
        if (node is TextureRect texture) texture.Texture = null;
        if (node is Button button) button.Icon = null;
        foreach (var child in node.GetChildren()) ReleaseTextureReferences(child);
    }
    public override void _ExitTree()
    {
        generation++; current = null;
        ReleaseTextureReferences(this);
        art?.ReleaseOwnedTextures();
    }
    public event Action<TacticalHudIntent>? IntentRequested;
    public TacticalHudProjection? Projection => current;

    public void Apply(TacticalHudProjection projection)
    {
        var focused = GetViewport()?.GuiGetFocusOwner();
        var focusKey = focused?.GetMeta("hud_key", "").AsString();
        current = projection;
        var renderedGeneration = ++generation;
        submitted = false;
        foreach (var child in GetChildren()) { ReleaseTextureReferences(child); RemoveChild(child); child.QueueFree(); }
        foreach (var section in projection.Sections)
        {
            var group = new VBoxContainer { Name = section.Key };
            group.AddChild(new Label { Text = section.Title, AutowrapMode = TextServer.AutowrapMode.WordSmart });
            if (section.Key == "unit" && projection.Portrait is { } portrait && art is not null)
            {
                var plate = new PanelContainer { Name = "InspectedPortrait", CustomMinimumSize = new Vector2(84,98) };
                plate.AddThemeStyleboxOverride("panel",new StyleBoxFlat { BgColor = new Color(portrait.TeamColor,0.42f), BorderColor = new Color("#201914"), BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1 });
                plate.AddChild(new TextureRect { Texture = art.Portrait(portrait), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(84,98) });
                group.AddChild(plate);
            }
            foreach (var row in section.Rows)
            {
                if (row.Intent is { } intent)
                {
                    var button = new Button { Text = row.Label, TooltipText = row.Hint, Disabled = !row.Enabled || projection.Busy,
                        ToggleMode = row.Selected, ButtonPressed = row.Selected, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    if (row.Icon is { } icon && art is not null) { button.Icon = art.Icon(icon); button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width",24); }
                    if (row.UnitIcon is { } unitIcon && art is not null) { button.Icon = art.UnitIcon(unitIcon); button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width",32); }
                    button.SetMeta("hud_key", section.Key + "/" + row.Key);
                    button.Pressed += () => {
                        if (IsInsideTree() && button.IsInsideTree() && renderedGeneration == generation && !submitted && !projection.Busy && !button.Disabled && ReferenceEquals(current, projection) && row.Enabled && intent.Stamp == current.Stamp)
                        {
                            submitted = true;
                            IntentRequested?.Invoke(intent);
                        }
                    };
                    group.AddChild(button);
                    if (focusKey == section.Key + "/" + row.Key && !button.Disabled) button.CallDeferred(Control.MethodName.GrabFocus);
                }
                else
                {
                    var line = new HBoxContainer();
                    var rowTexture = row.UnitIcon is { } unitIcon && art is not null ? art.UnitIcon(unitIcon) : row.Icon is { } icon ? art?.Icon(icon) : null;
                    if (rowTexture is { } texture)
                        line.AddChild(new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(24,24) });
                    line.AddChild(new Label { Text = row.Label, TooltipText = row.Hint, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill });
                    group.AddChild(line);
                    if (row.Selected) line.Modulate = new Color("#e4bf69");
                    if (row.Gauge is { } gauge) group.AddChild(new ProgressBar { MinValue = 0, MaxValue = gauge.Maximum, Value = gauge.Value, ShowPercentage = false, CustomMinimumSize = new Vector2(0,8) });
                }
            }
            AddChild(group);
        }
        AddChild(new Label { Text = projection.Hint, AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }
}
