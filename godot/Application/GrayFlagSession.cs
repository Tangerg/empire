using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Tactics;
using Empire.Core.Transactions;
using Empire.Presentation;

namespace Empire.Application;

/// <summary>Thin application adapter. All choices come from the same core that dispatches them.</summary>
public sealed class GrayFlagSession : IBattleSession
{
    private readonly GrayFlagEngine engine;
    private readonly Battle battle;
    private long revision;
    private readonly List<JsonObject> actions = new();
    private readonly List<JsonObject> events = new();

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

    public bool AdvanceComputer()
    {
        if (!IsComputerTurn) return false;
        Commit(engine.Ai.ChooseAction(battle.Snapshot()));
        return true;
    }

    public BattleSnapshot GetSnapshot() => new(revision, JsonSerializer.SerializeToElement(battle.Snapshot()));
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
            var field = engine.Movement.ComputeMoveField(state, unit);
            foreach (var stop in field.Stops)
            {
                var at = engine.Movement.Board(state).CoordOf(stop);
                var path = engine.Movement.PathTo(field, state, at);
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
            foreach (var direction in engine.Movement.Grid(state).Directions)
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
            var board = engine.Movement.Board(state);
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
        var command = JsonNode.Parse(action.GetRawText()) as JsonObject;
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
        var receipt = battle.Dispatch(command);
        actions.Add((JsonObject)command.DeepClone());
        events.AddRange(receipt.Events.Select(value => (JsonObject)value.DeepClone()));
        revision++;
        return receipt;
    }

    private static BattleOrderChoice Choice(string label, string role, Coord? at, JsonObject action) =>
        new(label, role, at is { } target ? new BoardCell(target.X, target.Y) : null,
            JsonSerializer.SerializeToElement(action));
}
