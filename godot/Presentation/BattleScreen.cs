#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

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
    private Label _turn = null!;
    private Label _selection = null!;
    private Label _modeHint = null!;
    private Label _feedback = null!;
    private Label _coverage = null!;
    private VBoxContainer _modeButtons = null!;
    private VBoxContainer _orders = null!;
    private VBoxContainer _globalOrders = null!;
    private HBoxContainer _applicationActions = null!;
    private ConfirmationDialog _restartConfirmation = null!;
    private Action? _restart;
    private IReadOnlyList<BattleOrderChoice> _legalOrders = Array.Empty<BattleOrderChoice>();
    private int? _selectedId;
    private BoardCell? _target;
    private string _role = "";
    private bool _built;
    private bool _bound;

    public BoardView Board => _board;
    public int? SelectedUnitId => _selectedId;
    public long? DisplayedRevision => _snapshot?.Revision;

    public override void _Ready()
    {
        BuildInterface();
        FitBoard();
    }

    public void BindSession(IBattleSession session, AssetCatalog assets, JsonObject level,
        AuthoredScene authoredScene, EnvironmentCatalog environment, Func<string?, bool> facesLeft)
    {
        ArgumentNullException.ThrowIfNull(session);
        BuildInterface();
        _board.Configure(assets, level, authoredScene, environment, facesLeft);
        _session = session;
        _bound = true;
        _selectedId = null;
        _target = null;
        _feedback.Text = "选择单位，查看规则允许的命令。";
        Refresh();
        FitBoard();
    }

    public void ConfigureTerrain(WorldMaterialsManifest materials)
    {
        _board.ConfigureTerrain(materials);
        if (_state is not null) Refresh();
    }

    public void BindApplicationActions(Action? onSave = null, Action? onLoad = null, Action? onRestart = null)
    {
        BuildInterface();
        Clear(_applicationActions);
        _restart = onRestart;
        if (onSave is not null) _applicationActions.AddChild(Button("保存", onSave));
        if (onLoad is not null) _applicationActions.AddChild(Button("读取", onLoad));
        if (onRestart is not null)
            _applicationActions.AddChild(Button("重新开始", () => _restartConfirmation.PopupCentered()));
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
        if (!_bound || _session is null) return;
        _snapshot = _session.GetSnapshot();
        _state = JsonNode.Parse(_snapshot.State.GetRawText()) as JsonObject
            ?? throw new InvalidOperationException("Battle session returned a non-object state.");
        _board.RenderState(_state);
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
        _applicationActions = new HBoxContainer { Name = "ApplicationActions" };
        heading.AddChild(_applicationActions);
        _turn = Text("尚未绑定战斗", 18, new Color("#d1cfbc"));
        heading.AddChild(_turn);
        _restartConfirmation = new ConfirmationDialog {
            Title = "重新开始？", DialogText = "当前未保存的战斗进度将丢失。",
            OkButtonText = "重新开始", CancelButtonText = "取消" };
        _restartConfirmation.Confirmed += () => _restart?.Invoke();
        AddChild(_restartConfirmation);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 20);
        column.AddChild(body);
        _mapArea = new Control { Name = "MapArea", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true, MouseFilter = MouseFilterEnum.Stop };
        body.AddChild(_mapArea);
        _world = new Node2D { Name = "World" };
        _mapArea.AddChild(_world);
        _world.AddChild(_board);
        _world.AddChild(_overlay);
        _mapArea.GuiInput += MapInput;
        _mapArea.Resized += FitBoard;

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
        _coverage = Wrapped("Native C# migration");
        _coverage.AddThemeFontSizeOverride("font_size", 12);
        _coverage.AddThemeColorOverride("font_color", new Color("#aab6a5"));
        column.AddChild(_coverage);
    }

    private void FitBoard()
    {
        if (!_built || _board.FieldSize.X <= 0 || _mapArea.Size.X <= 0) return;
        // Matches the source scene's world-frame insets; this changes only the camera.
        var frame = _board.FieldSize + new Vector2(148, 136);
        var scale = Math.Min(_mapArea.Size.X / frame.X, _mapArea.Size.Y / frame.Y);
        _world.Scale = Vector2.One * scale;
        _world.Position = (_mapArea.Size - frame * scale) / 2 + new Vector2(74, 62) * scale;
    }

    private void MapInput(InputEvent input)
    {
        if (!_bound || input is not InputEventMouseButton { Pressed: true } mouse) return;
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            _target = null;
            _selectedId = null;
            Refresh();
            AcceptEvent();
            return;
        }
        if (mouse.ButtonIndex != MouseButton.Left) return;
        var point = (mouse.Position - _world.Position) / _world.Scale;
        var cell = new BoardCell(Mathf.FloorToInt(point.X / 32), Mathf.FloorToInt(point.Y / 32));
        if (point.X < 0 || point.Y < 0 || point.X >= _board.FieldSize.X || point.Y >= _board.FieldSize.Y) return;
        SelectCell(cell);
        AcceptEvent();
    }

    /// <summary>Cell interaction entry point also used by native contract checks.</summary>
    public void SelectCell(BoardCell cell)
    {
        if (_state is null || _session is null) return;
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
        if (_session is null || _snapshot is null) return;
        // The JSON is the session's complete canonical command, not a UI reconstruction.
        var result = _session.Dispatch(order.Action, _snapshot.Revision);
        ShowFeedback(result.Message);
        Refresh();
    }

    private IEnumerable<BattleOrderChoice> TargetChoices(BoardCell cell) =>
        _legalOrders.Where(order => order.PresentationRole == _role && order.Target == cell);
    private IEnumerable<JsonObject> Units() => (_state?["units"] as JsonArray)?.OfType<JsonObject>()
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
