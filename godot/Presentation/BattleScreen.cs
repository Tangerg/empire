#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Empire.Core.State;
using Empire.Presentation.Feedback;
using Empire.Presentation.Tactical;
using Empire.Core.Determinism;
using Empire.Localization;

namespace Empire.Presentation;

public sealed record BattleScreenRules(GameText Messages, BattleText BattleText, PortraitCatalog Portraits,
    ITacticalBattleSession Session, BoardViewRules Board);

/// <summary>
/// Thin interactive view. Selection and target mode are local UI state; all legal
/// choices and committed state transitions are owned by the injected session.
/// </summary>
public partial class BattleScreen : Control
{
    private static readonly Color ChromeHeading = new("#efe5d5");
    private ITacticalBattleSession? _session;
    private GameText _text = null!;
    private BattleText _battleText = null!;
    private BattleSnapshot? _snapshot;
    private JsonObject? _state;
    private readonly BoardView _board = new() { Name = "Board" };
    private readonly BoardOverlay _overlay = new() { Name = "LegalTargets", ZIndex = 3000 };
    private Control _mapArea = null!;
    private Node2D _world = null!;
    private BattlefieldCamera? _camera;
    private BattlefieldGestures? _gestures;
    // Only actual map input establishes pointer ownership. Keyboard focus clears it.
    private CameraPoint? _pointerMapPosition;
    private Button _zoomOut = null!;
    private Button _zoomIn = null!;
    private Label _turn = null!;
    private TacticalSelectionController? _tactical;
    private long _tacticalBinding;
    private bool _applicationBusy;
    private TacticalHud _hud = null!;
    private ScrollContainer _hudScroll = null!;
    private long _hudApplyGeneration;
    public bool IsInputBlocked => _presenting || _applicationBusy;
    public TacticalSelection? Selection => _tactical?.Selection;
    private Label _modeHint = null!;
    private Label _feedback = null!;
    private Label _eventLog = null!;
    private readonly BattleLog _messages = new();
    public System.Collections.Generic.IReadOnlyList<string> RecentMessages => _messages.Recent();
    private Label _coverage = null!;
    private HBoxContainer _globalOrders = null!;
    private HBoxContainer _applicationActions = null!;
    private PopupPanel _systemMenu = null!;
    private PanelContainer _commandPanel = null!;
    private ConfirmationDialog _restartConfirmation = null!;
    private Action? _restart;
    private Action? _restartRequest;
    private bool _built;
    private bool _bound;
    private bool _presenting;
    private int _presentationGeneration;
    private int _bindingGeneration;
    private long _presentedRevision = -1;
    public bool IsPresenting => _presenting;

    public override void _ExitTree()
    {
        _bindingGeneration++;
        _tacticalBinding++;
        _tactical = null;
        _gestures?.Dispose();
        _gestures = null;
        _pointerMapPosition = null;
        _camera = null;
        _presentationGeneration++;
        _presenting = false;
        _bound = false;
        _board.CancelPresentation();
    }

    public BoardView Board => _board;
    public int? SelectedUnitId => _tactical?.Selection.UnitId;
    public long? DisplayedRevision => _snapshot?.Revision;

    public override void _Ready()
    {
        ResizeHud();
        ResizeCamera();
    }

    public override void _Process(double delta)
    {
        if (!_built)
            return;
        _zoomOut.Disabled = _zoomIn.Disabled = !_bound || IsInputBlocked;
        if (_bound && _camera?.ApplyPendingZoom() == true)
            ApplyCamera();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut || what == NotificationApplicationFocusOut)
        {
            _gestures?.Cancel();
            _pointerMapPosition = null;
            HoverField(null);
        }
    }

    public override void _Input(InputEvent input)
    {
        // Source shortcuts are window-level, before focused controls consume Tab,
        // arrows or Space. The handled marker also prevents a second unhandled pass.
        if (input is InputEventKey)
        {
            _UnhandledKeyInput(input);
            return;
        }
        if (!_bound || IsInputBlocked || _gestures?.ActivePointerId is null)
            return;
        // An active board gesture owns its release even after crossing a HUD panel.
        switch (input)
        {
            case InputEventMouseMotion motion when _gestures.ActivePointerId == 0:
                _gestures.PointerMove(0, TrackPointer(ViewportToMap(motion.Position)));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton mouse when _gestures.ActivePointerId == 0:
                if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
                    MapInput(mouse, ViewportToMap(mouse.Position));
                else if (mouse.Canceled)
                    _gestures.Cancel(0);
                else if (!mouse.Pressed && SourceButton(mouse.ButtonIndex) is { } button)
                    _gestures.PointerUp(0, button, TrackPointer(ViewportToMap(mouse.Position)));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenDrag drag when _gestures.ActivePointerId == drag.Index + 1:
                _gestures.PointerMove(drag.Index + 1, ViewportToMap(drag.Position));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenTouch touch when _gestures.ActivePointerId == touch.Index + 1:
                if (touch.Canceled)
                    _gestures.Cancel(touch.Index + 1);
                else if (!touch.Pressed)
                    _gestures.PointerUp(touch.Index + 1, 0, ViewportToMap(touch.Position));
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public void BindSession(BattleScreenRules rules, JsonObject level, AuthoredScene authoredScene, string? initialLogMessage = null)
    {
        var text = rules.Messages;
        var battleText = rules.BattleText;
        var session = rules.Session;
        ArgumentNullException.ThrowIfNull(session);
        _text = text;
        _battleText = battleText;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        BuildInterface();
        _bindingGeneration++;
        _tacticalBinding++;
        _tactical = null;
        _gestures?.Dispose();
        _gestures = null;
        _pointerMapPosition = null;
        _camera = null;
        _restart = null;
        _restartRequest = null;
        _restartConfirmation.Hide();
        _presentationGeneration++;
        _presenting = false;
        _board.CancelPresentation();
        _snapshot = null;
        _presentedRevision = session.Revision;
        _board.Configure(rules.Board, level, authoredScene);
        _hud.ConfigureArt(TacticalHudArt.Load(rules.Board.Assets, rules.Portraits));
        _session = session;
        _tactical = new TacticalSelectionController(_session.GetTacticalQueries(_tacticalBinding));
        _applicationBusy = false;
        _bound = true;
        _feedback.Text = _text.Format("ui.battle.choose_unit");
        _messages.Clear();
        AppendMessage(initialLogMessage ?? session.GetSnapshot().InitialLogMessage ?? "");
        Refresh();
        var state = session.GetSnapshot().State;
        var fallback = new CameraPoint(_board.FieldSize.X / 2, _board.FieldSize.Y / 2);
        var focus = BattlefieldFocus.Initial(state, CellCenter, fallback);
        if (authoredScene.OpeningFocus is { } opening)
        {
            if (opening.Length != 2 || !_tactical.InteractiveCells.Contains(new BoardCell(opening[0], opening[1])))
                throw new InvalidOperationException("Authored opening focus must identify one playable canonical cell.");
            focus = CellCenter(new CameraPoint(opening[0], opening[1]));
        }
        _camera = new BattlefieldCamera(new(_board.FieldSize.X, _board.FieldSize.Y), new(74, 62, 74, 74), focus);
        _gestures = new BattlefieldGestures(new(ProjectMapPoint, PressField, HoverField, () => HoverField(null), delta => { _camera?.Pan(delta); ApplyCamera(); }, notches => QueueZoom(notches * .1)));
        ResizeCamera();
    }

    public void ConfigureGround(BoardGroundFactory createGround)
    {
        _board.ConfigureGround(createGround);
        if (_state is not null)
            Refresh();
    }

    public void ConfigureDynamicState()
    {
        _board.ConfigureDynamicState();
        if (_state is not null)
            Refresh();
    }

    public void BindApplicationActions(Action? onSave = null, Action? onLoad = null, Action? onRestart = null, Action? onExit = null)
    {
        BuildInterface();
        Clear(_applicationActions);
        _restartRequest = null;
        _restartConfirmation.Hide();
        var generation = ++_bindingGeneration;
        var session = _session;
        bool Current() => _bound && IsInsideTree() && generation == _bindingGeneration && ReferenceEquals(session, _session);
        _restart = onRestart is null ? null : () => { if (Current() && !IsInputBlocked) onRestart(); };
        void AddAction(string text, Action callback, bool requireIdle = false)
        {
            Button button = null!;
            button = Button(text, () => { if (button.IsInsideTree() && Current() && (!requireIdle || !IsInputBlocked)) { _systemMenu.Hide(); callback(); } });
            button.AutowrapMode = TextServer.AutowrapMode.Off;
            button.CustomMinimumSize = new Vector2(90, 38);
            _applicationActions.AddChild(button);
        }
        if (onSave is not null)
            AddAction(_text.Format("ui.battle.save"), onSave);
        if (onLoad is not null)
            AddAction(_text.Format("ui.battle.load"), onLoad);
        if (onRestart is not null)
            AddAction(_text.Format("ui.battle.restart"), () =>
            {
                _restartRequest = _restart;
                _restartConfirmation.PopupCentered();
            }, true);
        if (onExit is not null)
            AddAction(_text.Format("ui.battle.camp"), onExit, true);
    }

    public void AppendMessage(string message)
    {
        BuildInterface();
        _messages.Add(message);
        _eventLog.Text = string.Join("\n", _messages.Recent().TakeLast(4));
    }

    public void ShowFeedback(string message)
    {
        BuildInterface();
        _feedback.Text = message;
        _feedback.TooltipText = message;
    }

    /// <summary>Allows the application root to refresh after an external session transition.</summary>
    public void Refresh()
    {
        if (!_bound || _session is null || !IsInsideTree())
            return;
        var next = _session.GetSnapshot();
        if (_presenting && next.Revision == _presentedRevision)
            return;
        if (next.Transition is { } transition && transition.Revision == next.Revision && next.Revision > _presentedRevision)
        {
            _presentedRevision = next.Revision;
            PresentCommitted(next, transition);
            return;
        }
        if (_presenting)
        {
            _presentationGeneration++;
            _presenting = false;
            _board.CancelPresentation();
        }
        ApplySnapshot(next);
    }

    private async void PresentCommitted(BattleSnapshot next, BattlePresentationTransition transition)
    {
        var generation = ++_presentationGeneration;
        var session = _session;
        _gestures?.Cancel();
        _board.CancelPresentation();
        _presenting = true;
        if (BattlefieldFocus.CommandDestination(transition, CellCenter) is { } destination)
        {
            _camera?.CenterOn(destination);
            ApplyCamera();
        }
        _snapshot = next;
        SynchronizeTactical(next);
        UpdateTactical();
        try
        {
            var settled = await _board.PresentTransitionAsync(transition, next.VisibleUnitIds);
            if (generation != _presentationGeneration || !IsInsideTree() || !ReferenceEquals(_session, session))
                return;
            var current = session!.GetSnapshot();
            ApplySnapshot(settled && current.Revision == next.Revision ? next : current);
        }
        catch (MissingArtResourceException error)
        {
            if (generation != _presentationGeneration || !IsInsideTree() || !ReferenceEquals(_session, session))
                return;
            ApplySnapshot(next);
            ShowFeedback(_text.Format("error.presentation.playback_failed"));
            GD.PushWarning(error.ToString());
        }
        finally
        {
            if (generation == _presentationGeneration)
            {
                _presenting = false;
                UpdateTactical();
            }
        }
    }

    private void ApplySnapshot(BattleSnapshot next)
    {
        if (_session is null)
            return;
        _snapshot = next;
        _presentedRevision = next.Revision;
        _state = Empire.Core.Determinism.EcmaJson.Parse(_snapshot.State.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Battle session returned a non-object state.");
        _board.RenderState(_state, _snapshot.VisibleUnitIds);
        SynchronizeTactical(next);
        if (_pointerMapPosition is { } pointer)
            HoverField(ProjectMapPoint(pointer));
        _overlay.ShowPublicState(_board.Projection, _tactical!.InteractiveCells, _snapshot.VisibleCells, _snapshot.Incoming);
        UpdateHeader();
        UpdateTactical();
        _coverage.Text = !_board.SurfaceReady ? _text.Format("ui.battle.surface_pending")
            : _board.RenderingIssueCount > 0 ? _text.Format("ui.battle.artwork_issues", _board.RenderingIssueCount) : "";
        _coverage.TooltipText = "";
    }

    private void BuildInterface()
    {
        if (_built)
            return;
        if (_text is null)
            throw new InvalidOperationException("Bind the battle text dependencies before building the screen.");
        _built = true;
        _board.EventPresented += (transition, index) =>
        {
            if (_presenting && _bound && IsInsideTree() && transition.Revision == _presentedRevision)
                AppendMessage(transition.EventDescriptions[index]);
        };
        _board.PresentationRepaintRequested += () =>
        {
            if (!_presenting || !_bound || _session is null || !IsInsideTree())
                return;
            var current = _session.GetSnapshot();
            if (current.Revision != _presentedRevision)
                return;
            _state = Empire.Core.Determinism.EcmaJson.Parse(current.State.GetRawText())!.AsObject();
            UpdateHeader();

        };
        Theme = TacticalHudStyle.Create();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Material layers use negative world depths; a default-Z UI backdrop would cover them.
        var background = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color("#151e19"),
            ZIndex = -4096,
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margins = new MarginContainer { Name = "Content" };
        margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
            margins.AddThemeConstantOverride($"margin_{edge}", 12);
        AddChild(margins);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margins.AddChild(column);
        var heading = new HBoxContainer();
        column.AddChild(heading);
        var title = Text(_text.Format("ui.battle.title"), 20, ChromeHeading);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(title);
        var cameraControls = new HBoxContainer { Name = "CameraControls" };
        _zoomOut = Button("−", () => QueueZoom(-.15));
        _zoomIn = Button("+", () => QueueZoom(.15));
        _zoomOut.Name = "ZoomOut";
        _zoomIn.Name = "ZoomIn";
        _zoomOut.CustomMinimumSize = _zoomIn.CustomMinimumSize = new Vector2(38, 38);
        _zoomOut.TooltipText = _text.Format("ui.battle.zoom_out");
        _zoomIn.TooltipText = _text.Format("ui.battle.zoom_in");
        cameraControls.AddChild(_zoomOut);
        cameraControls.AddChild(_zoomIn);
        heading.AddChild(cameraControls);
        _systemMenu = new PopupPanel { Name = "SystemMenu" };
        _systemMenu.AddThemeStyleboxOverride("panel", TacticalHudStyle.Panel("#202c24", 12));
        AddChild(_systemMenu);
        _applicationActions = new HBoxContainer { Name = "ApplicationActions" };
        _systemMenu.AddChild(_applicationActions);
        var system = Button(_text.Format("ui.battle.system"), () => _systemMenu.PopupCentered());
        system.Name = "SystemMenuButton";
        system.CustomMinimumSize = new Vector2(72, 38);
        heading.AddChild(system);
        _turn = Text(_text.Format("ui.battle.unbound"), 18, new Color("#c9c4bc"));
        heading.AddChild(_turn);
        _restartConfirmation = new ConfirmationDialog
        {
            Title = _text.Format("ui.battle.restart_title"),
            DialogText = _text.Format("ui.battle.restart_warning"),
            OkButtonText = _text.Format("ui.battle.restart"),
            CancelButtonText = _text.Format("ui.common.cancel")
        };
        _restartConfirmation.Confirmed += () =>
        {
            var request = _restartRequest;
            _restartRequest = null;
            if (_bound && IsInsideTree())
                request?.Invoke();
        };
        _restartConfirmation.Canceled += () => _restartRequest = null;
        AddChild(_restartConfirmation);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        column.AddChild(body);
        _mapArea = new Control
        {
            Name = "MapArea",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = _text.Format("ui.battle.camera_hint")
        };
        body.AddChild(_mapArea);
        _world = new Node2D { Name = "World" };
        _mapArea.AddChild(_world);
        _world.AddChild(_board);
        _world.AddChild(_overlay);
        _mapArea.GuiInput += MapInput;
        _mapArea.Resized += ResizeCamera;
        _mapArea.MouseExited += () =>
        {
            _pointerMapPosition = null;
            _gestures?.PointerLeave();
        };

        var panel = new PanelContainer { Name = "CommandPanel", CustomMinimumSize = new Vector2(340, 0) };
        _commandPanel = panel;
        panel.AddThemeStyleboxOverride("panel", TacticalHudStyle.Panel("#202a23", 14));
        body.AddChild(panel);
        var side = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        side.AddThemeConstantOverride("separation", 10);
        panel.AddChild(side);
        Resized += ResizeHud;

        var scroll = new ScrollContainer
        {
            Name = "CommandScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 60),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _hudScroll = scroll;
        side.AddChild(scroll);
        _hud = new TacticalHud { Name = "TacticalHud", ShowHint = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _hud.IntentRequested += HandleHudIntent;
        scroll.AddChild(_hud);
        _modeHint = Wrapped("");
        _modeHint.Visible = true;
        _modeHint.AddThemeFontSizeOverride("font_size", 14);
        _modeHint.AddThemeColorOverride("font_color", TacticalHudStyle.Gold);
        side.AddChild(_modeHint);
        _globalOrders = new HBoxContainer();
        side.AddChild(_globalOrders);
        _feedback = Wrapped(_text.Format("ui.battle.not_connected"));
        _feedback.MaxLinesVisible = 2;
        _feedback.AddThemeColorOverride("font_color", new Color("#dfad94"));
        side.AddChild(_feedback);
        _eventLog = Wrapped("");
        _eventLog.Name = "BattleEventLog";
        _eventLog.AddThemeFontSizeOverride("font_size", 14);
        _eventLog.MaxLinesVisible = 3;
        _eventLog.AddThemeColorOverride("font_color", TacticalHudStyle.Muted);
        side.AddChild(_eventLog);
        _coverage = Wrapped("");
        _coverage.AddThemeFontSizeOverride("font_size", 12);
        _coverage.AddThemeColorOverride("font_color", new Color("#ada69d"));
        column.AddChild(_coverage);
    }

    private void ResizeHud()
    {
        if (_commandPanel is not null)
            _commandPanel.CustomMinimumSize = new Vector2(Math.Clamp(Size.X * .29f, 300, 390), 0);
    }

    private void ResizeCamera()
    {
        if (!_built || _camera is null)
            return;
        // MapArea already excludes the native HUD. Reserving the source overlay bands again would double-pad it.
        _camera.Resize(new(_mapArea.Size.X, _mapArea.Size.Y));
        ApplyCamera();
    }

    private void ApplyCamera()
    {
        if (_camera is null || !_camera.HasViewport)
            return;
        var scale = Vector2.One * (float)_camera.Zoom;
        var position = new Vector2((float)_camera.FieldOrigin.X, (float)_camera.FieldOrigin.Y);
        if (_world.Scale == scale && _world.Position == position)
            return;
        _world.Scale = scale;
        _world.Position = position;
        // The renderer remains the only hit owner after pan/zoom; a previous pixel
        // result must not be reused at a different transform by Enter or the HUD.
        if (_pointerMapPosition is { } pointer)
            HoverField(ProjectMapPoint(pointer));
    }

    private void QueueZoom(double delta)
    {
        if (_bound && IsInsideTree() && !IsInputBlocked)
            _camera?.QueueZoom(delta);
    }

    private CameraPoint TrackPointer(CameraPoint point)
    {
        _pointerMapPosition = point;
        return point;
    }

    private void MapInput(InputEvent input) => MapInput(input, null);

    private void MapInput(InputEvent input, CameraPoint? capturedMapPosition)
    {
        if (!_bound || !IsInsideTree() || _gestures is null || _camera is null)
            return;
        if (input is InputEventMouse mousePosition)
            TrackPointer(capturedMapPosition ?? new(mousePosition.Position.X, mousePosition.Position.Y));
        else if (input is InputEventPanGesture panPosition)
            TrackPointer(new(panPosition.Position.X, panPosition.Position.Y));
        else if (input is InputEventScreenTouch or InputEventScreenDrag)
            _pointerMapPosition = null;
        if (IsInputBlocked)
        {
            // Inspection/cursor projection remains read-only while actions settle.
            if (input is InputEventMouseMotion hover)
                HoverField(ProjectMapPoint(new(hover.Position.X, hover.Position.Y)));
            return;
        }
        switch (input)
        {
            case InputEventMouseButton mouse:
                if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
                {
                    if (!mouse.Pressed)
                        return;
                    var vertical = mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown;
                    var sign = mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelLeft ? -1 : 1;
                    if (!_gestures.Wheel(vertical ? sign : 0, mouse.CtrlPressed, mouse.MetaPressed))
                    {
                        // Browser wheel distance is platform-defined; native discrete notches use one logical tile in screen pixels.
                        var amount = sign * 32 * (mouse.Factor == 0 ? 1 : mouse.Factor);
                        _camera.Pan(vertical ? new(0, -amount) : new(-amount, 0));
                        ApplyCamera();
                    }
                    AcceptEvent();
                    return;
                }
                if (SourceButton(mouse.ButtonIndex) is not { } button)
                    return;
                if (mouse.Canceled)
                    _gestures.Cancel(0);
                else if (mouse.Pressed)
                    _gestures.PointerDown(0, button, new(mouse.Position.X, mouse.Position.Y));
                else
                    _gestures.PointerUp(0, button, new(mouse.Position.X, mouse.Position.Y));
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                _gestures.PointerMove(0, new(motion.Position.X, motion.Position.Y));
                break;
            case InputEventScreenTouch touch:
                if (touch.Canceled)
                    _gestures.Cancel(touch.Index + 1);
                else if (touch.Pressed)
                    _gestures.PointerDown(touch.Index + 1, 0, new(touch.Position.X, touch.Position.Y), touch.Index == 0);
                else
                    _gestures.PointerUp(touch.Index + 1, 0, new(touch.Position.X, touch.Position.Y));
                AcceptEvent();
                break;
            case InputEventScreenDrag drag:
                _gestures.PointerMove(drag.Index + 1, new(drag.Position.X, drag.Position.Y));
                AcceptEvent();
                break;
            case InputEventPanGesture pan:
                if (!_gestures.Wheel(pan.Delta.Y, pan.CtrlPressed, pan.MetaPressed))
                {
                    _camera.Pan(new(-pan.Delta.X, -pan.Delta.Y));
                    ApplyCamera();
                }
                AcceptEvent();
                break;
        }
    }

    private CameraPoint? ProjectMapPoint(CameraPoint point) =>
        _camera?.HasViewport == true && point.X >= 0 && point.Y >= 0
        && point.X < _mapArea.Size.X && point.Y < _mapArea.Size.Y
            ? _camera.ToField(point) : null;

    private void PressField(CameraPoint point, int button)
    {
        if (IsInputBlocked || !_bound || !IsInsideTree() || _state is null)
            return;
        if (point.X < 0 || point.Y < 0 || point.X >= _board.FieldSize.X || point.Y >= _board.FieldSize.Y)
            return;
        if (_board.Pick(new Vector2((float)point.X, (float)point.Y)) is not { } picked)
            return;
        if (_tactical is null || !Current(_tactical.Stamp) || !_tactical.InteractiveCells.Contains(picked.Cell))
            return;
        _tactical.Hover(_tactical.Stamp, picked);
        if (button == 0)
            SelectHit(picked);
        else if (button == 2)
        {
            HandleTactical(_tactical.Back(_tactical.Stamp));
        }
    }

    private CameraPoint ViewportToMap(Vector2 point)
    {
        var local = _mapArea.GetGlobalTransformWithCanvas().AffineInverse() * point;
        return new(local.X, local.Y);
    }
    private CameraPoint CellCenter(CameraPoint cell)
    {
        var point = _board.Projection.CellCenter(new Empire.Core.Geometry.Coord((int)cell.X, (int)cell.Y));
        return new(point.X, point.Y);
    }
    private static int? SourceButton(MouseButton button) => button switch
    {
        MouseButton.Left => 0,
        MouseButton.Middle => 1,
        MouseButton.Right => 2,
        _ => null,
    };

    /// <summary>Explicit subject interaction entry point also used by native contract checks.</summary>
    public void SelectHit(TacticalHit hit)
    {
        if (_tactical is null || !Current(_tactical.Stamp))
            return;
        if (GetViewport().GuiGetFocusOwner() is Button focused && IsAncestorOf(focused))
            focused.ReleaseFocus();
        HandleTactical(_tactical.Click(_tactical.Stamp, hit));
    }

    private void UpdateHeader()
    {
        if (_state is null)
            return;
        var over = _state["phase"]!.GetValue<string>() == "over";
        _turn.Text = over ? _text.Format("battle.header.finished") : _text.Format("battle.header.turn",
            _state["turn"]!.GetValue<int>(), _battleText.PlayerName(_state["levelId"]!.GetValue<string>(), _state["currentPlayer"]!.GetValue<int>()));
        if (over)
            _feedback.Text = _battleText.DescribeConclusion(EcmaJson.ToElement(_state));
    }

    // Tactical intent integration lives in BattleScreen.Tactical.cs.

    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
    private static Label Wrapped(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = SizeFlags.ExpandFill
    };
    private static Button Button(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 38),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        button.Pressed += action;
        return button;
    }
    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
