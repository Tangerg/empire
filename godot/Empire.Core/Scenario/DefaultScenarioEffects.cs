using System.Text.Json.Nodes;
using Empire.Core.References;
using Empire.Core.Determinism;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Scenario;

public static class DefaultScenarioEffects
{
    public static ScenarioEffectHandlerRegistry Create() => new ScenarioEffectHandlerRegistry()
        .Register(new("setVariable", (context, effect) => context.Rules.Commands.SetVariable(context.State, effect, context.Emit)))
        .Register(new("addVariable", (context, effect) => context.Rules.Commands.AddVariable(context.State, effect, context.Emit)))
        .Register(new("addStatus", (context, effect) => context.Rules.Commands.AddStatus(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect).Status(Text(effect, "status"))
        })
        .Register(new("removeStatus", (context, effect) => context.Rules.Commands.RemoveStatus(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect).Status(Text(effect, "status"))
        })
        .Register(new("changeUnitOwner", (context, effect) => context.Rules.Commands.ChangeUnitOwner(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect)
        })
        .Register(new("spawnUnits", (context, effect) => context.Rules.Commands.Spawn(context.State, effect, context.Emit))
        {
            References = effect => Objects(effect["units"], "units").Aggregate(new PayloadReferences(), (references, unit) => references.UnitType(Text(unit, "unit")).Player(Number(unit["owner"], "owner")).Cell(unit))
        })
        .Register(new("withdrawUnits", (context, effect) => context.Rules.Commands.WithdrawUnits(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect)
        })
        .Register(new("reviveMarkers", (context, effect) => context.Rules.Commands.ReviveMarkers(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect)
        })
        .Register(new("removeMarkers", (context, effect) => context.Rules.Commands.RemoveMarkers(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect)
        })
        .Register(new("setPlayerTeam", (context, effect) => context.Rules.Commands.SetPlayerTeam(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Player(Number(effect["player"], "player"))
        })
        .Register(new("forceMove", (context, effect) => context.Rules.Commands.ForceMove(context.State, effect, context.Emit))
        {
            References = effect => WholeDistance(Selector(effect).Cell(Object(effect["source"], "source")), effect["distance"])
        })
        .Register(new("teleportUnits", (context, effect) => context.Rules.Commands.TeleportSelected(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Zone(Text(effect, "zone")).Selector(Object(effect["selector"], "selector"))
        })
        .Register(new("addOverlay", (context, effect) => context.Rules.Commands.AddOverlay(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Overlay(Text(effect, "overlay")).Zone(Text(effect, "zone"))
        })
        .Register(new("removeOverlay", (context, effect) => context.Rules.Commands.RemoveOverlay(context.State, effect, context.Emit)))
        .Register(new("activateObjective", (context, effect) => context.Rules.Commands.ActivateObjective(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Objective(Number(effect["player"], "player"), Text(effect, "id"))
        })
        .Register(new("cancelObjective", (context, effect) => context.Rules.Commands.CancelObjective(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Objective(Number(effect["player"], "player"), Text(effect, "id"))
        })
        .Register(new("completeObjective", (context, effect) => context.Rules.Commands.CompleteObjective(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Objective(Number(effect["player"], "player"), Text(effect, "id"))
        })
        .Register(new("revealObjective", (context, effect) => context.Rules.Commands.RevealObjective(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Objective(Number(effect["player"], "player"), Text(effect, "id"))
        })
        .Register(new("changeUnitResource", (context, effect) => context.Rules.Commands.ChangeUnitResource(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect)
        })
        .Register(new("changeMorale", (context, effect) => context.Rules.Commands.ChangeMorale(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect)
        })
        .Register(new("surrenderUnits", (context, effect) => context.Rules.Commands.SurrenderUnits(context.State, effect, context.Emit))
        {
            References = effect => Selector(effect)
        })
        .Register(new("restoreWithdrawnUnits", (context, effect) => context.Rules.Commands.RestoreWithdrawn(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Zone(Text(effect, "zone")).Selector(Object(effect["selector"], "selector"))
        })
        .Register(new("setUnitDirective", (context, effect) => context.Rules.Commands.ChangeDirective(context.State, effect, context.Emit))
        {
            References = effect => DirectiveReferences(effect)
        })
        .Register(new("addEngagementRule", (context, effect) => context.Rules.Commands.AddEngagementRule(context.State, effect, context.Emit))
        {
            References = effect => EngagementReferences(Object(effect["rule"], "rule"))
        })
        .Register(new("removeEngagementRule", (context, effect) => context.Rules.Commands.RemoveEngagementRule(context.State, effect, context.Emit)))
        .Register(new("replaceTerrain", (context, effect) => context.Rules.Commands.ReplaceTerrain(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Zone(Text(effect, "zone")).Terrain(Text(effect, "terrain"))
        })
        .Register(new("setElevation", (context, effect) => context.Rules.Commands.SetElevation(context.State, effect, context.Emit))
        {
            References = effect => WholeElevation(new PayloadReferences().Zone(Text(effect, "zone")), effect["value"])
        })
        .Register(new("addElevation", (context, effect) => context.Rules.Commands.AddElevation(context.State, effect, context.Emit))
        {
            References = effect => WholeElevation(new PayloadReferences().Zone(Text(effect, "zone")), effect["amount"])
        })
        .Register(new("setCliffs", (context, effect) => context.Rules.Commands.SetCliffs(context.State, effect, context.Emit))
        {
            References = effect => Objects(effect["edges"], "edges").Aggregate(new PayloadReferences(), (references, edge) => references.Edge(edge))
        })
        .Register(new("setDirectionalCover", (context, effect) => context.Rules.Commands.SetDirectionalCover(context.State, effect, context.Emit))
        {
            References = effect => Objects(effect["covers"], "covers").Aggregate(new PayloadReferences(), (references, cover) => references.Cell(Object(cover["at"], "at")))
        })
        .Register(new("damageStructure", (context, effect) => context.Rules.Commands.DamageStructure(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Structure(Text(effect, "id"))
        })
        .Register(new("disableStructure", (context, effect) => context.Rules.Commands.DisableStructure(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Structure(Text(effect, "id"))
        })
        .Register(new("repairStructure", (context, effect) => context.Rules.Commands.RepairStructure(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Structure(Text(effect, "id"))
        })
        .Register(new("moveComposite", (context, effect) => context.Rules.Commands.MoveComposite(context.State, effect, context.Emit))
        {
            References = effect => new PayloadReferences().Composite(Text(effect, "id"))
        })
        .Register(new("emitSignal", (context, effect) => context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = Text(effect, "signal") }))
        {
            Signals = effect => new[] { Text(effect, "signal") }
        });
    private static PayloadReferences Selector(JsonObject effect) => new PayloadReferences().Selector(Object(effect["selector"], "selector"));
    private static PayloadReferences WholeDistance(PayloadReferences references, JsonNode? distance) =>
        DefaultScenarioConditions.Whole(distance, 0) ? references : references.Fault("强制位移距离必须是非负整数");
    private static PayloadReferences WholeElevation(PayloadReferences references, JsonNode? height) =>
        DefaultScenarioConditions.Whole(height) ? references : references.Fault("动态海拔必须使用整数");
    private static PayloadReferences DirectiveReferences(JsonObject effect)
    {
        var directive = Object(effect["directive"], "directive");
        var references = Selector(effect).Zone(directive["zone"] is { } zone ? Text(zone, "zone") : null).Directive(Text(directive, "mode"));
        foreach (var waypoint in Objects(directive["waypoints"] ?? new JsonArray(), "waypoints"))
            references.Cell(waypoint);
        return references;
    }
    private static PayloadReferences EngagementReferences(JsonObject rule)
    {
        var references = new PayloadReferences().Zone(Text(rule, "zone"));
        if (EcmaText.IsBlank(Text(rule, "id")))
            references.Fault("交战规则缺少 id");
        foreach (var owner in Array(rule["players"] ?? new JsonArray(), "players"))
            references.Player(Number(owner, "player"));
        return references;
    }
}
