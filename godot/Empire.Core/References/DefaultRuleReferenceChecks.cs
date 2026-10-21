using System.Text.Json.Nodes;
using Empire.Core.State;
using static Empire.Core.State.StateJson;

namespace Empire.Core.References;

internal static class DefaultRuleReferenceChecks
{
    private static IEnumerable<JsonNode?> Values(JsonNode? raw) => raw is null ? Enumerable.Empty<JsonNode?>() : Array(raw, "references");
    private static string Id(JsonObject value) => Text(value, "type");
    private static IEnumerable<JsonObject> Triggers(JsonObject document) => Objects(document["scenario"]?["triggers"], "triggers");
    private static IEnumerable<(JsonObject Unit, string By)> LevelUnits(JsonObject level) => Objects(level["units"], "units")
        .Select(unit => (unit, $"单位 {Text(unit["key"] ?? unit["unit"], "unit")}"));
    private static IEnumerable<(JsonObject Unit, string By)> StateUnits(JsonObject state)
    {
        foreach (var unit in Objects(state["units"], "units"))
            yield return (unit, $"单位 {unit["key"] ?? unit["id"]}");
        foreach (var entry in Objects(state["embarkedUnits"], "embarkedUnits"))
        {
            var unit = Object(entry["unit"], "passenger");
            yield return (unit, $"载具乘员 {unit["key"] ?? unit["id"]}");
        }
        foreach (var marker in Objects(state["markers"], "markers"))
            if (marker["fallenUnit"] is JsonObject unit)
                yield return (unit, $"离场单位 {unit["key"] ?? marker["id"]}");
    }
    private static IEnumerable<JsonObject> Tree(IRuleTreeRegistry registry, IEnumerable<JsonObject> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            if (!registry.Has(Id(root)))
                continue;
            foreach (var child in Tree(registry, registry.Children(root)))
                yield return child;
        }
    }
    private static IEnumerable<JsonObject> Objectives(JsonObject level) => Objects(level["players"], "players")
        .SelectMany(player => Objects(player["objectives"] is JsonArray { Count: > 0 } own ? own : level["victory"], "objectives"));
    private static IEnumerable<RuleReference> Named(IEnumerable<JsonNode?> values, string by) => values.Select(value => new RuleReference(by, Text(value, "reference")));
    private static IEnumerable<RuleReference> LevelDirectives(JsonObject level, RuleReferenceRules rules)
    {
        foreach (var (unit, by) in LevelUnits(level))
            if (unit["directive"] is JsonObject directive)
                yield return new(by, Text(directive, "mode"));
        foreach (var trigger in Triggers(level))
            foreach (var effect in Objects(trigger["effects"], "effects"))
                foreach (var name in rules.ScenarioEffects.References(effect).Directives)
                    yield return new($"触发器 {Text(trigger, "id")}", name);
    }
    private static IEnumerable<RuleReference> RuleSetting(JsonObject level, string name) => level["rules"]?[name] is { } value && Text(value, name).Length > 0
        ? new[] { new RuleReference("关卡规则", Text(value, name)) } : Enumerable.Empty<RuleReference>();
    internal static BattleReferenceCheckRegistry Create() => new BattleReferenceCheckRegistry()
        .Register(new("abilities", "能力", rules => rules.Abilities.Keys())
        {
            InContent = content => content.Source.All("units").SelectMany(unit => Named(Values(unit["abilities"]), $"兵种 {Text(unit, "id")}"))
                .Concat(content.Source.All("careers").SelectMany(career => Named(Values(career["masteryAbilities"]), $"职业 {Text(career, "id")}"))),
            InLevel = (level, _) => LevelUnits(level).SelectMany(entry => Named(Values(entry.Unit["learnedAbilities"]), entry.By)),
            InState = (state, _) => StateUnits(state).SelectMany(entry => Named(Values(entry.Unit["learnedAbilities"]), entry.By)),
        })
        .Register(new("hitEffects", "命中效果", rules => rules.HitEffects.Keys())
        {
            InContent = content => content.Source.All("weapons").SelectMany(weapon => Objects(weapon["hitEffects"], "hit effects")
                .Select(effect => new RuleReference($"武器 {Text(weapon, "id")}", Id(effect)))),
        })
        .Register(new("areaShapes", "爆炸形状", rules => rules.AreaShapes.Keys())
        {
            InContent = content => content.Source.All("weapons").Select(weapon => new RuleReference($"武器 {Text(weapon, "id")}", Text(weapon, "area"))),
        })
        .Register(new("reactions", "反应姿态", rules => rules.Reactions.Keys())
        {
            InContent = content => content.Source.All("units").Select(unit => new RuleReference($"兵种 {Text(unit, "id")}", Text(unit, "defaultReaction"))),
            InLevel = (level, _) => LevelUnits(level).Where(entry => entry.Unit["reaction"] is { } reaction && Text(reaction, "reaction").Length > 0)
                .Select(entry => new RuleReference(entry.By, Text(entry.Unit, "reaction"))),
            InState = (state, _) => StateUnits(state).Select(entry => new RuleReference(entry.By, Text(entry.Unit, "reaction"))),
        })
        .Register(new("directives", "常驻命令", rules => rules.Directives.Keys())
        {
            InLevel = LevelDirectives,
            InState = (state, _) => StateUnits(state).Select(entry => new RuleReference(entry.By, Text(Object(entry.Unit["directive"], "directive"), "mode"))),
        })
        .Register(new("turnOrders", "行动顺序策略", rules => rules.TurnOrders.Keys())
        {
            InLevel = (level, _) => RuleSetting(level, "turnOrder"),
            InState = (state, _) => new[] { new RuleReference("行动顺序", Text(Object(state["turnOrder"], "turnOrder"), "policy")) },
        })
        .Register(new("grids", "棋盘几何", rules => rules.Grids.Keys())
        {
            InLevel = (level, _) => RuleSetting(level, "grid"),
            InState = (state, _) => new[] { new RuleReference("棋盘", Text(Object(state["rules"], "rules"), "grid")) },
        })
        .Register(new("objectives", "目标类型", rules => rules.Objectives.Keys())
        {
            InLevel = (level, rules) => Tree(rules.Objectives, Objectives(level)).Select(objective => new RuleReference("作战目标", Id(objective))),
            InState = (state, rules) => Tree(rules.Objectives, Objects(state["players"], "players").SelectMany(player => Objects(player["objectives"], "objectives")))
                .Select(objective => new RuleReference("作战目标", Id(objective))),
        })
        .Register(new("scenarioConditions", "场景条件", rules => rules.ScenarioConditions.Keys())
        {
            InLevel = (level, rules) => Tree(rules.ScenarioConditions, Triggers(level).Select(trigger => Object(trigger["condition"], "condition")))
                .Select(condition => new RuleReference("触发条件", Id(condition))),
            InState = (state, rules) => Tree(rules.ScenarioConditions, Triggers(state).Select(trigger => Object(trigger["condition"], "condition")))
                .Select(condition => new RuleReference("触发条件", Id(condition))),
        })
        .Register(new("scenarioEffects", "场景效果", rules => rules.ScenarioEffects.Keys())
        {
            InLevel = (level, _) => EffectReferences(level),
            InState = (state, _) => EffectReferences(state),
        });
    private static IEnumerable<RuleReference> EffectReferences(JsonObject document) => Triggers(document).SelectMany(trigger =>
        Objects(trigger["effects"], "effects").Select(effect => new RuleReference($"触发器 {Text(trigger, "id")}", Id(effect))));
}
