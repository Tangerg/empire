using System.Text.Json.Nodes;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

public static class DefaultAbilityAiEvaluators
{
    public static AbilityAiEvaluatorRegistry Create()
    {
        var registry = new AbilityAiEvaluatorRegistry();
        registry.Register(new("wait", context => context.TileValue));
        registry.Register(new("capture", context =>
        {
            var terrain = context.Turn.Battlefield.Cell(context.At).Terrain;
            var prize = IsTrue(terrain["hq"]) ? 4000 : Array(terrain["produces"], "produces").Count > 0 ? 900 : 600;
            return context.TileValue * 0.3 + prize;
        }));
        registry.Register(new("heal", context =>
        {
            if (context.Target is not { } target || UnitAt(context.Turn.State, target) is not { } ally) return null;
            var definition = context.Turn.Content.Get("units", Text(ally, "type"));
            var healed = DefaultAbilities.HealAmount(context.Turn.Rules.Content, ally);
            return context.TileValue * 0.3 + healed / Number(definition["maxHp"], "maxHp") * Number(definition["value"], "value") * 0.9;
        }));
        registry.Register(new("attack", Attack));
        registry.Seal();
        return registry;
    }

    private static double? Attack(AbilityAiEvaluationContext context)
    {
        if (context.Target is not { } target || context.Option.Weapon is not { } weapon) return null;
        var turn = context.Turn;
        var content = turn.Content;
        if (Number(content.Get("weapons", weapon)["castTurns"], "castTurns") > 0) return null;
        var foe = UnitAt(turn.State, target);
        if (foe is not null && !AreEnemies(turn.State, Integer(foe, "owner"), Integer(context.Unit, "owner"))) return null;
        var plan = turn.Rules.Combat.Forecast(turn.State, context.Unit, target, context.At, weapon);
        var mission = turn.Agenda.Mission;
        if (foe is null)
        {
            var structure = StructureAt(turn.State, target);
            if (structure is null || plan.PrimaryStructure is not { } structureForecast) return null;
            return context.TileValue * 0.25 + structureForecast.Damage * 8 + (structureForecast.Destroyed ? 900 : 0) +
                mission.PriorityStructures.GetValueOrDefault(Text(structure, "id")) * 300 + CollateralValue(content, turn.State, plan);
        }
        if (plan.PrimaryUnit is not { } forecast) return null;
        var foeDefinition = content.Get("units", Text(foe, "type"));
        var unitDefinition = content.Get("units", Text(context.Unit, "type"));
        var dealt = Math.Min(forecast.Strike.Damage, Number(foe["hp"], "hp")) / Number(foeDefinition["maxHp"], "maxHp");
        var score = dealt * Number(foeDefinition["value"], "value") * 1.15;
        if (forecast.DefenderDies) score += AiMeasures.UnitWorth(content, foe) * 0.65;
        if (forecast.Counter is { } counter)
        {
            var taken = Math.Min(counter.Damage, Number(context.Unit["hp"], "hp")) / Number(unitDefinition["maxHp"], "maxHp");
            score -= taken * Number(unitDefinition["value"], "value");
            if (forecast.AttackerDies) score -= AiMeasures.UnitWorth(content, context.Unit) * 0.9;
        }
        if (AiMeasures.Includes(foeDefinition["tags"], "support") ||
            turn.Rules.Combat.Forecasts.UnitWeapons(foe).All(candidate => Number(candidate["minRange"], "minRange") > 1)) score *= 1.1;
        score += mission.PriorityUnits.GetValueOrDefault(Integer(foe, "id")) * 260;
        foreach (var (id, weight) in mission.ProtectedUnits)
            if (UnitById(turn.State, id) is { } protectedUnit)
                score += weight * 80 / (1 + turn.Distance(Position(foe), Position(protectedUnit)));
        return context.TileValue * 0.25 + score + CollateralValue(content, turn.State, plan);
    }

    private static double CollateralValue(StateContent content, JsonObject state, CombatPlan plan)
    {
        var score = 0.0;
        foreach (var hit in plan.UnitHits.Where(candidate => !candidate.Primary))
        {
            if (UnitById(state, hit.Target) is not { } target) continue;
            var definition = content.Get("units", Text(target, "type"));
            var dealt = Math.Min(hit.Damage.Damage, Number(target["hp"], "hp")) / Number(definition["maxHp"], "maxHp");
            score += dealt * Number(definition["value"], "value");
            if (hit.Killed) score += AiMeasures.UnitWorth(content, target) * 0.5;
        }
        foreach (var hit in plan.StructureHits.Where(candidate => !candidate.Primary))
            score += hit.Forecast.Damage * 7 + (hit.Forecast.Destroyed ? 700 : 0);
        if (plan.SupportAttack is { } support && UnitById(state, support.Target) is { } supportTarget)
        {
            var definition = content.Get("units", Text(supportTarget, "type"));
            score += Math.Min(support.Damage.Damage, support.HpBefore) / Number(definition["maxHp"], "maxHp") * Number(definition["value"], "value");
            if (support.Killed) score += AiMeasures.UnitWorth(content, supportTarget) * 0.5;
        }
        return score;
    }
}
