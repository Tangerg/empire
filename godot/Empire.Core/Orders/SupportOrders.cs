using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Orders;

public sealed record TacticOption(string Id, string Name, IReadOnlyList<Coord> Targets, IReadOnlyList<BattleResources.Cost> Costs);

/// <summary>Commander orders and site capture, using shared account and effect writers.</summary>
public sealed record SupportOrderRules(
    ReadOnlyContent Content,
    GridRegistry Grids,
    BattleResources Resources,
    CombatEffects Effects);

public sealed class SupportOrders
{
    public static IReadOnlyList<string> SupportedTacticEffects { get; } = System.Array.AsReadOnly(new[] { "addStatus", "removeStatus" });
    private readonly StateContent content;
    private readonly GridRegistry grids;
    private readonly CombatEffects effects;
    private readonly BattleResources resources;

    public SupportOrders(SupportOrderRules rules)
    {
        content = rules.Content.Source;
        resources = rules.Resources;
        effects = rules.Effects;
        grids = rules.Grids;
    }

    private ITacticalGrid Grid(JsonObject state) => grids.Get(StateJson.Text(state["rules"]!["grid"], "grid"));
    private static Coord Position(JsonObject unit) => new(StateJson.Integer(unit, "x"), StateJson.Integer(unit, "y"));
    private static IEnumerable<JsonObject> Units(JsonObject state) => StateJson.Objects(state["units"], "units");
    private static JsonObject Player(JsonObject state, int id) => StateJson.Objects(state["players"], "players")
        .FirstOrDefault(player => StateJson.Integer(player, "id") == id)
        ?? throw new DomainInvariantException($"unknown player {id}");

    public IReadOnlyList<TacticOption> Tactics(JsonObject state, string commanderId)
    {
        var commander = StateJson.Objects(state["commanders"], "commanders")
            .FirstOrDefault(candidate => StateJson.Text(candidate, "id") == commanderId);
        if (commander is null || StateJson.Integer(commander, "owner") != StateJson.Integer(state, "currentPlayer")) return [];
        var leader = Units(state).FirstOrDefault(unit => StateJson.Integer(unit, "id") == StateJson.Integer(commander, "unitId"));
        if (leader is null) return [];
        var subject = new BattleResources.Subject("player", Player(state, StateJson.Integer(commander, "owner")));
        var used = StateJson.Array(commander["usedTactics"], "usedTactics");
        var result = new List<TacticOption>();
        foreach (var entry in StateJson.Array(commander["tactics"], "tactics"))
        {
            var id = StateJson.Text(entry, "tactic");
            if (used.Any(value => StateJson.Text(value, "used tactic") == id)) continue;
            var tactic = content.Get("tactics", id);
            if (!resources.CanAfford(subject, StateJson.Array(tactic["costs"], "costs"))) continue;
            var targets = new List<Coord>();
            if (StateJson.Text(tactic, "target") == "self") targets.Add(Position(leader));
            else
            {
                var map = StateJson.Object(state["map"], "map");
                for (var y = 0; y < StateJson.Integer(map, "height"); y++)
                    for (var x = 0; x < StateJson.Integer(map, "width"); x++)
                        if (Grid(state).Distance(Position(leader), new Coord(x, y)) <= StateJson.Number(tactic["range"], "range"))
                            targets.Add(new Coord(x, y));
            }
            result.Add(new TacticOption(id, StateJson.Text(tactic, "name"), targets, BattleResources.CopyCosts(StateJson.Array(tactic["costs"], "costs"))));
        }
        return result;
    }

    public void ExecuteTactic(JsonObject state, string commanderId, string tacticId, Coord? requested, Action<JsonObject> emit)
    {
        var commander = StateJson.Objects(state["commanders"], "commanders")
            .FirstOrDefault(candidate => StateJson.Text(candidate, "id") == commanderId)
            ?? throw new IllegalActionException(new ActionRefusal("support.commander_unknown", new() { ["commanderId"] = commanderId }));
        var ownerId = StateJson.Integer(commander, "owner");
        if (ownerId != StateJson.Integer(state, "currentPlayer")) throw new IllegalActionException(new ActionRefusal("support.commander_not_owner", new() { ["commanderId"] = commanderId }));
        var option = Tactics(state, commanderId).FirstOrDefault(candidate => candidate.Id == tacticId)
            ?? throw new IllegalActionException(new ActionRefusal("support.tactic_unavailable", new() { ["tacticId"] = tacticId }));
        var leader = Units(state).First(unit => StateJson.Integer(unit, "id") == StateJson.Integer(commander, "unitId"));
        var tactic = content.Get("tactics", tacticId);
        var target = StateJson.Text(tactic, "target") == "self" ? Position(leader) : requested;
        if (target is not { } at || !option.Targets.Contains(at)) throw new IllegalActionException(new ActionRefusal("support.target_invalid", new() { ["tacticId"] = tacticId }));
        var owner = Player(state, ownerId);
        var subject = new BattleResources.Subject("player", owner);
        var spent = resources.SpendAll(subject, StateJson.Array(tactic["costs"], "costs"));
        StateJson.Array(commander["usedTactics"], "usedTactics").Add(tacticId);
        foreach (var cost in spent) resources.Announce(subject, cost.Resource, -cost.Value, emit);
        emit(new JsonObject { ["type"] = "tacticUsed", ["commander"] = commanderId, ["tactic"] = tacticId,
            ["target"] = new JsonObject { ["x"] = at.X, ["y"] = at.Y } });
        var affected = Units(state).Where(unit => TacticalQueries.AreAllies(state, StateJson.Integer(unit, "owner"), ownerId)
            && Grid(state).Distance(Position(unit), at) <= StateJson.Number(tactic["radius"], "radius")).ToArray();
        foreach (var effect in StateJson.Objects(tactic["effects"], "effects"))
            foreach (var unit in affected)
            {
                var id = StateJson.Text(effect, "status");
                if (StateJson.Text(effect, "type") == "addStatus")
                    effects.AddStatus(unit, id, StateJson.Integer(effect, "duration"), StateJson.Integer(leader, "id"), emit);
                else if (StateJson.Text(effect, "type") == "removeStatus") effects.RemoveStatus(unit, id, emit);
                else throw new DomainInvariantException("unsupported tactic effect");
            }
    }

    public bool CanCapture(JsonObject state, JsonObject unit, Coord at)
    {
        var map = StateJson.Object(state["map"], "map");
        var index = at.Y * StateJson.Integer(map, "width") + at.X;
        return !effects.CannotCapture(unit)
            && StateJson.IsTrue(content.Get("terrains", StateJson.Text(map["tiles"]![index], "terrain"))["capturable"])
            && (map["owners"]![index] is not { } owner || StateJson.Integer(owner, "owner") != StateJson.Integer(unit, "owner"));
    }

    public void Capture(JsonObject state, JsonObject unit, Coord at, Action<JsonObject> emit)
    {
        if (!CanCapture(state, unit, at)) throw new IllegalActionException(new ActionRefusal("ability.unusable", new() { ["abilityId"] = "capture" }));
        var map = StateJson.Object(state["map"], "map");
        var rules = StateJson.Object(state["rules"], "rules");
        var index = at.Y * StateJson.Integer(map, "width") + at.X;
        var threshold = StateJson.Number(rules["captureThreshold"], "captureThreshold");
        var maximum = StateJson.Number(content.Get("units", StateJson.Text(unit, "type"))["maxHp"], "maxHp");
        var ratio = Math.Clamp(StateJson.Number(unit["hp"], "hp") / maximum, 0, 1);
        var contribution = Math.Max(1, Math.Floor(threshold * ratio + 0.5));
        var progress = StateJson.Number(map["captureProgress"]![index], "captureProgress") + contribution;
        var captured = StateJson.Text(rules, "captureMode") == "instant" || progress >= threshold;
        if (captured) map["owners"]![index] = unit["owner"]!.DeepClone();
        map["captureProgress"]![index] = captured ? 0 : progress;
        emit(new JsonObject { ["type"] = "capture", ["at"] = new JsonObject { ["x"] = at.X, ["y"] = at.Y },
            ["player"] = unit["owner"]!.DeepClone(), ["progress"] = captured ? 1 : progress / threshold, ["captured"] = captured });
        effects.AwardRankProgress(unit, captured ? 60 : 20, emit);
    }
}
