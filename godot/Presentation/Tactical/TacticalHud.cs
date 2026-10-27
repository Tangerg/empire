#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Native, keyboard-focusable controls over revision-bound immutable rows.</summary>
public partial class TacticalHud : Control
{
    private TacticalHudProjection? current;
    private long generation;
    private bool submitted;
    private TacticalHudArt? art;
    private ScrollContainer? detailScroll;
    private PopupPanel? detailPopup;
    private readonly HashSet<string> expanded = new() { "objectives" };
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
        var focused = detailPopup?.GuiGetFocusOwner() ?? GetViewport()?.GuiGetFocusOwner();
        var focusKey = focused?.GetMeta("hud_key", "").AsString();
        var scrollPosition = detailScroll?.ScrollVertical ?? 0;
        var dossierOpen = detailPopup?.Visible == true;
        current = projection;
        var renderedGeneration = ++generation;
        submitted = false;
        foreach (var child in GetChildren())
        {
            ReleaseTextureReferences(child);
            RemoveChild(child);
            child.QueueFree();
        }
        MouseFilter = MouseFilterEnum.Ignore;
        var details = new VBoxContainer { Name = "Details", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 10);
        detailPopup = new PopupPanel { Name = "UnitDossier", Size = new Vector2I(360, 340) };
        detailPopup.AddThemeStyleboxOverride("panel", TacticalHudStyle.Panel("#30291e", 14, "#756344"));
        AddChild(detailPopup);
        detailScroll = new ScrollContainer
        {
            Name = "DetailScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(328, 300)
        };
        detailScroll.AddChild(details);
        detailPopup.AddChild(detailScroll);
        var forecast = projection.Sections.FirstOrDefault(section => section.Key == "forecast");
        var mainActions = projection.Sections.FirstOrDefault(section => section.Key is "commands" or "target" or "deployment" or "recruit" or "tactics");
        if (projection.Sections.Any(section => section.Key == "unit"))
        {
            var portraitTray = TacticalHudStyle.CommandTray();
            portraitTray.Name = "PortraitTray";
            AddChild(portraitTray);
            Place(portraitTray, 0, 1, 0, 1, 14, -126, 274, -8);
        }
        var actionCount = mainActions?.Key == "target" ? forecast is null ? 2 : 0 : mainActions?.Rows.Count(row => row.Key != "cancel")
            ?? projection.Sections.Count(section => section.Key is "unit-details" or "stances" or "formations");
        var actionSlots = Math.Max(2, Math.Min(4, actionCount));
        if (actionCount > 0)
        {
            var actionTray = TacticalHudStyle.CommandTray();
            actionTray.Name = "OrderTray";
            AddChild(actionTray);
            Place(actionTray, 0, 1, 0, 1, 278, -126, 278 + actionSlots * 106 + 28, -8);
        }
        var target = projection.Sections.FirstOrDefault(section => section.Key == "target");
        foreach (var section in projection.Sections.OrderBy(section => Priority(section.Key)))
        {
            if (section.Key == "target" && forecast is not null)
                continue;
            var group = new VBoxContainer { Name = section.Key, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            group.AddThemeConstantOverride("separation", section.Key is "crown" or "unit" ? 2 : 5);
            if (section.Key == "crown")
                MountPlate(group, false, 0, 0, 0, 0, 14, 8, 350, 108);
            else if (section.Key == "objectives")
                MountPlate(group, true, 1, 0, 1, 0, -326, 58, -14, 160);
            else if (section.Key == "unit")
            {
                AddChild(group);
                Place(group, 0, 1, 0, 1, 120, -118, 264, -35);
            }
            else if (section.Key == "forecast")
                MountPlate(group, false, .5f, 1, .5f, 1, -182, -266, 182, -138);
            else if (section == mainActions)
            {
                AddChild(group);
                Place(group, 0, 1, 0, 1, 292, -113, 292 + actionSlots * 106, -35);
            }
            else
                details.AddChild(group);
            var header = new HBoxContainer { Name = "Heading", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var heading = Label(section.Title, section.Key is "crown" or "unit" ? 19 : 13,
                section.Key == "objectives" ? new Color("#453921") : section.Key is "crown" or "unit" or "forecast" or "commands" ? TacticalHudStyle.Gold : TacticalHudStyle.Muted);
            header.AddChild(heading);
            group.AddChild(header);
            var collapsible = section.Key is "objectives" or "forecast-details" or "unit-details" or "roster" or "tile" or "stances" or "formations" or "careers";
            VBoxContainer rows = group;
            if (collapsible)
            {
                header.Visible = false;
                var summary = section.Key == "objectives" ? section.Rows.FirstOrDefault(row => row.Hint.Length > 0) : null;
                var title = summary is null ? section.Title : summary.Label;
                var toggle = new Button
                {
                    Text = (expanded.Contains(section.Key) ? "−  " : "+  ") + title,
                    TooltipText = summary?.Hint ?? "",
                    Alignment = HorizontalAlignment.Left,
                    ToggleMode = true,
                    ButtonPressed = expanded.Contains(section.Key)
                };
                CompactButton(toggle);
                if (section.Key == "objectives")
                {
                    toggle.AddThemeColorOverride("font_color", new Color("#453921"));
                    toggle.AddThemeColorOverride("font_pressed_color", new Color("#453921"));
                    toggle.AddThemeStyleboxOverride("normal", TacticalHudStyle.Panel("#00000000", 2, "#00000000"));
                    toggle.AddThemeStyleboxOverride("pressed", TacticalHudStyle.Panel("#00000000", 2, "#00000000"));
                }
                toggle.SetMeta("hud_key", section.Key + "/disclosure");
                group.AddChild(toggle);
                RestoreFocus(toggle, section.Key + "/disclosure");
                rows = new VBoxContainer { Name = "Content", Visible = expanded.Contains(section.Key), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                rows.AddThemeConstantOverride("separation", 4);
                group.AddChild(rows);
                var content = rows;
                toggle.Toggled += open =>
                {
                    if (generation != renderedGeneration || !toggle.IsInsideTree())
                        return;
                    if (open)
                        expanded.Add(section.Key);
                    else
                        expanded.Remove(section.Key);
                    content.Visible = open;
                    toggle.Text = (open ? "−  " : "+  ") + title;
                };
            }
            if (section.Key == "forecast" && target is not null)
                foreach (var row in target.Rows)
                    header.AddChild(Order(row, target.Key, compact: true));
            if (section.Key == "target")
            {
                foreach (var row in section.Rows)
                    header.AddChild(Order(row, section.Key, compact: true));
                continue;
            }
            if (section.Key == "unit")
            {
                if (projection.Portrait is { } portrait && art is not null)
                {
                    var portraitPlate = new PanelContainer { Name = "PortraitMedallion" };
                    var rim = TacticalHudStyle.Panel("#37382b", 3, "#b29862");
                    rim.CornerRadiusTopLeft = rim.CornerRadiusTopRight = rim.CornerRadiusBottomLeft = rim.CornerRadiusBottomRight = 36;
                    rim.BorderWidthLeft = rim.BorderWidthRight = rim.BorderWidthTop = rim.BorderWidthBottom = 2;
                    portraitPlate.AddThemeStyleboxOverride("panel", rim);
                    AddChild(portraitPlate);
                    Place(portraitPlate, 0, 1, 0, 1, 28, -144, 108, -64);
                    portraitPlate.AddChild(new TextureRect
                    {
                        Name = "InspectedPortrait",
                        Texture = art.Portrait(portrait),
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        CustomMinimumSize = new Vector2(74, 74)
                    });
                }
                rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                rows.AddThemeConstantOverride("separation", 2);
                group.AddChild(rows);
                var identity = section.Rows.FirstOrDefault(row => row.Key == "selection");
                if (identity is not null)
                {
                    var state = Label(identity.Label, 12, TacticalHudStyle.Muted);
                    state.TooltipText = identity.Hint;
                    state.AutowrapMode = TextServer.AutowrapMode.Off;
                    rows.AddChild(state);
                }
            }
            var pairedForecast = section.Key == "forecast" && section.Rows.Any(row => row.Key == "attacker");
            if (pairedForecast)
            {
                var pair = new HBoxContainer { Name = "Exchange", SizeFlagsHorizontal = SizeFlags.ExpandFill };
                pair.AddThemeConstantOverride("separation", 12);
                group.AddChild(pair);
                foreach (var (identity, damage) in new[] { ("attacker", "counter"), ("recipient", "strike") })
                {
                    var column = new VBoxContainer { Name = identity, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
                    column.AddThemeConstantOverride("separation", 4);
                    pair.AddChild(column);
                    AddReadOnly(column, section.Rows.Single(row => row.Key == identity), 14);
                    AddReadOnly(column, section.Rows.Single(row => row.Key == damage), 15);
                }
            }
            GridContainer? commandGrid = null;
            if (section == mainActions && section.Key != "target")
            {
                commandGrid = new GridContainer { Name = "Orders", Columns = Math.Max(1, Math.Min(4, section.Rows.Count(row => row.Key != "cancel"))), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
                rows.AddChild(commandGrid);
            }
            foreach (var row in section.Rows)
            {
                if (pairedForecast && row.Key is "attacker" or "recipient" or "strike" or "counter")
                    continue;
                if (section.Key == "unit" && row.Key is "selection" or "owner")
                    continue;
                if (section.Key == "objectives" && row.Hint.Length > 0)
                    continue;
                if (row.Intent is not null)
                {
                    var button = Order(row, section.Key, compact: row.Key == "cancel");
                    if (row.Key == "cancel" && commandGrid is not null)
                        header.AddChild(button);
                    else if (commandGrid is not null)
                    {
                        var dense = section.Rows.Count(value => value.Key != "cancel") > 4;
                        button.CustomMinimumSize = new Vector2(98, dense ? 30 : 54);
                        button.AddThemeFontSizeOverride("font_size", dense ? 12 : 14);
                        button.Alignment = HorizontalAlignment.Center;
                        button.IconAlignment = dense ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                        button.VerticalIconAlignment = dense ? VerticalAlignment.Center : VerticalAlignment.Top;
                        if (dense)
                            button.AddThemeConstantOverride("icon_max_width", 18);
                        TacticalHudStyle.OrderBadge(button);
                        commandGrid.AddChild(button);
                    }
                    else
                        rows.AddChild(button);
                }
                else
                {
                    AddReadOnly(rows, row, section.Key == "crown" ? 13 : section.Key == "objectives" ? 13 : row.Key == "hp" ? 16 : 14);
                    if (section.Key == "objectives")
                        foreach (var label in rows.GetChildren().OfType<HBoxContainer>().SelectMany(line => line.GetChildren().OfType<Label>()))
                            label.AddThemeColorOverride("font_color", new Color("#453921"));
                }
            }
        }
        if (mainActions is null)
        {
            var shortcuts = new HBoxContainer { Name = "ContextTabs" };
            AddChild(shortcuts);
            Place(shortcuts, 0, 1, 0, 1, 292, -104, 292 + Math.Min(3, actionCount) * 106, -46);
            foreach (var section in projection.Sections.Where(section => section.Key is "unit-details" or "stances" or "formations").Take(3))
            {
                var tab = new Button { Text = section.Title, SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
                tab.AddThemeFontSizeOverride("font_size", 14);
                TacticalHudStyle.OrderBadge(tab);
                shortcuts.AddChild(tab);
                var popup = detailPopup;
                tab.Pressed += () =>
                {
                    if (generation != renderedGeneration || !IsInsideTree())
                        return;
                    expanded.Add(section.Key);
                    var content = (VBoxContainer)details.FindChild(section.Key, true, false);
                    content.GetNode<VBoxContainer>("Content").Visible = true;
                    var toggle = content.GetChildren().OfType<Button>().Single();
                    toggle.ButtonPressed = true;
                    popup.PopupCentered();
                    toggle.GrabFocus();
                };
            }
        }
        if (projection.Sections.Any(section => section.Key is "unit-details" or "site" or "structure" or "stances" or "formations" or "roster"))
        {
            var dossierTitle = projection.Sections.FirstOrDefault(section => section.Key is "site" or "structure" or "unit-details")?.Title
                ?? projection.Sections.FirstOrDefault(section => section.Key == "roster")?.Title ?? "";
            var dossier = new Button { Name = "DossierButton", Text = dossierTitle };
            CompactButton(dossier);
            AddChild(dossier);
            Place(dossier, 0, 1, 0, 1, 28, -34, projection.Portrait is null ? 148 : 264, -9);
            var popup = detailPopup;
            dossier.Pressed += () =>
            {
                if (generation == renderedGeneration && IsInsideTree())
                    popup.PopupCentered();
            };
        }
        var hint = Label(projection.Hint, 13, TacticalHudStyle.Gold);
        hint.Name = "SelectionHint";
        hint.Visible = ShowHint;
        hint.TooltipText = projection.Hint;
        hint.MouseFilter = MouseFilterEnum.Pass;
        hint.MaxLinesVisible = 1;
        hint.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        hint.AddThemeColorOverride("font_shadow_color", new Color("#17120c"));
        hint.AddThemeConstantOverride("shadow_outline_size", 2);
        if (forecast is not null)
        {
            var forecastGroup = (VBoxContainer)FindChild("forecast", true, false);
            forecastGroup.AddChild(hint);
        }
        else
        {
            AddChild(hint);
            Place(hint, 0, 1, 0, 1, 292, -32, 292 + actionSlots * 106, -10);
        }
        if (dossierOpen)
            detailPopup.PopupCentered();
        var mountedScroll = detailScroll;
        Callable.From(() =>
        {
            if (generation == renderedGeneration && IsInsideTree() && mountedScroll.IsInsideTree())
                mountedScroll.ScrollVertical = scrollPosition;
        }).CallDeferred();

        void RestoreFocus(Button button, string key)
        {
            if (focusKey != key || button.Disabled)
                return;
            Callable.From(() =>
            {
                if (generation == renderedGeneration && IsInsideTree() && button.IsInsideTree() && !button.Disabled)
                    button.GrabFocus();
            }).CallDeferred();
        }
        Button Order(TacticalHudRow row, string sectionKey, bool compact = false)
        {
            var intent = row.Intent ?? throw new InvalidOperationException("An order requires a revision-bound intent.");
            var button = new Button
            {
                Text = row.Label,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Alignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, compact ? 26 : 34),
                TooltipText = row.Hint,
                Disabled = !row.Enabled || projection.Busy,
                ToggleMode = row.Selected,
                ButtonPressed = row.Selected,
                SizeFlagsHorizontal = compact ? SizeFlags.ShrinkEnd : SizeFlags.ExpandFill
            };
            if (compact)
            {
                CompactButton(button);
                button.AutowrapMode = TextServer.AutowrapMode.Off;
            }
            if (row.Icon is { } icon && art is not null)
            {
                button.Icon = art.Icon(icon);
                button.ExpandIcon = true;
                button.AddThemeConstantOverride("icon_max_width", 22);
            }
            if (row.UnitIcon is { } unitIcon && art is not null)
            {
                button.Icon = art.UnitIcon(unitIcon);
                button.ExpandIcon = true;
                button.AddThemeConstantOverride("icon_max_width", 30);
            }
            var key = sectionKey + "/" + row.Key;
            button.SetMeta("hud_key", key);
            button.Pressed += () =>
            {
                if (IsInsideTree() && button.IsInsideTree() && renderedGeneration == generation && !submitted && !projection.Busy && !button.Disabled && ReferenceEquals(current, projection) && row.Enabled && intent.Stamp == current.Stamp)
                {
                    submitted = true;
                    IntentRequested?.Invoke(intent);
                }
            };
            RestoreFocus(button, key);
            return button;
        }
    }

    private static void Place(Control control, float left, float top, float right, float bottom, float x1, float y1, float x2, float y2)
    {
        control.AnchorLeft = left;
        control.AnchorTop = top;
        control.AnchorRight = right;
        control.AnchorBottom = bottom;
        control.OffsetLeft = x1;
        control.OffsetTop = y1;
        control.OffsetRight = x2;
        control.OffsetBottom = y2;
    }

    private void MountPlate(VBoxContainer content, bool paper, float left, float top, float right, float bottom, float x1, float y1, float x2, float y2)
    {
        var panel = new Control { Name = content.Name + "Plate", MouseFilter = MouseFilterEnum.Stop, GrowVertical = bottom == 1 ? GrowDirection.Begin : GrowDirection.End };
        AddChild(panel);
        Place(panel, left, top, right, bottom, x1, y1, x2, y2);
        Control plate = content.Name == "crown"
            ? TacticalHudStyle.PaintedImage("battle-banner-v1.png", new Rect2(20, 180, 1960, 415))
            : paper ? TacticalHudStyle.PaintedImage("objectives-paper-v1.png", new Rect2(20, 46, 2120, 605))
            : TacticalHudStyle.CommandTray();
        plate.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(plate);
        plate.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var inset = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        inset.AddThemeConstantOverride("margin_left", content.Name == "crown" ? 32 : 20);
        inset.AddThemeConstantOverride("margin_right", content.Name == "crown" ? 42 : 18);
        inset.AddThemeConstantOverride("margin_top", content.Name == "crown" ? 15 : 10);
        inset.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(inset);
        inset.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        inset.AddChild(content);
        void FitHeight() => panel.CustomMinimumSize = new Vector2(0, inset.GetCombinedMinimumSize().Y);
        inset.MinimumSizeChanged += FitHeight;
        FitHeight();
    }

    private void AddReadOnly(VBoxContainer destination, TacticalHudRow row, int size)
    {
        var line = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var texture = row.UnitIcon is { } unitIcon && art is not null ? art.UnitIcon(unitIcon) : row.Icon is { } icon ? art?.Icon(icon) : null;
        if (texture is not null)
            line.AddChild(new TextureRect { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(24, 24) });
        var label = Label(row.Label, size, row.Key is "recipient-dies" or "attacker-dies" ? new Color("#e5a08b") : TacticalHudStyle.Ink);
        label.TooltipText = row.Hint;
        line.AddChild(label);
        destination.AddChild(line);
        if (row.Selected)
            label.AddThemeColorOverride("font_color", TacticalHudStyle.Gold);
        if (row.Gauge is { } gauge)
        {
            var bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = gauge.Maximum,
                Value = gauge.Value,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(0, 6)
            };
            if (gauge.Maximum > 0 && gauge.Value / gauge.Maximum <= .3)
                bar.AddThemeStyleboxOverride("fill", TacticalHudStyle.Panel("#cc926f", 0, "#cc926f"));
            destination.AddChild(bar);
        }
    }

    private static void CompactButton(Button button)
    {
        button.CustomMinimumSize = new Vector2(0, 26);
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeStyleboxOverride("normal", TacticalHudStyle.Panel("#30291e", 4, "#756344"));
        button.AddThemeStyleboxOverride("hover", TacticalHudStyle.Panel("#655033", 4, "#b4a477"));
        button.AddThemeStyleboxOverride("pressed", TacticalHudStyle.Panel("#493b29", 4, "#b4a477"));
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
        "unit" => 1,
        "forecast" => 2,
        "target" or "commands" => 3,
        "objectives" => 4,
        "site" => 5,
        "roster" => 90,
        "tile" => 91,
        _ => 10
    };
}
