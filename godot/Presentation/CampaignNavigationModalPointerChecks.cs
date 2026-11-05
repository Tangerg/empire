#nullable enable
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class CampaignNavigationChecks
{
    private async Task CheckModalPointerIsolation()
    {
        ResetAutosave();
        var originalSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1280, 720);
        var game = MountGame();
        try
        {
            await Activate(Button(View<CampaignMenuScreen>(game), "NewCampaign"));
            while (Campaign(game).Node()["type"]!.GetValue<string>() == "story")
                await CompleteStory(game);
            await Activate(Button(View<StoryScreen>(game), "Choice_rush-north-hill"));
            await Activate(Button(View<CampaignStagingScreen>(game), "StartBattle"));
            game.ProcessMode = ProcessModeEnum.Inherit;
            await Layout();
            var session = Campaign(game).Battle!;
            var screen = View<BattleScreen>(game);
            var map = (Control)screen.FindChild("MapArea", true, false);
            var world = map.GetNode<Node2D>("World");
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            var viewport = GetViewport();
            var actor = session.GetTacticalQueries(0).Unit(1)!;
            var sprite = screen.Board.GetNode<Sprite2D>("CanonicalProjection/Unit_1/AuthoredSprite");
            var rect = sprite.GetRect();
            Vector2? pixel = null;
            for (var y = rect.Position.Y + .5f; y < rect.End.Y && pixel is null; y++)
                for (var x = rect.Position.X + .5f; x < rect.End.X && pixel is null; x++)
                {
                    var local = new Vector2(x, y);
                    var point = sprite.GetGlobalTransformWithCanvas() * local;
                    if (sprite.IsPixelOpaque(local) && map.GetGlobalRect().HasPoint(point)
                        && screen.Board.Pick(screen.Board.ToLocal(point)) == TacticalHit.Unit(actor.At, actor.Id))
                        pixel = point;
                }
            var at = pixel ?? throw new InvalidOperationException("Formal C01 modal fixture needs an opaque, selectable actor pixel.");
            var state = EcmaJson.Stringify(session.Snapshot());
            var events = session.Events.Select(value => EcmaJson.Stringify(value)).ToArray();
            var transform = world.Transform;

            async Task SettleInput()
            {
                for (var frame = 0; frame < 8; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            void Motion(Vector2? position = null)
            {
                using var input = new InputEventMouseMotion { Position = position ?? at, GlobalPosition = position ?? at };
                viewport.PushInput(input, true);
            }
            void Pointer(MouseButton button, bool pressed, Vector2? position = null)
            {
                using var input = new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = position ?? at, GlobalPosition = position ?? at };
                viewport.PushInput(input, true);
            }
            async Task KeyPress(Key key)
            {
                foreach (var down in new[] { true, false })
                {
                    using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = down };
                    viewport.PushInput(input, true);
                }
                await SettleInput();
            }
            async Task<Window> Open(string entry, string name)
            {
                Button(screen, entry).GrabFocus();
                await KeyPress(Key.Enter);
                var popup = (Window)screen.FindChild(name, true, false);
                Require(popup.Visible && !new Rect2(popup.Position, popup.Size).HasPoint(at),
                    name + " opens natively with a real actor pixel outside its bounds.");
                return popup;
            }
            void Unchanged(string context)
            {
                Require(session.Revision == 0 && EcmaJson.Stringify(session.Snapshot()) == state
                    && session.Events.Select(value => EcmaJson.Stringify(value)).SequenceEqual(events),
                    context + " preserves the complete canonical state, revision and events.");
                Require(world.Transform == transform, context + " preserves the camera transform.");
            }
            async Task SelectActor()
            {
                Motion();
                await SettleInput();
                Require(viewport.GuiGetHoveredControl() == map, "The positive pointer control must actually reach the map.");
                Pointer(MouseButton.Left, true);
                Pointer(MouseButton.Left, false);
                await SettleInput();
                Require(screen.SelectedUnitId == actor.Id, "A fresh native click selects the formal actor at the tested pixel.");
            }
            await SelectActor();
            foreach (var (entry, name) in new[] { ("DossierButton", "UnitDossier"), ("SystemMenuButton", "SystemMenu") })
            {
                foreach (var split in new[] { false, true })
                {
                    viewport.GuiReleaseFocus();
                    await KeyPress(Key.Escape);
                    Require(screen.Selection?.Kind == TacticalSelectionKind.Idle, "Each dismissal begins from independent Idle selection.");
                    var popup = await Open(entry, name);
                    Motion();
                    await SettleInput();
                    Pointer(MouseButton.Left, true);
                    Require(screen.Selection?.Kind == TacticalSelectionKind.Idle,
                        name + " outside press cannot change the underlying Idle selection.");
                    if (split)
                        await SettleInput();
                    Pointer(MouseButton.Left, false);
                    await SettleInput();
                    Require(!popup.Visible && screen.SelectedUnitId is null,
                        $"{name} outside dismissal (split={split}) must not select the unit beneath it.");
                    Unchanged(name + " dismissal");
                    await SelectActor();
                }
                var endTurn = Button(screen, "EndTurn");
                var endTurnPoint = endTurn.GetGlobalRect().GetCenter();
                var endTurnPresses = 0;
                void CountEndTurn() => endTurnPresses++;
                endTurn.Pressed += CountEndTurn;
                try
                {
                    foreach (var split in new[] { false, true })
                    {
                        var popup = await Open(entry, name);
                        Require(!new Rect2(popup.Position, popup.Size).HasPoint(endTurnPoint),
                            name + " exposes an outside native EndTurn button for the dismissal regression.");
                        Motion(endTurnPoint);
                        await SettleInput();
                        Pointer(MouseButton.Left, true, endTurnPoint);
                        Require(endTurnPresses == 0 && session.Revision == 0,
                            name + " outside HUD press must not dispatch EndTurn.");
                        if (split)
                            await SettleInput();
                        Pointer(MouseButton.Left, false, endTurnPoint);
                        await SettleInput();
                        Require(!popup.Visible && endTurnPresses == 0 && screen.SelectedUnitId == actor.Id,
                            $"{name} outside HUD dismissal (split={split}) cannot activate EndTurn or change selection.");
                        Unchanged(name + " HUD dismissal");
                    }
                }
                finally
                {
                    if (IsInstanceValid(endTurn))
                        endTurn.Pressed -= CountEndTurn;
                }
                var closePopup = await Open(entry, name);
                var close = closePopup.GuiGetFocusOwner() as Button
                    ?? throw new InvalidOperationException(name + " must focus its native close control.");
                var closePoint = close.GetGlobalRect().GetCenter() + new Vector2(closePopup.Position.X, closePopup.Position.Y);
                Motion(closePoint);
                await SettleInput();
                Require(closePopup.GuiGetHoveredControl() == close, name + " native close positive control reaches the popup button.");
                Pointer(MouseButton.Left, true, closePoint);
                Pointer(MouseButton.Left, false, closePoint);
                await SettleInput();
                Require(!closePopup.Visible && screen.SelectedUnitId == actor.Id,
                    name + " internal close remains clickable and does not change the battlefield selection.");
                Unchanged(name + " native close");
                var selectedPopup = await Open(entry, name);
                Pointer(MouseButton.Right, true);
                Pointer(MouseButton.Right, false);
                await SettleInput();
                Require(screen.SelectedUnitId == actor.Id, name + " right dismissal cannot cancel the underlying selection.");
                selectedPopup.Hide();
                await SettleInput();
                Unchanged(name + " right dismissal");

                // Direct adapter coverage includes events native dialogs may consume before the map.
                var modal = await Open(entry, name);
                var mapPoint = map.GetGlobalTransformWithCanvas().AffineInverse() * at;
                foreach (var input in new InputEvent[]
                {
                    new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = mapPoint },
                    new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, CtrlPressed = true, Position = mapPoint },
                    new InputEventPanGesture { Position = mapPoint, Delta = new Vector2(32, 32) },
                    new InputEventScreenTouch { Index = 0, Position = mapPoint, Pressed = true },
                    new InputEventScreenDrag { Index = 0, Position = mapPoint + new Vector2(48, 24) },
                    new InputEventScreenTouch { Index = 0, Position = mapPoint + new Vector2(48, 24), Pressed = false }
                })
                {
                    using (input)
                        map.EmitSignal(Control.SignalName.GuiInput, input);
                }
                await SettleInput();
                Unchanged(name + " modal map admission");
                Require(screen.SelectedUnitId == actor.Id, name + " modal adapter events preserve selection.");
                modal.Hide();
                await SettleInput();

                // Integration coverage for the existing native focus-loss capture cancellation.
                viewport.GuiReleaseFocus();
                await KeyPress(Key.Escape);
                Motion();
                Pointer(MouseButton.Left, true);
                var heldPopup = await Open(entry, name);
                heldPopup.Hide();
                await SettleInput();
                Pointer(MouseButton.Left, false);
                await SettleInput();
                Require(screen.SelectedUnitId is null, name + " close cannot resume a preexisting held map gesture.");
                Unchanged(name + " held gesture");
                await SelectActor();
            }
            Require(hud.Projection is not null, "Native HUD remains bound after the complete popup gesture sequence.");
            var freshEndTurn = Button(screen, "EndTurn");
            var positivePresses = 0;
            void CountPositive() => positivePresses++;
            freshEndTurn.Pressed += CountPositive;
            try
            {
                var endTurnPoint = freshEndTurn.GetGlobalRect().GetCenter();
                Motion(endTurnPoint);
                await SettleInput();
                Require(viewport.GuiGetHoveredControl() == freshEndTurn && !freshEndTurn.Disabled,
                    "Fresh HUD positive control reaches the enabled EndTurn button.");
                Pointer(MouseButton.Left, true, endTurnPoint);
                Pointer(MouseButton.Left, false, endTurnPoint);
                await SettleInput();
                Require(positivePresses == 1 && session.Revision > 0,
                    "A separate click after dismissal still ends the turn exactly once.");
            }
            finally
            {
                if (IsInstanceValid(freshEndTurn))
                    freshEndTurn.Pressed -= CountPositive;
            }
        }
        finally
        {
            Retire(game);
            GetWindow().Size = originalSize;
            await Layout();
        }
    }
}
