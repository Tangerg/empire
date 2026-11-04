#nullable enable
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Actual C01 opening projection and presentation-only layout checks; no battle actions.</summary>
public partial class IdleGuidanceChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
    private async Task Settle()
    {
        for (var frame = 0; frame < 4; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task CheckInstructionLifetime()
    {
        var hud = new TacticalHud { Size = new Vector2(960, 640), Theme = TacticalHudStyle.Create() };
        var holder = new Control();
        AddChild(hud);
        AddChild(holder);
        try
        {
            var stamp = new TacticalStamp(71, 2);
            const string hintText = "请选择行动目标，确认后下达命令；可以随时取消并重新查看战场。";
            var command = new TacticalHudSection("commands", "Orders", new[]
            {
                new TacticalHudRow("wait", "Wait", Intent: new(stamp, TacticalHudIntentKind.Command, "wait")),
                new TacticalHudRow("cancel", "Cancel", Intent: new(stamp, TacticalHudIntentKind.Back))
            });
            TacticalHudProjection Frame(int tile, params TacticalHudSection[] actions) => new(stamp, hintText,
                actions.Append(new TacticalHudSection("tile", "Terrain", new[] { new TacticalHudRow("coordinate", tile + ", 1") })).ToArray());
            Label Hint() => (Label)hud.FindChild("SelectionHint", true, false);
            Control Tray() => hud.GetNode<Control>("OrderTray");
            void CheckLayout(Control tray, Control? actions = null)
            {
                var hint = Hint();
                var lines = hint.GetLineCount();
                var textHeight = Enumerable.Range(0, lines).Sum(hint.GetLineHeight)
                    + Math.Max(0, lines - 1) * hint.GetThemeConstant("line_spacing");
                Require(hint.Size.Y >= textHeight && tray.GetGlobalRect().Encloses(hint.GetGlobalRect()),
                    $"Retained native guidance fits every line inside its current backing: lines={lines}, textHeight={textHeight}, hint={hint.GetGlobalRect()}, tray={tray.GetGlobalRect()}, font={hint.GetThemeFontSize("font_size")}.");
                if (actions is not null)
                    Require(tray.GetGlobalRect().Encloses(actions.GetGlobalRect())
                        && actions.GetGlobalRect().End.Y <= hint.GetGlobalRect().Position.Y,
                        "Retained action layout remains above full guidance inside the same tray.");
            }
            hud.Apply(Frame(0, command));
            await Settle();
            var hint = Hint();
            var tray = Tray();
            var actions = hud.GetNode<Control>("commands");
            var listenerCount = actions.GetSignalConnectionList(Control.SignalName.MinimumSizeChanged).Count;
            for (var tile = 1; tile <= 4; tile++)
            {
                hud.Apply(Frame(tile, command));
                Require(ReferenceEquals(hint, Hint()) && ReferenceEquals(tray, Tray())
                    && ReferenceEquals(actions, hud.GetNode<Control>("commands"))
                    && actions.GetSignalConnectionList(Control.SignalName.MinimumSizeChanged).Count == listenerCount,
                    "Same instruction and retained action composition keep one live layout subscription across terrain updates.");
            }
            var action = actions.FindChildren("*", "Button", true, false).OfType<Button>().First();
            var originalMinimum = action.CustomMinimumSize;
            var originalTop = tray.OffsetTop;
            action.CustomMinimumSize = new Vector2(originalMinimum.X, originalMinimum.Y + 130);
            await Settle();
            CheckLayout(tray, actions);
            Require(tray.OffsetTop < originalTop - 100, "A retained hint callback still expands the tray when a native action grows.");
            action.CustomMinimumSize = originalMinimum;
            await Settle();
            CheckLayout(tray, actions);
            Require(Math.Abs(tray.OffsetTop - originalTop) < 1, "Native action shrink restores the compact retained tray without stale height.");
            for (var refresh = 0; refresh < 4; refresh++)
            {
                var updated = Frame(5, command) with
                {
                    Hint = hintText + refresh
                };
                hud.Apply(updated);
                await Settle();
                Require(Hint().Text == updated.Hint && Hint().TooltipText == updated.Hint,
                    "Changed guidance text and tooltip always match the current projection.");
                Require(actions.GetSignalConnectionList(Control.SignalName.MinimumSizeChanged).Count == listenerCount,
                    "Replacing hint text releases the previous action listener instead of accumulating subscriptions.");
            }
            var staleHint = Hint();
            hud.RemoveChild(staleHint);
            holder.AddChild(staleHint);
            Require(actions.GetSignalConnectionList(Control.SignalName.MinimumSizeChanged).Count == listenerCount - 1,
                "Moving a hint out of its HUD disconnects its action-size subscription immediately.");
            hud.Apply(Frame(6, command));
            await Settle();
            var liveTop = Tray().OffsetTop;
            staleHint.AddThemeFontSizeOverride("font_size", 40);
            staleHint.EmitSignal(Control.SignalName.MinimumSizeChanged);
            staleHint.EmitSignal(Control.SignalName.Resized);
            await Settle();
            Require(Tray().OffsetTop == liveTop && actions.GetSignalConnectionList(Control.SignalName.MinimumSizeChanged).Count == listenerCount,
                "A stale hint reparented into another live control cannot resize the current HUD's action tray.");
            holder.RemoveChild(staleHint);
            staleHint.Free();
            hud.Apply(Frame(7));
            await Settle();
            var idleHint = Hint();
            var idleTray = hud.GetNode<Control>("InstructionTray");
            hud.Apply(Frame(8));
            Require(ReferenceEquals(idleHint, Hint()) && ReferenceEquals(idleTray, hud.GetNode<Control>("InstructionTray")),
                "Idle terrain updates retain their actual instruction-only composition.");
            var idleTop = idleTray.OffsetTop;
            idleHint.AddThemeFontSizeOverride("font_size", 25);
            await Settle();
            CheckLayout(idleTray);
            Require(idleTray.OffsetTop < idleTop, "Retained idle backing responds to new native text metrics.");
            idleHint.AddThemeFontSizeOverride("font_size", 13);
            await Settle();
            CheckLayout(idleTray);
            Require(Math.Abs(idleTray.OffsetTop - idleTop) < 1, "Restored text metrics shrink retained idle backing again.");
            var target = new TacticalHudSection("target", "Target", new[]
            {
                new TacticalHudRow("confirm", "Confirm", Intent: new(stamp, TacticalHudIntentKind.Command, "confirm")),
                new TacticalHudRow("cancel", "Cancel", Intent: new(stamp, TacticalHudIntentKind.Back))
            });
            var forecast = new TacticalHudSection("forecast", "Forecast", new[] { new TacticalHudRow("damage", "Damage 12") });
            var frames = new[]
            {
                Frame(9, target, command),
                Frame(9, command, target),
                Frame(9, command) with { Stamp = new(71, 3) },
                Frame(9, command) with { Stamp = new(72, 2) },
                Frame(9, command) with { Busy = true },
                Frame(9, command with { Rows = command.Rows.Take(1).ToArray() }),
                Frame(9), Frame(9, target), Frame(9, target, forecast)
            };
            foreach (var frame in frames)
            {
                hud.Apply(Frame(10, command));
                await Settle();
                var oldHint = Hint();
                hud.Apply(frame);
                await Settle();
                var hasForecast = frame.Sections.Any(section => section.Key == "forecast");
                // A trailing target does not change the actual primary commands or instruction composition.
                var unchangedPrimary = frame.Sections.Count == 3 && frame.Sections[0].Key == "commands" && frame.Sections[1].Key == "target";
                Require(ReferenceEquals(oldHint, Hint()) == unchangedPrimary,
                    "Hint reuse follows the actual primary section and independent stamp/busy/layout boundaries.");
                Require(Hint().Text == hintText && Hint().MaxLinesVisible == (hasForecast ? 2 : -1)
                    && (Hint().GetParent() == hud) == !hasForecast,
                    "Same-text target and forecast transitions preserve current hint parent, content and line policy.");
            }
            foreach (var idleFrame in new[] { Frame(10) with { Stamp = new(71, 3) },
                Frame(10) with { Stamp = new(72, 2) }, Frame(10) with { Busy = true } })
            {
                hud.Apply(Frame(10));
                var oldIdleHint = Hint();
                hud.Apply(idleFrame);
                Require(!ReferenceEquals(oldIdleHint, Hint()), "Idle guidance independently invalidates on revision, binding or busy changes.");
            }
            hud.Apply(Frame(11, command));
            await Settle();
            var beforeArt = Hint();
            hud.ConfigureArt(TacticalHudArt.Load(AssetCatalog.Load(), PortraitCatalogResources.Load()));
            hud.Apply(Frame(11, command));
            Require(!ReferenceEquals(beforeArt, Hint()), "Art reconfiguration keeps its complete projection invalidation boundary.");
            await Settle();
            var remountHint = Hint();
            var remountTray = Tray();
            var remountActions = hud.GetNode<Control>("commands");
            var remountTop = remountTray.OffsetTop;
            RemoveChild(hud);
            AddChild(hud);
            remountHint.AddThemeFontSizeOverride("font_size", 30);
            remountActions.EmitSignal(Control.SignalName.MinimumSizeChanged);
            await Settle();
            Require(hud.Projection is null && remountTray.OffsetTop == remountTop,
                "Remount without Apply cannot revive old instruction layout callbacks.");
            hud.Apply(Frame(12, command));
            await Settle();
            Require(!ReferenceEquals(remountHint, Hint()), "Fresh Apply rebuilds the invalidated instruction composition.");
            CheckLayout(Tray(), hud.GetNode<Control>("commands"));
        }
        finally
        {
            RemoveChild(holder);
            holder.Free();
            RemoveChild(hud);
            hud.Free();
        }
    }

    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(value => value["id"]!.GetValue<string>() == "c01-01");
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var before = EcmaJson.Stringify(session.Snapshot());
            var opening = session.ProjectHud(new(0, 0), new(), null, null, false);
            GD.Print("C01 opening sections: " + string.Join(",", opening.Sections.Select(section => section.Key)));
            Require(!opening.Sections.Any(section => section.Key is "commands" or "target" or "deployment" or "recruit" or "tactics"),
                "The actual initial C01 projection has no primary action tray; do not manufacture a tactics section for this regression.");
            Require(opening.Hint.Contains("空格", StringComparison.Ordinal), "The canonical opening projection supplies the existing keyboard instructions.");
            foreach (var size in new[] { new Vector2I(960, 640), new Vector2I(1180, 812) })
            {
                GetWindow().Size = size;
                var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
                AddChild(hud);
                hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var intents = 0;
                hud.IntentRequested += _ => intents++;
                hud.Apply(opening);
                await Settle();
                var hint = (Label)hud.FindChild("SelectionHint", true, false);
                Require(hint.IsVisibleInTree(), "Opening guidance must remain visible when there are no affordable tactics or unit commands.");
                Require(hint.GetVisibleLineCount() == hint.GetLineCount() && hint.MaxLinesVisible == -1,
                    "Opening instructions must be fully readable without pointer-only tooltip access.");
                var tray = (Control)hud.FindChild("InstructionTray", false, false);
                Require(tray.GetGlobalRect().Encloses(hint.GetGlobalRect()), "The complete instruction stays inside its material backing.");
                Require(tray.GetGlobalRect().End.X <= size.X - 194 && tray.GetGlobalRect().Position.X >= 278
                    && tray.GetGlobalRect().Position.Y >= size.Y - 126 && tray.GetGlobalRect().End.Y <= size.Y - 8,
                    "Guidance respects existing portrait/dossier and global-order safe areas without adding a full-height action tray.");
                Require(!hud.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.GetMeta("hud_key", "").AsString().StartsWith("tactics/", StringComparison.Ordinal)),
                    "Showing instructions cannot invent an unavailable gameplay action.");
                hud.Apply(opening with
                {
                    Hint = text.Format("ui.hud.selection.idle_recruit")
                });
                await Settle();
                hint = (Label)hud.FindChild("SelectionHint", true, false);
                tray = (Control)hud.FindChild("InstructionTray", false, false);
                Require(hint.IsVisibleInTree() && hint.GetVisibleLineCount() == hint.GetLineCount()
                    && tray.GetGlobalRect().Encloses(hint.GetGlobalRect()) && tray.Size.Y <= 126,
                    "The longer existing recruitment guidance wraps fully in the same bounded instruction tray.");
                var commandFrame = opening with
                {
                    Hint = text.Format("ui.hud.selection.unit"),
                    Sections = opening.Sections.Append(new TacticalHudSection("commands", "只读命令夹具", new[]
                    {
                        new TacticalHudRow("wait", "待机", Intent: new(opening.Stamp, TacticalHudIntentKind.Command, "wait")),
                        new TacticalHudRow("cancel", "取消 Esc", Intent: new(opening.Stamp, TacticalHudIntentKind.Back))
                    })).ToArray()
                };
                hud.Apply(commandFrame);
                await Settle();
                Require(hud.FindChild("InstructionTray", false, false) is null && hud.FindChild("OrderTray", false, false) is Control,
                    "A real action tray replaces instruction-only backing rather than accumulating both.");
                hint = (Label)hud.FindChild("SelectionHint", true, false);
                Require(hint.IsVisibleInTree() && hint.GetVisibleLineCount() == hint.GetLineCount(),
                    "Selected-unit guidance retains its complete existing wrapping.");
                hud.Apply(opening);
                await Settle();
                Require(hud.GetChildren().Count(child => child.Name == "InstructionTray") == 1 && hud.FindChild("OrderTray", false, false) is null,
                    "Returning idle restores exactly one instruction tray and no stale action tray.");
                hud.ShowHint = false;
                hud.Apply(opening);
                await Settle();
                Require(!((Label)hud.FindChild("SelectionHint", true, false)).Visible && hud.FindChild("InstructionTray", false, false) is null,
                    "Hiding unchanged guidance immediately removes its pointer-blocking backing; an equivalent projection cannot retain it.");
                hud.ShowHint = true;
                await Settle();
                Require(((Label)hud.FindChild("SelectionHint", true, false)).IsVisibleInTree()
                    && hud.GetChildren().Count(child => child.Name == "InstructionTray") == 1,
                    "Showing guidance restores exactly one backing without requiring a new battle projection.");
                var unchangedHint = hud.FindChild("SelectionHint", true, false);
                hud.ShowHint = true;
                hud.Apply(opening);
                Require(ReferenceEquals(unchangedHint, hud.FindChild("SelectionHint", true, false)),
                    "Repeated visibility and equivalent projections do not rebuild the HUD.");
                hud.ShowHint = false;
                hud.Apply(opening with
                {
                    Hint = "Explicit hidden presentation fixture"
                });
                await Settle();
                Require(!((Label)hud.FindChild("SelectionHint", true, false)).Visible && hud.FindChild("InstructionTray", false, false) is null,
                    "An explicit hidden hint creates no empty backing.");
                hud.ShowHint = true;
                hud.Apply(opening with
                {
                    Hint = ""
                });
                await Settle();
                Require(!((Label)hud.FindChild("SelectionHint", true, false)).Visible && hud.FindChild("InstructionTray", false, false) is null,
                    "An empty instruction creates no empty backing.");
                Require(intents == 0 && session.Revision == 0 && session.Events.Count == 0 && EcmaJson.Stringify(session.Snapshot()) == before,
                    "Reading and layout cannot mutate the actual opening battle.");
                RemoveChild(hud);
                hud.Free();
                await Settle();
            }
            await CheckInstructionLifetime();
            GD.Print($"Idle guidance checks passed: {assertions} assertions; actual C01 guidance and labeled native layout/lifetime fixtures.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
