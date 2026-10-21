#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Presentation.Feedback;
using Empire.Localization;
using Empire.Core.Engine;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Real-session screen input and lifecycle regression checks, with no desktop acceptance claim.</summary>
public partial class CameraIntegrationChecks : Node
{
    private int assertions;
    private JsonObject level = null!;
    private JsonArray packs = null!;
    private BattleEngine engine = null!;
    private GameText text = null!;
    private SessionPresentationText presentation = null!;
    private BattleText battleText = null!;
    private BattleScreen screen = null!;
    private Control map = null!;
    private Node2D world = null!;

    public override async void _Ready()
    {
        try
        {
            level = BattleTextChecks.OpeningLevel();
            packs = PresentationJson.Load<JsonArray>("res://data/content-packs.json");
            engine = new BattleEngine(packs);
            text = GodotTextCatalog.LoadSimplifiedChinese();
            presentation = BattleTextChecks.Session(engine, text);
            battleText = presentation.Battle;
            screen = new BattleScreen();
            AddChild(screen);
            screen.Board.AdvanceAnimationsAutomatically = false;
            Bind(new GrayFlagSession(new(engine, presentation), level));
            await Layout();
            map = (Control)screen.FindChild("MapArea", true, false);
            world = map.GetNode<Node2D>("World");
            CheckInitialAndGestures();
            CheckZoomAndResize();
            CheckCancellation();
            CheckOutsideTerrain();
            CheckFollowAndBusyActions();
            CheckUnmountAndRebind();
            GD.Print($"Native camera screen integration passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Bind(ITacticalBattleSession session) => screen.BindSession(new(text, battleText, session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(), _ => false, null)), level, AuthoredScene.Load("res://data/scenes/c01-01.json"));
    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void CheckInitialAndGestures()
    {
        Require(map.Size.X > 0 && map.Size.Y > 0, "Usable native map viewport is laid out");
        Near(world.Scale, Vector2.One * 1.25f, "Initial screen scale is source zoom, not fit-to-panel");
        Require(screen.Board.Scale == Vector2.One, "Only the screen world owns camera scaling");
        screen.SelectCell(new BoardCell(5, 8));
        Require(screen.SelectedUnitId == 2, "Fixture actor is the real Torren unit");
        GuiButton(MouseButton.Right, true, new Vector2(-1000, -1000));
        Require(screen.SelectedUnitId == 2, "Right press outside the field must not cancel selection");
        GuiButton(MouseButton.Right, true, Field(new Vector2(5.5f, 8.5f) * 32));
        Require(screen.SelectedUnitId is null, "Right press on a valid field cell cancels immediately");
        var at = Field(new Vector2(5.5f, 8.5f) * 32);
        GuiButton(MouseButton.Left, true, at);
        Require(screen.SelectedUnitId is null, "Primary down does not select");
        CapturedButton(MouseButton.Left, false, at);
        Require(screen.SelectedUnitId == 2, "Matching primary release selects through current projection");
        GuiButton(MouseButton.Right, true, at);
        GuiButton(MouseButton.Left, true, at);
        CapturedMove(at + new Vector2(-6, 0));
        CapturedButton(MouseButton.Left, false, at + new Vector2(-6, 0));
        Require(screen.SelectedUnitId is null, "Exactly six pixels starts a drag rather than selecting");
        var before = world.Position;
        GuiButton(MouseButton.Middle, true, new Vector2(250, 200));
        CapturedMove(new Vector2(150, 150));
        CapturedButton(MouseButton.Middle, false, new Vector2(150, 150));
        Near(world.Position, before + new Vector2(-100, -50), "Middle drag pans in screen pixels without scale multiplication");
        Require(screen.SelectedUnitId is null, "Middle release never selects");
        GuiButton(MouseButton.Middle, true, new Vector2(100, 100));
        CapturedMove(new Vector2(-10000, -10000));
        CapturedButton(MouseButton.Middle, false, new Vector2(-10000, -10000));
        Near(world.Position + (screen.Board.FieldSize + new Vector2(74, 74)) * world.Scale,
            map.Size, "A captured drag beyond the panel reaches the right and bottom scene edges");
        var edge = world.Position;
        CapturedMove(new Vector2(500, 500));
        Near(world.Position, edge, "Release outside the panel ends the captured gesture");
    }

    private void CheckZoomAndResize()
    {
        Bind(new GrayFlagSession(new(engine, presentation), level));
        GuiButton(MouseButton.Middle, true, new Vector2(250, 200));
        CapturedMove(new Vector2(150, 150));
        CapturedButton(MouseButton.Middle, false, new Vector2(150, 150));
        var scroll = new Vector2(74, 62) * world.Scale - world.Position;
        map.Size += new Vector2(20, 10);
        Near(new Vector2(74, 62) * world.Scale - world.Position, scroll, "Resize preserves scroll pixels");
        Near(world.Scale, Vector2.One * 1.25f, "Resize preserves readable zoom");
        var center = (map.Size / 2 - world.Position) / world.Scale;
        Wheel(MouseButton.WheelUp, ctrl: true, factor: 10);
        Wheel(MouseButton.WheelUp, meta: true, factor: .01f);
        Near(world.Scale, Vector2.One * 1.25f, "Wheel deltas wait for a presentation frame");
        screen._Process(0);
        Near(world.Scale, Vector2.One * 1.45f, "Ctrl and Meta each contribute one source notch regardless of magnitude");
        Near((map.Size / 2 - world.Position) / world.Scale, center, "Wheel zoom anchors the viewport center, not pointer location");
        ((Button)screen.FindChild("ZoomIn", true, false)).EmitSignal(Button.SignalName.Pressed);
        screen._Process(0);
        Near(world.Scale, Vector2.One * 1.6f, "Source plus button contributes 0.15 through the same frame path");
        ((Button)screen.FindChild("ZoomOut", true, false)).EmitSignal(Button.SignalName.Pressed);
        screen._Process(0);
        Near(world.Scale, Vector2.One * 1.45f, "Source minus button contributes -0.15");
        var before = world.Position;
        Wheel(MouseButton.WheelDown);
        Near(world.Position, before + new Vector2(0, -32), "Native ordinary wheel scrolls one tile-sized screen notch");
        Wheel(MouseButton.WheelRight);
        Near(world.Position, before + new Vector2(-32, -32), "Horizontal wheel scrolls horizontally");
        for (var i = 0; i < 30; i++)
            Wheel(MouseButton.WheelUp, ctrl: true);
        screen._Process(0);
        Near(world.Scale, Vector2.One * 2.2f, "Maximum zoom clamp");
        for (var i = 0; i < 40; i++)
            Wheel(MouseButton.WheelDown, ctrl: true);
        screen._Process(0);
        Near(world.Scale, Vector2.One * .5f, "Minimum zoom clamp");
    }

    private void CheckCancellation()
    {
        Bind(new GrayFlagSession(new(engine, presentation), level));
        var at = Field(new Vector2(5.5f, 8.5f) * 32);
        GuiButton(MouseButton.Left, true, at);
        screen._Notification((int)Node.NotificationWMWindowFocusOut);
        GuiButton(MouseButton.Left, false, at);
        Require(screen.SelectedUnitId is null, "Window focus loss cancels a pending click");
        GuiButton(MouseButton.Left, true, at);
        Bind(new GrayFlagSession(new(engine, presentation), level));
        GuiButton(MouseButton.Left, false, at);
        Require(screen.SelectedUnitId is null, "Session rebind cancels a pending click");
        Near(world.Scale, Vector2.One * 1.25f, "A new binding starts at the source initial zoom");
        GuiButton(MouseButton.Left, true, at);
        screen._Input(new InputEventMouseButton { Position = Global(at), ButtonIndex = MouseButton.Left, Canceled = true });
        GuiButton(MouseButton.Left, false, at);
        Require(screen.SelectedUnitId is null, "Native canceled mouse release never selects");
        var beforeTouch = world.Position;
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventScreenTouch { Index = 0, Pressed = true, Position = at });
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventScreenTouch { Index = 1, Pressed = true, Position = at });
        screen._Input(new InputEventScreenDrag { Index = 1, Position = Global(at + new Vector2(100, 100)) });
        screen._Input(new InputEventScreenTouch { Index = 1, Pressed = false, Position = Global(at) });
        Near(world.Position, beforeTouch, "A secondary touch cannot move or release the primary gesture");
        screen._Input(new InputEventScreenTouch { Index = 0, Pressed = false, Position = Global(at) });
        Require(screen.SelectedUnitId == 2, "Primary touch release selects using the same camera projection");
        GuiButton(MouseButton.Right, true, at);
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventScreenTouch { Index = 0, Pressed = true, Position = at });
        screen._Input(new InputEventScreenTouch { Index = 0, Canceled = true, Position = Global(at) });
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventScreenTouch { Index = 0, Pressed = false, Position = at });
        Require(screen.SelectedUnitId is null, "Canceled touch cannot become a later click");
    }

    private void CheckOutsideTerrain()
    {
        var session = new GrayFlagSession(new(engine, presentation), level);
        var snapshot = session.GetSnapshot();
        var state = JsonNode.Parse(snapshot.State.GetRawText())!.AsObject();
        state["map"]!["tiles"]![0] = "c01.outside";
        var outside = GrayFlagSession.FromSave(new(engine, presentation), engine.SaveBattle(state));
        Bind(outside);
        var materials = WorldMaterialsManifest.Load();
        materials.TerrainTags["c01.outside"] = [];
        screen.ConfigureTerrain(materials);
        GuiButton(MouseButton.Middle, true, new Vector2(100, 100));
        CapturedMove(new Vector2(10000, 10000));
        CapturedButton(MouseButton.Middle, false, new Vector2(10000, 10000));
        screen.SelectCell(new BoardCell(5, 8));
        GuiButton(MouseButton.Right, true, Field(new Vector2(16, 16)));
        Require(screen.SelectedUnitId == 2, "Canonical outside terrain excludes holes even when material tags say playable");
        GuiButton(MouseButton.Left, true, Field(new Vector2(16, 16)));
        CapturedButton(MouseButton.Left, false, Field(new Vector2(16, 16)));
        Require(screen.SelectedUnitId == 2, "Outside-ground cells cannot receive primary tactical selection");
        var previous = world.Position;
        GuiButton(MouseButton.Middle, true, Field(new Vector2(16, 16)));
        CapturedMove(Field(new Vector2(16, 16)) + new Vector2(-20, -20));
        CapturedButton(MouseButton.Middle, false, Field(new Vector2(16, 16)));
        Require(world.Position != previous, "Irregular holes remain valid camera drag surfaces");
        Require(session.GetSnapshot().State.GetRawText() == snapshot.State.GetRawText(), "Detached fixture setup never mutates the original session");
        Bind(session);
        materials = WorldMaterialsManifest.Load();
        materials.TerrainTags["plain"] = ["outside"];
        screen.ConfigureTerrain(materials);
        var actor = Field(new Vector2(5.5f, 8.5f) * 32);
        GuiButton(MouseButton.Left, true, actor);
        CapturedButton(MouseButton.Left, false, actor);
        Require(screen.SelectedUnitId == 2, "Material outside tags cannot block primary input on canonical playable terrain");
        GuiButton(MouseButton.Right, true, actor);
        Require(screen.SelectedUnitId is null, "Material outside tags cannot block right-click cancellation on canonical playable terrain");
        Require(session.GetSnapshot().State.GetRawText() == snapshot.State.GetRawText(), "Disagreeing material metadata cannot advance the session");
    }

    private void CheckFollowAndBusyActions()
    {
        foreach (var (computer, hidden) in new[] { (false, false), (true, false), (true, true) })
        {
            var session = new GrayFlagSession(new(engine, presentation), level);
            var projected = new ProjectedSession(session, computer, hidden);
            Bind(projected);
            GuiButton(MouseButton.Middle, true, new Vector2(100, 100));
            CapturedMove(new Vector2(-10000, -10000));
            CapturedButton(MouseButton.Middle, false, new Vector2(-10000, -10000));
            var previous = world.Position;
            var exits = 0;
            var saves = 0;
            var restarts = 0;
            screen.BindApplicationActions(() => saves++, null, () => restarts++, () => exits++);
            var exit = Buttons(screen).Single(button => button.Text == "战役营地");
            var save = Buttons(screen).Single(button => button.Text == "保存");
            var restart = Buttons(screen).Single(button => button.Text == "重新开始");
            GuiButton(MouseButton.Left, true, new Vector2(150, 150));
            var destination = TacticalSceneInputChecks.Move(screen, projected);
            var during = world.Position;
            if (computer && !hidden)
            {
                var expected = new BattlefieldCamera(new(screen.Board.FieldSize.X, screen.Board.FieldSize.Y),
                    new(74, 62, 74, 74), new(0, 0));
                expected.Resize(new(map.Size.X, map.Size.Y));
                expected.CenterOn(new((destination.X + .5) * 32, (destination.Y + .5) * 32));
                Near(during, new((float)expected.FieldOrigin.X, (float)expected.FieldOrigin.Y), "Visible AI command follows the canonical final path point");
            }
            else
                Near(during, previous, hidden ? "Hidden AI command cannot reveal its camera location" : "Human movement does not auto-follow");
            if (screen.IsPresenting)
            {
                var revision = session.GetSnapshot().Revision;
                exit.EmitSignal(Button.SignalName.Pressed);
                restart.EmitSignal(Button.SignalName.Pressed);
                save.EmitSignal(Button.SignalName.Pressed);
                Require(exits == 0 && restarts == 0 && saves == 1, "Busy exit/restart are blocked while save remains available");
                Wheel(MouseButton.WheelUp, ctrl: true);
                screen._Process(0);
                GuiButton(MouseButton.Left, false, new Vector2(150, 150));
                Near(world.Position, during, "Busy map input cannot move the camera");
                Require(session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null, "Busy gestures cannot dispatch or select");
                if (!computer)
                {
                    var target = destination;
                    map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = Field(new Vector2(target.X + .5f, target.Y + .5f) * 32) });
                    var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                    Require(hud.Projection!.Sections.Any(section => section.Key == "tile") && hud.Projection.Portrait?.ActorKey == "campaign-torren",
                        "Busy pointer motion projects hovered terrain and visible inspection without tactical selection.");
                    var portrait = hud.Projection.Portrait;
                    map.EmitSignal(Control.SignalName.MouseExited);
                    Require(!hud.Projection!.Sections.Any(section => section.Key == "tile") && hud.Projection.Portrait == portrait,
                        "Busy mouse leave clears cursor terrain but preserves source inspection.");
                    Require(session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null, "Busy hover and leave never commit or arm a unit.");
                }

            }
            Settle();
            GuiButton(MouseButton.Left, false, new Vector2(150, 150));
            Require(screen.SelectedUnitId is null, "Presentation start canceled the pre-existing pointer gesture");
            exit.EmitSignal(Button.SignalName.Pressed);
            Require(exits == 1, "Settled camp exit invokes once through the current mounted binding");
            Bind(new GrayFlagSession(new(engine, presentation), level));
            exit.EmitSignal(Button.SignalName.Pressed);
            Require(exits == 1, "An old-binding camp button cannot invoke after a rebind");
        }
    }

    private void CheckUnmountAndRebind()
    {
        Bind(new GrayFlagSession(new(engine, presentation), level));
        var exits = 0;
        var restarts = 0;
        screen.BindApplicationActions(onRestart: () => restarts++, onExit: () => exits++);
        var exit = Buttons(screen).Single(button => button.Text == "战役营地");
        var restart = Buttons(screen).Single(button => button.Text == "重新开始");
        restart.EmitSignal(Button.SignalName.Pressed);
        var confirmation = screen.GetChildren().OfType<ConfirmationDialog>().Single();
        Bind(new GrayFlagSession(new(engine, presentation), level));
        screen.BindApplicationActions(onRestart: () => restarts += 100, onExit: () => exits += 100);
        confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Require(restarts == 0, "A superseded restart dialog cannot invoke the newer binding's callback");
        GuiButton(MouseButton.Left, true, Field(new Vector2(5.5f, 8.5f) * 32));
        RemoveChild(screen);
        exit.EmitSignal(Button.SignalName.Pressed);
        screen._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        Require(exits == 0, "Unmounted application callback cannot exit camp");
        AddChild(screen);
        Bind(new GrayFlagSession(new(engine, presentation), level));
        GuiButton(MouseButton.Left, false, Field(new Vector2(5.5f, 8.5f) * 32));
        Require(screen.SelectedUnitId is null, "Reattached screen cannot replay an old held gesture");
        Near(world.Scale, Vector2.One * 1.25f, "Explicit rebind after reattach restores a usable camera");
        screen.QueueFree();
    }

    private void Settle()
    {
        for (var i = 0; i < 100 && screen.IsPresenting; i++)
            screen.Board.AdvancePresentation(.1);
        Require(!screen.IsPresenting, "Presentation settles before the next order");
    }
    private Vector2 Field(Vector2 point) => world.Position + point * world.Scale;
    private Vector2 Global(Vector2 point) => map.GetGlobalTransformWithCanvas() * point;
    private void GuiButton(MouseButton button, bool pressed, Vector2 point) => map.EmitSignal(Control.SignalName.GuiInput,
        new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = point });
    private void CapturedButton(MouseButton button, bool pressed, Vector2 point) => screen._Input(
        new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = Global(point) });
    private void CapturedMove(Vector2 point) => screen._Input(new InputEventMouseMotion { Position = Global(point) });
    private void Wheel(MouseButton button, bool ctrl = false, bool meta = false, float factor = 1) =>
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = true,
            Position = new Vector2(13, 17),
            CtrlPressed = ctrl,
            MetaPressed = meta,
            Factor = factor
        });
    private static IEnumerable<Button> Buttons(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button button)
                yield return button;
            foreach (var descendant in Buttons(child))
                yield return descendant;
        }
    }
    private void Near(Vector2 actual, Vector2 expected, string message) => Require(actual.DistanceTo(expected) < .002f,
        $"{message}: expected {expected}, actual {actual}");
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ProjectedSession(GrayFlagSession inner, bool computer, bool hidden) : ITacticalBattleSession
    {
        public BattleSnapshot GetSnapshot()
        {
            var snapshot = inner.GetSnapshot();
            if (snapshot.Transition is not { } original)
                return snapshot;
            var transition = new BattlePresentationTransition(original.Revision, original.Before, original.Action,
                original.Events, original.After, computer, hidden ? new HashSet<int>() : original.BeforeVisibleUnitIds,
                original.EventDescriptions);
            return snapshot with
            {
                Transition = transition
            };
        }
        public ITacticalSelectionQueries GetTacticalQueries(long binding) => inner.GetTacticalQueries(binding);
        public TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, BoardCell? cursor, int? inspectId, bool busy) => inner.ProjectHud(stamp, selection, cursor, inspectId, busy);
        public BattleDispatchResult Undo(long expectedRevision) => inner.Undo(expectedRevision);
        public BattleDispatchResult Dispatch(JsonElement action, long expectedRevision) => inner.Dispatch(action, expectedRevision);
    }
}
