using System.Text.Json.Nodes;
using Empire.Core.State;
using Empire.Core.Geometry;
using Empire.Core.Validation;

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

        // A future effect can introduce a capability that is not yet present on
        // any live unit. Inspect its declarations now, without rerunning dynamic
        // placement, occupancy, selector matches or target-liveness rules.
        void PayloadZone(JsonNode? raw, string owner)
        {
            if (raw is null) return;
            var id = StateJson.Text(raw, "zone");
            if (state["scenario"]!["zones"]![id] is null)
                issues.Add($"{owner} 引用了未知区域「{id}」");
        }
        void PayloadCell(JsonNode? raw, string owner)
        {
            StoredSchema.Require(raw, owner, ("x", StoredKind.Integer), ("y", StoredKind.Integer));
            var cell = raw!.AsObject();
            var map = state["map"]!.AsObject();
            var x = StateJson.Integer(cell, "x"); var y = StateJson.Integer(cell, "y");
            if (x < 0 || y < 0 || x >= StateJson.Integer(map, "width") || y >= StateJson.Integer(map, "height"))
                issues.Add($"{owner} 引用了越界坐标 {x},{y}");
        }
        void PayloadDirective(JsonNode? raw, string owner)
        {
            if (raw is null) return;
            var directive = StateJson.Object(raw, "directive");
            Reference("directives", "常驻命令", StateJson.Text(directive, "mode"), owner);
            PayloadZone(directive["zone"], owner);
            if (directive["waypoints"] is { } waypoints)
                foreach (var cell in StateJson.Array(waypoints, "waypoints")) PayloadCell(cell, owner);
        }
        void SpawnPayload(JsonObject source, string owner)
        {
            var type = StateJson.Text(source, "unit");
            RequireContent("units", type, owner);
            var definition = content.Get("units", type);
            var player = StateJson.Integer(source, "owner");
            if (!StateJson.Objects(state["players"], "players").Any(candidate => StateJson.Integer(candidate, "id") == player))
                issues.Add($"{owner} 引用了未知玩家「{player}」");
            PayloadCell(source, owner);
            foreach (var ability in StateJson.Array(definition["abilities"], "abilities")
                .Concat(source["learnedAbilities"] is { } learned ? StateJson.Array(learned, "learnedAbilities") : new JsonArray()))
                Reference("abilities", "能力", StateJson.Text(ability, "ability"), owner);
            Reference("reactions", "反应姿态", StateJson.Text(source["reaction"] ?? definition["defaultReaction"], "reaction"), owner);
            Accounts(definition["resources"], owner);
            if (source["resources"] is { } resources) Accounts(resources, owner);
            foreach (var rawWeapon in StateJson.Array(definition["weapons"], "weapons"))
            {
                var id = StateJson.Text(rawWeapon, "weapon");
                Weapon(id, owner);
                Accounts(content.Get("weapons", id)["resources"], $"{owner}的武器 {id}");
            }
            PayloadDirective(source["directive"], owner);
            if (source["formation"] is { } formation && StateJson.Text(formation, "formation").Length > 0)
                RequireContent("formations", StateJson.Text(formation, "formation"), owner);
            var careerId = source["career"]?.GetValue<string>();
            JsonObject? career = null;
            if (!string.IsNullOrEmpty(careerId))
            {
                RequireContent("careers", careerId, owner);
                career = content.Get("careers", careerId);
            }
            else career = content.All("careers").Where(candidate => StateJson.Text(candidate, "unitType") == type)
                .OrderBy(candidate => StateJson.Number(candidate["tier"], "tier"))
                .ThenBy(candidate => StateJson.Text(candidate, "id"), StringComparer.Ordinal).FirstOrDefault();
            if (career is not null)
                foreach (var ability in StateJson.Array(career["masteryAbilities"], "masteryAbilities"))
                    Reference("abilities", "能力", StateJson.Text(ability, "ability"), owner);
        }
        void Payload(RulePayloadReferences cited, string owner, bool missingStructuresAllowed = false)
        {
            foreach (var zone in cited.Zones) PayloadZone(JsonValue.Create(zone), owner);
            foreach (var player in cited.Players)
                if (!StateJson.Objects(state["players"], "players").Any(candidate => StateJson.Number(candidate["id"], "id") == player))
                    issues.Add($"{owner} 引用了未知玩家「{player}」");
            if (!missingStructuresAllowed)
                foreach (var id in cited.Structures)
                    if (!StateJson.Objects(state["structures"], "structures").Any(value => StateJson.Text(value, "id") == id))
                        issues.Add($"{owner} 引用了未知结构「{id}」");
            foreach (var id in cited.Composites)
                if (!StateJson.Objects(state["composites"], "composites").Any(value => StateJson.Text(value, "id") == id))
                    issues.Add($"{owner} 引用了未知复合目标「{id}」");
            foreach (var objective in cited.Objectives)
            {
                var player = StateJson.Objects(state["players"], "players").FirstOrDefault(value => StateJson.Number(value["id"], "id") == objective.Player);
                if (player?["objectiveStates"]?[objective.Id] is null)
                    issues.Add($"{owner} 引用了未知目标「{objective.Player}:{objective.Id}」");
            }
            foreach (var id in cited.Statuses) RequireContent("statuses", id, owner);
            foreach (var id in cited.Terrains) RequireContent("terrains", id, owner);
            foreach (var id in cited.Overlays) RequireContent("terrainOverlays", id, owner);
            foreach (var id in cited.UnitTypes) RequireContent("units", id, owner);
            foreach (var id in cited.Directives) Reference("directives", "常驻命令", id, owner);
            foreach (var cell in cited.Cells) PayloadCell(cell, owner);
            foreach (var edge in cited.Edges)
            {
                PayloadCell(edge["from"], owner); PayloadCell(edge["to"], owner);
                var from = StateJson.Object(edge["from"], "edge origin"); var to = StateJson.Object(edge["to"], "edge destination");
                if (!GridStorage.SharesEdge(new Coord(StateJson.Integer(from, "x"), StateJson.Integer(from, "y")),
                    new Coord(StateJson.Integer(to, "x"), StateJson.Integer(to, "y")))) issues.Add($"{owner} 引用了无效地图边");
            }
            foreach (var fault in cited.Faults) issues.Add($"{owner}：{fault}");
            foreach (var condition in cited.Conditions) Condition(condition);
        }
        void EffectPayload(JsonObject effect, string owner)
        {
            var kind = StateJson.Text(effect, "type");
            Reference("scenarioEffects", "场景效果", kind, owner);
            if (!features.Has("scenarioEffects", kind)) return;
            Payload(RulePayloadReferences.ForEffect(effect), owner, missingStructuresAllowed: kind == "damageStructure");
            if (kind == "setUnitDirective") PayloadDirective(StateJson.Required(effect, "directive"), owner);
            if (kind == "spawnUnits")
                foreach (var source in StateJson.Objects(StateJson.Required(effect, "units"), "spawn units")) SpawnPayload(source, owner);
            if (kind == "changeUnitResource") Reference("resources", "资源", StateJson.Text(effect, "resource"), owner);
            // Damage to a structure that no longer exists is a deliberate no-op;
            // do not reject a saved trigger merely because its target is gone.
        }

        void Condition(JsonObject condition)
        {
            var type = StateJson.Text(condition, "type");
            Reference("scenarioConditions", "场景条件", type, "触发条件");
            if (!features.Has("scenarioConditions", type)) return;
            Payload(RulePayloadReferences.ForCondition(condition), "触发条件", missingStructuresAllowed: true);
            foreach (var child in RulePayloadReferences.ConditionChildren(condition)) Condition(child);
        }
        void Objective(JsonObject objective)
        {
            var type = StateJson.Text(objective, "type");
            Reference("objectives", "目标类型", type, "作战目标");
            if (!features.Has("objectives", type)) return;
            Payload(RulePayloadReferences.ForObjective(objective), "作战目标", missingStructuresAllowed: true);
            foreach (var child in RulePayloadReferences.ObjectiveChildren(objective)) Objective(child);
        }
        foreach (var player in StateJson.Objects(state["players"], "players"))
            foreach (var objective in StateJson.Objects(StateJson.Required(player, "objectives"), "objectives")) Objective(objective);
        foreach (var trigger in StateJson.Objects(state["scenario"]!["triggers"], "triggers"))
            Condition(StateJson.Object(trigger["condition"], "trigger condition"));
        foreach (var trigger in StateJson.Objects(state["scenario"]!["triggers"], "triggers"))
            foreach (var effect in StateJson.Objects(StateJson.Required(trigger, "effects"), "effects"))
                EffectPayload(effect, $"触发器 {StateJson.Text(trigger, "id")}");
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
