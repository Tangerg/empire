#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class BattleScreenChecks
{
    private void CheckStructureCollateralDisclosure()
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/native/c01-09-structure-collateral.json");
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
            .Single(value => value["id"]!.GetValue<string>() == "c01-09");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var battle = engine.CreateBattle(level);
        Require(battle.StateHash == fixture["initialStateHash"]!.GetValue<string>(),
            "The collateral witness starts from the unchanged formal C01-09 opening.");
        foreach (var action in fixture["prefix"]!.AsArray().OfType<JsonObject>())
            battle.Dispatch(action);
        Require(battle.StateHash == fixture["beforeStateHash"]!.GetValue<string>(),
            "The original 58 legal orders must reach the exact structure collateral witness.");
        var before = battle.Snapshot();
        var plan = engine.Combat.PreviewAttack(before, 7, new(10, 7), new(9, 6), "c01.satchel-charge");
        Require(plan.PrimaryStructure is { Structure: "chain-east", Damage: 57, HpAfter: 0, Destroyed: true }
            && plan.PrimaryUnit is null && plan.StructureHits.Count == 1 && plan.UnitHits.Count == 1
            && plan.UnitHits[0] is { Target: 22, HpBefore: 98, HpAfter: 52, Damage.Damage: 46, Primary: false }
            && plan.UnitHits[0].Effects.Count == 0,
            "The formal owner predicts one additional unit hit, without inventing an effect or counterattack.");
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var originalSize = GetWindow().Size;
        try
        {
            foreach (var size in new[] { new Vector2I(1180, 760), new Vector2I(960, 640) })
            {
                GetWindow().Size = size;
                var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(before));
                var original = EcmaJson.Stringify(session.Snapshot());
                var save = EcmaJson.Stringify(engine.SaveBattle(session.Snapshot(), "2026-11-04T00:00:00.000Z"));
                var screen = new BattleScreen();
                AddChild(screen);
                try
                {
                    screen.Board.AnimationsEnabled = false;
                    var scene = AuthoredScene.Load("res://data/scenes/c01-09.json");
                    var rendering = BoardSceneRendering.Load(scene, level, before["map"]!.AsObject());
                    screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), session,
                        new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
                    screen.ConfigureGround(rendering.Ground);
                    GetViewport().GuiReleaseFocus();
                    void Press(Key key)
                    {
                        foreach (var pressed in new[] { true, false })
                        {
                            using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed };
                            GetViewport().PushInput(input, true);
                        }
                    }
                    screen.SelectHit(TacticalHit.Unit(new(8, 9), 7));
                    foreach (var key in new[] { Key.Right, Key.Right, Key.Up, Key.Up, Key.Enter })
                        Press(key);
                    var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                    var order = hud.Projection!.Sections.Single(section => section.Key == "commands").Rows
                        .Single(row => row.Intent?.Key == "attack:c01.satchel-charge");
                    var button = Descendants(hud).OfType<Button>()
                        .Single(value => value.GetMeta("hud_key", "").AsString() == "commands/" + order.Key);
                    Require(button.IsVisibleInTree() && !button.Disabled, "The original satchel order is available on the native HUD.");
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    Require(screen.Selection is { Kind: TacticalSelectionKind.Target, Weapon: "c01.satchel-charge" } target
                        && target.TargetSubjects.Count == 4 && session.Revision == 0,
                        "The actual command button enters target selection, without automatic dispatch.");
                    GetViewport().GuiReleaseFocus();
                    Press(Key.Left);
                    Press(Key.Up);
                    Require(screen.Selection?.PreviewTarget == TacticalHit.Object(new(9, 6))
                        && session.Revision == 0 && session.ActionLog.Count == 0 && session.Events.Count == 0
                        && EcmaJson.Stringify(session.Snapshot()) == original
                        && EcmaJson.Stringify(engine.SaveBattle(session.Snapshot(), "2026-11-04T00:00:00.000Z")) == save,
                        "Native keyboard targeting previews the real chain without changing state, events or save data.");
                    var forecast = hud.Projection!.Sections.Single(section => section.Key == "forecast");
                    var strike = forecast.Rows.Single(row => row.Key == "strike");
                    Require(strike.Label == text.Format("ui.hud.strike", 57, 57, 0) && strike.Gauge?.Value == 0,
                        "The existing structure damage and destruction prediction remain unchanged.");
                    var expected = text.Format("ui.hud.collateral", 1, 0);
                    var disclosed = forecast.Rows.Any(row => row.Key == "collateral" && row.Label == expected)
                        && Descendants(hud).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text == expected);
                    Press(Key.Enter);
                    var events = new JsonArray(session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray());
                    Require(session.Revision == 1 && session.ActionLog.Count == 1 && !screen.IsPresenting
                        && EcmaJson.DeepEquals(session.ActionLog.Single(), fixture["action"])
                        && StateDigest.Hash(session.Snapshot()) == fixture["afterStateHash"]!.GetValue<string>()
                        && EcmaJson.Sha256(events) == fixture["eventsSha256"]!.GetValue<string>()
                        && session.Events.Single(value => value["type"]!.GetValue<string>() == "areaAttack")["damage"]!.GetValue<double>() == 46
                        && session.Snapshot()["units"]!.AsArray().OfType<JsonObject>()
                            .Single(unit => unit["id"]!.GetValue<int>() == 22)["hp"]!.GetValue<double>() == 52,
                        "Confirmation preserves the exact original command, ordered events and 46-damage collateral execution.");
                    GD.Print($"STRUCTURE_COLLATERAL size={size}: main=57, visible unit damage=46 HP98->52, disclosed={disclosed}, state={StateDigest.Hash(session.Snapshot())}");
                    Require(disclosed, "The native structure forecast must disclose its additional visible unit hit before confirmation.");
                }
                finally
                {
                    RemoveChild(screen);
                    screen.Free();
                }
            }
        }
        finally { GetWindow().Size = originalSize; }
    }
}
