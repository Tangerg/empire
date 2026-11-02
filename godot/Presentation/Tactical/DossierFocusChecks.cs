#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
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

    public override async void _Ready()
    {
        try
        {
            await Check(new Vector2I(960, 640));
            await Check(new Vector2I(1180, 760));
            GD.Print($"Dossier focus checks passed: {assertions} assertions; presentation-only, no battle state or actions.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
