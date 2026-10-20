using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.Tactics;
using Empire.Core.Transport;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Scenario;

public sealed partial class ScenarioExtensions
{
    private Coord? OpenCell(JsonObject state, TacticalBattlefield battlefield, string zone, string movementClass) =>
        Lifecycle.ScenarioSystem.Zone(state, zone).Select(TacticalQueries.Position).OrderBy(at => at.Y).ThenBy(at => at.X)
            .Cast<Coord?>().FirstOrDefault(at => battlefield.Cell(at!.Value).CanReceive(movementClass));

    private void TeleportSelected(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var battlefield = movement.Battlefield(state);
        foreach (var unit in Select(state, effect))
        {
            var destination = OpenCell(state, battlefield, Text(effect, "zone"), Text(content.Get("units", Text(unit, "type")), "movementClass"));
            if (destination is not { } at) continue;
            var from = TacticalQueries.Position(unit);
            if (from == at) continue;
            // Only the destination matters for a teleport; facing, action readiness,
            // patrol cursor and intermediate cliffs deliberately remain untouched.
            Array(battlefield.Map["captureProgress"], "captureProgress")[battlefield.IndexOf(from)] = 0;
            unit["x"] = at.X; unit["y"] = at.Y; unit["capture"] = 0;
            emit(new JsonObject { ["type"] = "forcedMove", ["unit"] = unit["id"]!.DeepClone(), ["mode"] = "teleport",
                ["from"] = TacticalQueries.CoordinateJson(from), ["to"] = TacticalQueries.CoordinateJson(at),
                ["path"] = TacticalQueries.PathJson(new[] { from, at }), ["collided"] = false });
        }
    }

    private void ReviveMarkers(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var ratio = Math.Max(0.01, Math.Min(1, Number(effect, "hpPercent", 0.5)));
        foreach (var marker in selection.SelectMarkers(state, Object(effect["selector"], "selector")))
        {
            if (marker["fallenUnit"] is not JsonObject fallen) continue;
            var hp = Math.Max(1, Round(Number(content.Get("units", Text(fallen, "type"))["maxHp"], "maxHp") * ratio));
            BattleUnitReturn.Restore(state, marker, new UnitReturnRequest(TacticalQueries.Position(Object(marker["at"], "marker at")),
                effect["owner"] is null ? null : Integer(effect, "owner"), hp), emit);
        }
    }

    private void RestoreWithdrawn(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var battlefield = movement.Battlefield(state);
        foreach (var marker in selection.SelectMarkers(state, Object(effect["selector"], "selector")))
        {
            if (marker["fallenUnit"] is not JsonObject fallen) continue;
            var destination = OpenCell(state, battlefield, Text(effect, "zone"), Text(content.Get("units", Text(fallen, "type")), "movementClass"));
            if (destination is not { } at) break;
            BattleUnitReturn.Restore(state, marker, new UnitReturnRequest(at, effect["owner"] is null ? null : Integer(effect, "owner")), emit);
        }
    }

    private static void ChangeObjective(JsonObject state, JsonObject effect, string? status, bool? hidden, Action<JsonObject> emit)
    {
        var owner = Integer(effect, "player");
        var id = Text(effect, "id");
        var player = TacticalQueries.Player(state, owner);
        var objective = Object(player["objectiveStates"], "objectiveStates")[id] as JsonObject
            ?? throw new DomainInvariantException($"unknown objective \"{id}\" for player {owner}");
        if (status is not null) objective["status"] = status;
        if (hidden is not null) objective["hidden"] = hidden.Value;
        emit(new JsonObject { ["type"] = "objectiveChanged", ["player"] = owner, ["objective"] = id,
            ["status"] = objective["status"]!.DeepClone(), ["hidden"] = objective["hidden"]!.DeepClone() });
    }

    private void ChangeResource(JsonObject unit, string resource, double requested, Action<JsonObject> emit)
    {
        var subject = new BattleResources.Subject("unit", unit);
        if (!resources.HasAccount(subject, resource) || requested == 0) return;
        var balance = resources.Balance(subject, resource);
        var amount = requested > 0 ? resources.Credit(subject, resource, requested)
            : -resources.Spend(subject, resource, Math.Min(-requested, balance ?? 0));
        resources.Announce(subject, resource, amount, emit);
    }

    private static void AddEngagementRule(JsonObject state, JsonObject rule)
    {
        var scenario = Object(state["scenario"], "scenario");
        var zone = Text(rule, "zone");
        if (Object(scenario["zones"], "zones")[zone] is null) throw new DomainInvariantException($"unknown scenario zone \"{zone}\"");
        var rules = Array(scenario["engagementRules"], "engagementRules");
        var id = Text(rule, "id");
        if (Objects(rules, "engagementRules").Any(candidate => Text(candidate, "id") == id))
            throw new DomainInvariantException($"engagement rule already exists: \"{id}\"");
        rules.Add(rule.DeepClone());
    }
}
