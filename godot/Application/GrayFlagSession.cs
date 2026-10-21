using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;
using Empire.Presentation;
using Empire.Presentation.Feedback;

namespace Empire.Application;

/// <summary>Thin application adapter. All choices come from the same core that dispatches them.</summary>
public sealed partial class GrayFlagSession
{
    private readonly GrayFlagEngine engine;
    private readonly Battle battle;
    private long revision;
    private BattlePresentationTransition? transition;
    private readonly BattleEventText eventText = BattleEventText.CreateDefault().Seal();
    private readonly List<JsonObject> actions = new();
    private readonly List<JsonObject> events = new();
    private readonly Stack<UndoCheckpoint> undo = new();
    private sealed record UndoCheckpoint(BattleReceipt Receipt, int ActionCount, int EventCount);
    private ITacticalGrid RuleGrid(JsonObject state) => engine.Grids.Get(state["rules"]!["grid"]!.GetValue<string>());
    private HashSet<int> VisibleUnitIds(JsonObject state, int viewer, IReadOnlySet<int>? seen = null) =>
        state["units"]!.AsArray().OfType<JsonObject>().Where(unit => engine.Space.IsUnitVisible(state, viewer, unit, seen))
            .Select(unit => unit["id"]!.GetValue<int>()).ToHashSet();
    public bool? LastActionHandsOffTurn { get; private set; }
    public bool CanUndo => undo.Count > 0 && !IsComputerTurn && battle.Snapshot()["phase"]?.GetValue<string>() != "over";

    public BattleDispatchResult Undo(long expectedRevision)
    {
        if (expectedRevision != revision) return new(false, "战场已变化，请重新选择命令。");
        if (!CanUndo) return new(false, "当前无法撤销。");
        var checkpoint = undo.Peek();
        battle.Restore(checkpoint.Receipt);
        undo.Pop();
        actions.RemoveRange(checkpoint.ActionCount, actions.Count - checkpoint.ActionCount);
        events.RemoveRange(checkpoint.EventCount, events.Count - checkpoint.EventCount);
        revision++;
        transition = null;
        LastActionHandsOffTurn = null;
        return new(true, "已撤销上一步");
    }

    public GrayFlagSession(GrayFlagEngine engine, JsonObject level)
    {
        this.engine = engine;
        battle = engine.CreateBattle(level);
    }

    private GrayFlagSession(GrayFlagEngine engine, Battle battle)
    { this.engine = engine; this.battle = battle; }

    public static GrayFlagSession FromSave(GrayFlagEngine engine, JsonNode save, JsonArray? recordedEvents = null)
    {
        var restored = new GrayFlagSession(engine, engine.LoadSavedBattle(save));
        if (recordedEvents is not null) restored.events.AddRange(recordedEvents.OfType<JsonObject>().Select(value => (JsonObject)value.DeepClone()));
        return restored;
    }
    public JsonObject Snapshot() => battle.Snapshot();
    public JsonObject Save() => engine.SaveBattle(battle.Snapshot());
    public IReadOnlyList<JsonObject> Events => events.Select(value => (JsonObject)value.DeepClone()).ToArray();

    public bool IsComputerTurn
    {
        get
        {
            var state = battle.Snapshot();
            return state["phase"]!.GetValue<string>() is "playing" or "deployment" &&
                TacticalQueries.Player(state, state["currentPlayer"]!.GetValue<int>())["controller"]!.GetValue<string>() == "ai";
        }
    }

    public bool AdvanceComputer() => AdvanceComputerStep() is not null;

    public ComputerStepResult? AdvanceComputerStep(Action<string>? onRefusal = null)
    {
        if (!IsComputerTurn) return null;
        var attempted = engine.Ai.ChooseAction(battle.Snapshot());
        return ComputerActionExecution.Execute(attempted, Commit, engine.HandsOffTurn, onRefusal);
    }

    public BattleSnapshot GetSnapshot()
    {
        var state = battle.Snapshot();
        IReadOnlySet<int>? visible = null;
        IReadOnlySet<BoardCell>? cells = null;
        if (state["rules"]?["fog"]?.GetValue<bool>() == true)
        {
            var viewer = Viewer(state);
            var seen = engine.Space.VisibleTiles(state, viewer);
            visible = VisibleUnitIds(state, viewer, seen);
            cells = seen.Select(index => engine.Space.Board(state).CoordOf(index)).Select(at => new BoardCell(at.X, at.Y)).ToHashSet();
        }
        var incoming = engine.Combat.IncomingCasts(state).ToDictionary(pair => new BoardCell(pair.Key.X, pair.Key.Y), pair => pair.Value);
        var objectives = state["composites"]!.AsArray().OfType<JsonObject>().Select(composite => {
            var id = composite["id"]!.GetValue<string>();
            var status = Empire.Core.Scenario.CompositeRules.Status(state, id);
            return $"{id}：{status.Neutralized}/{composite["parts"]!.AsArray().Count} · {status.State}";
        });
        return new BattleSnapshot(revision, Empire.Core.Determinism.EcmaJson.ToElement(state), visible, cells, incoming, string.Join("\n", objectives), transition,
            state["phase"]?.GetValue<string>() == "deployment" ? "战前部署 · 调整站位后确认部署"
                : $"第 1 回合 · {state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => player["controller"]?.GetValue<string>() == "human")?["name"]?.GetValue<string>() ?? ""} 开始行动");
    }
    public IReadOnlyList<JsonObject> ActionLog => actions.Select(action => (JsonObject)action.DeepClone()).ToArray();

    public BattleDispatchResult Dispatch(JsonElement action, long expectedRevision)
    {
        if (expectedRevision != revision) return new(false, "战场已变化，请重新选择命令。");
        if (IsComputerTurn) return new(false, "正在执行敌方回合。");
        var command = Empire.Core.Determinism.EcmaJson.Parse(action.GetRawText()) as JsonObject;
        if (command is null) return new(false, "命令必须是对象。");
        try
        {
            var receipt = Commit(command);
            return new(true, $"命令完成 · {receipt.Events.Count} 条战斗事件");
        }
        catch (IllegalActionException error) { return new(false, error.Message); }
    }

    private BattleReceipt Commit(JsonObject command)
    {
        var before = battle.Snapshot();
        var computer = IsComputerTurn;
        IReadOnlySet<int>? beforeVisible = null;
        if (before["rules"]?["fog"]?.GetValue<bool>() == true)
        {
            var viewer = Viewer(before);
            beforeVisible = VisibleUnitIds(before, viewer);
        }
        var receipt = battle.Dispatch(command);
        if (receipt.HandsOffTurn) undo.Clear();
        else undo.Push(new(receipt, actions.Count, events.Count));
        LastActionHandsOffTurn = receipt.HandsOffTurn;
        actions.Add((JsonObject)command.DeepClone());
        events.AddRange(receipt.Events.Select(value => (JsonObject)value.DeepClone()));
        revision++;
        var after = Empire.Core.Determinism.EcmaJson.ToElement(battle.Snapshot());
        var eventValues = receipt.Events.Select(Empire.Core.Determinism.EcmaJson.ToElement).ToArray();
        var logContext = new BattleLogContext(after, engine.UnitTypeName, engine.CareerName, id => engine.Resources.Name(id) ?? id);
        transition = new BattlePresentationTransition(revision,
            Empire.Core.Determinism.EcmaJson.ToElement(receipt.Before),
            Empire.Core.Determinism.EcmaJson.ToElement(command),
            eventValues, after, computer, beforeVisible,
            eventValues.Select(value => eventText.Describe(logContext, value)).ToArray());
        return receipt;
    }

    private static int Viewer(JsonObject state) =>
        state["players"]!.AsArray().OfType<JsonObject>().FirstOrDefault(player => player["controller"]?.GetValue<string>() == "human")?["id"]?.GetValue<int>()
        ?? state["currentPlayer"]!.GetValue<int>();
}

/// <summary>The pause belongs to the attempted order, even when endTurn recovered its refusal.</summary>
public readonly record struct ComputerStepResult(bool Committed, bool AttemptedHandsOffTurn);

internal static class ComputerActionExecution
{
    internal static ComputerStepResult Execute(JsonObject attempted, Func<JsonObject, BattleReceipt> dispatch,
        Func<JsonObject, bool> handsOffTurn, Action<string>? onRefusal)
    {
        var committed = false;
        try { dispatch(attempted); committed = true; }
        catch (IllegalActionException error)
        {
            onRefusal?.Invoke($"AI 的行动被拒绝：{error.Message}");
            try { dispatch(new JsonObject { ["kind"] = "endTurn" }); committed = true; }
            catch (IllegalActionException) { /* The source settles an empty event list after a refused fallback. */ }
        }
        return new(committed, handsOffTurn(attempted));
    }
}
