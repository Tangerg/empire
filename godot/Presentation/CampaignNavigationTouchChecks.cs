#nullable enable
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class CampaignNavigationChecks
{
    private async Task CheckNativeTouchActivation()
    {
        var windowSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1280, 720);
        try
        {
            Require(Input.EmulateMouseFromTouch && !Input.EmulateTouchFromMouse,
                "Native touch regression uses the shipped pointing configuration, not a second input policy.");
            foreach (var scenario in new[] { "orders", "dossier" })
            {
                ResetAutosave();
                var game = MountGame();
                try
                {
                    Press(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
                    while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                        CompleteStory(game);
                    Press(Button(View<StoryScreen>(game), "Choice_rush-north-hill"));
                    Press(Button(View<CampaignStagingScreen>(game), "StartBattle"));
                    game.ProcessMode = ProcessModeEnum.Inherit;
                    await Layout();
                    var session = Campaign(game).Battle!;
                    var screen = View<BattleScreen>(game);
                    var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                    var map = (Control)screen.FindChild("MapArea", true, false);
                    var viewport = GetViewport();
                    async Task SettleInput()
                    {
                        for (var frame = 0; frame < 8; frame++)
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    async Task Touch(Vector2 at, bool split = false)
                    {
                        foreach (var down in new[] { true, false })
                        {
                            using var input = new InputEventScreenTouch { Index = 0, Position = at, Pressed = down };
                            Input.ParseInputEvent(input);
                            if (split)
                                await SettleInput();
                        }
                        await SettleInput();
                    }
                    Vector2 ButtonPoint(Button button)
                    {
                        Require(button.IsVisibleInTree() && !button.Disabled, "Touch targets an enabled, visible native control.");
                        var at = button.GetGlobalRect().GetCenter();
                        if (button.GetWindow() != GetWindow())
                            at += new Vector2(button.GetWindow().Position.X, button.GetWindow().Position.Y);
                        return at;
                    }
                    async Task SettleBattle()
                    {
                        for (var frame = 0; frame < 900 && screen.IsInputBlocked; frame++)
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        Require(!screen.IsInputBlocked, "Touch orders settle before the next explicit order.");
                    }
                    var unit = session.GetTacticalQueries(0).Unit(1)!;
                    var sprite = screen.Board.GetNode<Sprite2D>("CanonicalProjection/Unit_1/AuthoredSprite");
                    var rect = sprite.GetRect();
                    Vector2? pixel = null;
                    for (var y = rect.Position.Y + .5f; y < rect.End.Y && pixel is null; y++)
                        for (var x = rect.Position.X + .5f; x < rect.End.X && pixel is null; x++)
                        {
                            var local = new Vector2(x, y);
                            var at = sprite.GetGlobalTransformWithCanvas() * local;
                            if (sprite.IsPixelOpaque(local) && map.GetGlobalRect().HasPoint(at)
                                && screen.Board.Pick(screen.Board.ToLocal(at)) == TacticalHit.Unit(unit.At, unit.Id))
                                pixel = at;
                        }
                    var actorPoint = pixel ?? throw new InvalidOperationException("Touch fixture requires an actual selectable C01 actor pixel.");
                    var original = EcmaJson.Stringify(session.Snapshot());
                    await Touch(actorPoint);
                    Require(screen.SelectedUnitId == unit.Id && session.Revision == 0,
                        "An actual touch selects the formal actor without issuing an order.");
                    Require(hud.Projection!.DossierSubject == TacticalHit.Unit(unit.At, unit.Id),
                        "The session projects the visible dossier subject independently from incidental hovered terrain.");
                    if (scenario == "dossier")
                    {
                        viewport.GuiReleaseFocus();
                        foreach (var down in new[] { true, false })
                        {
                            using var key = new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = down };
                            Input.ParseInputEvent(key);
                        }
                        await SettleInput();
                        Require(screen.Selection?.Kind == TacticalSelectionKind.Idle, "Dossier handoff starts from real idle inspection.");
                        await Touch(ButtonPoint(Button(screen, "DossierButton")));
                        var popup = (PopupPanel)screen.FindChild("UnitDossier", true, false);
                        Require(popup.Visible, "A quick native touch must open the dossier despite the deferred map-hover clear.");
                        await Touch(ButtonPoint(Button(hud, "DossierClose")));
                        Require(!popup.Visible && session.Revision == 0 && EcmaJson.Stringify(session.Snapshot()) == original,
                            "Quick touch reading and closure preserve the entire battle state.");
                        continue;
                    }
                    var wait = Buttons(hud).Single(button => button.Text == "待机"
                        && button.GetMeta("hud_key", "").AsString().StartsWith("commands/", StringComparison.Ordinal));
                    await Touch(ButtonPoint(wait));
                    await SettleBattle();
                    Require(session.Revision == 1 && session.Snapshot()["units"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>()
                        .Single(value => value["id"]!.GetValue<int>() == unit.Id)["done"]!.GetValue<bool>(),
                        "A native touch Wait commits exactly one ordinary HUD order.");
                    await Touch(ButtonPoint(Button(screen, "Undo")));
                    await SettleBattle();
                    Require(session.Revision == 2 && !session.Snapshot()["units"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>()
                        .Single(value => value["id"]!.GetValue<int>() == unit.Id)["done"]!.GetValue<bool>(),
                        "A native touch Undo reverses the one Wait, without a duplicate activation.");
                    await Touch(actorPoint);
                    Require(screen.SelectedUnitId == unit.Id, "Actor selection remains usable after touch dispatch and undo.");
                    await Touch(ButtonPoint(Button(screen, "DossierButton")), split: true);
                    var dossier = (PopupPanel)screen.FindChild("UnitDossier", true, false);
                    Require(dossier.Visible, "The ordinary touch order sequence opens the real dossier.");
                    var stances = (VBoxContainer)hud.FindChild("stances", true, false);
                    var disclosure = stances.GetChildren().OfType<Button>().Single();
                    await Touch(ButtonPoint(disclosure));
                    var face = Buttons(stances).Single(button => button.GetMeta("hud_key", "").AsString() == "stances/face:north");
                    await Touch(ButtonPoint(face));
                    await SettleBattle();
                    Require(session.Revision == 3 && session.Snapshot()["units"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>()
                        .Single(value => value["id"]!.GetValue<int>() == unit.Id)["facing"]!.GetValue<string>() == "north",
                        "Native touch on a real dossier action commits exactly one facing change.");
                    await Touch(ButtonPoint(Button(hud, "DossierClose")));
                    var endTurn = Button(screen, "EndTurn");
                    var presses = 0;
                    void Count() => presses++;
                    endTurn.Pressed += Count;
                    try
                    {
                        await Touch(ButtonPoint(endTurn), split: true);
                        Require(presses == 1 && session.Revision > 3,
                            "A native touch EndTurn activates once after the other touch controls remain usable.");
                    }
                    finally
                    {
                        if (IsInstanceValid(endTurn))
                            endTurn.Pressed -= Count;
                    }
                }
                finally
                {
                    Retire(game);
                    await Layout();
                }
            }
        }
        finally
        {
            GetWindow().Size = windowSize;
            await Layout();
        }
    }
}
