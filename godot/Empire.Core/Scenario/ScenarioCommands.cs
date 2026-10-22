using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Lifecycle;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Scenario;

public sealed record ScenarioCommandRules(
    ReadOnlyContent Content,
    BattleCombat Combat,
    BattleResources Resources,
    Func<JsonObject, JsonObject, bool, JsonObject> SpawnUnit);

/// <summary>Canonical scenario writers; payload dispatch belongs to the effect registry.</summary>
public sealed partial class ScenarioCommands
{
    private readonly StateContent content;
    private readonly ReadOnlyContent catalog;
    private readonly BattleCombat combat;
    private readonly BattleResources resources;
    private readonly Empire.Core.Terrain.BattleOverlays overlays;
    private readonly Func<JsonObject, JsonObject, bool, JsonObject> spawnUnit;

    public ScenarioCommands(ScenarioCommandRules rules)
    {
        content = rules.Content.Source;
        catalog = rules.Content;
        combat = rules.Combat;
        resources = rules.Resources;
        overlays = new Empire.Core.Terrain.BattleOverlays(rules.Content);
        spawnUnit = rules.SpawnUnit;
    }

    public void SetVariable(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var scenario = Object(state["scenario"], "scenario");
        Object(scenario["variables"], "variables")[Text(effect, "key")] = effect["value"]?.DeepClone();
        return;
    }
    public void AddVariable(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var scenario = Object(state["scenario"], "scenario");
        var key = Text(effect, "key");
        var variables = Object(scenario["variables"], "variables");
        var current = variables[key];
        if (current is not null && current.GetValueKind() != JsonValueKind.Number)
            throw new DomainInvariantException($"scenario variable \"{key}\" is not numeric");
        variables[key] = (current is null ? 0 : Number(current, "variable")) + Number(effect["amount"], "amount");
        return;
    }
    public void ChangeUnitOwner(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var owner = Integer(effect, "owner");
        var changes = new BattleUnitOwnership(state).Assign(Select(state, effect), owner);
        foreach (var change in changes)
        {
            TacticalQueries.RequireUnit(state, change.Unit)["done"] = true;
            emit(new JsonObject
            {
                ["type"] = "unitOwnerChanged",
                ["unit"] = change.Unit,
                ["from"] = change.PreviousOwner,
                ["to"] = change.Owner,
            });
        }
    }

    public void RemoveEngagementRule(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var scenario = Object(state["scenario"], "scenario");
        var rules = Array(scenario["engagementRules"], "engagementRules");
        var removed = rules.OfType<JsonObject>().FirstOrDefault(rule => Text(rule, "id") == Text(effect, "id"));
        if (removed is not null)
            rules.Remove(removed);
        return;
    }
    public void DamageStructure(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        combat.DamageStructure(state, Text(effect, "id"), Number(effect["amount"], "amount"), emit);
        return;
    }
    public void DisableStructure(JsonObject state, JsonObject effect, Action<JsonObject> emit) =>
        combat.DisableStructure(state, Text(effect, "id"), emit);
    public void RepairStructure(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        combat.RepairStructure(state, Text(effect, "id"), Number(effect["amount"], "amount"), emit);
        return;
    }
    public void AddStatus(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var unit in Select(state, effect))
            combat.Effects.AddStatus(unit, Text(effect, "status"), Number(effect["duration"], "duration"), null, emit);
        return;
    }
    public void RemoveStatus(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var unit in Select(state, effect))
            combat.Effects.RemoveStatus(unit, Text(effect, "status"), emit);
        return;
    }
    public void WithdrawUnits(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var unit in Select(state, effect))
            combat.Damage.Withdraw(state, unit, IsTrue(effect["leaveCorpse"]), emit);
        return;
    }
    public void SurrenderUnits(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var unit in Standing(state, effect))
            combat.Damage.Surrender(state, Integer(unit, "id"), effect["to"] is null ? null : Integer(effect, "to"), emit);
        return;
    }
    public void ForceMove(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var unit in Standing(state, effect))
            combat.ForceMove(state, Integer(unit, "id"), TacticalQueries.Position(Object(effect["source"], "source")),
                    Text(effect, "mode"), Number(effect["distance"], "distance"), Number(effect, "collisionDamage", 0), emit);
        return;
    }
    public void RemoveMarkers(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var marker in ScenarioSelection.SelectMarkers(state, Object(effect["selector"], "selector")))
        {
            Array(state["markers"], "markers").Remove(marker);
            emit(new JsonObject { ["type"] = "markerRemoved", ["marker"] = marker["id"]!.DeepClone(), ["kind"] = marker["kind"]!.DeepClone(), ["at"] = marker["at"]!.DeepClone() });
        }
        return;
    }
    public void SetPlayerTeam(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var player = TacticalQueries.Player(state, Integer(effect, "player"));
        var from = Integer(player, "team");
        var team = Integer(effect, "team");
        if (from == team)
            return;
        player["team"] = team;
        emit(new JsonObject { ["type"] = "playerTeamChanged", ["player"] = player["id"]!.DeepClone(), ["from"] = from, ["to"] = team });
        return;
    }
    public void ActivateObjective(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        ChangeObjective(state, effect, "active", null, emit);
        return;
    }
    public void CancelObjective(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        ChangeObjective(state, effect, "cancelled", null, emit);
        return;
    }
    public void CompleteObjective(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        ChangeObjective(state, effect, "completed", null, emit);
        return;
    }
    public void RevealObjective(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        ChangeObjective(state, effect, null, false, emit);
        return;
    }
    public void ChangeUnitResource(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var unit in Select(state, effect))
            ChangeResource(unit, Text(effect, "resource"), Number(effect["amount"], "amount"), emit);
        return;
    }
    public void AddEngagementRule(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        AddEngagementRule(state, Object(effect["rule"], "rule"));
        return;
    }
    public void RemoveOverlay(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        Empire.Core.Terrain.BattleOverlays.Remove(state, Text(effect, "id"), emit);
        return;
    }
    public void ChangeMorale(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        foreach (var unit in Select(state, effect))
            if (TacticalQueries.UnitById(state, Integer(unit, "id")) is not null)
                combat.Damage.ChangeMorale(state, Integer(unit, "id"), Number(effect["amount"], "amount"),
                    effect["reason"] is null ? "scenario" : Text(effect, "reason"), emit);
        return;
    }
    public void MoveComposite(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        CompositeRules.Move(state, Text(effect, "id"), Number(effect["dx"], "dx"), Number(effect["dy"], "dy"), emit);
        return;
    }

    private IReadOnlyList<JsonObject> Select(JsonObject state, JsonObject effect) =>
        ScenarioSelection.SelectUnits(catalog, state, Object(effect["selector"], "selector"));

    private IEnumerable<JsonObject> Standing(JsonObject state, JsonObject effect)
    {
        foreach (var unit in Select(state, effect))
            if (TacticalQueries.UnitById(state, Integer(unit, "id")) is not null)
                yield return unit;
    }

    public void Spawn(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var battlefield = new TacticalBattlefield(state, content);
        foreach (var source in Objects(effect["units"], "units"))
        {
            TacticalQueries.Player(state, Integer(source, "owner"));
            var at = TacticalQueries.Position(source);
            if (!battlefield.Contains(at))
                throw new DomainInvariantException($"spawn cell out of bounds: {at.X},{at.Y}");
            var key = source["key"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(key) && Objects(state["units"], "units").Any(unit => unit["key"]?.GetValue<string>() == key))
                throw new DomainInvariantException($"unit key already exists: \"{key}\"");
            if (!string.IsNullOrEmpty(key) && Objects(state["markers"], "markers").Any(marker => marker["fallenUnit"]?["key"]?.GetValue<string>() == key))
                throw new DomainInvariantException($"unit key is reserved by a fallen unit: \"{key}\"");
            var type = Text(source, "unit");
            var definition = content.Get("units", type);
            var cell = battlefield.Cell(at);
            if (!cell.Admits(Text(definition, "movementClass")))
                throw new DomainInvariantException($"unit \"{type}\" cannot spawn at {at.X},{at.Y}");
            if (cell.Occupant is not null)
                continue;
            var unit = spawnUnit(state, source, !IsTrue(effect["ready"]));
            emit(new JsonObject
            {
                ["type"] = "unitSpawned",
                ["unit"] = unit["id"]!.DeepClone(),
                ["unitType"] = unit["type"]!.DeepClone(),
                ["owner"] = unit["owner"]!.DeepClone(),
                ["at"] = TacticalQueries.CoordinateJson(TacticalQueries.Position(unit)),
                ["reason"] = effect["reason"]?.DeepClone() ?? JsonValue.Create("reinforcement")
            });
        }
    }

    public void ChangeDirective(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var requested = Object(effect["directive"], "directive");
        var zone = requested["zone"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(zone) && state["scenario"]?["zones"]?[zone] is null)
            throw new DomainInvariantException($"unknown scenario zone \"{zone}\"");
        foreach (var unit in Select(state, effect))
        {
            var directive = new JsonObject
            {
                ["mode"] = requested["mode"]!.DeepClone(),
                ["waypoints"] = requested["waypoints"]?.DeepClone() ?? new JsonArray(),
                ["cursor"] = Math.Max(0, Round(Number(requested, "cursor", 0)))
            };
            if (requested.ContainsKey("zone"))
                directive["zone"] = requested["zone"]?.DeepClone();
            unit["directive"] = directive;
            emit(new JsonObject { ["type"] = "directiveChanged", ["unit"] = unit["id"]!.DeepClone(), ["mode"] = directive["mode"]!.DeepClone() });
        }
    }
}
