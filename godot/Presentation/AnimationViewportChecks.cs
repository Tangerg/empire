#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Localization;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class AnimationChecks
{
    private async Task CheckAnnouncementLifetime(BoardView board)
    {
        var turn = new JsonObject { ["type"] = "turnStart", ["player"] = 1, ["turn"] = 3 };
        var transition = Transition(Copy(), Copy(), IdleAction(), [turn]);
        var unrelated = new Control { Name = "UnrelatedFeedback", MouseFilter = Control.MouseFilterEnum.Ignore };
        viewportFeedback.AddChild(unrelated);
        var completed = new List<int>();
        void Presented(BattlePresentationTransition _, int index) => completed.Add(index);
        board.EventPresented += Presented;
        try
        {
            foreach (var phase in new[] { "entrance", "hold", "exit" })
            {
                var task = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, phase == "entrance" ? .11 : .2201);
                if (phase == "exit")
                {
                    await Tick(board, .4201);
                    await Tick(board, .11);
                }
                Require(board.IsPresenting && viewportFeedback.GetNodeOrNull("ViewportEffects/TurnAnnouncement") is not null,
                    $"Cancellation fixture reaches the actual announcement {phase}.");
                board.CancelPresentation();
                Require(!await task && completed.Count == 0 && board.GetNodeOrNull("BattleEffects") is null
                    && viewportFeedback.GetNodeOrNull("ViewportEffects") is null && unrelated.GetParent() == viewportFeedback,
                    $"Cancelling during {phase} releases both owned effect roots, keeps unrelated feedback and publishes no unfinished receipt.");
            }

            var before = Copy();
            var after = Copy();
            var id = Int(before["units"]![0]!.AsObject(), "id");
            after["units"]![0]!["x"] = Int(after["units"]![0]!.AsObject(), "x") + 1;
            var replacementState = Copy();
            replacementState["units"]![0]!["x"] = Int(replacementState["units"]![0]!.AsObject(), "x") + 2;
            var oldTask = board.PresentTransitionAsync(viewportFeedback, Transition(before, after, IdleAction(), [turn]));
            Task<bool>? nextTask = null;
            async Task ReplaceAfterCancel()
            {
                Require(!await oldTask, "The replaced playback reports cancellation.");
                nextTask = board.PresentTransitionAsync(viewportFeedback, Transition(replacementState, replacementState, IdleAction(), [turn]));
            }
            var replacement = ReplaceAfterCancel();
            board.CancelPresentation();
            await replacement;
            var currentRoot = viewportFeedback.GetNode<Control>("ViewportEffects");
            Require(nextTask is not null && !nextTask.IsCompleted && board.IsPresenting
                && Actor(board, id).Position.IsEqualApprox(board.Projection.CellContact(new(
                    Int(replacementState["units"]![0]!.AsObject(), "x"), Int(replacementState["units"]![0]!.AsObject(), "y")))),
                "A playback started by a cancellation continuation keeps its own before projection, not the old settlement.");
            await Tick(board, .11);
            Require(ReferenceEquals(currentRoot, viewportFeedback.GetNode<Control>("ViewportEffects"))
                && currentRoot.GetNodeOrNull("TurnAnnouncement") is not null && completed.Count == 0,
                "Old cancellation completion cannot remove the replacement's viewport root or publish its receipt.");
            board.CancelPresentation();
            Require(!await nextTask!, "The replacement retains its independent cancellation lifetime.");

            board.AnimationsEnabled = false;
            var reduced = board.PresentTransitionAsync(viewportFeedback, transition);
            Require(reduced.IsCompleted && await reduced && completed.SequenceEqual(new[] { 0 })
                && viewportFeedback.GetNodeOrNull("ViewportEffects") is null && board.GetNodeOrNull("BattleEffects") is null,
                "Reduced-motion announcements complete synchronously and publish their receipt once without residual effects.");
            board.AnimationsEnabled = true;
            completed.Clear();

            var fault = new InvalidOperationException("announcement receipt subscriber defect");
            void Fail(BattlePresentationTransition _, int index) => throw fault;
            board.EventPresented += Fail;
            try
            {
                var failed = board.PresentTransitionAsync(viewportFeedback, transition);
                await Tick(board, .2201);
                await Tick(board, .4201);
                await Tick(board, .2201);
                try
                {
                    await failed;
                    throw new Exception("Announcement receipt failure was swallowed.");
                }
                catch (InvalidOperationException error)
                {
                    Require(ReferenceEquals(error, fault) && !board.IsPresenting
                        && viewportFeedback.GetNodeOrNull("ViewportEffects") is null && board.GetNodeOrNull("BattleEffects") is null
                        && unrelated.GetParent() == viewportFeedback,
                        "Announcement failure propagates and cleans only the failed playback's two effect roots.");
                }
            }
            finally { board.EventPresented -= Fail; }
        }
        finally
        {
            board.AnimationsEnabled = true;
            board.EventPresented -= Presented;
            viewportFeedback.RemoveChild(unrelated);
            unrelated.QueueFree();
        }
    }

    private async Task CheckViewportAnnouncements()
    {
        var nativeSize = GetWindow().Size;
        try
        {
            foreach (var size in new[] { new Vector2I(960, 640), new Vector2I(1180, 812) })
                foreach (var topLeft in new[] { true, false })
                    await CheckViewportAnnouncement(size, topLeft);
        }
        finally { GetWindow().Size = nativeSize; }
    }

    private async Task CheckViewportAnnouncement(Vector2I size, bool topLeft)
    {
        GetWindow().Size = size;
        var campaignLevel = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
            .OfType<JsonObject>().Single(value => value["id"]!.GetValue<string>() == "c01-03");
        var engine = new BattleEngine(packs);
        var text = GodotTextCatalog.LoadSimplifiedChinese();
        var presentation = BattleTextChecks.Session(engine, text);
        var session = new GrayFlagSession(new(engine, presentation, new JsonArray()), campaignLevel);
        var scene = AuthoredScene.Load("res://data/scenes/c01-03.json");
        var rendering = BoardSceneRendering.Load(scene, campaignLevel, session.Snapshot()["map"]!.AsObject());
        var screen = new BattleScreen();
        AddChild(screen);
        void Bind(GrayFlagSession value)
        {
            screen.BindSession(new(text, presentation.Battle, PortraitCatalogResources.Load(), value,
                new(assets, EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane)), campaignLevel, scene);
            screen.ConfigureGround(rendering.Ground);
            screen.ConfigureDynamicState();
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            screen.Size = size;
        }
        try
        {
            screen.Board.AdvanceAnimationsAutomatically = false;
            Bind(session);
            await AnnouncementLayout();
            var map = screen.GetNode<Control>("MapArea");
            var world = map.GetNode<Node2D>("World");
            var host = map.GetNode<Control>("ViewportFeedback");
            Require(host.GetParent() == world.GetParent() && host.GetGlobalRect() == map.GetGlobalRect()
                && host.MouseFilter == Control.MouseFilterEnum.Ignore && host.FocusMode == Control.FocusModeEnum.None,
                "Screen owns a full-viewport, noninteractive feedback host independent of the camera world.");
            // Exercise the same captured native gesture path as CameraIntegrationChecks.
            var end = topLeft ? new Vector2(10000, 10000) : new Vector2(-10000, -10000);
            using (var down = new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true, Position = new(400, 300) })
                map.EmitSignal(Control.SignalName.GuiInput, down);
            var globalEnd = map.GetGlobalTransformWithCanvas() * end;
            using (var motion = new InputEventMouseMotion { Position = globalEnd, GlobalPosition = globalEnd })
                screen._Input(motion);
            using (var up = new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false, Position = globalEnd })
                screen._Input(up);
            var expectedOrigin = topLeft ? new Vector2(16, 110) + new Vector2(74, 62) * world.Scale
                : (Vector2)size - new Vector2(16, 134) - (screen.Board.FieldSize + new Vector2(74, 74)) * world.Scale;
            Require(world.Position.DistanceTo(expectedOrigin) < .002f && session.Revision == 0,
                "C03 announcement regression starts at the actual opposite camera pan limits without changing the battle.");
            var cameraBefore = world.Transform;
            var completed = new List<int>();
            screen.Board.EventPresented += (_, index) => completed.Add(index);
            EndTurnThroughViewport();
            var transition = session.GetSnapshot().Transition!;
            var turnIndex = transition.Events.Select((value, index) => (value, index))
                .Single(pair => pair.value.GetProperty("type").ValueEquals("turnStart")).index;
            var afterHash = StateDigest.Hash(session.Snapshot());
            Require(session.Revision == 1 && session.ActionLog.Count == 1
                && EcmaJson.DeepEquals(session.ActionLog.Single(), IdleAction())
                && screen.IsInputBlocked && completed.Count == turnIndex,
                "Native E input commits one endTurn and holds the single presentation gate before publishing turnStart.");
            var banner = host.GetNode<Control>("ViewportEffects/TurnAnnouncement");
            var band = banner.GetNode<Control>("Band");
            var label = band.GetNode<Label>("Number");
            Require(label.Text == transition.EventDescriptions[turnIndex] && label.Text.Length > 0
                && Mathf.IsZeroApprox(band.Modulate.A) && screen.Board.GetNode("BattleEffects").GetChildCount() == 0,
                "The real receipt text starts transparent in viewport feedback, with no duplicate world announcement.");
            await Tick(screen.Board, .11);
            Require(world.Transform.IsEqualApprox(cameraBefore), "Turn announcement does not move or zoom the camera.");
            var fraction = band.AnchorLeft;
            var resized = size == new Vector2I(960, 640) ? new Vector2I(1180, 812) : new Vector2I(960, 640);
            screen.Size = resized;
            await AnnouncementLayout();
            Require(Mathf.IsEqualApprox(band.Position.X, fraction * map.Size.X)
                && Mathf.IsEqualApprox(band.Size.X, map.Size.X) && Mathf.IsEqualApprox(banner.Position.Y, map.Size.Y / 2 - 17),
                "Resizing during entrance preserves normalized slide progress and the viewport-centered 34px band.");
            screen.Size = size;
            await AnnouncementLayout();
            await Tick(screen.Board, .1101);
            cameraBefore = world.Transform;
            CheckAnnouncementRect(map, band, label);
            var overlay = world.GetNode<BoardOverlay>("LegalTargets");
            var hoverPoint = session.GetTacticalQueries(0).InteractiveCells
                .Select(cell => screen.Board.Projection.CellCenter(new(cell.X, cell.Y)))
                .Where(point => screen.Board.Pick(point) is not null)
                .Select(point => world.Transform * point)
                .Where(point => new Rect2(Vector2.Zero, map.Size).HasPoint(point))
                .OrderBy(point => Math.Abs(point.Y - map.Size.Y / 2) + Math.Abs(point.X - map.Size.X / 2) * .01f)
                .First();
            using (var motion = new InputEventMouseMotion { Position = hoverPoint })
                map.EmitSignal(Control.SignalName.GuiInput, motion);
            // Observe the actual rendered projection without adding a production test API.
            var cursorProjection = (TacticalSelectionOverlay)typeof(BoardOverlay)
                .GetField("_tactical", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
            Require(screen.IsInputBlocked && cursorProjection.Cursor is not null,
                "Native busy-time mouse hover retains a real cursor ring while the announcement is held.");
            Require(host.ZIndex > overlay.ZIndex
                && overlay.GetChildren().OfType<BoardRangePlane>().All(plane => host.ZIndex > plane.ZIndex)
                && host.ZIndex < screen.GetNode<Control>("TacticalHud").ZIndex,
                "Viewport announcements draw above the busy cursor and absolute public-state range planes, below the HUD.");
            var heldRect = band.GetGlobalRect();
            foreach (var scale in new[] { .65f, 1.25f, 2.2f })
            {
                world.Scale = new Vector2(scale, scale);
                world.Position = new Vector2(-2100, 1400) * scale;
                Require(band.GetGlobalRect() == heldRect, "Viewport announcement geometry is invariant under world pan and zoom.");
            }
            world.Transform = cameraBefore;
            screen.Size = resized;
            await AnnouncementLayout();
            CheckAnnouncementRect(map, band, label);
            screen.Size = size;
            await AnnouncementLayout();
            await Tick(screen.Board, .4201);
            await Tick(screen.Board, .11);
            screen.Size = resized;
            await AnnouncementLayout();
            CheckAnnouncementRect(map, band, label);
            Require(banner.Modulate.A > .45f && banner.Modulate.A < .55f && completed.Count == turnIndex,
                "Resize during exit preserves the single 220ms fade and does not prematurely publish the receipt.");
            screen.Size = size;
            await AnnouncementLayout();
            for (var tick = 0; screen.IsPresenting && tick < 100; tick++)
                await Tick(screen.Board, .05);
            Require(!screen.IsInputBlocked && !screen.Board.IsPresenting && host.GetChildCount() == 0
                && screen.Board.GetNodeOrNull("BattleEffects") is null && completed.SequenceEqual(Enumerable.Range(0, transition.Events.Count))
                && screen.RecentMessages.Count(message => message == transition.EventDescriptions[turnIndex]) == 1,
                "C03 finishes both effect roots and delivers every receipt, including the announcement journal line, exactly once in order.");
            var messages = screen.RecentMessages.ToArray();
            screen.Refresh();
            screen.Refresh();
            Require(screen.RecentMessages.SequenceEqual(messages) && completed.Count == transition.Events.Count
                && session.Revision == 1 && StateDigest.Hash(session.Snapshot()) == afterHash,
                "Repeated refresh cannot replay the banner, duplicate the journal, or change canonical state.");

            var rebound = new GrayFlagSession(new(engine, presentation, new JsonArray()), campaignLevel);
            Bind(rebound);
            EndTurnThroughViewport();
            Require(host.GetNodeOrNull("ViewportEffects/TurnAnnouncement") is not null, "Rebind fixture interrupts a real live turn banner.");
            var fresh = new GrayFlagSession(new(engine, presentation, new JsonArray()), campaignLevel);
            Bind(fresh);
            screen.Board.AdvancePresentation(100);
            Require(!screen.IsInputBlocked && host.GetChildCount() == 0 && screen.DisplayedRevision == 0
                && screen.RecentMessages.SequenceEqual(new[] { fresh.GetSnapshot().InitialLogMessage }),
                "Rebinding cancels the old viewport effects and stale completion cannot publish into the new journal.");
            EndTurnThroughViewport();
            RemoveChild(screen);
            Require(!screen.IsPresenting && !screen.Board.IsPresenting && host.GetChildCount() == 0,
                "Unmounting a live screen releases the announcement and both playback gates immediately.");
            AddChild(screen);
            var reattached = new GrayFlagSession(new(engine, presentation, new JsonArray()), campaignLevel);
            Bind(reattached);
            screen.Board.AnimationsEnabled = false;
            EndTurnThroughViewport();
            var reduced = reattached.GetSnapshot().Transition!;
            Require(reattached.Revision == 1 && !screen.IsInputBlocked && host.GetChildCount() == 0
                && reduced.EventDescriptions.Where(line => line.Length > 0).All(line => screen.RecentMessages.Contains(line)),
                "Reattached reduced-motion screen commits and logs normally without a lingering viewport effect.");
            GD.Print($"TURN_BANNER_VIEWPORT size={size}; corner={(topLeft ? "top-left" : "bottom-right")}; visible=true; resize=entrance,hold,exit; orderedOnce=true");
        }
        finally
        {
            screen.GetParent()?.RemoveChild(screen);
            screen.Free();
        }
    }

    private void CheckAnnouncementRect(Control map, Control band, Label label)
    {
        var bounds = map.GetGlobalRect();
        var rect = band.GetGlobalRect();
        Require(band.IsVisibleInTree() && label.IsVisibleInTree() && bounds.Encloses(rect) && bounds.Encloses(label.GetGlobalRect())
            && Mathf.IsEqualApprox(rect.Size.X, bounds.Size.X) && Mathf.IsEqualApprox(rect.Size.Y, 34)
            && Mathf.IsEqualApprox(rect.GetCenter().Y, bounds.GetCenter().Y) && label.GetThemeFontSize("font_size") == 15,
            "The full entered band and localized label remain inside the visible map viewport at their original unscaled metrics.");
        Require(band.FindChildren("*", "Control", true, false).Cast<Control>().All(control =>
                control.MouseFilter == Control.MouseFilterEnum.Ignore && control.FocusMode == Control.FocusModeEnum.None),
            "Every banner child ignores pointer and keyboard focus so the overlay cannot become an input owner.");
    }

    private void EndTurnThroughViewport()
    {
        GetViewport().GuiReleaseFocus();
        foreach (var pressed in new[] { true, false })
        {
            using var input = new InputEventKey { Keycode = Key.E, PhysicalKeycode = Key.E, Pressed = pressed };
            GetViewport().PushInput(input, true);
        }
    }

    private async Task AnnouncementLayout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
