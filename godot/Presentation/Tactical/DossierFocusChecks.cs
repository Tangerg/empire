#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

public partial class DossierFocusChecks : Node
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
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Tab(Window popup, bool reverse = false)
    {
        popup.PushInput(new InputEventKey { Keycode = Key.Tab, PhysicalKeycode = Key.Tab, Pressed = true, ShiftPressed = reverse }, true);
        popup.PushInput(new InputEventKey { Keycode = Key.Tab, PhysicalKeycode = Key.Tab, Pressed = false, ShiftPressed = reverse }, true);
        await Settle();
    }

    private void VisibleFocus(ScrollContainer scroll, Control expected, string context)
    {
        Require(expected.HasFocus(), context + ": keyboard reached the expected control.");
        var viewport = scroll.GetGlobalRect();
        var focused = expected.GetGlobalRect();
        Require(focused.Position.Y >= viewport.Position.Y && focused.End.Y <= viewport.End.Y,
            context + $": focused control must remain inside the dossier's visible vertical viewport: scroll={viewport}, focused={focused}, offset={scroll.ScrollVertical}, max={scroll.GetVScrollBar().MaxValue}, page={scroll.GetVScrollBar().Page}.");
    }

    private async Task Check(Vector2I size)
    {
        GetWindow().Size = size;
        var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
        AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var intents = 0;
        hud.IntentRequested += _ => intents++;
        var stamp = new TacticalStamp(1, 1);
        TacticalHudProjection Frame(int extra = 0) => new(stamp, "静态键盘阅读夹具 " + extra, new[]
        {
            new TacticalHudSection("unit-details", "兵种与能力", Enumerable.Range(0, 24 + extra)
                .Select(i => new TacticalHudRow("ability-" + i, "能力说明 · 只读内容 " + i)).ToArray()),
            new TacticalHudSection("stances", "反应与朝向", new[]
            {
                new TacticalHudRow("guard", "防御（静态夹具）", Intent: new(stamp, TacticalHudIntentKind.Command, "guard")),
                new TacticalHudRow("east", "朝向东", Intent: new(stamp, TacticalHudIntentKind.Command, "east"))
            }),
            new TacticalHudSection("formations", "阵形", new[] { new TacticalHudRow("line", "保持队形") })
        }, CloseLabel: "关闭 Esc");
        Button Disclosure(string key) => ((VBoxContainer)hud.FindChild(key, true, false)).GetChildren().OfType<Button>().Single();
        Button Command(string key) => ((VBoxContainer)hud.FindChild("stances", true, false)).FindChildren("*", "Button", true, false)
            .OfType<Button>().Single(button => button.GetMeta("hud_key", "").AsString() == "stances/" + key);
        hud.Apply(Frame());
        await Settle();
        var opener = (Button)hud.FindChild("DossierButton", true, false);
        opener.EmitSignal(BaseButton.SignalName.Pressed);
        await Settle();
        var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
        var scroll = (ScrollContainer)hud.FindChild("DetailScroll", true, false);
        Require(Disclosure("unit-details").ButtonPressed,
            "A named dossier entry immediately reveals its own section, not an empty collapsed index.");
        Disclosure("stances").ButtonPressed = true;
        await Settle();
        await Tab(popup);
        Require(scroll.HasFocus(), "The dossier exposes a continuous reading focus stop.");
        await Tab(popup);
        VisibleFocus(scroll, Disclosure("unit-details"), "First disclosure");
        await Tab(popup);
        VisibleFocus(scroll, Disclosure("stances"), "Tab beyond long description");
        Require(scroll.ScrollVertical > 0, "Keyboard navigation scrolls long content without a mouse wheel.");
        await Tab(popup);
        VisibleFocus(scroll, Command("guard"), "First action");
        await Tab(popup);
        VisibleFocus(scroll, Command("east"), "Second action");
        hud.Apply(Frame(10));
        await Settle();
        VisibleFocus(scroll, Command("east"), "Refresh with longer preceding content");
        scroll.ScrollVertical = 0;
        await Settle();
        hud.Apply(Frame(11));
        await Settle();
        Require(scroll.ScrollVertical == 0 && Command("east").HasFocus(),
            "Refresh preserves a manually chosen reading offset when focus was already outside the viewport.");
        await Tab(popup, true);
        VisibleFocus(scroll, Command("guard"), "Reverse navigation");
        GetWindow().Size = size == new Vector2I(960, 640) ? new Vector2I(1180, 760) : new Vector2I(960, 640);
        await Settle();
        await Tab(popup, true);
        VisibleFocus(scroll, Disclosure("stances"), "Resize then reverse navigation");
        await Tab(popup, true);
        VisibleFocus(scroll, Disclosure("unit-details"), "Reverse past long description");
        var close = (Button)hud.FindChild("DossierClose", true, false);
        close.EmitSignal(BaseButton.SignalName.Pressed);
        await Settle();
        opener = (Button)hud.FindChild("DossierButton", true, false);
        Require(!popup.Visible && opener.HasFocus(), "Close returns focus to the current dossier opener.");
        opener.EmitSignal(BaseButton.SignalName.Pressed);
        await Settle();
        Require(popup.Visible && close.HasFocus(), "Reopening begins on the visible safe close control.");
        popup.Hide();
        await Settle();
        Require(!popup.Visible && opener.HasFocus(), "Native popup hide restores opener focus; real Escape is covered by GUI validation.");
        Require(intents == 0, "Reading, navigation, refresh and closure must never issue a gameplay intent.");
        hud.Apply(Frame(12));
        RemoveChild(hud);
        hud.Free();
        await Settle();
        Require(!GodotObject.IsInstanceValid(hud), "Pending refresh callbacks must not retain or resurrect an unloaded HUD.");
    }

    private async Task CheckReadingSubject()
    {
        var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
        AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var intents = 0;
        hud.IntentRequested += _ => intents++;
        TacticalHudProjection Frame(int unit, int binding = 1, int revision = 1) => new(new(binding, revision), "", new[]
        {
            new TacticalHudSection("unit-details", "Unit " + unit, Enumerable.Range(0, 35)
                .Select(i => new TacticalHudRow("fact-" + i, "Read-only fact " + i)).ToArray())
        }, DossierSubject: TacticalHit.Unit(new BoardCell(1, 1), unit));
        async Task Open()
        {
            ((Button)hud.FindChild("DossierButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
        }
        hud.Apply(Frame(1));
        await Settle();
        await Open();
        var scroll = (ScrollContainer)hud.FindChild("DetailScroll", true, false);
        var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
        scroll.ScrollVertical = 200;
        await Settle();
        Require(scroll.ScrollVertical == 200, "Reading-subject fixture must have a nonzero readable offset.");
        popup.Hide();
        await Settle();
        hud.Apply(Frame(1, revision: 2));
        await Settle();
        await Open();
        Require(scroll.ScrollVertical == 200, "Reopening the same subject after a revision preserves its reading position.");
        popup.Hide();
        await Settle();
        hud.Apply(Frame(2, revision: 2));
        await Settle();
        await Open();
        Require(scroll.ScrollVertical == 0, "A different dossier subject must open at its beginning, not the previous subject's offset.");
        scroll.ScrollVertical = 200;
        await Settle();
        popup.Hide();
        await Settle();
        hud.Apply(Frame(2, binding: 2));
        await Settle();
        await Open();
        Require(scroll.ScrollVertical == 0, "A new battle binding must not inherit an old dossier's reading position.");
        Require(intents == 0, "Changing dossier reading position never dispatches a gameplay action.");
        RemoveChild(hud);
        hud.Free();
        await Settle();
    }

    private async Task CheckNamedEntry()
    {
        var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
        AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var intents = 0;
        hud.IntentRequested += _ => intents++;
        Button? previous = null;
        foreach (var key in new[] { "roster", "unit-details", "site", "structure" })
        {
            hud.Apply(new(new(1, 1), "", new[]
            {
                new TacticalHudSection(key, "Reading " + key, new[]
                {
                    new TacticalHudRow("fact", "Read-only information"),
                    new("action-a", "Do not activate", Intent: new(new(1, 1), TacticalHudIntentKind.Command, "a")),
                    new("action-b", "Do not activate either", Intent: new(new(1, 1), TacticalHudIntentKind.Command, "b"))
                })
            }));
            await Settle();
            var opener = (Button)hud.FindChild("DossierButton", true, false);
            Require(previous is null || ReferenceEquals(previous, opener), "A same-subject refresh retains the native opener.");
            previous = opener;
            Require(opener.Text == "Reading " + key, "The retained entry names the currently available section.");
            opener.EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            var group = (VBoxContainer)hud.FindChild(key, true, false);
            var disclosure = group.GetChildren().OfType<Button>()
                .SingleOrDefault(button => button.GetMeta("hud_key", "").AsString() == key + "/disclosure");
            Require(disclosure is null || disclosure.ButtonPressed,
                "Opening a retained entry resolves its current section rather than its original binding.");
            Require(group.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.IsVisibleInTree() && label.Text == "Read-only information"),
                "Both collapsible and ordinary dossier sections expose their information immediately.");
            ((Button)hud.FindChild("DossierClose", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
        }
        Require(intents == 0, "Opening named information never dispatches a tactical intent.");
        RemoveChild(hud);
        hud.Free();
        await Settle();
    }

    private async Task CheckCommittedOrder()
    {
        GetWindow().Size = new Vector2I(960, 640);
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var level = BattleTextChecks.OpeningLevel();
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var screen = new BattleScreen();
        AddChild(screen);
        screen.Board.AnimationsEnabled = false;
        screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
            new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, BoardPlane.Dimetric)),
            level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        foreach (var nativeHide in new[] { false, true })
        {
            var unit = session.GetTacticalQueries(0).Unit(2)!;
            screen.SelectHit(TacticalHit.Unit(unit.At, unit.Id));
            await Settle();
            var openingStamp = hud.Projection!.Stamp;
            ((Button)hud.FindChild("DossierButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
            Require(popup.Visible, "The real selected unit opens its dossier before a gameplay adjustment.");
            var stances = (VBoxContainer)hud.FindChild("stances", true, false);
            stances.GetChildren().OfType<Button>().Single().ButtonPressed = true;
            await Settle();
            var actionKey = nativeHide ? "stances/face:north" : "stances/reaction:guard";
            var action = stances.FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(button => button.GetMeta("hud_key", "").AsString() == actionKey);
            action.GrabFocus();
            popup.PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true }, true);
            popup.PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false }, true);
            await Settle();
            var state = session.Snapshot();
            var changed = state["units"]!.AsArray().OfType<JsonObject>().Single(value => value["id"]!.GetValue<int>() == unit.Id);
            Require(session.Revision == openingStamp.Revision + 1 && !screen.IsPresenting
                && changed[nativeHide ? "facing" : "reaction"]!.GetValue<string>() == (nativeHide ? "north" : "guard"),
                "Native keyboard activation commits the real dossier adjustment exactly once and settles its presentation.");
            var opener = (Button)hud.FindChild("DossierButton", true, false);
            Require(popup.Visible && opener.IsVisibleInTree() && hud.Projection!.Stamp.Binding == openingStamp.Binding
                && hud.Projection.Stamp.Revision == session.Revision,
                "A committed adjustment keeps the same dossier window and a current opener within the same battle binding.");
            var beforeClose = EcmaJson.Stringify(state);
            var eventCount = session.Events.Count;
            if (nativeHide)
                popup.Hide();
            else
                ((Button)hud.FindChild("DossierClose", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            Require(!popup.Visible && opener.HasFocus(),
                "Closing the dossier after a committed adjustment returns keyboard focus to its current opener.");
            Require(EcmaJson.Stringify(session.Snapshot()) == beforeClose && session.Events.Count == eventCount,
                "Closing and restoring focus cannot issue another gameplay action or change the committed state.");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true }, true);
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false }, true);
            await Settle();
            Require(popup.Visible && ((Button)hud.FindChild("DossierClose", true, false)).HasFocus()
                && session.Revision == openingStamp.Revision + 1,
                "The restored entry reopens by keyboard on the safe close control without a second battle order.");
            popup.Hide();
            await Settle();
        }
        RemoveChild(screen);
        screen.Free();
        await Settle();
    }

    private async Task CheckContinuousReading()
    {
        GetWindow().Size = new Vector2I(960, 640);
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), BattleTextChecks.OpeningLevel());
        var unit = session.GetTacticalQueries(0).Unit(1)!;
        var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
        AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var projection = session.ProjectHud(new(0, 0), new()
        {
            Kind = TacticalSelectionKind.Unit,
            UnitId = unit.Id
        },
            TacticalHit.Unit(unit.At, unit.Id), null, false);
        var state = EcmaJson.Stringify(session.Snapshot());
        var intents = 0;
        hud.IntentRequested += _ => intents++;
        hud.Apply(projection);
        await Settle();
        var opener = (Button)hud.FindChild("DossierButton", true, false);
        opener.GrabFocus();
        async Task Press(Window window, Key code)
        {
            window.PushInput(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true }, true);
            window.PushInput(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false }, true);
            await Settle();
        }
        await Press(GetWindow(), Key.Enter);
        var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
        var scroll = (ScrollContainer)hud.FindChild("DetailScroll", true, false);
        Require(popup.Visible, "The actual opening commander's dossier opens by native keyboard.");
        await Tab(popup);
        Require(scroll.HasFocus(), "Tab reaches continuous dossier reading before skipping to section controls.");
        await Tab(popup);
        var disclosure = ((VBoxContainer)hud.FindChild("unit-details", true, false)).GetChildren().OfType<Button>().Single();
        Require(disclosure.HasFocus(), "Tab still reaches the actual unit disclosure after the reading surface.");
        Require(disclosure.ButtonPressed, "Opening the named entry already expands the real commander's details.");
        await Press(popup, Key.Enter);
        Require(!disclosure.ButtonPressed, "Native Enter can still collapse the automatically revealed details.");
        await Press(popup, Key.Enter);
        Require(disclosure.ButtonPressed, "Native Enter can reopen the real commander's read-only details.");
        await Tab(popup, true);
        Require(scroll.HasFocus(), "Reverse Tab returns to continuous reading without activating an order.");
        await Press(popup, Key.Pagedown);
        Require(scroll.HasFocus() && scroll.ScrollVertical > 0,
            "PageDown advances within long actual unit details without jumping to another section.");
        var labels = hud.FindChild("unit-details", true, false).FindChildren("*", "Label", true, false)
            .OfType<Label>().Where(label => label.IsVisibleInTree()).ToArray();
        Require(labels.Any(label => label.Text == "护甲 轻甲") && labels.Any(label => label.Text == "移动型 步行"),
            "The actual commander's details include the formerly inaccessible armor and movement facts.");
        var unread = labels.ToHashSet();
        await Press(popup, Key.Home);
        for (var step = 0; step < 64 && unread.Count > 0; step++)
        {
            var readable = scroll.GetGlobalRect();
            if (scroll.GetVScrollBar().IsVisibleInTree())
                readable.Size -= new Vector2(scroll.GetVScrollBar().Size.X, 0);
            unread.RemoveWhere(row => readable.Encloses(row.GetGlobalRect()));
            await Press(popup, Key.Down);
        }
        Require(unread.Count == 0, "Continuous keyboard reading reveals every complete real unit detail, including the former blind gap.");
        await Press(popup, Key.Home);
        await Press(popup, Key.Pagedown);
        var readingHint = (Label)hud.FindChild("ReadingHint", true, false);
        Require(scroll.GetVScrollBar().Size.X >= 10, "The dossier retains a usable scroll thumb without covering its text.");
        Require(readingHint.Visible && readingHint.Text == text.Format("ui.hud.dossier_reading_hint"),
            "The actual dossier exposes localized continuous reading instructions outside its scroll area.");
        Require(!scroll.IsAncestorOf(readingHint) && popup.GetVisibleRect().Encloses(readingHint.GetGlobalRect()),
            "The reading instructions remain inside the native popup and outside the scrolling text.");
        Require(GetViewport().GetVisibleRect().Encloses(new Rect2(popup.Position, popup.Size)),
            "The complete dossier including its close action fits the minimum supported battlefield viewport.");
        var offset = scroll.ScrollVertical;
        hud.Apply(projection with
        {
            ReadingHint = projection.ReadingHint + " · 阅读"
        });
        await Settle();
        Require(scroll.HasFocus() && scroll.ScrollVertical == offset, "A HUD refresh preserves continuous reading focus and offset.");
        Require(readingHint.Text == projection.ReadingHint + " · 阅读", "A same-stamp projection refresh updates existing reading instructions.");
        ((Button)hud.FindChild("DossierClose", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
        await Settle();
        ((Button)hud.FindChild("DossierButton", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
        await Settle();
        Require(scroll.ScrollVertical == offset, "Reopening already expanded details preserves the player's reading position.");
        await Tab(popup);
        await Press(popup, Key.End);
        Require(scroll.ScrollVertical >= scroll.GetVScrollBar().MaxValue - scroll.GetVScrollBar().Page - 1,
            "End reaches the final dossier section without a mouse.");
        await Press(popup, Key.Home);
        Require(scroll.ScrollVertical == 0, "Home returns to the beginning of the dossier.");
        await Press(popup, Key.Down);
        Require(scroll.ScrollVertical > 0 && scroll.HasFocus(), "Down reads a small increment rather than leaving the text.");
        await Press(popup, Key.Up);
        Require(scroll.ScrollVertical == 0 && scroll.HasFocus(), "Up reverses the reading increment.");
        await Tab(popup);
        Require(disclosure.HasFocus(), "Section controls remain reachable after continuous reading.");
        await Press(popup, Key.Enter);
        await Settle();
        Require(!disclosure.ButtonPressed && !readingHint.Visible,
            "Collapsing short content removes the now-unnecessary reading hint without scrolling a hidden section.");
        foreach (var section in new[] { "roster", "formations", "roster", "formations", "tile" })
        {
            var toggle = ((VBoxContainer)hud.FindChild(section, true, false)).GetChildren().OfType<Button>().Single();
            for (var step = 0; step < 32 && !toggle.HasFocus(); step++)
                await Tab(popup);
            Require(toggle.HasFocus(), "Native Tab reaches the actual disclosure: " + section);
            await Press(popup, Key.Enter);
            await Settle();
            var hintVisible = readingHint.Visible;
            for (var frame = 0; frame < 4; frame++)
            {
                await Settle();
                Require(readingHint.Visible == hintVisible &&
                    (scroll.GetVScrollBar().MaxValue <= scroll.GetVScrollBar().Page + 1 || readingHint.Visible),
                    "Actual near-threshold content keeps stable, truthful reading guidance: " + section);
                Require(GetViewport().GetVisibleRect().Encloses(new Rect2(popup.Position, popup.Size)),
                    "Actual disclosure layout stays inside the supported viewport: " + section);
            }
        }
        Require(intents == 0 && session.Revision == 0 && session.Events.Count == 0 && EcmaJson.Stringify(session.Snapshot()) == state,
            "Reading actual campaign facts never changes canonical battle state or issues orders.");
        popup.Hide();
        await Settle();
        Require(opener.HasFocus(), "Closing after keyboard reading returns focus to the current dossier entry.");
        RemoveChild(hud);
        hud.Free();
        await Settle();
    }

    private async Task CheckCloseBoundaries()
    {
        foreach (var boundary in new[] { "binding", "older-revision", "refresh", "hidden", "removed", "unloaded", "reopened" })
        {
            var hud = new TacticalHud { Size = new Vector2(960, 640), Theme = TacticalHudStyle.Create() };
            var destination = new Button { Text = "Current keyboard destination" };
            AddChild(hud);
            AddChild(destination);
            var projection = new TacticalHudProjection(new(20, 1), "", new[]
            {
                new TacticalHudSection("roster", "双方兵力", new[] { new TacticalHudRow("count", "灰旗军 11") })
            });
            hud.Apply(projection);
            await Settle();
            var opener = (Button)hud.FindChild("DossierButton", true, false);
            opener.EmitSignal(BaseButton.SignalName.Pressed);
            await Settle();
            var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
            Require(popup.Visible, "Close-boundary fixture opens through the mounted entry: " + boundary);
            if (boundary == "binding")
                hud.Apply(projection with
                {
                    Stamp = new(21, 1)
                });
            else if (boundary == "older-revision")
                hud.Apply(projection with
                {
                    Stamp = new(20, 0)
                });
            else if (boundary == "hidden")
                opener.Hide();
            else if (boundary == "removed")
                hud.RemoveChild(opener);
            popup.Hide();
            if (boundary == "refresh")
                hud.Apply(projection with
                {
                    Hint = "Newer projection after dismissal"
                });
            else if (boundary == "unloaded")
                RemoveChild(hud);
            else if (boundary == "reopened")
                opener.EmitSignal(BaseButton.SignalName.Pressed);
            if (boundary != "reopened")
                destination.GrabFocus();
            await Settle();
            if (boundary == "reopened")
            {
                Require(popup.Visible && ((Button)hud.FindChild("DossierClose", true, false)).HasFocus(),
                    "Reopening through the current entry safely supersedes pending close focus restoration.");
                await Tab(popup);
                Require(((ScrollContainer)hud.FindChild("DetailScroll", true, false)).HasFocus(), "Reopening retains the continuous reading focus stop.");
                await Tab(popup);
                var disclosure = ((VBoxContainer)hud.FindChild("roster", true, false)).GetChildren().OfType<Button>().Single();
                Require(disclosure.HasFocus(), "The reopened dossier retains native keyboard navigation.");
            }
            else
                Require(destination.HasFocus(), "A stale dossier close cannot steal newer focus across " + boundary + ".");
            if (boundary == "removed")
                opener.Free();
            if (hud.IsInsideTree())
                RemoveChild(hud);
            hud.Free();
            RemoveChild(destination);
            destination.Free();
            await Settle();
        }
    }

    public override async void _Ready()
    {
        try
        {
            await CheckNamedEntry();
            await CheckReadingSubject();
            await CheckContinuousReading();
            await CheckCommittedOrder();
            await CheckCloseBoundaries();
            await Check(new Vector2I(960, 640));
            await Check(new Vector2I(1180, 760));
            GD.Print($"Dossier focus checks passed: {assertions} assertions; native reading/navigation and isolated real-session adjustments.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
