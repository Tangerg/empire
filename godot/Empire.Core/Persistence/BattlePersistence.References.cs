using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Persistence;

public sealed partial class BattlePersistence
{
    private void CheckRuleReferences(JsonObject save)
    {
        var state = State(save);
        var issues = new List<string>();
        void Reference(string family, string subject, string id, string owner)
        {
            if (!features.Has(family, id)) issues.Add($"{owner} 引用了未注册的{subject}「{id}」");
        }
        void Accounts(JsonNode? raw, string owner)
        {
            foreach (var account in StateJson.Object(raw, "resources"))
            {
                Reference("resources", "资源", account.Key, owner);
                StoredSchema.Require(account.Value, $"{owner}的资源",
                    ("current", StoredKind.NullableNumber), ("capacity", StoredKind.NullableNumber));
                var value = account.Value!.AsObject();
                foreach (var field in new[] { "current", "capacity" })
                    if (value[field] is { } number && StateJson.Number(number, field) < 0)
                        StoredSchema.Reject($"{owner}的资源数值不合法");
            }
        }
        void Grants(JsonNode? raw, string owner)
        {
            foreach (var grant in StateJson.Objects(raw, "resource transactions"))
            {
                Reference("resources", "资源", StateJson.Text(grant, "resource"), owner);
                _ = StateJson.Number(grant["amount"], "resource amount");
            }
        }
        void Weapon(string id, string owner)
        {
            RequireContent("weapons", id, owner);
            var weapon = content.Get("weapons", id);
            Reference("areaShapes", "爆炸形状", StateJson.Text(weapon, "area"), $"武器 {id}");
            Grants(weapon["resourceRequirements"], $"武器 {id}");
            Grants(weapon["resourceCosts"], $"武器 {id}");
            foreach (var effect in StateJson.Objects(weapon["hitEffects"], "hitEffects"))
            {
                Reference("hitEffects", "命中效果", StateJson.Text(effect, "type"), $"武器 {id}");
                if (effect["status"] is { } status) RequireContent("statuses", StateJson.Text(status, "status"), $"武器 {id}");
            }
        }
        var units = AllUnits(state).ToArray();
        foreach (var (unit, owner) in units)
        {
            var definition = content.Get("units", StateJson.Text(unit, "type"));
            Accounts(unit["resources"], owner);
            foreach (var weapon in unit["weaponState"]!.AsObject())
                Accounts(weapon.Value!["resources"], $"{owner}的武器 {weapon.Key}");
            foreach (var ability in StateJson.Array(definition["abilities"], "abilities")
                .Concat(StateJson.Array(unit["learnedAbilities"], "learnedAbilities")))
                Reference("abilities", "能力", StateJson.Text(ability, "ability"), owner);
            foreach (var id in StateJson.Array(definition["weapons"], "weapons").Select(id => StateJson.Text(id, "weapon"))
                .Concat(unit["weaponState"]!.AsObject().Select(pair => pair.Key)).Distinct(StringComparer.Ordinal))
                Weapon(id, owner);
        }
        foreach (var player in StateJson.Objects(state["players"], "players"))
            Accounts(StateJson.Required(player, "resources"), $"玩家 {player["id"]}");
        Grants(state["rules"]!["baseResourceGrants"], "关卡规则");
        foreach (var resource in StateJson.Object(state["rules"]!["siteResourceOverrides"], "siteResourceOverrides"))
            Reference("resources", "资源", resource.Key, "据点资源");
        foreach (var terrain in state["map"]!["tiles"]!.AsArray().Select(id => StateJson.Text(id, "terrain")).Distinct(StringComparer.Ordinal))
            Grants(content.Get("terrains", terrain)["ownerTurnGrants"], $"地形 {terrain}");
        foreach (var commander in StateJson.Objects(state["commanders"], "commanders"))
        {
            var owner = $"指挥官 {StateJson.Text(commander, "id")}";
            Grants(commander["turnGrants"], owner);
            foreach (var tacticId in StateJson.Array(commander["tactics"], "tactics").Select(id => StateJson.Text(id, "tactic")))
            {
                var tactic = content.Get("tactics", tacticId);
                Grants(tactic["costs"], $"战术 {tacticId}");
                foreach (var effect in StateJson.Objects(tactic["effects"], "tactic effects"))
                {
                    Reference("tacticEffects", "战术效果", StateJson.Text(effect, "type"), $"战术 {tacticId}");
                    if (effect["status"] is { } status) RequireContent("statuses", StateJson.Text(status, "status"), $"战术 {tacticId}");
                }
            }
        }
        foreach (var (unit, owner) in units) Reference("reactions", "反应姿态", StateJson.Text(unit, "reaction"), owner);
        foreach (var (unit, owner) in units)
            Reference("directives", "常驻命令", StateJson.Text(unit["directive"]!.AsObject(), "mode"), owner);
        Reference("turnOrders", "行动顺序策略", StateJson.Text(state["turnOrder"]!.AsObject(), "policy"), "行动顺序");
        Reference("grids", "棋盘几何", StateJson.Text(state["rules"]!.AsObject(), "grid"), "棋盘");

        void Condition(JsonObject condition)
        {
            var type = StateJson.Text(condition, "type");
            Reference("scenarioConditions", "场景条件", type, "触发条件");
            if (!features.Has("scenarioConditions", type)) return;
            if (type is "all" or "any")
                foreach (var child in StateJson.Objects(condition["conditions"], "conditions")) Condition(child);
            else if (type == "not") Condition(StateJson.Object(condition["condition"], "condition"));
        }
        void Objective(JsonObject objective)
        {
            var type = StateJson.Text(objective, "type");
            Reference("objectives", "目标类型", type, "作战目标");
            if (!features.Has("objectives", type)) return;
            if (type is "all" or "any" or "sequence")
                foreach (var child in StateJson.Objects(objective["objectives"], "objectives")) Objective(child);
            else if (type is "failOn" or "optional")
            {
                if (type == "failOn") Condition(StateJson.Object(objective["condition"], "condition"));
                Objective(StateJson.Object(objective["objective"], "objective"));
            }
        }
        foreach (var player in StateJson.Objects(state["players"], "players"))
            foreach (var objective in StateJson.Objects(StateJson.Required(player, "objectives"), "objectives")) Objective(objective);
        foreach (var trigger in StateJson.Objects(state["scenario"]!["triggers"], "triggers"))
            Condition(StateJson.Object(trigger["condition"], "trigger condition"));
        foreach (var trigger in StateJson.Objects(state["scenario"]!["triggers"], "triggers"))
            foreach (var effect in StateJson.Objects(StateJson.Required(trigger, "effects"), "effects"))
                Reference("scenarioEffects", "场景效果", StateJson.Text(effect, "type"), $"触发器 {StateJson.Text(trigger, "id")}");
        foreach (var cast in StateJson.Objects(state["pendingCasts"], "pendingCasts"))
        {
            Reference("castAbilities", "咏唱能力", StateJson.Text(cast, "ability"), "咏唱");
            Weapon(StateJson.Text(cast, "weapon"), "咏唱");
        }
        if (state["deployment"] is not null || StateJson.Text(state, "phase") == "deployment")
            Reference("stateFeatures", "战斗功能", "deployment", "战斗");
        foreach (var feature in new[] { "embarkedUnits", "composites" })
            if (state[feature]!.AsArray().Count > 0) Reference("stateFeatures", "战斗功能", feature, "战斗");
        if (issues.Count > 0) StoredSchema.Reject(string.Join("；", issues.Distinct(StringComparer.Ordinal)));
    }
}
