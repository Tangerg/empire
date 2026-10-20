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
public sealed partial class GrayFlagSession : IBattleSession
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

    public IReadOnlyList<BattleOrderChoice> GetLegalOrders(int? selectedUnitId)
    {
        var state = battle.Snapshot();
        if (IsComputerTurn || state["phase"]!.GetValue<string>() == "over") return [];
        if (state["phase"]!.GetValue<string>() == "deployment")
        {
            var deployment = new List<BattleOrderChoice> { Choice("确认部署", "turn", null, new JsonObject { ["kind"]="finishDeployment" }) };
            var deployed = selectedUnitId is { } deployId ? TacticalQueries.UnitById(state,deployId) : null;
            if (deployed is not null && deployed["owner"]!.GetValue<int>() == state["currentPlayer"]!.GetValue<int>())
                foreach (var spot in engine.Deployment.Spots(state,deployed))
                    deployment.Add(Choice(spot.Swaps is null ? "部署到此处" : "交换部署", "move", spot.At, new JsonObject { ["kind"]="deployUnit", ["unit"]=selectedUnitId, ["at"]=TacticalQueries.CoordinateJson(spot.At) }));
            return deployment;
        }
        var choices = new List<BattleOrderChoice>
        {
            Choice("结束回合", "turn", null, new JsonObject { ["kind"] = "endTurn" })
        };
        var unit = selectedUnitId is { } id ? TacticalQueries.UnitById(state, id) : null;
        if (unit is not null && engine.Turns.MayAct(state, unit))
        {
            var field = engine.Space.ComputeMoveField(state, unit);
            foreach (var stop in field.Stops)
            {
                var at = engine.Space.Board(state).CoordOf(stop);
                var path = engine.Space.PathTo(field, state, at);
                if (path is null) continue;
                foreach (var command in engine.CommandsAt(state, unit, at))
                {
                    var targets = command.SelfTargeted ? new Coord?[] { null } : command.Targets.Select(target => (Coord?)target);
                    foreach (var target in targets)
                    {
                        var payload = new JsonObject { ["ability"] = command.Ability };
                        if (command.Weapon is not null) payload["weapon"] = command.Weapon;
                        if (target is { } coordinate) payload["target"] = TacticalQueries.CoordinateJson(coordinate);
                        var action = new JsonObject { ["kind"] = "command", ["unit"] = selectedUnitId,
                            ["path"] = TacticalQueries.PathJson(path), ["command"] = payload };
                        var destination = target ?? at;
                        var role = command.Ability == "attack" ? "attack" : command.Ability == "wait" ? "move" : "command";
                        choices.Add(Choice($"{command.Name} · 从 {at.X},{at.Y}", role, destination, action));
                    }
                }
            }
            foreach (var direction in RuleGrid(state).Directions)
                choices.Add(Choice($"朝向：{direction.Name}", "command", null,
                    new JsonObject { ["kind"]="face", ["unit"]=selectedUnitId, ["facing"]=direction.Id }));
            foreach (var option in engine.Progression.Careers.Options(state,unit).Where(option => option.Eligible))
                choices.Add(Choice($"转职：{option.Career["name"]}", "command", null,
                    new JsonObject { ["kind"]="changeCareer", ["unit"]=selectedUnitId, ["career"]=option.Career["id"]!.DeepClone() }));
            foreach (var option in engine.Progression.Formations.Options(state,unit).Where(option => option.Eligible && !option.Current))
                choices.Add(Choice($"阵形：{option.Formation["name"]}", "command", null,
                    new JsonObject { ["kind"]="changeFormation", ["unit"]=selectedUnitId, ["formation"]=option.Formation["id"]!.DeepClone() }));
            if (unit["formation"] is not null) choices.Add(Choice("解除阵形", "command", null,
                new JsonObject { ["kind"]="changeFormation", ["unit"]=selectedUnitId, ["formation"]=null }));
            foreach (var carrier in engine.Transport.CarrierOptions(state,unit).Where(option => option.Eligible && option.Carrier["owner"]!.GetValue<int>() == state["currentPlayer"]!.GetValue<int>()))
                choices.Add(Choice($"登载单位 {carrier.Carrier["id"]}", "command", TacticalQueries.Position(carrier.Carrier),
                    new JsonObject { ["kind"]="embark", ["unit"]=selectedUnitId, ["carrier"]=carrier.Carrier["id"]!.DeepClone() }));
            foreach (var passenger in engine.Transport.PassengerOptions(state,unit))
                foreach (var spot in passenger.Spots)
                    choices.Add(Choice($"卸载单位 {passenger.Unit["id"]}", "command", spot,
                        new JsonObject { ["kind"]="disembark", ["carrier"]=selectedUnitId, ["unit"]=passenger.Unit["id"]!.DeepClone(), ["at"]=TacticalQueries.CoordinateJson(spot) }));
            foreach (var stance in Empire.Core.Combat.CombatForecasts.DefaultReactions())
                choices.Add(Choice($"反应：{stance.Name}", "command", null,
                    new JsonObject { ["kind"] = "reaction", ["unit"] = selectedUnitId, ["stance"] = stance.Id }));
        }
        if (selectedUnitId is null)
        {
            var board = engine.Space.Board(state);
            for (var index=0;index<state["map"]!["tiles"]!.AsArray().Count;index++)
            {
                var at=board.CoordOf(index);
                foreach(var type in engine.Recruitment.Options(state,at))
                    choices.Add(Choice($"招募 {type}", "command",at,new JsonObject { ["kind"]="recruit",["unit"]=type,["at"]=TacticalQueries.CoordinateJson(at) }));
            }
        }
        foreach (var commander in state["commanders"]!.AsArray().OfType<JsonObject>())
            foreach (var tactic in engine.Support.Tactics(state, commander["id"]!.GetValue<string>()))
                foreach (var target in tactic.Targets)
                    choices.Add(Choice(tactic.Name, "tactic", target,
                        new JsonObject { ["kind"] = "tactic", ["commander"] = commander["id"]!.DeepClone(),
                            ["tactic"] = tactic.Id, ["target"] = TacticalQueries.CoordinateJson(target) }));
        return choices;
    }

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

    private static BattleOrderChoice Choice(string label, string role, Coord? at, JsonObject action) =>
        new(label, role, at is { } target ? new BoardCell(target.X, target.Y) : null,
            Empire.Core.Determinism.EcmaJson.ToElement(action));
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
