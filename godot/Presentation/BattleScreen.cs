#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Empire.Presentation.Feedback;

namespace Empire.Presentation;

/// <summary>
/// Thin interactive view. Selection and target mode are local UI state; all legal
/// choices and committed state transitions are owned by the injected session.
/// </summary>
public partial class BattleScreen : Control
{
    private IBattleSession? _session;
    private BattleSnapshot? _snapshot;
    private JsonObject? _state;
    private readonly BoardView _board = new() { Name = "Board" };
    private readonly BoardOverlay _overlay = new() { Name = "LegalTargets", ZIndex = 3000 };
    private Control _mapArea = null!;
    private Node2D _world = null!;
    private BattlefieldCamera? _camera;
    private BattlefieldGestures? _gestures;
    private HashSet<string> _outsideTerrain = new(StringComparer.Ordinal);
    private Button _zoomOut = null!;
    private Button _zoomIn = null!;
    private Label _turn = null!;
    private Label _selection = null!;
    private Label _modeHint = null!;
    private Label _feedback = null!;
    private Label _eventLog = null!;
    private readonly BattleLog _messages = new();
    public System.Collections.Generic.IReadOnlyList<string> RecentMessages => _messages.Recent();
    private Label _coverage = null!;
    private Label _objectiveSummary = null!;
    private VBoxContainer _modeButtons = null!;
    private VBoxContainer _orders = null!;
    private VBoxContainer _globalOrders = null!;
    private HBoxContainer _applicationActions = null!;
    private ConfirmationDialog _restartConfirmation = null!;
    private Action? _restart;
    private Action? _restartRequest;
    private IReadOnlyList<BattleOrderChoice> _legalOrders = Array.Empty<BattleOrderChoice>();
    private int? _selectedId;
    private BoardCell? _target;
    private string _role = "";
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
        _gestures?.Dispose();
        _gestures = null;
        _camera = null;
        _presentationGeneration++;
        _presenting = false;
        _bound = false;
        _board.CancelPresentation();
    }

    public BoardView Board => _board;
    public int? SelectedUnitId => _selectedId;
    public long? DisplayedRevision => _snapshot?.Revision;

    public override void _Ready()
    {
        BuildInterface();
        ResizeCamera();
    }

    public override void _Process(double delta)
    {
        if (!_built) return;
        _zoomOut.Disabled = _zoomIn.Disabled = !_bound || _presenting;
        if (_bound && _camera?.ApplyPendingZoom() == true) ApplyCamera();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut || what == NotificationApplicationFocusOut) _gestures?.Cancel();
    }

    public override void _Input(InputEvent input)
    {
        if (!_bound || _presenting || _gestures?.ActivePointerId is null) return;
        // An active board gesture owns its release even after crossing a HUD panel.
        switch (input)
        {
            case InputEventMouseMotion motion when _gestures.ActivePointerId == 0:
                _gestures.PointerMove(0, ViewportToMap(motion.Position));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton mouse when _gestures.ActivePointerId == 0:
                if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
                    MapInput(mouse);
                else if (mouse.Canceled) _gestures.Cancel(0);
                else if (!mouse.Pressed && SourceButton(mouse.ButtonIndex) is { } button)
                    _gestures.PointerUp(0, button, ViewportToMap(mouse.Position));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenDrag drag when _gestures.ActivePointerId == drag.Index + 1:
                _gestures.PointerMove(drag.Index + 1, ViewportToMap(drag.Position));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenTouch touch when _gestures.ActivePointerId == touch.Index + 1:
                if (touch.Canceled) _gestures.Cancel(touch.Index + 1);
                else if (!touch.Pressed) _gestures.PointerUp(touch.Index + 1, 0, ViewportToMap(touch.Position));
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public void BindSession(IBattleSession session, AssetCatalog assets, JsonObject level,
        AuthoredScene authoredScene, EnvironmentCatalog environment, Func<string?, bool> facesLeft, Func<string?, Vector2>? directionVector = null, string? initialLogMessage = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        BuildInterface();
        _bindingGeneration++;
        _gestures?.Dispose();
        _gestures = null;
        _camera = null;
        _outsideTerrain.Clear();
        _restart = null;
        _restartRequest = null;
        _restartConfirmation.Hide();
        _presentationGeneration++;
        _presenting = false;
        _board.CancelPresentation();
        _snapshot = null;
        _presentedRevision = session.GetSnapshot().Revision;
        _board.Configure(assets, level, authoredScene, environment, facesLeft, directionVector);
        _session = session;
        _bound = true;
        _selectedId = null;
        _target = null;
        _feedback.Text = "选择单位，查看规则允许的命令。";
        _messages.Clear();
        AppendMessage(initialLogMessage ?? session.GetSnapshot().InitialLogMessage ?? "");
        Refresh();
        var state = session.GetSnapshot().State;
        var fallback = new CameraPoint(_board.FieldSize.X / 2, _board.FieldSize.Y / 2);
        var focus = BattlefieldFocus.Initial(state, CellCenter, fallback);
        _camera = new BattlefieldCamera(new(_board.FieldSize.X, _board.FieldSize.Y), new(74, 62, 74, 74), focus);
        _gestures = new BattlefieldGestures(point => _camera?.HasViewport == true ? _camera.ToField(point) : null,
            PressField, _ => { }, () => { }, delta => { _camera?.Pan(delta); ApplyCamera(); }, notches => QueueZoom(notches * .1));
        ResizeCamera();
    }

    public void ConfigureTerrain(WorldMaterialsManifest materials)
    {
        _board.ConfigureTerrain(materials);
        _outsideTerrain = materials.TerrainTags.Where(entry =>
            entry.Value.Intersect(materials.GroundTagFamilies["outside"], StringComparer.Ordinal).Any())
            .Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        if (_state is not null) Refresh();
    }

    public void ConfigureDynamicState(JsonArray contentPacks)
    {
        _board.ConfigureDynamicState(contentPacks);
        if (_state is not null) Refresh();
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
        _restart = onRestart is null ? null : () => { if (Current() && !_presenting) onRestart(); };
        void AddAction(string text, Action callback, bool requireIdle = false)
        {
            Button button = null!;
            button = Button(text, () => { if (button.IsInsideTree() && Current() && (!requireIdle || !_presenting)) callback(); });
            button.AutowrapMode = TextServer.AutowrapMode.Off;
            button.CustomMinimumSize = new Vector2(90, 38);
            _applicationActions.AddChild(button);
        }
        if (onSave is not null) AddAction("保存", onSave);
        if (onLoad is not null) AddAction("读取", onLoad);
        if (onRestart is not null) AddAction("重新开始", () => {
            _restartRequest = _restart;
            _restartConfirmation.PopupCentered();
        }, true);
        if (onExit is not null) AddAction("战役营地", onExit, true);
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
        if (!_bound || _session is null || !IsInsideTree()) return;
        var next = _session.GetSnapshot();
        if (_presenting && next.Revision == _presentedRevision) return;
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
        _legalOrders = Array.Empty<BattleOrderChoice>();
        Clear(_orders); Clear(_globalOrders); Clear(_modeButtons);
        _overlay.ShowTargets(null, Array.Empty<BoardCell>(), "", null);
        try
        {
            var settled = await _board.PresentTransitionAsync(transition, next.VisibleUnitIds);
            if (generation != _presentationGeneration || !IsInsideTree() || !ReferenceEquals(_session, session)) return;
            var current = session!.GetSnapshot();
            ApplySnapshot(settled && current.Revision == next.Revision ? next : current);
        }
        catch (Exception error)
        {
            if (generation != _presentationGeneration || !IsInsideTree() || !ReferenceEquals(_session, session)) return;
            ApplySnapshot(next);
            ShowFeedback($"表现播放失败：{error.Message}");
            GD.PushError(error.ToString());
        }
        finally
        {
            if (generation == _presentationGeneration) _presenting = false;
        }
    }

    private void ApplySnapshot(BattleSnapshot next)
    {
        if (_session is null) return;
        _snapshot = next;
        _presentedRevision = next.Revision;
        _state = Empire.Core.Determinism.EcmaJson.Parse(_snapshot.State.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Battle session returned a non-object state.");
        _board.RenderState(_state, _snapshot.VisibleUnitIds);
        _overlay.ShowPublicState(_state["map"]!["width"]!.GetValue<int>(), _state["map"]!["height"]!.GetValue<int>(), _snapshot.VisibleCells, _snapshot.Incoming);
        _objectiveSummary.Text = _snapshot.ObjectiveSummary ?? "";
        if (SelectedUnit() is null) _selectedId = null;
        _legalOrders = _session.GetLegalOrders(_selectedId);
        var roles = _legalOrders.Where(order => order.Target is not null)
            .Select(order => order.PresentationRole).Distinct().ToArray();
        if (!roles.Contains(_role)) _role = _selectedId is null ? "" : roles.FirstOrDefault() ?? "";
        _target = null;
        UpdateHeader();
        UpdateSelection();
        UpdateOrders();
        _coverage.Text = _board.StatusText;
        _coverage.TooltipText = string.Join("\n", _board.Diagnostics);
    }

    private void BuildInterface()
    {
        if (_built) return;
        _built = true;
        _board.EventPresented += (transition, index) =>
        {
            if (_presenting && _bound && IsInsideTree() && transition.Revision == _presentedRevision)
                AppendMessage(transition.EventDescriptions[index]);
        };
        _board.PresentationRepaintRequested += () =>
        {
            if (!_presenting || !_bound || _session is null || !IsInsideTree()) return;
            var current = _session.GetSnapshot();
            if (current.Revision != _presentedRevision) return;
            _state = Empire.Core.Determinism.EcmaJson.Parse(current.State.GetRawText())!.AsObject();
            UpdateHeader();
            _objectiveSummary.Text = current.ObjectiveSummary ?? "";
        };
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Material layers use negative world depths; a default-Z UI backdrop would cover them.
        var background = new ColorRect { Name = "Backdrop", Color = new Color("#091712"),
            ZIndex = -4096, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margins = new MarginContainer { Name = "Content" };
        margins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" })
            margins.AddThemeConstantOverride($"margin_{edge}", 18);
        AddChild(margins);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        margins.AddChild(column);
        var heading = new HBoxContainer();
        column.AddChild(heading);
        var title = Text("EMPIRE  /  灰旗", 26, new Color("#eee1bd"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(title);
        var cameraControls = new HBoxContainer { Name = "CameraControls" };
        _zoomOut = Button("−", () => QueueZoom(-.15));
        _zoomIn = Button("+", () => QueueZoom(.15));
        _zoomOut.Name = "ZoomOut"; _zoomIn.Name = "ZoomIn";
        _zoomOut.CustomMinimumSize = _zoomIn.CustomMinimumSize = new Vector2(38, 38);
        _zoomOut.TooltipText = "缩小战场"; _zoomIn.TooltipText = "放大战场";
        cameraControls.AddChild(_zoomOut); cameraControls.AddChild(_zoomIn);
        heading.AddChild(cameraControls);
        _applicationActions = new HBoxContainer { Name = "ApplicationActions" };
        heading.AddChild(_applicationActions);
        _turn = Text("尚未绑定战斗", 18, new Color("#d1cfbc"));
        heading.AddChild(_turn);
        _restartConfirmation = new ConfirmationDialog {
            Title = "重新开始？", DialogText = "当前未保存的战斗进度将丢失。",
            OkButtonText = "重新开始", CancelButtonText = "取消" };
        _restartConfirmation.Confirmed += () => {
            var request = _restartRequest;
            _restartRequest = null;
            if (_bound && IsInsideTree()) request?.Invoke();
        };
        _restartConfirmation.Canceled += () => _restartRequest = null;
        AddChild(_restartConfirmation);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 20);
        column.AddChild(body);
        _mapArea = new Control { Name = "MapArea", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true, MouseFilter = MouseFilterEnum.Stop,
            TooltipText = "拖动平移战场 · 滚轮平移 · Ctrl/⌘ + 滚轮缩放" };
        body.AddChild(_mapArea);
        _world = new Node2D { Name = "World" };
        _mapArea.AddChild(_world);
        _world.AddChild(_board);
        _world.AddChild(_overlay);
        _mapArea.GuiInput += MapInput;
        _mapArea.Resized += ResizeCamera;
        _mapArea.MouseExited += () => _gestures?.PointerLeave();

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(282, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color("#14251f"), BorderColor = new Color("#465348"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 16, ContentMarginBottom = 16 });
        body.AddChild(panel);
        var side = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
        side.AddThemeConstantOverride("separation", 12);
        panel.AddChild(side);
        side.AddChild(Text("战场指令", 21, new Color("#eee1bd")));
        _selection = Wrapped("等待应用绑定真实战斗。");
        side.AddChild(_selection);
        _objectiveSummary = Wrapped("");
        _objectiveSummary.MaxLinesVisible = 4;
        side.AddChild(_objectiveSummary);
        var scroll = new ScrollContainer { Name = "CommandScroll", SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 60), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        side.AddChild(scroll);
        var commandColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        commandColumn.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(commandColumn);
        _modeButtons = new VBoxContainer();
        commandColumn.AddChild(_modeButtons);
        _modeHint = Wrapped("");
        _modeHint.AddThemeColorOverride("font_color", new Color("#b6c4b5"));
        commandColumn.AddChild(_modeHint);
        _orders = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        commandColumn.AddChild(_orders);
        _globalOrders = new VBoxContainer();
        side.AddChild(_globalOrders);
        _feedback = Wrapped("战斗尚未连接，不能提交命令。");
        _feedback.MaxLinesVisible = 4;
        _feedback.AddThemeColorOverride("font_color", new Color("#f1d99f"));
        side.AddChild(_feedback);
        _eventLog = Wrapped("");
        _eventLog.Name = "BattleEventLog";
        _eventLog.AddThemeFontSizeOverride("font_size", 14);
        _eventLog.MaxLinesVisible = 8;
        side.AddChild(_eventLog);
        _coverage = Wrapped("Native C# migration");
        _coverage.AddThemeFontSizeOverride("font_size", 12);
        _coverage.AddThemeColorOverride("font_color", new Color("#aab6a5"));
        column.AddChild(_coverage);
    }

    private void ResizeCamera()
    {
        if (!_built || _camera is null) return;
        // MapArea already excludes the native HUD. Reserving the source overlay bands again would double-pad it.
        _camera.Resize(new(_mapArea.Size.X, _mapArea.Size.Y));
        ApplyCamera();
    }

    private void ApplyCamera()
    {
        if (_camera is null || !_camera.HasViewport) return;
        _world.Scale = Vector2.One * (float)_camera.Zoom;
        _world.Position = new Vector2((float)_camera.FieldOrigin.X, (float)_camera.FieldOrigin.Y);
    }

    private void QueueZoom(double delta)
    {
        if (_bound && IsInsideTree() && !_presenting) _camera?.QueueZoom(delta);
    }

    private void MapInput(InputEvent input)
    {
        if (_presenting || !_bound || !IsInsideTree() || _gestures is null || _camera is null) return;
        switch (input)
        {
            case InputEventMouseButton mouse:
                if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
                {
                    if (!mouse.Pressed) return;
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
                if (SourceButton(mouse.ButtonIndex) is not { } button) return;
                if (mouse.Canceled) _gestures.Cancel(0);
                else if (mouse.Pressed) _gestures.PointerDown(0, button, new(mouse.Position.X, mouse.Position.Y));
                else _gestures.PointerUp(0, button, new(mouse.Position.X, mouse.Position.Y));
                AcceptEvent();
                break;
            case InputEventMouseMotion motion:
                _gestures.PointerMove(0, new(motion.Position.X, motion.Position.Y));
                break;
            case InputEventScreenTouch touch:
                if (touch.Canceled) _gestures.Cancel(touch.Index + 1);
                else if (touch.Pressed) _gestures.PointerDown(touch.Index + 1, 0, new(touch.Position.X, touch.Position.Y), touch.Index == 0);
                else _gestures.PointerUp(touch.Index + 1, 0, new(touch.Position.X, touch.Position.Y));
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

    private void PressField(CameraPoint point, int button)
    {
        if (_presenting || !_bound || !IsInsideTree() || _state is null) return;
        if (point.X < 0 || point.Y < 0 || point.X >= _board.FieldSize.X || point.Y >= _board.FieldSize.Y) return;
        var cell = new BoardCell((int)Math.Floor(point.X / 32), (int)Math.Floor(point.Y / 32));
        var width = _state["map"]!["width"]!.GetValue<int>();
        var tile = _state["map"]!["tiles"]![cell.Y * width + cell.X]!.GetValue<string>();
        if (_outsideTerrain.Contains(tile)) return;
        if (button == 0) SelectCell(cell);
        else if (button == 2)
        {
            _target = null;
            _selectedId = null;
            Refresh();
        }
    }

    private CameraPoint ViewportToMap(Vector2 point)
    {
        var local = _mapArea.GetGlobalTransformWithCanvas().AffineInverse() * point;
        return new(local.X, local.Y);
    }
    private static CameraPoint CellCenter(CameraPoint cell) => new((cell.X + .5) * 32, (cell.Y + .5) * 32);
    private static int? SourceButton(MouseButton button) => button switch
    {
        MouseButton.Left => 0, MouseButton.Middle => 1, MouseButton.Right => 2, _ => null,
    };

    /// <summary>Cell interaction entry point also used by native contract checks.</summary>
    public void SelectCell(BoardCell cell)
    {
        if (!_bound || !IsInsideTree() || _presenting || _state is null || _session is null) return;
        var choices = TargetChoices(cell).ToArray();
        if (choices.Length > 0)
        {
            _target = cell;
            UpdateOrders();
            return; // Explicit command button commits; a board click never commits an ambiguous action.
        }
        var unit = Units().FirstOrDefault(unit => Int(unit, "x") == cell.X && Int(unit, "y") == cell.Y);
        _selectedId = unit is null ? null : Int(unit, "id");
        _target = null;
        _legalOrders = _session.GetLegalOrders(_selectedId);
        var roles = _legalOrders.Where(order => order.Target is not null)
            .Select(order => order.PresentationRole).Distinct().ToArray();
        _role = _selectedId is null ? "" : roles.Contains("move") ? "move" : roles.FirstOrDefault() ?? "";
        UpdateSelection();
        UpdateOrders();
    }

    private void UpdateHeader()
    {
        if (_state is null) return;
        var active = _state["currentPlayer"]?.ToJsonString() ?? "?";
        var player = (_state["players"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(item => item["id"]?.ToJsonString() == active);
        var name = player?["name"]?.GetValue<string>() ?? $"玩家 {active}";
        var round = _state["turn"]?.ToJsonString() ?? "?";
        var phase = _state["phase"]?.GetValue<string>();
        _turn.Text = phase == "over" ? "战斗结束" : $"回合 {round}  ·  {name}";
        if (phase == "over")
        {
            var winner = _state["winnerTeam"]?.ToJsonString();
            _feedback.Text = (winner is null ? "战斗结束" : $"队伍 {winner} 获胜")
                + (_state["endReason"]?.GetValue<string>() is { } reason ? $"\n{reason}" : "");
        }
    }

    private void UpdateSelection()
    {
        var unit = SelectedUnit();
        _selection.Text = unit is null ? "点击单位查看可执行的命令。\n右键取消选择。"
            : $"{unit["key"]?.GetValue<string>() ?? unit["type"]?.GetValue<string>()}\n"
              + $"HP {unit["hp"]}  ·  玩家 {unit["owner"]}\n"
              + $"位置 ({unit["x"]}, {unit["y"]})  ·  {(unit["done"]?.GetValue<bool>() == true ? "已行动" : "未行动")}";
    }

    private void UpdateOrders()
    {
        Clear(_modeButtons);
        Clear(_orders);
        Clear(_globalOrders);
        foreach (var role in _legalOrders.Where(order => order.Target is not null)
                     .Select(order => order.PresentationRole).Distinct())
        {
            var count = _legalOrders.Where(order => order.PresentationRole == role)
                .Select(order => order.Target).Distinct().Count();
            var caption = role switch { "move" => "移动并待机", "attack" => "攻击",
                "tactic" => "战术", "command" => "技能", _ => role };
            var button = Button($"{caption} · {count} 个目标", () => {
                _role = role;
                _target = null;
                UpdateOrders();
            });
            button.ToggleMode = true;
            button.ButtonPressed = _role == role;
            _modeButtons.AddChild(button);
        }
        var targeted = _target is { } target ? TargetChoices(target).ToArray() : [];
        foreach (var order in targeted) _orders.AddChild(Button(order.Label, () => SubmitOrder(order)));
        var globalOrders = _session?.GetLegalOrders(null).Where(order => order.Target is null).ToArray() ?? [];
        var globalActions = globalOrders.Select(order => order.Action.GetRawText()).ToHashSet(StringComparer.Ordinal);
        foreach (var order in _legalOrders.Where(order => order.Target is null
                     && !globalActions.Contains(order.Action.GetRawText())))
            _orders.AddChild(Button(order.Label, () => SubmitOrder(order)));
        foreach (var order in globalOrders)
            _globalOrders.AddChild(Button(order.Label, () => SubmitOrder(order)));
        _modeHint.Text = _target is not null
            ? "选择下方完整命令以确认。"
            : _legalOrders.Any(order => order.Target is not null)
                ? "高亮格来自规则查询。\n点击目标，再确认完整命令。"
                : _selectedId is not null ? "规则当前没有提供目标命令。" : "";
        var unit = SelectedUnit();
        _overlay.ShowTargets(unit is null ? null : new BoardCell(Int(unit, "x"), Int(unit, "y")),
            _legalOrders.Where(order => order.PresentationRole == _role && order.Target is not null)
                .Select(order => order.Target!.Value), _role, _target);
    }

    public void SubmitOrder(BattleOrderChoice order)
    {
        if (!_bound || !IsInsideTree() || _presenting || _session is null || _snapshot is null) return;
        // The JSON is the session's complete canonical command, not a UI reconstruction.
        var result = _session.Dispatch(order.Action, _snapshot.Revision);
        ShowFeedback(result.Message);
        if (!result.Accepted) AppendMessage("无法执行：" + result.Message);
        Refresh();
    }

    private IEnumerable<BattleOrderChoice> TargetChoices(BoardCell cell) =>
        _legalOrders.Where(order => order.PresentationRole == _role && order.Target == cell);
    private IEnumerable<JsonObject> Units() => (_state?["units"] as JsonArray)?.OfType<JsonObject>()
        .Where(unit => _snapshot?.VisibleUnitIds is not { } visible || visible.Contains(Int(unit, "id")))
        ?? Enumerable.Empty<JsonObject>();
    private JsonObject? SelectedUnit() => _selectedId is { } id
        ? Units().FirstOrDefault(unit => Int(unit, "id") == id) : null;
    private static int Int(JsonObject value, string key) => value[key]!.GetValue<int>();
    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
    private static Label Wrapped(string text) => new() { Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 38),
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
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
