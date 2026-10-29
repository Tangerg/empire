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
    private long _pointerBoundaryGeneration;
    private Button _zoomOut = null!;
    private Button _zoomIn = null!;
    private Label _turn = null!;
    private TacticalSelectionController? _tactical;
    private long _tacticalBinding;
    private bool _applicationBusy;
    private TacticalHud _hud = null!;
    private long _hudApplyGeneration;
    public bool IsInputBlocked => _presenting || _applicationBusy;
    public TacticalSelection? Selection => _tactical?.Selection;
    private Window? _inputWindow;
    private Label _feedback = null!;
    private PanelContainer _feedbackPanel = null!;
    private Label _eventLog = null!;
    private readonly BattleLog _messages = new();
    public System.Collections.Generic.IReadOnlyList<string> RecentMessages => _messages.Recent();
    private Label _coverage = null!;
    private HBoxContainer _globalOrders = null!;
    private GridContainer _applicationActions = null!;
    private PopupPanel _systemMenu = null!;
    private AcceptDialog _battleLogDialog = null!;
    private RichTextLabel _battleLogText = null!;
    private long _toastGeneration;
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

    public override void _EnterTree()
    {
        _inputWindow = GetWindow();
        _inputWindow.MouseExited += LeaveWindowContext;
    }

    private void LeaveWindowContext()
    {
        _gestures?.Cancel();
        _pointerMapPosition = null;
        var boundary = ++_pointerBoundaryGeneration;
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || boundary != _pointerBoundaryGeneration)
                return;
            // An owned embedded dossier receives native window focus on opening.
            // That transfer is not an exit from the game's inspection context.
            if (_built && _hud.OwnsWindowFocus)
                return;
            ClearPointerContext();
        }).CallDeferred();
    }

    private void ClearPointerContext()
    {
        _gestures?.Cancel();
        _pointerMapPosition = null;
        _pointerBoundaryGeneration++;
        HoverField(null);
    }

    public override void _ExitTree()
    {
        if (_inputWindow is not null)
            _inputWindow.MouseExited -= LeaveWindowContext;
        _inputWindow = null;
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
        if (what == NotificationApplicationFocusOut)
            ClearPointerContext();
        else if (what == NotificationWMWindowFocusOut)
            LeaveWindowContext();
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
        _battleLogDialog.Hide();
        _systemMenu.Hide();
        _pointerBoundaryGeneration++;
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
        ShowFeedback("");
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
        var pending = false;
        bool Current() => GodotObject.IsInstanceValid(this) && _bound && IsInsideTree() && generation == _bindingGeneration && ReferenceEquals(session, _session);
        _restart = onRestart is null ? null : () => { if (Current() && !IsInputBlocked) onRestart(); };
        void AddAction(string text, Action callback, bool requireIdle = false)
        {
            Button button = null!;
            button = Button(text, () =>
            {
                if (pending || !button.IsInsideTree() || !Current() || (requireIdle && IsInputBlocked))
                    return;
                pending = true;
                _systemMenu.Hide();
                // Navigation can unmount this native Window; finish its input dispatch first.
                Callable.From(() =>
                {
                    try
                    {
                        if (Current() && GodotObject.IsInstanceValid(button) && button.IsInsideTree()
                            && (!requireIdle || !IsInputBlocked))
                            callback();
                    }
                    finally { pending = false; }
                }).CallDeferred();
            });
            button.AutowrapMode = TextServer.AutowrapMode.Off;
            button.CustomMinimumSize = new Vector2(140, 44);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (requireIdle)
            {
                button.AddThemeStyleboxOverride("normal", TacticalHudStyle.Panel("#382923", 10, "#795844"));
                button.AddThemeStyleboxOverride("hover", TacticalHudStyle.Panel("#50352b", 10, "#b58160"));
                button.AddThemeColorOverride("font_color", new Color("#dfb49a"));
            }
            _applicationActions.AddChild(button);
        }
        AddAction(_text.Format("ui.battle.log"), OpenBattleLog);
        if (onSave is not null)
            AddAction(_text.Format("ui.battle.save"), onSave);
        if (onLoad is not null)
            AddAction(_text.Format("ui.battle.load"), onLoad);
        if (onRestart is not null)
            AddAction(_text.Format("ui.battle.restart"), () =>
            {
                _restartRequest = _restart;
                _restartConfirmation.PopupCentered();
                // This runs after the opening input dispatch; destructive confirmation is never the default key target.
                _restartConfirmation.GetCancelButton().GrabFocus();
            }, true);
        if (onExit is not null)
            AddAction(_text.Format("ui.battle.camp"), onExit, true);
    }

    public void AppendMessage(string message)
    {
        if (message.Length == 0)
            return;
        BuildInterface();
        _messages.Add(message);
        if (_battleLogDialog.Visible)
            _battleLogText.Text = string.Join("\n\n", _messages.Recent());
        _eventLog.Text = message;
        _eventLog.Visible = true;
        var toast = ++_toastGeneration;
        if (!IsInsideTree())
        {
            _eventLog.Visible = false;
            return;
        }
        GetTree().CreateTimer(3).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && toast == _toastGeneration)
                _eventLog.Visible = false;
        };
    }

    private void OpenBattleLog()
    {
        if (!_bound || !IsInsideTree())
            return;
        _battleLogText.Text = string.Join("\n\n", _messages.Recent());
        _battleLogDialog.PopupCentered(new Vector2I(Math.Min(640, (int)Size.X - 48), Math.Min(420, (int)Size.Y - 80)));
    }

    public void ShowFeedback(string message)
    {
        BuildInterface();
        _feedback.Text = message;
        _feedback.Visible = message.Length > 0;
        _feedbackPanel.Visible = _feedback.Visible;
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
            if (generation != _presentationGeneration || _board.IsExiting || !IsInsideTree() || !ReferenceEquals(_session, session))
                return;
            var current = session!.GetSnapshot();
            if (settled && current.Revision == next.Revision)
                // BoardView owns settlement of an accepted playback. Adopt that exact
                // revision for screen/UI state without replacing its finished projection.
                SynchronizeSnapshot(next, ReadSnapshotState(next));
            else
                ApplySnapshot(current);
        }
        catch (MissingArtResourceException error)
        {
            if (generation != _presentationGeneration || _board.IsExiting || !IsInsideTree() || !ReferenceEquals(_session, session))
                return;
            ApplySnapshot(session!.GetSnapshot());
            ShowFeedback(_text.Format("error.presentation.playback_failed"));
            GD.PushWarning(error.ToString());
        }
        finally
        {
            // A child board exits before this parent; its cancellation may resume us synchronously.
            if (generation == _presentationGeneration && !_board.IsExiting && IsInsideTree())
            {
                _presenting = false;
                UpdateTactical();
            }
        }
    }

    private static JsonObject ReadSnapshotState(BattleSnapshot snapshot) =>
        Empire.Core.Determinism.EcmaJson.Parse(snapshot.State.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Battle session returned a non-object state.");

    private void ApplySnapshot(BattleSnapshot next)
    {
        if (_session is null)
            return;
        var state = ReadSnapshotState(next);
        _board.RenderState(state, next.VisibleUnitIds);
        SynchronizeSnapshot(next, state);
    }

    private void SynchronizeSnapshot(BattleSnapshot next, JsonObject state)
    {
        _snapshot = next;
        _presentedRevision = next.Revision;
        _state = state;
        SynchronizeTactical(next);
        if (_pointerMapPosition is { } pointer)
            HoverField(ProjectMapPoint(pointer));
        _overlay.ShowPublicState(_board.Projection, _tactical!.InteractiveCells, _snapshot.VisibleCells, _snapshot.Incoming);
        UpdateHeader();
        UpdateTactical();
        _coverage.Text = !_board.SurfaceReady ? _text.Format("ui.battle.surface_pending")
            : _board.RenderingIssueCount > 0 ? _text.Format("ui.battle.artwork_issues", _board.RenderingIssueCount) : "";
        _coverage.TooltipText = "";
        _coverage.Visible = _coverage.Text.Length > 0;
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
        _mapArea = new Control
        {
            Name = "MapArea",
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Stop
        };
        _mapArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_mapArea);
        _world = new Node2D { Name = "World" };
        _mapArea.AddChild(_world);
        _world.AddChild(_board);
        _world.AddChild(_overlay);
        _mapArea.GuiInput += MapInput;
        _mapArea.Resized += ResizeCamera;
        _mapArea.MouseExited += () =>
        {
            _pointerMapPosition = null;
            var boundary = ++_pointerBoundaryGeneration;
            var stamp = _tactical?.Stamp;
            // Godot clears its old hovered control before MouseExited and installs
            // the new one afterwards. Resolve ownership after that input dispatch.
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() || boundary != _pointerBoundaryGeneration
                    || stamp is not { } expected || _tactical is null || _tactical.Stamp != expected)
                    return;
                var hovered = GetViewport().GuiGetHoveredControl();
                if (Current(expected) && _gestures?.ActivePointerId is null
                    && _tactical.Selection.Kind == TacticalSelectionKind.Target && _tactical.Selection.Weapon is not null
                    && _tactical.Selection.PreviewTarget is not null && hovered is not null && _hud.IsAncestorOf(hovered))
                    return;
                _gestures?.PointerLeave();
            }).CallDeferred();
        };
        _hud = new TacticalHud { Name = "TacticalHud", ZIndex = 4000 };
        _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _hud.IntentRequested += HandleHudIntent;
        AddChild(_hud);
        var heading = new HBoxContainer { ZIndex = 4001 };
        AddChild(heading);
        OverlayRect(heading, 1, 0, 1, 0, -252, 12, -14, 50);
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
        _systemMenu.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(_systemMenu);
        _battleLogDialog = new AcceptDialog
        {
            Name = "BattleLogDialog",
            Title = _text.Format("ui.battle.log"),
            OkButtonText = _text.Format("ui.common.close"),
            MinSize = new Vector2I(360, 240)
        };
        _battleLogDialog.AddThemeStyleboxOverride("panel", TacticalHudStyle.Panel("#30291e", 14, "#756344"));
        AddChild(_battleLogDialog);
        _battleLogText = new RichTextLabel
        {
            Name = "BattleLogText",
            BbcodeEnabled = false,
            ScrollActive = true,
            ScrollFollowing = true,
            SelectionEnabled = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _battleLogText.AddThemeColorOverride("default_color", TacticalHudStyle.Ink);
        _battleLogText.AddThemeFontSizeOverride("normal_font_size", 15);
        _battleLogDialog.AddChild(_battleLogText);
        _battleLogText.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _battleLogText.OffsetLeft = 16;
        _battleLogText.OffsetTop = 12;
        _battleLogText.OffsetRight = -16;
        _battleLogText.OffsetBottom = -54;
        var logEntry = Button(_text.Format("ui.battle.log"), OpenBattleLog);
        logEntry.Name = "BattleLogButton";
        logEntry.CustomMinimumSize = new Vector2(72, 38);
        heading.AddChild(logEntry);
        var systemPanel = new DispatchPanel { CustomMinimumSize = new Vector2(350, 0) };
        _systemMenu.AddChild(systemPanel);
        var systemBody = new VBoxContainer();
        systemBody.AddThemeConstantOverride("separation", 14);
        systemPanel.AddChild(systemBody);
        var systemHeading = new HBoxContainer();
        systemBody.AddChild(systemHeading);
        var systemTitle = Text(_text.Format("ui.battle.system"), 20, TacticalHudStyle.Ink);
        systemTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        systemHeading.AddChild(systemTitle);
        var closeSystem = Button(_text.Format("ui.common.cancel") + "  Esc", () => _systemMenu.Hide());
        closeSystem.AutowrapMode = TextServer.AutowrapMode.Off;
        closeSystem.CustomMinimumSize = new Vector2(94, 38);
        systemHeading.AddChild(closeSystem);
        _applicationActions = new GridContainer { Name = "ApplicationActions", Columns = 2 };
        _applicationActions.AddThemeConstantOverride("h_separation", 10);
        _applicationActions.AddThemeConstantOverride("v_separation", 10);
        systemBody.AddChild(_applicationActions);
        var system = Button(_text.Format("ui.battle.system"), () => _systemMenu.PopupCentered());
        system.Name = "SystemMenuButton";
        system.CustomMinimumSize = new Vector2(72, 38);
        heading.AddChild(system);
        _turn = Text(_text.Format("ui.battle.unbound"), 18, new Color("#c9c4bc"));
        _turn.Visible = false; // Full turn/limit/side context lives in the command rail.
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
        Resized += ResizeHud;
        _globalOrders = new HBoxContainer { ZIndex = 4001 };
        AddChild(_globalOrders);
        OverlayRect(_globalOrders, 1, 1, 1, 1, -184, -84, -26, -30);
        _feedbackPanel = new PanelContainer { Name = "FeedbackPanel", ZIndex = 4001, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _feedbackPanel.AddThemeStyleboxOverride("panel", TacticalHudStyle.Panel("#29251ef2", 10, "#a98b57"));
        AddChild(_feedbackPanel);
        OverlayRect(_feedbackPanel, .5f, 1, .5f, 1, -240, -304, 240, -250);
        _feedback = Wrapped("");
        _feedback.Name = "BattleFeedback";
        _feedback.MaxLinesVisible = 2;
        _feedback.AddThemeColorOverride("font_color", new Color("#dfad94"));
        _feedback.ZIndex = 4001;
        _feedback.MouseFilter = MouseFilterEnum.Ignore;
        _feedbackPanel.AddChild(_feedback);
        _eventLog = Wrapped("");
        _eventLog.Name = "BattleEventLog";
        _eventLog.AddThemeFontSizeOverride("font_size", 14);
        _eventLog.MaxLinesVisible = 2;
        _eventLog.AddThemeColorOverride("font_color", TacticalHudStyle.Muted);
        _eventLog.ZIndex = 4001;
        _eventLog.MouseFilter = MouseFilterEnum.Ignore;
        _eventLog.AddThemeColorOverride("font_shadow_color", new Color("#15130c"));
        _eventLog.AddThemeConstantOverride("shadow_outline_size", 2);
        AddChild(_eventLog);
        OverlayRect(_eventLog, 0, 0, 0, 0, 28, 104, 324, 148);
        _coverage = Wrapped("");
        _coverage.AddThemeFontSizeOverride("font_size", 12);
        _coverage.AddThemeColorOverride("font_color", new Color("#ada69d"));
        _coverage.Visible = false;
        AddChild(_coverage);
        ResizeHud();
    }

    private static void OverlayRect(Control control, float left, float top, float right, float bottom, float x1, float y1, float x2, float y2)
    {
        control.AnchorLeft = left;
        control.AnchorTop = top;
        control.AnchorRight = right;
        control.AnchorBottom = bottom;
        control.OffsetLeft = x1;
        control.OffsetTop = y1;
        control.OffsetRight = x2;
        control.OffsetBottom = y2;
    }

    private void ResizeHud()
    {
        if (_hud is not null)
            _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private void ResizeCamera()
    {
        if (!_built || _camera is null)
            return;
        // The world uses the full native viewport; HUD controls intercept only their painted surfaces.
        _camera.Resize(new(_mapArea.Size.X, _mapArea.Size.Y), new(16, 110, 16, 134));
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
        {
            _pointerBoundaryGeneration++;
            TrackPointer(capturedMapPosition ?? new(mousePosition.Position.X, mousePosition.Position.Y));
        }
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
            ShowFeedback(_battleText.DescribeConclusion(EcmaJson.ToElement(_state)));
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
