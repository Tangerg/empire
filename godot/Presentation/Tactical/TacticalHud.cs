#nullable enable
using System;
using System.Linq;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Native, keyboard-focusable controls over revision-bound immutable rows.</summary>
public partial class TacticalHud : Control
{
    private static readonly Vector2I DossierSize = new(360, 420);
    private TacticalHudProjection? current;
    private long generation;
    private bool submitted;
    private Button? pendingOrder;
    private TacticalHudArt? art;
    private ReadingScroll? detailScroll;
    private VBoxContainer? details;
    private PopupPanel? detailPopup;
    private Button? detailClose;
    private Label? detailHeading;
    private string? detailOpenerName;
    private TacticalStamp? detailOpeningStamp;
    private readonly HashSet<string> expanded = new() { "objectives" };
    public void ConfigureArt(TacticalHudArt value)
    {
        if (ReferenceEquals(art, value))
            return;
        pendingOrder = null;
        ReleaseTextureReferences(this);
        art = value;
        current = null;
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
        pendingOrder = null;
        current = null;
        ReleaseTextureReferences(this);
    }
    public event Action<TacticalHudIntent>? IntentRequested;
    public TacticalHudProjection? Projection => current;
    private bool showHint = true;
    public bool ShowHint
    {
        get => showHint;
        set
        {
            if (showHint == value)
                return;
            showHint = value;
            if (current is { } projection)
                Apply(projection, refresh: true);
        }
    }
    public event Action? DossierVisibilityChanged;
    public bool DossierVisible => GodotObject.IsInstanceValid(detailPopup) && detailPopup!.IsInsideTree() && detailPopup.Visible;
    public bool OwnsWindowFocus => DossierVisible && detailPopup!.HasFocus();

    public void Apply(TacticalHudProjection projection) => Apply(projection, refresh: false);

    private void Apply(TacticalHudProjection projection, bool refresh)
    {
        if (!refresh && current is { } previous && Equivalent(previous, projection))
            return;
        var oldActions = current?.Sections.FirstOrDefault(section => section.Key is "commands" or "deployment" or "recruit" or "tactics");
        var nextActions = projection.Sections.FirstOrDefault(section => section.Key is "commands" or "deployment" or "recruit" or "tactics");
        var oldMainActions = current?.Sections.FirstOrDefault(section => section.Key is "commands" or "target" or "deployment" or "recruit" or "tactics");
        var mainActions = projection.Sections.FirstOrDefault(section => section.Key is "commands" or "target" or "deployment" or "recruit" or "tactics");
        var forecast = projection.Sections.FirstOrDefault(section => section.Key == "forecast");
        var preserveActions = current is not null && current.Stamp == projection.Stamp && current.Busy == projection.Busy
            && oldActions is not null && nextActions is not null && SameSection(oldActions, nextActions);
        // Compound forecasts still rebuild; bottom instructions also require their layout composition to match.
        // Unchanged sections must not reshape all their text on every hover.
        var retainedSections = current is not null && current.Stamp == projection.Stamp && current.Busy == projection.Busy
            ? projection.Sections.Where(section => section.Key is not ("forecast" or "target")
                && (section.Key != "unit" || current.Portrait == projection.Portrait)
                && current.Sections.Any(old => SameSection(old, section))).Select(section => section.Key).ToHashSet()
            : new HashSet<string>();
        var nextTile = projection.Sections.FirstOrDefault(section => section.Key == "tile");
        var tileGroup = details?.GetNodeOrNull<VBoxContainer>("tile");
        var refreshTile = tileGroup is not null && current is not null && current.Stamp == projection.Stamp && current.Busy == projection.Busy
            && nextTile is not null && current.Sections.FirstOrDefault(section => section.Key == "tile") is { } oldTile
            && SameTextStructure(oldTile, nextTile);
        if (refreshTile)
            retainedSections.Add("tile");
        var oldUnitDetails = current?.Sections.FirstOrDefault(section => section.Key == "unit-details");
        var nextUnitDetails = projection.Sections.FirstOrDefault(section => section.Key == "unit-details");
        var unitDetailsGroup = details?.GetNodeOrNull<VBoxContainer>("unit-details");
        var refreshUnitDetails = unitDetailsGroup is not null && current is not null
            && current.Stamp == projection.Stamp && current.Busy == projection.Busy
            && !retainedSections.Contains("unit-details")
            && oldUnitDetails is not null && nextUnitDetails is not null
            && CanRefreshReadOnlyRows(oldUnitDetails) && CanRefreshReadOnlyRows(nextUnitDetails);
        if (refreshUnitDetails)
            retainedSections.Add("unit-details");
        var hasDossier = projection.Sections.Any(section => section.Key is "unit-details" or "site" or "structure" or "stances" or "formations" or "roster");
        var retained = preserveActions ? GetChildren().Where(child => child.Name == oldActions!.Key || child.Name == "OrderTray").ToHashSet() : new HashSet<Node>();
        foreach (var child in GetChildren())
            if (retainedSections.Contains(child.Name) || retainedSections.Any(key => child.Name == key + "Plate")
                || retainedSections.Contains("unit") && (child.Name == "PortraitTray" || child.Name == "PortraitMedallion"))
                retained.Add(child);
        if (detailPopup is not null)
            retained.Add(detailPopup);
        if (hasDossier && current is not null && current.Stamp == projection.Stamp && current.Busy == projection.Busy
            && current.DossierSubject == projection.DossierSubject
            && GetNodeOrNull<Button>("DossierButton") is { } dossierEntry)
            retained.Add(dossierEntry);
        var mountedHint = GetNodeOrNull<Label>("SelectionHint");
        var preserveInstruction = current is not null && current.Stamp == projection.Stamp && current.Busy == projection.Busy
            && current.Hint == projection.Hint && forecast is null && current.Sections.All(section => section.Key != "forecast")
            && mountedHint is not null && mountedHint.Visible == (ShowHint && projection.Hint.Length > 0)
            && (oldMainActions is null && mainActions is null
                || preserveActions && ReferenceEquals(oldMainActions, oldActions) && ReferenceEquals(mainActions, nextActions));
        if (preserveInstruction)
        {
            retained.Add(mountedHint!);
            if (mainActions is null && GetNodeOrNull<Control>("InstructionTray") is { } instructionBacking)
                retained.Add(instructionBacking);
        }
        var focused = detailPopup?.GuiGetFocusOwner() ?? GetViewport()?.GuiGetFocusOwner();
        var sameReadingSubject = current is not null && current.Stamp.Binding == projection.Stamp.Binding
            && current.DossierSubject == projection.DossierSubject;
        var focusKey = !sameReadingSubject && focused is not null && detailScroll?.IsAncestorOf(focused) == true
            ? null : focused?.GetMeta("hud_key", "").AsString();
        var scrollPosition = sameReadingSubject ? detailScroll?.ScrollVertical ?? 0 : 0;
        var preserveVisibleFocus = sameReadingSubject && focused is not null && detailScroll is not null
            && detailScroll.IsAncestorOf(focused) && detailScroll.GetGlobalRect().Encloses(focused.GetGlobalRect());
        if (refreshUnitDetails)
            RefreshUnitDetails(unitDetailsGroup!, oldUnitDetails!, nextUnitDetails!);
        current = projection;
        var renderedGeneration = ++generation;
        if (refreshTile)
            RefreshTileText(tileGroup!, nextTile!);
        if (!refresh)
            submitted = false;
        foreach (var child in GetChildren())
        {
            if (retained.Contains(child))
                continue;
            ReleaseTextureReferences(child);
            RemoveChild(child);
            child.QueueFree();
        }
        MouseFilter = MouseFilterEnum.Ignore;
        if (detailPopup is null)
        {
            detailPopup = new PopupPanel { Name = "UnitDossier", MinSize = DossierSize, WrapControls = false };
            detailPopup.AddThemeStyleboxOverride("panel", TacticalHudStyle.Panel("#30291e", 14, "#756344"));
            AddChild(detailPopup);
            detailPopup.VisibilityChanged += () => DossierVisibilityChanged?.Invoke();
            var body = new VBoxContainer { Name = "DossierBody" };
            body.AddThemeConstantOverride("separation", 8);
            detailPopup.AddChild(body);
            var header = new HBoxContainer { Name = "DossierHeader" };
            header.AddThemeConstantOverride("separation", 8);
            body.AddChild(header);
            detailHeading = new Label
            {
                Name = "DossierHeading",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = MouseFilterEnum.Pass
            };
            detailHeading.AddThemeFontSizeOverride("font_size", 18);
            detailHeading.AddThemeColorOverride("font_color", TacticalHudStyle.Ink);
            header.AddChild(detailHeading);
            detailClose = new Button { Name = "DossierClose", Text = projection.CloseLabel, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
            CompactButton(detailClose);
            detailClose.Pressed += () => detailPopup.Hide();
            header.AddChild(detailClose);
            detailPopup.PopupHide += () =>
            {
                var openerName = detailOpenerName;
                var openingStamp = detailOpeningStamp;
                var closingGeneration = generation;
                detailOpenerName = null;
                detailOpeningStamp = null;
                Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(this) && IsInsideTree() && generation == closingGeneration && !DossierVisible
                        && current is { } live && openingStamp is { } opened && live.Stamp.Binding == opened.Binding
                        && live.Stamp.Revision >= opened.Revision
                        && openerName is not null && FindChild(openerName, true, false) is Button opener && opener.IsVisibleInTree())
                        opener.GrabFocus();
                }).CallDeferred();
            };
            detailScroll = new ReadingScroll
            {
                Name = "DetailScroll",
                FollowFocus = true,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                CustomMinimumSize = new Vector2(328, 0),
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            body.AddChild(detailScroll);
            details = new VBoxContainer { Name = "Details", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            details.AddThemeConstantOverride("separation", 10);
            detailScroll.AddChild(details);
            body.AddChild(detailScroll.CreateReadingHint(projection.ReadingHint));
        }
        detailClose!.Text = projection.CloseLabel;
        detailHeading!.Text = projection.Sections.FirstOrDefault(section => section.Key == "unit")?.Title
            ?? DossierSection(projection)?.Title ?? "";
        detailHeading.TooltipText = detailHeading.Text;
        detailScroll!.SetReadingHint(projection.ReadingHint);
        foreach (var child in details!.GetChildren())
        {
            if (retainedSections.Contains(child.Name))
                continue;
            ReleaseTextureReferences(child);
            details.RemoveChild(child);
            child.QueueFree();
        }
        void WireDossier(Button button, string? expandedKey = null)
        {
            var popup = detailPopup;
            button.Pressed += () =>
            {
                if (current is null || !IsInsideTree() || !button.IsInsideTree())
                    return;
                // A map-hover refresh may remove ancillary terrain while this native click
                // is pending. Bind the read request to its subject, not every render.
                var openingSubject = current.DossierSubject;
                var openingStamp = current.Stamp;
                Callable.From(() =>
                {
                    if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || IsQueuedForDeletion()
                        || current?.Stamp != openingStamp || current.DossierSubject != openingSubject
                        || !GodotObject.IsInstanceValid(button) || button.IsQueuedForDeletion() || !button.IsInsideTree() || !IsAncestorOf(button)
                        || !GodotObject.IsInstanceValid(popup) || popup.IsQueuedForDeletion() || !popup.IsInsideTree() || popup.Visible)
                        return;
                    var readingKey = expandedKey ?? DossierSection(current)?.Key;
                    if (readingKey is not null && details.GetNodeOrNull<VBoxContainer>(readingKey) is { } section
                        && section.GetChildren().OfType<Button>().SingleOrDefault(button =>
                            button.GetMeta("hud_key", "").AsString() == readingKey + "/disclosure") is { } disclosure)
                    {
                        if (expandedKey is not null || !disclosure.ButtonPressed)
                            detailScroll!.ScrollVertical = 0;
                        disclosure.ButtonPressed = true;
                    }
                    detailOpenerName = button.Name;
                    detailOpeningStamp = openingStamp;
                    // The window owns the reading viewport; wrapped text and disclosures
                    // consume its available space instead of resizing it outside the screen.
                    popup.PopupCentered(DossierSize);
                    detailClose!.GrabFocus();
                }).CallDeferred();
            };
        }
        if (!retainedSections.Contains("unit") && projection.Sections.Any(section => section.Key == "unit"))
        {
            var portraitTray = TacticalHudStyle.CommandTray();
            portraitTray.Name = "PortraitTray";
            AddChild(portraitTray);
            Place(portraitTray, 0, 1, 0, 1, 14, -126, 274, -8);
        }
        var actionCount = mainActions?.Key == "target" ? forecast is null ? 2 : 0 : mainActions?.Rows.Count(row => row.Key != "cancel") ?? 0;
        const int maximumActionSlots = 4;
        var actionSlots = Math.Max(2, Math.Min(maximumActionSlots, actionCount));
        if (actionCount > 0 && !preserveActions)
        {
            var actionTray = TacticalHudStyle.CommandTray();
            actionTray.Name = "OrderTray";
            AddChild(actionTray);
            Place(actionTray, 0, 1, 0, 1, 278, -126, 278 + actionSlots * 106 + 28, -8);
        }
        var target = projection.Sections.FirstOrDefault(section => section.Key == "target");
        foreach (var section in projection.Sections.OrderBy(section => Priority(section.Key)))
        {
            if (retainedSections.Contains(section.Key))
                continue;
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
                MountPlate(group, false, 0, 1, 0, 1, 278, -126, 666, -8);
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
                var summary = section.Key == "objectives" ? section.Rows.FirstOrDefault(row => row.Hint.Length > 0 && row.Intent is null) : null;
                var title = DisclosureTitle(section);
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
                    TacticalHudStyle.PaperButton(toggle);
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
                    if (current is null || !IsInsideTree() || !toggle.IsInsideTree())
                        return;
                    if (open)
                        expanded.Add(section.Key);
                    else
                        expanded.Remove(section.Key);
                    content.Visible = open;
                    var liveSection = current.Sections.Single(value => value.Key == section.Key);
                    toggle.Text = (open ? "−  " : "+  ") + DisclosureTitle(liveSection);
                };
            }
            if (section.Key == "forecast")
            {
                if (projection.Sections.FirstOrDefault(value => value.Key == "forecast-details") is { } calculation)
                {
                    var detail = new Button { Name = "ForecastDetailsButton", Text = calculation.Title };
                    CompactButton(detail);
                    header.AddChild(detail);
                    WireDossier(detail, "forecast-details");
                }
                if (target is not null)
                    foreach (var row in target.Rows)
                        header.AddChild(Order(row, target.Key, compact: true));
            }
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
                group.TooltipText = string.Join("\n", section.Rows.Select(row => row.Label)) + "\n" + projection.Hint;
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
                    AddReadOnly(column, section.Rows.Single(row => row.Key == identity), 13);
                    AddReadOnly(column, section.Rows.Single(row => row.Key == damage), 12);
                }
            }
            if (pairedForecast)
            {
                var effects = new GridContainer { Name = "ForecastEffects", Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                effects.AddThemeConstantOverride("h_separation", 12);
                effects.AddThemeConstantOverride("v_separation", 3);
                foreach (var row in section.Rows.Where(row => row.Key is not ("attacker" or "recipient" or "strike" or "counter" or "forecast-scope")
                    && !row.Key.StartsWith("hit-effect-", StringComparison.Ordinal)))
                {
                    var effect = Label(row.Label, 12, row.Key is "recipient-dies" or "attacker-dies" ? new Color("#e5a08b") : TacticalHudStyle.Gold);
                    effect.Name = row.Key;
                    effect.TooltipText = row.Hint;
                    effects.AddChild(effect);
                }
                if (effects.GetChildCount() > 0)
                    group.AddChild(effects);
                else
                    effects.Free();
            }
            GridContainer? commandGrid = null;
            if (section == mainActions && section.Key != "target")
            {
                commandGrid = new GridContainer { Name = "Orders", Columns = Math.Max(1, Math.Min(maximumActionSlots, section.Rows.Count(row => row.Key != "cancel"))), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
                rows.AddChild(commandGrid);
            }
            foreach (var row in section.Rows)
            {
                if (pairedForecast || section.Key == "forecast"
                    && (row.Key == "forecast-scope" || row.Key.StartsWith("hit-effect-", StringComparison.Ordinal)))
                    continue;
                if (section.Key == "unit" && row.Key is "selection" or "owner")
                    continue;
                if (section.Key == "objectives" && row.Hint.Length > 0 && row.Intent is null)
                    continue;
                if (row.Intent is not null)
                {
                    var button = Order(row, section.Key, compact: row.Key == "cancel");
                    if (section.Key == "objectives")
                    {
                        TacticalHudStyle.PaperButton(button);
                    }
                    if (row.Key == "cancel" && section.Key == "unit")
                    {
                        button.Text = "×";
                        button.TooltipText = row.Label;
                        header.AddChild(button);
                    }
                    else if (row.Key == "cancel" && commandGrid is not null)
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
            if (section.Key == "forecast")
                foreach (var row in section.Rows.Where(row => row.Key == "forecast-scope" || row.Key.StartsWith("hit-effect-", StringComparison.Ordinal)))
                {
                    var notice = Label(row.Label, row.Key == "forecast-scope" ? 11 : 12, TacticalHudStyle.Gold);
                    notice.Name = row.Key;
                    notice.TooltipText = row.Hint.Length > 0 ? row.Hint : row.Label;
                    notice.MaxLinesVisible = 1;
                    notice.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    group.AddChild(notice);
                }
        }
        var detailIndex = 0;
        foreach (var section in projection.Sections.OrderBy(section => Priority(section.Key)))
            if (details.GetNodeOrNull<Control>(section.Key) is { } group)
                details.MoveChild(group, detailIndex++);
        if (hasDossier)
        {
            var dossierTitle = DossierSection(projection)?.Title ?? "";
            var dossier = GetNodeOrNull<Button>("DossierButton");
            if (dossier is null)
            {
                dossier = new Button { Name = "DossierButton" };
                CompactButton(dossier);
                AddChild(dossier);
                WireDossier(dossier);
            }
            dossier.Text = dossierTitle;
            // Newly mounted portrait trays must remain behind a retained interactive opener.
            MoveChild(dossier, -1);
            Place(dossier, 0, 1, 0, 1, 28, -34, projection.Portrait is null ? 148 : 264, -9);
        }
        if (!preserveInstruction)
        {
            var hint = Label(projection.Hint, 13, TacticalHudStyle.Gold);
            hint.Name = "SelectionHint";
            hint.Visible = ShowHint && projection.Hint.Length > 0;
            hint.TooltipText = projection.Hint;
            hint.MouseFilter = MouseFilterEnum.Pass;
            hint.MaxLinesVisible = forecast is null ? -1 : 2;
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
                var hintSlots = mainActions is null ? maximumActionSlots : actionSlots;
                if (mainActions is null && hint.Visible)
                {
                    var instructionTray = TacticalHudStyle.CommandTray();
                    instructionTray.Name = "InstructionTray";
                    AddChild(instructionTray);
                    Place(instructionTray, 0, 1, 0, 1, 278, -50, 278 + hintSlots * 106 + 28, -8);
                }
                AddChild(hint);
                Place(hint, 0, 1, 0, 1, 292, -32, 292 + hintSlots * 106, -10);
                // Native minimum sizes own wrapped text and multi-row order height.
                // Grow the existing tray upward without shrinking command targets.
                var mountedActions = mainActions is null ? null : GetNodeOrNull<Control>(mainActions.Key);
                var trayKey = mainActions is null ? "InstructionTray" : "OrderTray";
                var mountedTray = GetNodeOrNull<Control>(trayKey);
                void FitInstruction()
                {
                    if (current is null || !IsInsideTree() || !GodotObject.IsInstanceValid(hint) || !hint.IsInsideTree()
                        || !ReferenceEquals(GetNodeOrNull<Label>("SelectionHint"), hint)
                        || !GodotObject.IsInstanceValid(mountedTray) || !mountedTray!.IsInsideTree()
                        || !ReferenceEquals(GetNodeOrNull<Control>(trayKey), mountedTray)
                        || mainActions is not null && (!GodotObject.IsInstanceValid(mountedActions) || !mountedActions!.IsInsideTree()
                            || !ReferenceEquals(GetNodeOrNull<Control>(mainActions.Key), mountedActions)))
                        return;
                    var actions = mountedActions;
                    var tray = mountedTray;
                    var lines = hint.GetLineCount();
                    var textHeight = Enumerable.Range(0, lines).Sum(hint.GetLineHeight)
                        + Math.Max(0, lines - 1) * hint.GetThemeConstant("line_spacing");
                    var bottomInset = lines > 1 ? 6 : 0;
                    var extra = Math.Max(0, textHeight - 22) + bottomInset;
                    hint.OffsetBottom = -10 - bottomInset;
                    hint.OffsetTop = -32 - extra;
                    if (actions is null)
                        tray.OffsetTop = hint.OffsetTop - 10;
                    else
                    {
                        actions.OffsetBottom = -35 - extra;
                        actions.OffsetTop = actions.OffsetBottom - Math.Max(78, actions.GetCombinedMinimumSize().Y);
                        tray.OffsetTop = actions.OffsetTop - 13;
                    }
                }
                if (mountedActions is not null)
                {
                    var fit = Callable.From(FitInstruction);
                    mountedActions.Connect(Control.SignalName.MinimumSizeChanged, fit);
                    hint.TreeExiting += () =>
                    {
                        if (GodotObject.IsInstanceValid(mountedActions) && mountedActions.IsConnected(Control.SignalName.MinimumSizeChanged, fit))
                            mountedActions.Disconnect(Control.SignalName.MinimumSizeChanged, fit);
                    };
                }
                // Ellipsized labels can keep the same minimum size when font metrics change.
                // Refit after the native theme cache has handled its notification.
                hint.Connect(Control.SignalName.ThemeChanged, Callable.From(FitInstruction), (uint)ConnectFlags.Deferred);
                hint.MinimumSizeChanged += FitInstruction;
                hint.Resized += FitInstruction;
                hint.Ready += FitInstruction;
                FitInstruction();
            }
        }
        var mountedScroll = detailScroll;
        if (IsInsideTree())
            // Native container layout must finish before restoring a retained control's visibility.
            GetTree().Connect(SceneTree.SignalName.ProcessFrame, Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(this) && generation == renderedGeneration && IsInsideTree()
                    && GodotObject.IsInstanceValid(mountedScroll) && mountedScroll.IsInsideTree())
                {
                    // Restore either visible focus or a deliberate reading offset. Writing the old
                    // offset first leaves native child coordinates stale until the next layout.
                    if (preserveVisibleFocus && detailPopup?.GuiGetFocusOwner() is { } restoredFocus && mountedScroll.IsAncestorOf(restoredFocus))
                        mountedScroll.EnsureControlVisible(restoredFocus);
                    else
                        mountedScroll.ScrollVertical = scrollPosition;
                }
            }), (uint)ConnectFlags.OneShot);

        void RestoreFocus(Button button, string key)
        {
            if (focusKey != key || button.Disabled)
                return;
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(this) && generation == renderedGeneration && IsInsideTree()
                    && GodotObject.IsInstanceValid(button) && button.IsInsideTree() && !button.Disabled)
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
            bool CanActivate() => GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion() && IsInsideTree()
                && GodotObject.IsInstanceValid(button) && !button.IsQueuedForDeletion() && button.IsInsideTree()
                && IsAncestorOf(button) && !submitted && current is { Busy: false } live && !button.Disabled
                && intent.Stamp == live.Stamp && live.Sections.FirstOrDefault(value => value.Key == sectionKey)?.Rows
                    .Any(value => SameRow(value, row) && value.Enabled) == true;
            button.Pressed += () =>
            {
                if (pendingOrder is not null || !CanActivate())
                    return;
                pendingOrder = button;
                // Publishing can rebuild this row. Finish native mouse/touch dispatch
                // before letting its owner replace the control that handled the press.
                Callable.From(() =>
                {
                    if (!ReferenceEquals(pendingOrder, button))
                        return;
                    pendingOrder = null;
                    if (!CanActivate())
                        return;
                    submitted = intent.Kind != TacticalHudIntentKind.FocusObjective;
                    IntentRequested?.Invoke(intent);
                }).CallDeferred();
            };
            RestoreFocus(button, key);
            return button;
        }
    }

    private static bool SameRow(TacticalHudRow first, TacticalHudRow second) =>
        first with
        {
            Intent = null,
            TargetCells = null
        } == second with
        {
            Intent = null,
            TargetCells = null
        }
        && (first.TargetCells ?? System.Array.Empty<BoardCell>()).SequenceEqual(second.TargetCells ?? System.Array.Empty<BoardCell>())
        && (first.Intent is null ? second.Intent is null : second.Intent is { } other
            && first.Intent with
            {
                Action = null
            } == other with
            {
                Action = null
            }
            && first.Intent.Action?.GetRawText() == other.Action?.GetRawText());

    // The projection owns terrain facts; only the text changes within an unchanged native row shape.
    private static void RefreshTileText(VBoxContainer group, TacticalHudSection section)
    {
        var heading = group.GetNode<HBoxContainer>("Heading").GetChildren().OfType<Label>().Single();
        if (heading.Text != section.Title)
            heading.Text = section.Title;
        var disclosure = group.GetChildren().OfType<Button>().Single();
        var title = (disclosure.ButtonPressed ? "−  " : "+  ") + section.Title;
        if (disclosure.Text != title)
            disclosure.Text = title;
        var labels = group.GetNode<VBoxContainer>("Content").GetChildren().OfType<HBoxContainer>()
            .Select(line => line.GetChildren().OfType<Label>().Single());
        foreach (var (label, row) in labels.Zip(section.Rows))
        {
            if (label.Text != row.Label)
                label.Text = row.Label;
            if (label.TooltipText != row.Hint)
                label.TooltipText = row.Hint;
        }
    }

    private static bool CanRefreshReadOnlyRows(TacticalHudSection section) =>
        section.Rows.All(row => row.Intent is null)
        && section.Rows.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count() == section.Rows.Count;

    private void RefreshUnitDetails(VBoxContainer group, TacticalHudSection previous, TacticalHudSection next)
    {
        var heading = group.GetNode<HBoxContainer>("Heading").GetChildren().OfType<Label>().Single();
        var disclosure = group.GetChildren().OfType<Button>().Single();
        var content = group.GetNode<VBoxContainer>("Content");
        var children = content.GetChildren();
        var fragments = new Dictionary<string, (TacticalHudRow Row, HBoxContainer Line, Label Label, ProgressBar? Bar)>(StringComparer.Ordinal);
        var index = 0;
        // The previous immutable projection describes the mounted fragments; no independent row cache survives Apply.
        foreach (var row in previous.Rows)
        {
            if (index >= children.Count || children[index++] is not HBoxContainer line
                || line.GetChildren().OfType<Label>().ToArray() is not { Length: 1 } labels)
                throw new InvalidOperationException("Unit-details row does not match its mounted read-only fragment.");
            ProgressBar? bar = null;
            if (row.Gauge is not null)
            {
                if (index >= children.Count || children[index++] is not ProgressBar gauge)
                    throw new InvalidOperationException("Unit-details gauge does not match its mounted read-only fragment.");
                bar = gauge;
            }
            fragments.Add(row.Key, (row, line, labels[0], bar));
        }
        if (index != children.Count)
            throw new InvalidOperationException("Unit-details contains unexpected mounted row fragments.");

        var position = 0;
        foreach (var row in next.Rows)
        {
            if (fragments.Remove(row.Key, out var fragment)
                && fragment.Row with
                {
                    Label = row.Label,
                    Hint = row.Hint
                } == row)
            {
                if (fragment.Label.Text != row.Label)
                    fragment.Label.Text = row.Label;
                if (fragment.Label.TooltipText != row.Hint)
                    fragment.Label.TooltipText = row.Hint;
                content.MoveChild(fragment.Line, position++);
                if (fragment.Bar is not null)
                    content.MoveChild(fragment.Bar, position++);
            }
            else
            {
                if (fragment.Line is not null)
                    RemoveFragment(fragment.Line, fragment.Bar);
                var start = content.GetChildCount();
                AddReadOnly(content, row, row.Key == "hp" ? 16 : 14);
                var line = content.GetChild(start);
                var bar = row.Gauge is not null ? content.GetChild(start + 1) : null;
                content.MoveChild(line, position++);
                if (bar is not null)
                    content.MoveChild(bar, position++);
            }
        }
        foreach (var fragment in fragments.Values)
            RemoveFragment(fragment.Line, fragment.Bar);

        if (heading.Text != next.Title)
            heading.Text = next.Title;
        var title = (disclosure.ButtonPressed ? "−  " : "+  ") + DisclosureTitle(next);
        if (disclosure.Text != title)
            disclosure.Text = title;

        void RemoveFragment(Node line, Node? bar)
        {
            ReleaseTextureReferences(line);
            content.RemoveChild(line);
            line.QueueFree();
            if (bar is not null)
            {
                ReleaseTextureReferences(bar);
                content.RemoveChild(bar);
                bar.QueueFree();
            }
        }
    }

    private static string DisclosureTitle(TacticalHudSection section) => section.Key == "objectives"
        ? section.Rows.FirstOrDefault(row => row.Hint.Length > 0 && row.Intent is null)?.Label ?? section.Title : section.Title;

    private static bool SameTextStructure(TacticalHudSection first, TacticalHudSection second) =>
        first.Rows.Count == second.Rows.Count && first.Rows.Zip(second.Rows).All(pair =>
            pair.First.Intent is null && pair.Second.Intent is null
            && pair.First with
            {
                Label = pair.Second.Label,
                Hint = pair.Second.Hint
            } == pair.Second);

    private static bool SameSection(TacticalHudSection first, TacticalHudSection second) =>
        first.Key == second.Key && first.Title == second.Title && first.Rows.Count == second.Rows.Count
        && first.Rows.Zip(second.Rows).All(pair => SameRow(pair.First, pair.Second));

    private static bool Equivalent(TacticalHudProjection first, TacticalHudProjection second) =>
        first.Stamp == second.Stamp && first.DossierSubject == second.DossierSubject && first.Hint == second.Hint && first.Busy == second.Busy && first.Portrait == second.Portrait && first.CloseLabel == second.CloseLabel && first.ReadingHint == second.ReadingHint
        && first.Sections.Count == second.Sections.Count && first.Sections.Zip(second.Sections).All(pair => SameSection(pair.First, pair.Second));

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
        inset.AddThemeConstantOverride("margin_top", content.Name == "crown" ? 15 : content.Name == "forecast" ? 6 : 10);
        inset.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(inset);
        inset.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        inset.AddChild(content);
        void FitHeight()
        {
            var minimum = inset.GetCombinedMinimumSize().Y;
            panel.CustomMinimumSize = new Vector2(0, minimum);
            if (content.Name == "objectives")
                panel.OffsetBottom = y1 + Math.Max(minimum, expanded.Contains("objectives") ? y2 - y1 : 0);
        }
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

    private static TacticalHudSection? DossierSection(TacticalHudProjection projection) =>
        (projection.Sections.Any(section => section.Key == "unit")
            ? projection.Sections.FirstOrDefault(section => section.Key == "unit-details") : null)
        ?? projection.Sections.FirstOrDefault(section => section.Key == "site")
        ?? projection.Sections.FirstOrDefault(section => section.Key == "structure")
        ?? projection.Sections.FirstOrDefault(section => section.Key == "unit-details")
        ?? projection.Sections.FirstOrDefault(section => section.Key == "roster");

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
