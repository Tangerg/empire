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

    public GrayFlagSession(GrayFlagEngine engine, JsonObject level)
    {
        this.engine = engine;
        battle = engine.CreateBattle(level);
    }

    public BattleSnapshot GetSnapshot() => new(revision, JsonSerializer.SerializeToElement(battle.Snapshot()));
    public IReadOnlyList<JsonObject> ActionLog => actions.Select(action => (JsonObject)action.DeepClone()).ToArray();

    public IReadOnlyList<BattleOrderChoice> GetLegalOrders(int? selectedUnitId)
    {
        var state = battle.Snapshot();
        if (state["phase"]!.GetValue<string>() != "playing") return [];
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
            foreach (var stance in Empire.Core.Combat.CombatForecasts.DefaultReactions())
                choices.Add(Choice($"反应：{stance.Name}", "command", null,
                    new JsonObject { ["kind"] = "reaction", ["unit"] = selectedUnitId, ["stance"] = stance.Id }));
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
        var command = JsonNode.Parse(action.GetRawText()) as JsonObject;
        if (command is null) return new(false, "命令必须是对象。");
        try
        {
            var receipt = battle.Dispatch(command);
            actions.Add((JsonObject)command.DeepClone());
            revision++;
            return new(true, $"命令完成 · {receipt.Events.Count} 条战斗事件");
        }
        catch (IllegalActionException error) { return new(false, error.Message); }
    }

    private static BattleOrderChoice Choice(string label, string role, Coord? at, JsonObject action) =>
        new(label, role, at is { } target ? new BoardCell(target.X, target.Y) : null,
            JsonSerializer.SerializeToElement(action));
}
