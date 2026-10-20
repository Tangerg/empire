using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

public static class DefaultAiIntents
{
    private sealed record ScoredAction(JsonObject Action, double Score);

    public static AiIntentRegistry Create()
    {
        var registry = new AiIntentRegistry();
        registry.Register(new("tactic", 10, ProposeTactic));
        registry.Register(new("recruit", 20, ProposeRecruit));
        registry.Register(new("reaction", 30, ProposeStanceChange));
        registry.Register(new("command", 40, ProposeCommand));
        registry.Seal();
        return registry;
    }

    private static JsonObject? ProposeCommand(AiTurnContext context)
    {
        ScoredAction? best = null;
        foreach (var unit in context.Actors())
        {
            var candidate = BestCommandFor(context, unit);
            if (candidate is not null && (best is null || candidate.Score > best.Score)) best = candidate;
        }
        return best?.Action;
    }

    private static ScoredAction? BestCommandFor(AiTurnContext context, JsonObject unit)
    {
        var field = context.Engine.Space.ComputeMoveField(context.State, unit);
        ScoredAction? best = null;
        foreach (var stop in field.Stops)
        {
            var at = context.Board.CoordOf(stop);
            var path = context.Engine.Space.PathTo(field, context.State, at);
            if (path is null) continue;
            var tileValue = new TileAppraisal(context, unit, at).Value;
            foreach (var option in context.Engine.CommandsAt(context.State, unit, at))
            {
                if (context.AbilityEvaluators.TryGet(option.Ability) is not { } evaluator) continue;
                IEnumerable<Coord?> targets = option.SelfTargeted ? new Coord?[] { null } : option.Targets.Select(target => (Coord?)target);
                foreach (var target in targets)
                {
                    var score = evaluator.Score(new(context, unit, at, option, target, tileValue));
                    if (score is null || !double.IsFinite(score.Value)) continue;
                    var withdrawing = option.Engagement is not null ? context.Engine.Directives.FightPenalty(unit) : 0;
                    var command = new JsonObject { ["ability"] = option.Ability };
                    if (option.Weapon is not null) command["weapon"] = option.Weapon;
                    if (target is { } targetAt) command["target"] = CoordinateJson(targetAt);
                    var action = new JsonObject { ["kind"] = "command", ["unit"] = Integer(unit, "id"),
                        ["path"] = PathJson(path), ["command"] = command };
                    var candidate = new ScoredAction(action, score.Value + withdrawing);
                    if (best is null || candidate.Score > best.Score) best = candidate;
                }
            }
        }
        return best;
    }

    private static JsonObject? ProposeTactic(AiTurnContext context)
    {
        ScoredAction? best = null;
        foreach (var commander in Objects(context.State["commanders"], "commanders").Where(entry => Integer(entry, "owner") == context.Player))
        foreach (var option in context.Engine.Support.Tactics(context.State, Text(commander, "id")))
        {
            var tactic = context.Content.Get("tactics", option.Id);
            foreach (var target in option.Targets)
            {
                var affected = UnitsOf(context.State, context.Player)
                    .Where(unit => context.Distance(Position(unit), target) <= Number(tactic["radius"], "radius")).ToArray();
                var score = 0.0;
                foreach (var effect in Objects(tactic["effects"], "effects"))
                {
                    var status = Text(effect, "status");
                    score += Text(effect, "type") == "addStatus"
                        ? affected.Count(unit => !AiMeasures.HasStatus(unit, status)) * 30
                        : affected.Count(unit => AiMeasures.HasStatus(unit, status)) * 35;
                }
                score -= context.Engine.Resources.PlanningValue(Array(tactic["costs"], "costs"));
                if (score <= 0 || best is not null && score <= best.Score) continue;
                best = new(new JsonObject { ["kind"] = "tactic", ["commander"] = Text(commander, "id"),
                    ["tactic"] = option.Id, ["target"] = CoordinateJson(target) }, score);
            }
        }
        return best?.Action;
    }

    private static JsonObject? ProposeStanceChange(AiTurnContext context)
    {
        foreach (var unit in UnitsOf(context.State, context.Player).Where(candidate => !IsTrue(candidate["done"])))
        {
            var stance = PreferredStance(context, unit);
            if (stance != Text(unit, "reaction"))
                return new JsonObject { ["kind"] = "reaction", ["unit"] = Integer(unit, "id"), ["stance"] = stance };
        }
        return null;
    }

    private static string PreferredStance(AiTurnContext context, JsonObject unit)
    {
        var definition = context.Content.Get("units", Text(unit, "type"));
        var protectedWeight = context.Agenda.Mission.ProtectedUnits.GetValueOrDefault(Integer(unit, "id"));
        var enemyNear = context.Enemies(Integer(unit, "owner")).Any(enemy =>
            context.Distance(Position(unit), Position(enemy)) <=
            Number(context.Content.Get("units", Text(enemy, "type"))["movement"], "movement") +
            AiMeasures.MaximumWeaponRange(context.Content, Text(enemy, "type")));
        if (AiMeasures.HpRatio(context.Content, unit) < 0.35 || protectedWeight >= 4 && enemyNear) return "guard";
        var adjacentAlly = Objects(context.State["units"], "units").Any(candidate =>
            Integer(candidate, "id") != Integer(unit, "id") &&
            AreAllies(context.State, Integer(candidate, "owner"), Integer(unit, "owner")) &&
            context.Distance(Position(candidate), Position(unit)) == 1);
        if (adjacentAlly && (AiMeasures.Includes(definition["tags"], "support") || AiMeasures.Includes(definition["tags"], "monster"))) return "support";
        var scarce = AiMeasures.Includes(definition["tags"], "ranged") && context.Engine.Combat.Forecasts.UnitWeapons(unit).Any(weapon =>
            AiMeasures.Includes(weapon["tags"], "ranged") &&
            (Number(weapon["cooldown"], "cooldown") > 0 || Array(weapon["resourceCosts"], "resourceCosts").Count > 0));
        return scarce ? "conserve" : Text(definition, "defaultReaction");
    }

    private static double ScoreRecruit(AiTurnContext context, string type)
    {
        var content = context.Content;
        var definition = content.Get("units", type);
        var foes = context.Enemies(context.Player);
        var mine = UnitsOf(context.State, context.Player);
        var matchup = 1.0;
        if (foes.Count > 0)
        {
            var total = 0.0;
            foreach (var foe in foes)
            {
                var armor = Text(content.Get("units", Text(foe, "type")), "armorClass");
                var maximum = double.NegativeInfinity;
                foreach (var weapon in AiMeasures.WeaponsForType(content, type))
                    maximum = Math.Max(maximum, context.Engine.Combat.Forecasts.Effectiveness(Text(weapon, "damageType"), armor));
                total += maximum;
            }
            matchup = total / foes.Count;
        }
        var score = (AiMeasures.MaximumWeaponPower(content, type) * matchup + Number(definition["maxHp"], "maxHp") * 0.35 +
            Number(definition["movement"], "movement") * 12) /
            Math.Max(0.1, context.Engine.Resources.PlanningValue(Array(definition["recruitCosts"], "recruitCosts")) / 100);
        var infantry = mine.Count(unit => AiMeasures.Includes(content.Get("units", Text(unit, "type"))["abilities"], "capture"));
        if (AiMeasures.Includes(definition["abilities"], "capture") && infantry < 2) score *= 1.6;
        var composition = mine.Count(unit => Text(unit, "type") == type);
        score *= 1 - Math.Min(0.4, composition * 0.08);
        return score;
    }

    private static JsonObject? ProposeRecruit(AiTurnContext context)
    {
        var account = new BattleResources.Subject("player", Player(context.State, context.Player));
        var cap = context.State["rules"]!["maxUnitsPerPlayer"];
        if (cap is not null && UnitsOf(context.State, context.Player).Count >= Integer(cap, "maxUnitsPerPlayer")) return null;
        var map = context.Board.Map;
        var owners = Array(map["owners"], "owners");
        for (var tile = 0; tile < owners.Count; tile++)
        {
            if (Integer(owners[tile], "owner") != context.Player) continue;
            var at = context.Board.CoordOf(tile);
            var terrain = context.Battlefield.Cell(at).Terrain;
            var produces = Array(terrain["produces"], "produces");
            if (produces.Count == 0 || UnitAt(context.State, at) is not null) continue;
            var affordable = produces.Select(id => Text(id, "unit type"))
                .Where(id => context.Engine.Resources.CanAfford(account, Array(context.Content.Get("units", id)["recruitCosts"], "recruitCosts")))
                .Select(id => (Id: id, Score: ScoreRecruit(context, id))).ToArray();
            if (affordable.Length == 0) continue;
            // JS sort treats a NaN comparison as equality. Preserve that and stable content order.
            var ranked = affordable.Select((entry, index) => (entry.Id, entry.Score, Index: index)).ToList();
            ranked.Sort((left, right) =>
            {
                var difference = right.Score - left.Score;
                return double.IsNaN(difference) || difference == 0 ? left.Index.CompareTo(right.Index) : difference < 0 ? -1 : 1;
            });
            return new JsonObject { ["kind"] = "recruit", ["at"] = CoordinateJson(at), ["unit"] = ranked[0].Id };
        }
        return null;
    }
}
