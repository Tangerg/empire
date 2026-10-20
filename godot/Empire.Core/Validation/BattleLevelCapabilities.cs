using System.Text.Json.Nodes;
using Empire.Core.Persistence;
using Empire.Core.State;

namespace Empire.Core.Validation;

/// <summary>
/// Native fail-closed execution admission beyond the source level linter.
/// A delayed declaration must be executable even before it materializes in state.
/// This does not run selectors, placement, combat, or action legality.
/// </summary>
internal sealed class BattleLevelCapabilities
{
    private readonly BattleSaveFeatures features;
    internal BattleLevelCapabilities(BattleSaveFeatures features) => this.features = features.Snapshot();

    internal IReadOnlyList<string> Issues(JsonObject level)
    {
        var issues = new List<string>();
        void Reference(string family, string subject, string id, string owner)
        {
            if (!features.Has(family, id)) issues.Add($"{owner} 引用了未注册的{subject}「{id}」");
        }
        void Accounts(JsonNode? accounts, string owner)
        {
            foreach (var pair in (accounts is null ? new JsonObject() : StateJson.Object(accounts, "resource accounts")))
                Reference("resources", "资源", pair.Key, owner);
        }
        void Grants(JsonNode? grants, string owner)
        {
            foreach (var grant in StateJson.Objects(grants, "resource grants"))
                Reference("resources", "资源", StateJson.Text(grant, "resource"), owner);
        }
        void Source(JsonObject source, string owner)
        {
            foreach (var ability in (source["learnedAbilities"] is { } learned ? StateJson.Array(learned, "learned abilities") : new JsonArray()))
                Reference("abilities", "能力", StateJson.Text(ability, "ability"), owner);
            if (source["reaction"] is { } reaction) Reference("reactions", "反应姿态", StateJson.Text(reaction, "reaction"), owner);
            if (source["directive"] is JsonObject directive)
                Reference("directives", "常驻命令", StateJson.Text(directive, "mode"), owner);
            Accounts(source["resources"], owner);
        }
        void Condition(JsonObject condition, string owner)
        {
            var kind = StateJson.Text(condition, "type");
            Reference("scenarioConditions", "场景条件", kind, owner);
            if (features.Has("scenarioConditions", kind))
                foreach (var child in RulePayloadReferences.ConditionChildren(condition)) Condition(child, owner);
        }
        void Objective(JsonObject objective, string owner)
        {
            foreach (var condition in RulePayloadReferences.ForObjective(objective).Conditions) Condition(condition, owner);
            foreach (var child in RulePayloadReferences.ObjectiveChildren(objective)) Objective(child, owner);
        }
        foreach (var source in StateJson.Objects(level["units"], "units"))
            Source(source, $"单位 {StateJson.Text(source["key"] ?? source["unit"], "unit")}");
        foreach (var player in StateJson.Objects(level["players"], "players"))
        {
            var owner = $"玩家 {player["id"]}";
            Accounts(player["resources"], owner);
            var objectives = player["objectives"] is JsonArray { Count: > 0 } own ? own : level["victory"];
            foreach (var objective in StateJson.Objects(objectives, "objectives")) Objective(objective, owner + "的作战目标");
        }
        Grants(level["rules"]?["baseResourceGrants"], "关卡规则");
        foreach (var pair in (level["rules"]?["siteResourceOverrides"] is { } overrides ? StateJson.Object(overrides, "site resource overrides") : new JsonObject()))
            Reference("resources", "资源", pair.Key, "据点资源");
        foreach (var commander in StateJson.Objects(level["commanders"], "commanders")) Grants(commander["turnGrants"], $"指挥官 {commander["id"]}");
        foreach (var trigger in StateJson.Objects(level["scenario"]?["triggers"], "triggers"))
        {
            var owner = $"触发器 {StateJson.Text(trigger, "id")}";
            Condition(StateJson.Object(trigger["condition"], "condition"), owner);
            foreach (var effect in StateJson.Objects(trigger["effects"], "effects"))
            {
                var kind = StateJson.Text(effect, "type");
                if (kind == "spawnUnits")
                    foreach (var source in StateJson.Objects(effect["units"], "spawn units")) Source(source, owner + "的增援单位");
                if (kind == "changeUnitResource") Reference("resources", "资源", StateJson.Text(effect, "resource"), owner);
            }
        }
        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }
}
