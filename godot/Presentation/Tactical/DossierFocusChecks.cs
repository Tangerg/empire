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
            context + ": focused control must remain inside the dossier's visible vertical viewport.");
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
        Disclosure("unit-details").ButtonPressed = true;
        Disclosure("stances").ButtonPressed = true;
        await Settle();
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
