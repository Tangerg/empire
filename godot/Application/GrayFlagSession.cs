using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;
using Empire.Presentation;
using Empire.Presentation.Feedback;
using Empire.Localization;

namespace Empire.Application;

/// <summary>Thin application adapter. All choices come from the same core that dispatches them.</summary>
public sealed partial class GrayFlagSession
{
    private readonly GrayFlagEngine engine;
    private readonly GameText text;
    private readonly ActionRefusalText refusals;
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
        if (expectedRevision != revision) return new(false, text.Format("error.session.revision_changed"));
        if (!CanUndo) return new(false, text.Format("error.session.undo_unavailable"));
        var checkpoint = undo.Peek();
        battle.Restore(checkpoint.Receipt);
        undo.Pop();
        actions.RemoveRange(checkpoint.ActionCount, actions.Count - checkpoint.ActionCount);
        events.RemoveRange(checkpoint.EventCount, events.Count - checkpoint.EventCount);
        revision++;
        transition = null;
        LastActionHandsOffTurn = null;
        return new(true, text.Format("feedback.session.undone"));
    }

    public GrayFlagSession(GrayFlagEngine engine, JsonObject level, GameText text, ActionRefusalText refusals)
    {
        this.engine = engine;
        this.text = text;
        this.refusals = refusals;
        battle = engine.CreateBattle(level);
    }

    private GrayFlagSession(GrayFlagEngine engine, Battle battle, GameText text, ActionRefusalText refusals)
    { this.engine = engine; this.battle = battle; this.text = text; this.refusals = refusals; }

    public static GrayFlagSession FromSave(GrayFlagEngine engine, JsonNode save, GameText text, ActionRefusalText refusals, JsonArray? recordedEvents = null)
    {
        var restored = new GrayFlagSession(engine, engine.LoadSavedBattle(save), text, refusals);
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
        return ComputerActionExecution.Execute(attempted, Commit, engine.HandsOffTurn,
            refusal => onRefusal?.Invoke(refusals.DescribeComputer(refusal)));
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
        if (expectedRevision != revision) return new(false, text.Format("error.session.revision_changed"));
        if (IsComputerTurn) return new(false, text.Format("error.session.computer_turn"));
        var command = Empire.Core.Determinism.EcmaJson.Parse(action.GetRawText()) as JsonObject;
        if (command is null) return new(false, text.Format("error.session.command_shape"));
        try
        {
            var receipt = Commit(command);
            return new(true, text.Format("feedback.session.command_completed", receipt.Events.Count));
        }
        catch (IllegalActionException error) { return new(false, refusals.Describe(error.Refusal)); }
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
        Func<JsonObject, bool> handsOffTurn, Action<ActionRefusal>? onRefusal)
    {
        var committed = false;
        try { dispatch(attempted); committed = true; }
        catch (IllegalActionException error)
        {
            onRefusal?.Invoke(error.Refusal);
            try { dispatch(new JsonObject { ["kind"] = "endTurn" }); committed = true; }
            catch (IllegalActionException) { /* The source settles an empty event list after a refused fallback. */ }
        }
        return new(committed, handsOffTurn(attempted));
    }
}
