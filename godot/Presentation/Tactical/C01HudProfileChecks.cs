using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Godot;

namespace Empire.Presentation.Tactical;

/// <summary>Optional C01 header chrome must leave default rows, controls and intents unchanged.</summary>
public partial class C01HudProfileChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        assertions++;
    }
    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var text = GodotTextCatalog.LoadSimplifiedChinese();
            var presentation = BattleTextChecks.Session(engine, text);
            var art = TacticalHudArt.Load(AssetCatalog.Load(), PortraitCatalogResources.Load());
            var levels = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().ToArray();
            foreach (var level in levels.Skip(1))
            {
                var profile = TacticalHudPresentation.Resolve(TerrainChecks.Scene(level));
                Require(profile == TacticalHudPresentation.Default, "Only C01 may select alternate header chrome");
                var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
                var unit = session.Snapshot()["units"]!.AsArray().OfType<JsonObject>().First(u => u["owner"]!.GetValue<int>() == 1);
                foreach (var selection in new[] { new TacticalSelection(), new TacticalSelection { Kind = TacticalSelectionKind.Unit, UnitId = unit["id"]!.GetValue<int>() } })
                {
                    var projection = session.ProjectHud(new(0, 0), selection, null, selection.UnitId, false);
                    foreach (var width in new[] { 960, 1180 })
                    {
                        GetWindow().Size = new(width, 760);
                        var original = await Mount(projection, art, null);
                        var expected = Snapshot(original);
                        RemoveChild(original);
                        original.Free();
                        var configured = await Mount(projection, art, profile);
                        Require(expected == Snapshot(configured), $"Default chrome, geometry and text changed: {level["id"]}/{width}/{selection.UnitId}");
                        Require(configured.FindChild("objectives", true, false)!.GetChildren().OfType<Button>().Single().ButtonPressed,
                            "Classic objective expansion default must not change");
                        RemoveChild(configured);
                        configured.Free();
                    }
                }
            }
            var opening = levels[0];
            var c01 = new GrayFlagSession(new(engine, presentation, new JsonArray()), opening);
            var stateBefore = EcmaJson.Stringify(c01.Snapshot());
            var rows = c01.ProjectHud(new(4, 0), new(), null, null, false);
            foreach (var size in new[] { new Vector2I(960, 640), new Vector2I(968, 640), new Vector2I(1180, 760) })
            {
                GetWindow().Size = size;
                var hud = await Mount(rows, art, TacticalHudPresentation.Resolve(TerrainChecks.Scene(opening)));
                var objectives = (VBoxContainer)hud.FindChild("objectives", true, false);
                var toggle = objectives.GetChildren().OfType<Button>().Single();
                Require(!toggle.ButtonPressed && !objectives.GetNode<Control>("Content").Visible, "C01 starts with compact objectives");
                Require(hud.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.IsVisibleInTree() && l.Text == rows.Sections.Single(s => s.Key == "objectives").Rows.Single(r => r.Key == "failure:primary").Label), "Failure condition remains visible while objectives are collapsed");
                Require(toggle.Text.Contains("0/2", StringComparison.Ordinal), "Compact summary preserves live aggregate progress");
                CheckBounds(hud, size);
                GD.Print($"C01_HEADER_BOUNDS {size} crown={hud.GetNode<Control>("crownPlate").GetGlobalRect()} objectives={hud.GetNode<Control>("objectivesPlate").GetGlobalRect()}");
                toggle.GrabFocus();
                SendEnter();
                await Frames();
                Require(toggle.ButtonPressed && objectives.GetNode<Control>("Content").Visible, "Keyboard expands objectives");
                var villageRow = rows.Sections.Single(section => section.Key == "objectives").Rows.Single(row =>
                    row.Intent is { Kind: TacticalHudIntentKind.FocusObjective, Key: "village-control" });
                var locate = objectives.FindChildren("*", "Button", true, false).OfType<Button>()
                    .Single(button => button.Text == villageRow.Label);
                Require(locate.IsVisibleInTree() && !locate.Disabled,
                    "The current village-control objective remains visible and actionable after expansion");
                TacticalHudIntent? emitted = null;
                hud.IntentRequested += intent => emitted = intent;
                locate.GrabFocus();
                SendEnter();
                await Frames();
                Require(emitted is { Kind: TacticalHudIntentKind.FocusObjective, Key: "village-control" } && emitted.Stamp == rows.Stamp, "Locate preserves the real revision-bound objective intent");
                toggle.GrabFocus();
                SendEnter();
                await Frames();
                Require(!toggle.ButtonPressed && toggle.HasFocus(), "Collapse retains keyboard disclosure focus");
                CheckBounds(hud, size);
                hud.ConfigurePresentation(TacticalHudPresentation.Default);
                hud.Apply(rows);
                await Frames();
                var restoredObjectives = (VBoxContainer)hud.FindChild("objectives", true, false);
                Require(restoredObjectives.GetChildren().OfType<Button>().Single().ButtonPressed,
                    "Rebinding the same view to the default profile restores objective expansion");
                RemoveChild(hud);
                hud.Free();
            }
            Require(stateBefore == EcmaJson.Stringify(c01.Snapshot()), "HUD interaction did not advance canonical battle state");
            GD.Print($"C01_HUD_PROFILE_CHECKS_OK {assertions} assertions");
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task<TacticalHud> Mount(TacticalHudProjection projection, TacticalHudArt art, TacticalHudPresentation? profile)
    {
        var hud = new TacticalHud { Theme = TacticalHudStyle.Create() };
        hud.ConfigureArt(art);
        if (profile is not null)
            hud.ConfigurePresentation(profile);
        AddChild(hud);
        hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hud.Apply(projection);
        await Frames();
        return hud;
    }
    private async Task Frames()
    {
        for (var i = 0; i < 6; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private void SendEnter()
    {
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = pressed };
            GetViewport().PushInput(input, true);
        }
    }
    private void CheckBounds(TacticalHud hud, Vector2I size)
    {
        foreach (var label in hud.FindChildren("*", "Label", true, false).OfType<Label>().Where(l => l.IsVisibleInTree()))
            Require(new Rect2(Vector2.Zero, size).Encloses(label.GetGlobalRect()) && label.GetLineCount() == label.GetVisibleLineCount(), "C01 label must not clip or leave viewport");
        foreach (var button in hud.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.IsVisibleInTree()))
            Require(new Rect2(Vector2.Zero, size).Encloses(button.GetGlobalRect()) && button.Size.X + .01f >= button.GetCombinedMinimumSize().X, "C01 button must remain reachable");
    }
    private static string Snapshot(Node root) => string.Join("\n", Walk(root).OfType<Control>().Select(c => $"{c.GetClass()}|{c.GetGlobalRect()}|{c.Visible}|{c.MouseFilter}|{(c is Label l ? l.Text : c is Button b ? b.Text : "")}|{c.GetThemeFontSize("font_size")}|{c.GetThemeColor("font_color")}|{(c is TextureRect t ? t.Texture?.ResourcePath : "")}"));
    private static IEnumerable<Node> Walk(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren())
            foreach (var node in Walk(child))
                yield return node;
    }
}
