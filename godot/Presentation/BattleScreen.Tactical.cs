#nullable enable
using System;
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

    private void UpdateTactical()
    {
        if (_tactical is null || _session is null || !Current(_tactical.Stamp, true))
            return;
        var stamp = _tactical.Stamp;
        _tactical.SetBusy(IsInputBlocked);
        var applyGeneration = ++_hudApplyGeneration;
        var projection = _session.ProjectHud(stamp, _tactical.Selection, _tactical.CursorHit, _tactical.InspectId, IsInputBlocked);
        _hud.Apply(projection);
        var overlay = _tactical.Overlay();
        _overlay.ShowTactical(overlay);
        _board.ShowTacticalEmphasis(overlay);
        var focusedGlobal = GetViewport().GuiGetFocusOwner();
        var globalFocusName = focusedGlobal is Button && _globalOrders.IsAncestorOf(focusedGlobal)
            ? focusedGlobal.Name.ToString() : null;
        Clear(_globalOrders);
        var queries = _session.GetTacticalQueries(_tacticalBinding);
        void Add(string name, string label, bool enabled, Action action)
        {
            Button button = null!;
            button = Button(label, () =>
            {
                if (button.IsInsideTree() && !button.Disabled && Current(stamp))
                    action();
            });
            button.Name = name;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.AutowrapMode = TextServer.AutowrapMode.Off;
            button.AddThemeFontSizeOverride("font_size", 14);
            button.CustomMinimumSize = new Vector2(name == "EndTurn" ? 100 : 48, 50);
            button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            if (name == "EndTurn")
                TacticalHudStyle.OrderBadge(button, prominent: true);
            button.Disabled = !enabled || IsInputBlocked;
            _globalOrders.AddChild(button);
            if (globalFocusName == name && !button.Disabled)
                Callable.From(() =>
                {
                    if (applyGeneration == _hudApplyGeneration && Current(stamp) && button.IsInsideTree() && !button.Disabled)
                        button.GrabFocus();
                }).CallDeferred();
        }
        if (queries.Phase == "deployment")
            Add("FinishDeployment", _text.Format("ui.battle.finish_deployment"), queries.HumanInput,
                () => DispatchTactical(new(stamp, TacticalRequestKind.Action, EcmaJson.ToElement(new JsonObject { ["kind"] = "finishDeployment" }))));
        else if (queries.Phase != "over")
        {
            Add("EndTurn", _text.Format("ui.battle.end_turn"), queries.HumanTurn,
                () => HandleTactical(_tactical.Key(stamp, "e")));
            Add("Undo", _text.Format("ui.battle.undo"), queries.HumanTurn && queries.CanUndo,
                () => HandleTactical(_tactical.Key(stamp, "u")));
        }
    }

    private void HandleHudIntent(TacticalHudIntent intent)
    {
        if (_tactical is null || !Current(intent.Stamp))
            return;
        if (intent.Kind == TacticalHudIntentKind.Action)
        {
            if (intent.Action is { } action)
                DispatchTactical(new(intent.Stamp, TacticalRequestKind.Action, action));
            return;
        }
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
        // Explicit keyboard/roster focus replaces pointer ownership before camera
        // movement, so Reveal cannot revive an older physical pointer hit.
        if (result.CursorFocused)
            _pointerMapPosition = null;
        if (result.Reveal is { } cell)
        {
            var point = CellCenter(new(cell.X, cell.Y));
            if (result.Center)
                _camera?.CenterOn(point);
            else if (_camera is not null)
                _camera.Reveal(point, 32 * _camera.Zoom / 2);
            ApplyCamera();
        }
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
        ShowFeedback(result.Message);
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
        if (_tactical.CursorHit == hit)
            return;
        if (hit is not null)
            _tactical.Hover(_tactical.Stamp, hit);
        else
            _tactical.Leave(_tactical.Stamp);
        UpdateTactical();
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true } key || _tactical is null || !Current(_tactical.Stamp))
            return;
        // Tab belongs to native focus traversal; Space cycles actors only while
        // the board owns input. Focused controls keep native activation/navigation.
        if (_systemMenu.Visible || key.Keycode == Key.Tab)
            return;
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
        var result = _tactical.Key(_tactical.Stamp, sourceKey);
        HandleTactical(result);
        // A key routed to the board cannot also activate a native control.
        GetViewport().SetInputAsHandled();
    }
}
