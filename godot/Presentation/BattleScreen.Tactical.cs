#nullable enable
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class BattleScreen
{
    // Check the live revision before invoking a captured query adapter: stale adapters
    // intentionally throw rather than combine facts from different battle revisions.
    private bool Current(TacticalStamp stamp, bool allowBusy = false) =>
        _bound && IsInsideTree() && _session is not null && _tactical is not null &&
        stamp.Binding == _tacticalBinding && stamp == _tactical.Stamp &&
        _session.Revision == stamp.Revision && (allowBusy || !IsInputBlocked);

    private void SynchronizeTactical(BattleSnapshot next)
    {
        if (_tactical is null || _session is null)
            return;
        if (_tactical.Stamp.Revision == next.Revision)
            return;
        var queries = _session.GetTacticalQueries(_tacticalBinding);
        if (queries.Stamp.Revision != next.Revision)
            throw new InvalidOperationException("Tactical projection and battle snapshot must share a revision.");
        _tactical.CompleteDispatch(_tactical.Stamp, queries);
    }

    public void SetApplicationBusy(bool busy)
    {
        _applicationBusy = busy;
        if (busy)
            _gestures?.Cancel();
        UpdateTactical();
    }

    private (TacticalStamp Stamp, bool Busy)? _globalOrderBinding;
    private string? _focusedObjective;

    private void UpdateTactical()
    {
        if (_tactical is null || _session is null || !Current(_tactical.Stamp, true))
            return;
        var stamp = _tactical.Stamp;
        _tactical.SetBusy(IsInputBlocked);
        var applyGeneration = ++_hudApplyGeneration;
        var projection = _session.ProjectHud(stamp, _tactical.Selection, _tactical.CursorHit, _tactical.InspectId, IsInputBlocked);
        _hud.Apply(projection);
        var objectiveCells = !IsInputBlocked && _focusedObjective is not null
            ? projection.Sections.FirstOrDefault(section => section.Key == "objectives")?.Rows
                .FirstOrDefault(row => row.Key == _focusedObjective)?.TargetCells : null;
        if (objectiveCells is not { Count: > 0 })
            _focusedObjective = null;
        _overlay.ShowObjective(objectiveCells ?? Array.Empty<BoardCell>());
        var overlay = _tactical.Overlay();
        _overlay.ShowTactical(overlay);
        _board.ShowTacticalEmphasis(overlay);
        var globalBinding = (stamp, IsInputBlocked);
        if (_globalOrderBinding == globalBinding)
            return;
        _globalOrderBinding = globalBinding;
        var focusedGlobal = GetViewport().GuiGetFocusOwner();
        var globalFocusName = focusedGlobal is Button && _globalOrders.IsAncestorOf(focusedGlobal)
            ? focusedGlobal.Name.ToString() : null;
        Clear(_globalOrders);
        var queries = _session.GetTacticalQueries(_tacticalBinding);
        void Add(Container parent, string name, string label, bool enabled, Action action, string tooltip = "")
        {
            Button button = null!;
            var pending = false;
            bool CanActivate() => GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion()
                && GodotObject.IsInstanceValid(button) && !button.IsQueuedForDeletion()
                && button.IsInsideTree() && _globalOrders.IsAncestorOf(button) && !button.Disabled && Current(stamp);
            button = Button(label, () =>
            {
                if (pending || !CanActivate())
                    return;
                pending = true;
                // Emulated mouse release precedes raw touch release. Keep its native
                // control mounted until both events have left the viewport dispatch.
                Callable.From(() =>
                {
                    pending = false;
                    if (CanActivate())
                        action();
                }).CallDeferred();
            });
            button.Name = name;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.AutowrapMode = TextServer.AutowrapMode.Off;
            button.AddThemeFontSizeOverride("font_size", 14);
            button.CustomMinimumSize = name == "NextUnit" ? new Vector2(158, 40) : new Vector2(name == "EndTurn" ? 100 : 48, 50);
            button.TooltipText = tooltip;
            button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            if (name == "EndTurn")
                TacticalHudStyle.OrderBadge(button, prominent: true);
            button.Disabled = !enabled || IsInputBlocked;
            parent.AddChild(button);
            if (globalFocusName == name && !button.Disabled)
                Callable.From(() =>
                {
                    if (applyGeneration == _hudApplyGeneration && Current(stamp) && button.IsInsideTree() && !button.Disabled)
                        button.GrabFocus();
                }).CallDeferred();
        }
        var turnOrders = new HBoxContainer { Name = "TurnOrders" };
        _globalOrders.AddChild(turnOrders);
        if (queries.Phase == "deployment")
            Add(turnOrders, "FinishDeployment", _text.Format("ui.battle.finish_deployment"), queries.HumanInput,
                () => DispatchTactical(new(stamp, TacticalRequestKind.Action, EcmaJson.ToElement(new JsonObject { ["kind"] = "finishDeployment" }))));
        else if (queries.Phase != "over")
        {
            var ready = _tactical.ReadyUnitCount;
            if (queries.HumanTurn)
            {
                Add(_globalOrders, "NextUnit", ready > 0 ? _text.Format("ui.battle.next_unit", ready) : _text.Format("ui.battle.no_ready_units"), ready > 0,
                    () => HandleTactical(_tactical.Key(stamp, " ")), _text.Format("ui.battle.next_unit_hint"));
                _globalOrders.MoveChild(turnOrders, -1);
            }
            Add(turnOrders, "EndTurn", _text.Format("ui.battle.end_turn"), queries.HumanTurn,
                () => HandleTactical(_tactical.Key(stamp, "e")));
            Add(turnOrders, "Undo", _text.Format("ui.battle.undo"), queries.HumanTurn && queries.CanUndo,
                () => HandleTactical(_tactical.Key(stamp, "u")));
        }
    }

    private void HandleHudIntent(TacticalHudIntent intent)
    {
        if (_tactical is null || !Current(intent.Stamp))
            return;
        if (intent.Kind == TacticalHudIntentKind.FocusObjective)
        {
            var projection = _session!.ProjectHud(intent.Stamp, _tactical.Selection, _tactical.CursorHit, _tactical.InspectId, false);
            var cells = projection.Sections.FirstOrDefault(section => section.Key == "objectives")?.Rows
                .FirstOrDefault(row => row.Key == intent.Key && row.Intent?.Kind == TacticalHudIntentKind.FocusObjective)?.TargetCells;
            if (cells is not { Count: > 0 } || cells.Any(cell => !_tactical.InteractiveCells.Contains(cell)))
                return;
            _focusedObjective = intent.Key;
            _pointerMapPosition = null;
            _pointerBoundaryGeneration++;
            _gestures?.Cancel();
            // Frame the same authored surface vertices used by the objective overlay.
            // A centroid alone can leave widely separated task markers behind the HUD.
            var points = Board.Projection.Triangles
                .Where(triangle => cells.Any(cell => cell.X == triangle.Cell.X && cell.Y == triangle.Cell.Y))
                .SelectMany(triangle => new[] { triangle.ProjectedA, triangle.ProjectedB, triangle.ProjectedC }).ToArray();
            if (points.Length > 0)
                _camera?.FrameBounds(new(points.Min(point => point.X), points.Min(point => point.Y)),
                    new(points.Max(point => point.X), points.Max(point => point.Y)), 12);
            ApplyCamera();
            UpdateTactical();
            return;
        }
        if (intent.Kind == TacticalHudIntentKind.Action)
        {
            if (intent.Action is { } action)
                DispatchTactical(new(intent.Stamp, TacticalRequestKind.Action, action));
            return;
        }
        if (intent.Context != _tactical.Selection.Context)
            return;
        var result = intent.Kind switch
        {
            TacticalHudIntentKind.Command when intent.Key is { } key => _tactical.ChooseCommand(intent.Stamp, key),
            TacticalHudIntentKind.Tactic when intent.Key is { } key => _tactical.ChooseTactic(intent.Stamp, key),
            TacticalHudIntentKind.Recruit when intent.Key is { } key => _tactical.Recruit(intent.Stamp, key),
            TacticalHudIntentKind.DeployPick when intent.Unit is { } id => _tactical.PickDeploymentUnit(intent.Stamp, id),
            TacticalHudIntentKind.Disembark when intent.Unit is { } id => _tactical.ChooseDisembark(intent.Stamp, id),
            TacticalHudIntentKind.Back => _tactical.Back(intent.Stamp),
            _ => new TacticalInputResult(TacticalInputStatus.Ignored),
        };
        HandleTactical(result);
    }

    private void HandleTactical(TacticalInputResult result)
    {
        _focusedObjective = null;
        // Explicit keyboard/roster focus replaces pointer ownership before camera
        // movement, so Reveal cannot revive an older physical pointer hit.
        if (result.CursorFocused)
        {
            _pointerMapPosition = null;
            _pointerBoundaryGeneration++;
        }
        if (result.Reveal is { } cell)
        {
            var point = CellCenter(new(cell.X, cell.Y));
            if (result.Center)
                _camera?.CenterOn(point);
            else if (_camera is not null)
                _camera.Reveal(point, 32 * _camera.Zoom / 2);
            ApplyCamera();
        }
        if (result.Request is null && _tactical?.Selection is
            { Kind: TacticalSelectionKind.Target, Destination: { } source, PreviewTarget: { } target })
        {
            // Explicit target selection owns this preview; a reframe must not repick the old screen pixel.
            _pointerMapPosition = null;
            _pointerBoundaryGeneration++;
            RevealPlayerAction(CellCenter(new(source.X, source.Y)), CellCenter(new(target.Cell.X, target.Cell.Y)));
        }
        if (result.Status == TacticalInputStatus.Ready && _snapshot?.State.GetProperty("phase").GetString() != "over")
            ShowFeedback("");
        if (result.Status is TacticalInputStatus.InvalidDestination or TacticalInputStatus.InvalidTarget)
            ShowFeedback(_text.Format(result.Status == TacticalInputStatus.InvalidDestination
                ? "ui.hud.invalid_destination" : "ui.hud.invalid_target"));
        if (result.Request is { } request)
            DispatchTactical(request);
        else
            UpdateTactical();
    }

    private void DispatchTactical(TacticalRequest request)
    {
        if (_session is null || _tactical is null || !Current(request.Stamp))
            return;
        var result = request.Kind == TacticalRequestKind.Undo
            ? _session.Undo(request.Stamp.Revision)
            : _session.Dispatch(request.Action ?? throw new InvalidOperationException("Action request lacks its canonical payload."), request.Stamp.Revision);
        ShowFeedback(result.Message, transient: result.Accepted);
        if (!result.Accepted)
            AppendMessage(_text.Format("feedback.battle.refused", result.Message));
        else if (request.Kind == TacticalRequestKind.Undo)
            AppendMessage(_text.Format("feedback.session.undone"));
        // Both accepted commands and legal refusals return to the source resting selection.
        _tactical.CompleteDispatch(request.Stamp, _session.GetTacticalQueries(_tacticalBinding));
        Refresh();
    }

    private void HoverField(CameraPoint? point)
    {
        if (_tactical is null || !Current(_tactical.Stamp, true))
            return;
        TacticalHit? hit = null;
        if (point is { } at && at.X >= 0 && at.Y >= 0 && at.X < _board.FieldSize.X && at.Y < _board.FieldSize.Y && _state is not null)
        {
            if (_board.Pick(new Vector2((float)at.X, (float)at.Y)) is { } picked
                && _tactical.InteractiveCells.Contains(picked.Cell))
                hit = picked;
        }
        // The same pixel can establish a new preview after cancel/re-arm. Let the
        // selection owner decide first, then suppress only an unchanged projection.
        var previous = (_tactical.CursorHit, _tactical.Selection, _tactical.InspectId);
        if (hit is not null)
            _tactical.Hover(_tactical.Stamp, hit);
        else
            _tactical.Leave(_tactical.Stamp);
        if (previous != (_tactical.CursorHit, _tactical.Selection, _tactical.InspectId))
            UpdateTactical();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        // Native UI and operating-system chords must not become bare battlefield orders.
        if (input is not InputEventKey { Pressed: true, CtrlPressed: false, AltPressed: false, MetaPressed: false } key
            || _tactical is null || !Current(_tactical.Stamp))
            return;
        // The application is mounted under a Node, so the viewport has no direct
        // Control from which to start traversal after a map click clears focus.
        if (_systemMenu.Visible)
            return;
        if (key.Keycode == Key.Tab)
        {
            if (!key.Echo && !_hud.DossierVisible && GetViewport().GuiGetFocusOwner() is null)
            {
                var entry = key.ShiftPressed ? FindPrevValidFocus() : FindNextValidFocus();
                if (entry is not null && IsAncestorOf(entry))
                {
                    entry.GrabFocus();
                    GetViewport().SetInputAsHandled();
                }
            }
            return;
        }
        // Space cycles actors only while the board owns input. Focused controls
        // keep their native activation and navigation.
        if (GetViewport().GuiGetFocusOwner() is Button focused && IsAncestorOf(focused) && focused.IsVisibleInTree())
        {
            if (key.Keycode == Key.Escape)
            {
                focused.ReleaseFocus();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode is Key.Enter or Key.KpEnter or Key.Space or Key.Up or Key.Down or Key.Left or Key.Right)
                return;
        }
        var sourceKey = key.Keycode switch
        {
            Key.Escape => "escape",
            Key.Space => " ",
            Key.Enter or Key.KpEnter => "enter",
            Key.Up => "arrowup",
            Key.Down => "arrowdown",
            Key.Left => "arrowleft",
            Key.Right => "arrowright",
            Key.A => "a",
            Key.C => "c",
            Key.D => "d",
            Key.E => "e",
            Key.H => "h",
            Key.S => "s",
            Key.U => "u",
            Key.W => "w",
            _ => null,
        };
        if (sourceKey is null)
            return;
        var result = _tactical.Key(_tactical.Stamp, sourceKey, key.Echo);
        if (!(key.Echo && result.Status == TacticalInputStatus.Ignored))
            HandleTactical(result);
        // A key routed to the board cannot also activate a native control.
        GetViewport().SetInputAsHandled();
    }
}
