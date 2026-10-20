using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Lifecycle;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Scenario;

/// <summary>Authored campaign effects, composed through the lifecycle's explicit extra-handler ports.</summary>
public sealed partial class ScenarioExtensions
{
    public static IReadOnlyList<string> SupportedEffects { get; } = System.Array.AsReadOnly(new[]
    {
        "setVariable", "addVariable", "changeUnitOwner", "spawnUnits", "removeEngagementRule",
        "damageStructure", "changeMorale", "setUnitDirective", "moveComposite",
        "addStatus", "removeStatus", "withdrawUnits", "reviveMarkers", "removeMarkers", "setPlayerTeam",
        "forceMove", "teleportUnits", "addOverlay", "removeOverlay", "activateObjective", "cancelObjective",
        "completeObjective", "revealObjective", "changeUnitResource", "surrenderUnits", "restoreWithdrawnUnits",
        "addEngagementRule", "replaceTerrain", "setElevation", "addElevation", "setCliffs", "setDirectionalCover", "repairStructure",
    });
    public static IReadOnlyList<string> SupportedConditions { get; } = System.Array.AsReadOnly(new[] { "composite" });
    private readonly StateContent content;
    private readonly ScenarioSystem selection;
    private readonly BattleCombat combat;
    private readonly BattleResources resources;
    private readonly Empire.Core.Terrain.BattleOverlays overlays;
    private readonly Func<JsonObject, JsonObject, bool, JsonObject> spawnUnit;

    public ScenarioExtensions(JsonArray packs, BattleCombat combat, BattleResources resources,
        Func<JsonObject, JsonObject, bool, JsonObject> spawnUnit)
    {
        content = new StateContent(packs);
        selection = new ScenarioSystem(packs);
        this.combat = combat;
        this.resources = resources;
        overlays = new Empire.Core.Terrain.BattleOverlays(packs);
        this.spawnUnit = spawnUnit;
    }

    public bool ConditionMet(JsonObject state, JsonObject condition) => Text(condition, "type") switch
    {
        "composite" => CompositeRules.Status(state, Text(condition, "id")).State == Text(condition, "state"),
        var kind => throw new DomainInvariantException($"unknown scenario condition \"{kind}\""),
    };

    public void Apply(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var scenario = Object(state["scenario"], "scenario");
        switch (Text(effect, "type"))
        {
            case "setVariable":
                Object(scenario["variables"], "variables")[Text(effect, "key")] = effect["value"]?.DeepClone();
                break;
            case "addVariable":
                var key = Text(effect, "key");
                var variables = Object(scenario["variables"], "variables");
                var current = variables[key];
                if (current is not null && current.GetValueKind() != JsonValueKind.Number)
                    throw new DomainInvariantException($"scenario variable \"{key}\" is not numeric");
                variables[key] = (current is null ? 0 : Number(current, "variable")) + Number(effect["amount"], "amount");
                break;
            case "changeUnitOwner":
                var owner = Integer(effect, "owner");
                foreach (var unit in Select(state, effect))
                {
                    var previous = Integer(unit, "owner");
                    if (previous == owner) continue;
                    unit["owner"] = owner;
                    unit["done"] = true;
                    emit(new JsonObject { ["type"] = "unitOwnerChanged", ["unit"] = unit["id"]!.DeepClone(), ["from"] = previous, ["to"] = owner });
                }
                break;
            case "spawnUnits": Spawn(state, effect, emit); break;
            case "removeEngagementRule":
                var rules = Array(scenario["engagementRules"], "engagementRules");
                var removed = rules.OfType<JsonObject>().FirstOrDefault(rule => Text(rule, "id") == Text(effect, "id"));
                if (removed is not null) rules.Remove(removed);
                break;
            case "damageStructure": combat.DamageStructure(state, Text(effect, "id"), Number(effect["amount"], "amount"), emit); break;
            case "repairStructure": combat.RepairStructure(state, Text(effect, "id"), Number(effect["amount"], "amount"), emit); break;
            case "addStatus":
                foreach (var unit in Select(state, effect)) combat.Effects.AddStatus(unit, Text(effect, "status"), Number(effect["duration"], "duration"), null, emit);
                break;
            case "removeStatus":
                foreach (var unit in Select(state, effect)) combat.Effects.RemoveStatus(unit, Text(effect, "status"), emit);
                break;
            case "withdrawUnits":
                foreach (var unit in Select(state, effect)) combat.Damage.Withdraw(state, unit, IsTrue(effect["leaveCorpse"]), emit);
                break;
            case "surrenderUnits":
                foreach (var unit in Standing(state, effect)) combat.Damage.Surrender(state, Integer(unit, "id"), effect["to"] is null ? null : Integer(effect, "to"), emit);
                break;
            case "forceMove":
                foreach (var unit in Standing(state, effect)) combat.ForceMove(state, Integer(unit, "id"), TacticalQueries.Position(Object(effect["source"], "source")),
                    Text(effect, "mode"), Number(effect["distance"], "distance"), Number(effect, "collisionDamage", 0), emit);
                break;
            case "teleportUnits": TeleportSelected(state, effect, emit); break;
            case "reviveMarkers": ReviveMarkers(state, effect, emit); break;
            case "restoreWithdrawnUnits": RestoreWithdrawn(state, effect, emit); break;
            case "removeMarkers":
                foreach (var marker in selection.SelectMarkers(state, Object(effect["selector"], "selector")))
                {
                    Array(state["markers"], "markers").Remove(marker);
                    emit(new JsonObject { ["type"] = "markerRemoved", ["marker"] = marker["id"]!.DeepClone(), ["kind"] = marker["kind"]!.DeepClone(), ["at"] = marker["at"]!.DeepClone() });
                }
                break;
            case "setPlayerTeam":
                var player = TacticalQueries.Player(state, Integer(effect, "player"));
                var from = Integer(player, "team");
                var team = Integer(effect, "team");
                if (from == team) break;
                player["team"] = team;
                emit(new JsonObject { ["type"] = "playerTeamChanged", ["player"] = player["id"]!.DeepClone(), ["from"] = from, ["to"] = team });
                break;
            case "activateObjective": ChangeObjective(state, effect, "active", null, emit); break;
            case "cancelObjective": ChangeObjective(state, effect, "cancelled", null, emit); break;
            case "completeObjective": ChangeObjective(state, effect, "completed", null, emit); break;
            case "revealObjective": ChangeObjective(state, effect, null, false, emit); break;
            case "changeUnitResource":
                foreach (var unit in Select(state, effect)) ChangeResource(unit, Text(effect, "resource"), Number(effect["amount"], "amount"), emit);
                break;
            case "addEngagementRule": AddEngagementRule(state, Object(effect["rule"], "rule")); break;
            case "addOverlay": AddOverlay(state, effect, emit); break;
            case "removeOverlay": Empire.Core.Terrain.BattleOverlays.Remove(state, Text(effect, "id"), emit); break;
            case "replaceTerrain": case "setElevation": case "addElevation": case "setCliffs": case "setDirectionalCover":
                ApplyMapEffect(state, effect, emit); break;
            case "changeMorale":
                foreach (var unit in Select(state, effect))
                    if (TacticalQueries.UnitById(state, Integer(unit, "id")) is not null)
                        combat.Damage.ChangeMorale(state, Integer(unit, "id"), Number(effect["amount"], "amount"),
                            effect["reason"] is null ? "scenario" : Text(effect, "reason"), emit);
                break;
            case "setUnitDirective": ChangeDirective(state, effect, emit); break;
            case "moveComposite": CompositeRules.Move(state, Text(effect, "id"), Number(effect["dx"], "dx"), Number(effect["dy"], "dy"), emit); break;
            default: throw new DomainInvariantException($"unknown scenario effect \"{Text(effect, "type") }\"");
        }
    }

    private IReadOnlyList<JsonObject> Select(JsonObject state, JsonObject effect) =>
        selection.SelectUnits(state, Object(effect["selector"], "selector"));

    private IEnumerable<JsonObject> Standing(JsonObject state, JsonObject effect)
    {
        foreach (var unit in Select(state, effect))
            if (TacticalQueries.UnitById(state, Integer(unit, "id")) is not null) yield return unit;
    }

    private void Spawn(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var battlefield = new TacticalBattlefield(state, content);
        foreach (var source in Objects(effect["units"], "units"))
        {
            TacticalQueries.Player(state, Integer(source, "owner"));
            var at = TacticalQueries.Position(source);
            if (!battlefield.Contains(at)) throw new DomainInvariantException($"spawn cell out of bounds: {at.X},{at.Y}");
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
            if (cell.Occupant is not null) continue;
            var unit = spawnUnit(state, source, !IsTrue(effect["ready"]));
            emit(new JsonObject { ["type"] = "unitSpawned", ["unit"] = unit["id"]!.DeepClone(),
                ["at"] = TacticalQueries.CoordinateJson(TacticalQueries.Position(unit)),
                ["reason"] = effect["reason"]?.DeepClone() ?? JsonValue.Create("reinforcement") });
        }
    }

    private void ChangeDirective(JsonObject state, JsonObject effect, Action<JsonObject> emit)
    {
        var requested = Object(effect["directive"], "directive");
        var zone = requested["zone"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(zone) && state["scenario"]?["zones"]?[zone] is null)
            throw new DomainInvariantException($"unknown scenario zone \"{zone}\"");
        foreach (var unit in Select(state, effect))
        {
            var directive = new JsonObject { ["mode"] = requested["mode"]!.DeepClone(),
                ["waypoints"] = requested["waypoints"]?.DeepClone() ?? new JsonArray(),
                ["cursor"] = Math.Max(0, Round(Number(requested, "cursor", 0))) };
            if (requested.ContainsKey("zone")) directive["zone"] = requested["zone"]?.DeepClone();
            unit["directive"] = directive;
            emit(new JsonObject { ["type"] = "directiveChanged", ["unit"] = unit["id"]!.DeepClone(), ["mode"] = directive["mode"]!.DeepClone() });
        }
    }
}
