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
            var initial = new ProjectedSession(new GrayFlagSession(new(engine, presentation, new JsonArray()), level), false, false);
            Bind(initial);
            // This detached screen has a Node parent, not an application-sized Control.
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = new Vector2(
                ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32(),
                ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32());
            await Layout();
            map = (Control)screen.FindChild("MapArea", true, false);
            world = map.GetNode<Node2D>("World");
            var snapshotReads = initial.SnapshotReads;
            map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = Field(new Vector2(10.5f, 15.5f) * 32) });
            map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = Field(new Vector2(10.6f, 15.5f) * 32) });
            Require(initial.SnapshotReads == snapshotReads, "Pointer freshness checks must not construct battle snapshots.");
            await CheckOverlayViewport();
            CheckInitialAndGestures();
            CheckZoomAndResize();
            screen.Size = new Vector2(960, 640);
            await Layout();
            CheckStationaryPointerFocus();
            await CheckForecastHandoff();
            CheckCancellation();
            CheckOutsideTerrain();
            await CheckFollowAndBusyActions();
            await CheckUnmountAndRebind();
            await PaintedPickingChecks.CheckSouthRoad(this, Require);
            GD.Print($"Native camera screen integration passed: {assertions} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Bind(ITacticalBattleSession session)
    {
        var scene = AuthoredScene.Load("res://data/scenes/c01-01.json");
        var rendering = BoardSceneRendering.Load(scene, level, engine.CreateState(level)["map"]!.AsObject());
        screen.BindSession(new(text, battleText, PortraitCatalogResources.Load(), session, new(AssetCatalog.Load(), EnvironmentCatalog.Load(),
            engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), level, scene);
        screen.ConfigureGround(rendering.Ground);
    }
    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private Vector2 SafeCenter() => new(map.Size.X / 2, (110 + map.Size.Y - 134) / 2);

    private async Task CheckOverlayViewport()
    {
        var originalSize = screen.Size;
        foreach (var size in new[] { new Vector2(960, 640), new Vector2(1180, 812) })
        {
            screen.Size = size;
            await Layout();
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            Bind(session);
            Near(map.Position, Vector2.Zero, "Overlay HUD does not offset the map origin");
            Near(map.Size, size, "Overlay HUD leaves the entire window available to the painted map");
            GuiButton(MouseButton.Middle, true, new Vector2(400, 300));
            CapturedMove(new Vector2(10000, 10000));
            CapturedButton(MouseButton.Middle, false, new Vector2(10000, 10000));
            Near(world.Position - new Vector2(74, 62) * world.Scale, new Vector2(16, 110),
                "Top/left safe insets apply exactly once, independently of authored scene margins");
            GuiButton(MouseButton.Middle, true, new Vector2(400, 300));
            CapturedMove(new Vector2(-10000, -10000));
            CapturedButton(MouseButton.Middle, false, new Vector2(-10000, -10000));
            Near(world.Position + (screen.Board.FieldSize + new Vector2(74, 74)) * world.Scale,
                size - new Vector2(16, 134), "Bottom/right safe insets apply exactly once at the pan limit");
            screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Space, Pressed = true });
            Require(screen.SelectedUnitId is not null, "Keyboard cycling selects an actual actor from an offscreen camera");
            var actor = session.GetTacticalQueries(0).Unit(screen.SelectedUnitId!.Value)!;
            var point = BoardField(screen.Board.Projection.CellCenter(new(actor.At.X, actor.At.Y)));
            var safe = new Rect2(new Vector2(16, 110), size - new Vector2(32, 244));
            Require(safe.HasPoint(point), $"Cycling reveals selected actor inside the unobscured safe rectangle at {size}: {point}");
            Require(session.Revision == 0, "Layout, pan and focus cannot advance battle state");
        }
        screen.Size = originalSize;
        await Layout();
        Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
    }

    private void CheckInitialAndGestures()
    {
        Require(map.Size.X > 0 && map.Size.Y > 0, "Usable native map viewport is laid out");
        Near(world.Scale, Vector2.One * 1.25f, "Initial screen scale is source zoom, not fit-to-panel");
        Require(screen.Board.Scale == Vector2.One, "Only the screen world owns camera scaling");
        screen.SelectHit(TacticalHit.Unit(new BoardCell(10, 15), 2));
        Require(screen.SelectedUnitId == 2, "Fixture actor is the real Torren unit");
        GuiButton(MouseButton.Right, true, new Vector2(-1000, -1000));
        Require(screen.SelectedUnitId == 2, "Right press outside the field must not cancel selection");
        var at = BoardField(ActorPoint());
        Require(new Rect2(Vector2.Zero, map.Size).HasPoint(at), $"Gesture fixture actor must be inside the map viewport: {at}, size {map.Size}, screen {screen.Size}, viewport {GetViewport().GetVisibleRect().Size}, world {world.Position}.");
        GuiButton(MouseButton.Right, true, at);
        Require(screen.SelectedUnitId is null, "Right press on a valid field cell cancels immediately");
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
            map.Size - new Vector2(16, 134), "A captured drag reaches the right and bottom safe edges with overlay padding applied once");
        var edge = world.Position;
        CapturedMove(new Vector2(500, 500));
        Near(world.Position, edge, "Release outside the panel ends the captured gesture");
    }

    private void CheckZoomAndResize()
    {
        Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
        GuiButton(MouseButton.Middle, true, new Vector2(250, 200));
        CapturedMove(new Vector2(150, 150));
        CapturedButton(MouseButton.Middle, false, new Vector2(150, 150));
        var scroll = new Vector2(74, 62) * world.Scale - world.Position;
        map.Size += new Vector2(20, 10);
        Near(new Vector2(74, 62) * world.Scale - world.Position, scroll, "Resize preserves scroll pixels");
        Near(world.Scale, Vector2.One * 1.25f, "Resize preserves readable zoom");
        var center = (SafeCenter() - world.Position) / world.Scale;
        Wheel(MouseButton.WheelUp, ctrl: true, factor: 10);
        Wheel(MouseButton.WheelUp, meta: true, factor: .01f);
        Near(world.Scale, Vector2.One * 1.25f, "Wheel deltas wait for a presentation frame");
        screen._Process(0);
        Near(world.Scale, Vector2.One * 1.45f, "Ctrl and Meta each contribute one source notch regardless of magnitude");
        Near((SafeCenter() - world.Position) / world.Scale, center, "Wheel zoom anchors the unobscured viewport center, not pointer location");
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

    private Vector2 ActorPoint() => ActorPoint(2, new BoardCell(10, 15));

    private Vector2 ActorPoint(int id, BoardCell at)
    {
        var actor = screen.Board.GetNode<Node2D>($"CanonicalProjection/Unit_{id}");
        var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
        var rect = sprite.GetRect();
        for (var y = rect.Position.Y + .5f; y < rect.End.Y; y += 1)
            for (var x = rect.Position.X + .5f; x < rect.End.X; x += 1)
            {
                var local = new Vector2(x, y);
                var point = screen.Board.ToLocal(sprite.ToGlobal(local));
                if (sprite.IsPixelOpaque(local) && screen.Board.Pick(point) == TacticalHit.Unit(at, id))
                    return point;
            }
        throw new InvalidOperationException($"Native pointer fixture needs an actual visible body pixel for Unit {id} at {at}.");
    }

    private async Task CheckForecastHandoff()
    {
        var originalWindowSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(960, 640);
        await Layout();
        // Detached interaction fixture: position one real enemy beside Torren, never a played battle.
        var initial = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var state = initial.Snapshot();
        var queries = initial.GetTacticalQueries(0);
        var origin = queries.Unit(2)!.At;
        var neighbor = queries.MoveStops(2).First(at => Math.Abs(at.X - origin.X) + Math.Abs(at.Y - origin.Y) == 1
            && !state["units"]!.AsArray().OfType<JsonObject>().Any(unit => unit["x"]!.GetValue<int>() == at.X && unit["y"]!.GetValue<int>() == at.Y));
        var enemy = state["units"]!.AsArray().OfType<JsonObject>().First(unit => unit["owner"]!.GetValue<int>() == 2);
        var enemyId = enemy["id"]!.GetValue<int>();
        enemy["x"] = neighbor.X;
        enemy["y"] = neighbor.Y;
        var session = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        var viewport = GetViewport();
        void Motion(Vector2 point) => viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        async Task Arm()
        {
            Bind(session);
            await Layout();
            screen.SelectHit(TacticalHit.Unit(origin, 2));
            screen.SelectHit(TacticalHit.Unit(neighbor, enemyId));
            Require(screen.Selection?.Kind == TacticalSelectionKind.Target, $"Adjacent-enemy fixture exposes an ordinary attack target selection: kind={screen.Selection?.Kind}, origin={origin}, neighbor={neighbor}, commands={string.Join(",", session.GetTacticalQueries(0).CommandsAt(2, origin).Select(command => command.Key + "/" + string.Join(";", command.Targets)))}.");
            await Layout();
            viewport.NotifyMouseExited();
            viewport.NotifyMouseEntered();
            Motion(world.Position + ActorPoint(enemyId, neighbor) * world.Scale);
            await Layout();
            Require(((TacticalHud)screen.FindChild("TacticalHud", true, false)).Projection!.Sections.Any(section => section.Key == "forecast"),
                $"A real visible enemy pixel must produce the current canonical forecast: point={world.Position + ActorPoint(enemyId, neighbor) * world.Scale}, hovered={viewport.GuiGetHoveredControl()?.Name}, size={screen.Size}, window={GetWindow().Size}, selection={screen.Selection?.Kind}, sections={string.Join(",", ((TacticalHud)screen.FindChild("TacticalHud", true, false)).Projection!.Sections.Select(section => section.Key))}.");
        }
        await Arm();
        var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
        var ground = session.GetTacticalQueries(0).InteractiveCells.First(at => at != origin && at != neighbor
            && !state["units"]!.AsArray().OfType<JsonObject>().Any(unit => unit["x"]!.GetValue<int>() == at.X && unit["y"]!.GetValue<int>() == at.Y)
            && screen.Board.Pick(screen.Board.Projection.CellContact(new(at.X, at.Y))) == TacticalHit.Ground(at)
            && new Rect2(new Vector2(350, 180), new Vector2(300, 200)).HasPoint(world.Position + screen.Board.Projection.CellContact(new(at.X, at.Y)) * world.Scale));
        Motion(world.Position + screen.Board.Projection.CellContact(new(ground.X, ground.Y)) * world.Scale);
        await Layout();
        Require(hud.Projection!.Sections.Any(section => section.Key == "forecast") && screen.Selection?.PreviewTarget?.UnitId == enemyId,
            "Crossing ordinary map ground on the way to HUD preserves the explicit target preview, independently of the activation cursor.");
        Require(hud.Projection!.Hint == text.Format("ui.hud.selection.preview_held"), "Pinned preview guidance never promises Enter on a non-target cursor.");
        screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
        Require(session.Revision == 0, "Retained preview cannot turn Enter on a non-target ground cursor into a combat order.");
        await Layout();
        var detail = (Button)hud.FindChild("ForecastDetailsButton", true, false);
        var point = detail.GetGlobalRect().GetCenter();
        Motion(point);
        await Layout();
        Require(hud.Projection!.Sections.Any(section => section.Key == "forecast") && detail.IsInsideTree(),
            $"Actual map-to-HUD pointer transfer preserves the controller's valid preview target and its live details control: preview={screen.Selection?.PreviewTarget}, forecast={hud.Projection!.Sections.Any(section => section.Key == "forecast")}, buttonAlive={GodotObject.IsInstanceValid(detail) && detail.IsInsideTree()}, hovered={viewport.GuiGetHoveredControl()?.Name}.");
        screen.ShowFeedback("待保留的拒绝原因");
        var retainedFeedback = (Label)screen.FindChild("BattleFeedback", true, false);
        var feedbackSurface = (Control)screen.FindChild("FeedbackPanel", true, false);
        Require(feedbackSurface.Visible, "The refusal is visible before opening a dossier.");
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
        await Layout();
        var popup = (PopupPanel)hud.FindChild("UnitDossier", true, false);
        Require(popup.Visible && hud.Projection!.Sections.Any(section => section.Key == "forecast-details") && session.Revision == 0,
            $"Clicking actual forecast details opens the latest projected facts without committing combat: popup={popup.Visible}, details={hud.Projection!.Sections.Any(section => section.Key == "forecast-details")}, revision={session.Revision}, preview={screen.Selection?.PreviewTarget}, hovered={viewport.GuiGetHoveredControl()?.Name}.");
        Require(!feedbackSurface.Visible && retainedFeedback.Text == "待保留的拒绝原因",
            "A dossier suppresses only competing feedback presentation, never its retained reason.");
        screen.ShowFeedback("弹层期间更新的拒绝原因");
        Require(!feedbackSurface.Visible && retainedFeedback.Text == "弹层期间更新的拒绝原因",
            "New feedback while a dossier is open remains retained and non-overlapping.");
        var close = (Button)hud.FindChild("DossierClose", true, false);
        Require(close.IsVisibleInTree() && close.Text == text.Format("ui.common.close") + "  Esc"
            && new Rect2(Vector2.Zero, popup.Size).Encloses(close.GetGlobalRect()), "The dossier has an explicit localized close affordance inside its bounds.");
        // Native pointer/focus transfer and a fast close can share one dispatch.
        screen._Notification((int)NotificationWMWindowFocusOut);
        close.EmitSignal(Button.SignalName.Pressed);
        await Layout();
        Require(feedbackSurface.Visible && retainedFeedback.Text == "弹层期间更新的拒绝原因", "Closing a dossier restores the latest retained feedback without replaying an event.");
        Require(!popup.Visible && hud.Projection!.Sections.Any(section => section.Key == "forecast")
            && GetViewport().GuiGetFocusOwner()?.Name == "ForecastDetailsButton" && session.Revision == 0,
            "Visible close returns focus to the current forecast entry and preserves prediction without executing combat.");
        ((Button)hud.FindChild("ForecastDetailsButton", true, false)).EmitSignal(Button.SignalName.Pressed);
        await Layout();
        Require(popup.Visible, "The current details entry reopens after explicit close.");
        viewport.PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        viewport.PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = false });
        await Layout();
        Require(!popup.Visible && hud.Projection!.Sections.Any(section => section.Key == "forecast") && session.Revision == 0,
            "Native Esc closes only the dossier and retains the active forecast.");
        GetWindow().EmitSignal(Window.SignalName.MouseExited);
        await Layout();
        Require(!hud.Projection!.Sections.Any(section => section.Key == "forecast"), "HUD-to-window-out also clears the preview; it cannot wait for a second map exit.");
        await Arm();
        screen._Notification((int)NotificationWMWindowFocusOut);
        await Layout();
        Require(!hud.Projection!.Sections.Any(section => section.Key == "forecast"), "Window focus loss still clears the preview.");
        await Arm();
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = new Vector2(-5000, -5000) });
        Require(!hud.Projection!.Sections.Any(section => section.Key == "forecast"), "Invalid map positions are not treated as HUD input handoff.");
        screen.SelectHit(TacticalHit.Ground(ground));
        var feedback = (Label)screen.FindChild("BattleFeedback", true, false);
        Require(feedback.Visible && ((Control)screen.FindChild("FeedbackPanel", true, false)).Visible, "A refused target has readable material-backed feedback.");
        screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        Require(!feedback.Visible && !((Control)screen.FindChild("FeedbackPanel", true, false)).Visible && screen.Selection?.PreviewTarget is null,
            "Successful cancel/new selection clears stale refusal feedback before any battle command.");
        await Arm();
        Bind(initial);
        Require(!hud.Projection!.Sections.Any(section => section.Key == "forecast") && session.Revision == 0,
            "Rebinding clears target preview and every pointer-only probe leaves canonical state unchanged.");
        GetWindow().Size = originalWindowSize;
        await Layout();
    }

    private void CheckStationaryPointerFocus()
    {
        string? HoveredCell()
        {
            var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
            return hud.Projection!.Sections.FirstOrDefault(section => section.Key == "tile")?.Rows.First().Label;
        }
        foreach (var zoom in new[] { false, true })
        {
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            Bind(session);
            var pointer = world.Position + ActorPoint() * world.Scale;
            Require(new Rect2(Vector2.Zero, map.Size).HasPoint(pointer), "Stationary pointer starts on the actual map viewport.");
            map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = pointer });
            Require(HoveredCell() == "10, 15", "Initial pointer focus must resolve the visible Unit hit before camera motion.");
            var before = world.Position;
            for (var step = 0; step < (zoom ? 8 : 3); step++)
                map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton
                {
                    ButtonIndex = zoom ? MouseButton.WheelUp : MouseButton.WheelDown,
                    Pressed = true,
                    CtrlPressed = zoom,
                    Position = pointer
                });
            screen._Process(0);
            var now = screen.Board.Pick((pointer - world.Position) / world.Scale);
            Require(world.Position != before && now != TacticalHit.Unit(new(10, 15), 2),
                "Pan/zoom must actually move a different rendered subject beneath the stationary pointer.");
            Require(HoveredCell() == (now is { } hit ? $"{hit.Cell.X}, {hit.Cell.Y}" : null),
                "Camera transforms must repick the exact remembered pointer without another mouse-motion event.");
            screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            Require(screen.SelectedUnitId != 2 && session.Revision == 0,
                "Enter after stationary-pointer pan/zoom must not activate the Unit identity from the old camera projection.");
            if (now is { Kind: TacticalHitKind.Unit, UnitId: { } id } && session.GetTacticalQueries(0).Unit(id) is { CanAct: true })
                Require(screen.SelectedUnitId == id, "Enter must activate the newly visible actionable Unit subject, not discard pointer focus.");
        }

        var keyboardSession = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        Bind(keyboardSession);
        var stationary = world.Position + ActorPoint() * world.Scale;
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = stationary });
        var originalCamera = world.Position;
        for (var step = 0; step < 18; step++)
            screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Right, Pressed = true });
        Require(world.Position != originalCamera && HoveredCell() == "28, 15",
            "Keyboard navigation must retain its explicit focused ground cell when Reveal pans the camera.");
        var underneath = screen.Board.Pick((stationary - world.Position) / world.Scale);
        Require(underneath?.Cell != new BoardCell(28, 15), "Keyboard reveal must exercise a pointer position distinct from the keyboard focus.");
        screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
        Require(screen.SelectedUnitId is null && keyboardSession.Revision == 0 && HoveredCell() == "28, 15",
            "Enter after keyboard Reveal must preserve keyboard Ground intent rather than repick the obsolete physical pointer.");

        var cycleSession = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        Bind(cycleSession);
        var oldPointer = world.Position + ActorPoint() * world.Scale;
        map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = oldPointer });
        var queries = cycleSession.GetTacticalQueries(0);
        var firstActor = queries.Actors().First();
        var focusAt = queries.Unit(firstActor)!.At;
        screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Space, Pressed = true });
        Require(screen.SelectedUnitId == firstActor && HoveredCell() == $"{focusAt.X}, {focusAt.Y}" && cycleSession.Revision == 0,
            "Cycling must transfer cursor ownership to the selected actor before its camera reveal can repick an old pointer.");
        screen._UnhandledKeyInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
        Require(screen.Selection is { Kind: TacticalSelectionKind.Destination } preview && preview.Destination == focusAt
            && cycleSession.Revision == 0,
            "Enter after cycling previews that actor's own cell, without committing wait or activating an obsolete pointer target.");
    }

    private void CheckCancellation()
    {
        Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
        var at = BoardField(ActorPoint());
        GuiButton(MouseButton.Left, true, at);
        screen._Notification((int)Node.NotificationWMWindowFocusOut);
        GuiButton(MouseButton.Left, false, at);
        Require(screen.SelectedUnitId is null, "Window focus loss cancels a pending click");
        GuiButton(MouseButton.Left, true, at);
        Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
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
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
        var snapshot = session.GetSnapshot();
        var state = JsonNode.Parse(snapshot.State.GetRawText())!.AsObject();
        state["map"]!["tiles"]![0] = "c01.outside";
        var outside = GrayFlagSession.FromSave(new(engine, presentation, new JsonArray()), engine.SaveBattle(state));
        Bind(outside);
        var materials = WorldMaterialsManifest.Load();
        materials.TerrainTags["c01.outside"] = [];
        screen.ConfigureGround(TerrainView.Factory(materials));
        GuiButton(MouseButton.Middle, true, new Vector2(100, 100));
        CapturedMove(new Vector2(10000, 10000));
        CapturedButton(MouseButton.Middle, false, new Vector2(10000, 10000));
        screen.SelectHit(TacticalHit.Unit(new BoardCell(10, 15), 2));
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
        var actorCell = session.GetTacticalQueries(0).Unit(2)!.At;
        var mapState = state["map"]!;
        var actorTerrain = mapState["tiles"]![actorCell.Y * mapState["width"]!.GetValue<int>() + actorCell.X]!.GetValue<string>();
        materials.TerrainTags[actorTerrain] = ["outside"];
        screen.ConfigureGround(TerrainView.Factory(materials));
        var actor = BoardField(ActorPoint());
        GuiButton(MouseButton.Left, true, actor);
        CapturedButton(MouseButton.Left, false, actor);
        Require(screen.SelectedUnitId == 2, "Material outside tags cannot block primary input on canonical playable terrain");
        GuiButton(MouseButton.Right, true, actor);
        Require(screen.SelectedUnitId is null, "Material outside tags cannot block right-click cancellation on canonical playable terrain");
        Require(session.GetSnapshot().State.GetRawText() == snapshot.State.GetRawText(), "Disagreeing material metadata cannot advance the session");
    }

    private async Task CheckFollowAndBusyActions()
    {
        foreach (var (computer, hidden, far) in new[] { (false, false, false), (false, false, true), (true, false, true), (true, true, true) })
        {
            var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), level);
            var projected = new ProjectedSession(session, computer, hidden);
            Bind(projected);
            // Cover both visible hover and offscreen human no-follow; AI cases prove follow/concealment.
            var panTo = far ? new Vector2(-10000, -10000) : new Vector2(80, 80);
            GuiButton(MouseButton.Middle, true, new Vector2(100, 100));
            CapturedMove(panTo);
            CapturedButton(MouseButton.Middle, false, panTo);
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
                expected.Resize(new(map.Size.X, map.Size.Y), new(16, 110, 16, 134));
                var projectedDestination = screen.Board.Projection.CellCenter(new(destination.X, destination.Y));
                expected.CenterOn(new(projectedDestination.X, projectedDestination.Y));
                Near(during, new((float)expected.FieldOrigin.X, (float)expected.FieldOrigin.Y), "Visible AI command follows the canonical final path point");
            }
            else
                Near(during, previous, hidden ? "Hidden AI command cannot reveal its camera location" : "Human movement does not auto-follow");
            if (!hidden)
                Require(screen.IsPresenting, "Visible moves must start the timeline exercised by the busy-state assertions.");
            if (screen.IsPresenting)
            {
                var revision = session.GetSnapshot().Revision;
                exit.EmitSignal(Button.SignalName.Pressed);
                restart.EmitSignal(Button.SignalName.Pressed);
                save.EmitSignal(Button.SignalName.Pressed);
                Require(saves == 0, "Application callbacks must wait until native popup dispatch completes.");
                await Layout();
                Require(exits == 0 && restarts == 0 && saves == 1, "Busy exit/restart are blocked while save remains available");
                Wheel(MouseButton.WheelUp, ctrl: true);
                screen._Process(0);
                GuiButton(MouseButton.Left, false, new Vector2(150, 150));
                Near(world.Position, during, "Busy map input cannot move the camera");
                Require(session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null, "Busy gestures cannot dispatch or select");
                if (!computer && far)
                {
                    var at = Field(new Vector2(destination.X + .5f, destination.Y + .5f) * 32);
                    Require(!new Rect2(Vector2.Zero, map.Size).HasPoint(at), "Far-panned human fixture must exercise an offscreen target.");
                    map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = at });
                    var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                    Require(!hud.Projection!.Sections.Any(section => section.Key == "tile")
                        && session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null,
                        "Off-viewport busy motion must not expose terrain or change tactical state.");
                }
                if (!computer && !far)
                {
                    // Inspect an actually stationary actor. The moving sprite still covers its old cell,
                    // while its canonical occupant has already moved to the committed destination.
                    var target = session.GetTacticalQueries(0).Unit(5)!.At;
                    var at = BoardField(ActorPoint(5, target));
                    Require(new Rect2(Vector2.Zero, map.Size).HasPoint(at), "Busy hover fixture must lie inside the actual map viewport.");
                    map.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseMotion { Position = at });
                    var hud = (TacticalHud)screen.FindChild("TacticalHud", true, false);
                    Require(hud.Projection!.Sections.Any(section => section.Key == "tile") && hud.Projection.Portrait?.ActorKey == "roderick-reserve",
                        $"Busy pointer motion projects hovered terrain and visible inspection without tactical selection: target {target}, picked {screen.Board.Pick((at - world.Position) / world.Scale)}, portrait {hud.Projection.Portrait?.ActorKey}, sections {string.Join(',', hud.Projection.Sections.Select(section => section.Key))}.");
                    var portrait = hud.Projection.Portrait;
                    map.EmitSignal(Control.SignalName.MouseExited);
                    await Layout();
                    Require(!hud.Projection!.Sections.Any(section => section.Key == "tile") && hud.Projection.Portrait == portrait,
                        "Busy mouse leave clears cursor terrain but preserves source inspection.");
                    Require(session.GetSnapshot().Revision == revision && screen.SelectedUnitId is null, "Busy hover and leave never commit or arm a unit.");
                }

            }
            Settle();
            GuiButton(MouseButton.Left, false, new Vector2(150, 150));
            Require(screen.SelectedUnitId is null, "Presentation start canceled the pre-existing pointer gesture");
            exit.EmitSignal(Button.SignalName.Pressed);
            await Layout();
            Require(exits == 1, "Settled camp exit invokes once through the current mounted binding");
            Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
            exit.EmitSignal(Button.SignalName.Pressed);
            await Layout();
            Require(exits == 1, "An old-binding camp button cannot invoke after a rebind");
        }
    }

    private async Task CheckUnmountAndRebind()
    {
        Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
        var exits = 0;
        var restarts = 0;
        screen.BindApplicationActions(onRestart: () => restarts++, onExit: () => exits++);
        var exit = Buttons(screen).Single(button => button.Text == "战役营地");
        var restart = Buttons(screen).Single(button => button.Text == "重新开始");
        restart.EmitSignal(Button.SignalName.Pressed);
        await Layout();
        var confirmation = screen.GetChildren().OfType<ConfirmationDialog>().Single();
        Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
        screen.BindApplicationActions(onRestart: () => restarts += 100, onExit: () => exits += 100);
        confirmation.EmitSignal(ConfirmationDialog.SignalName.Confirmed);
        Require(restarts == 0, "A superseded restart dialog cannot invoke the newer binding's callback");
        GuiButton(MouseButton.Left, true, BoardField(ActorPoint()));
        RemoveChild(screen);
        exit.EmitSignal(Button.SignalName.Pressed);
        screen._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        Require(exits == 0, "Unmounted application callback cannot exit camp");
        AddChild(screen);
        Bind(new GrayFlagSession(new(engine, presentation, new JsonArray()), level));
        GuiButton(MouseButton.Left, false, BoardField(ActorPoint()));
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
    private Vector2 BoardField(Vector2 point) => world.Position + point * world.Scale;
    private Vector2 Field(Vector2 point) => world.Position + screen.Board.Projection.Project(point) * world.Scale;
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
        public long Revision => inner.Revision;
        public int SnapshotReads
        {
            get; private set;
        }
        public BattleSnapshot GetSnapshot()
        {
            SnapshotReads++;
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
        public TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, TacticalHit? cursor, int? inspectId, bool busy) => inner.ProjectHud(stamp, selection, cursor, inspectId, busy);
        public BattleDispatchResult Undo(long expectedRevision) => inner.Undo(expectedRevision);
        public BattleDispatchResult Dispatch(JsonElement action, long expectedRevision) => inner.Dispatch(action, expectedRevision);
    }
}
