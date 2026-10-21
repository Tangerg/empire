using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Persistence;
using Empire.Core.References;
using Empire.Core.State;

namespace Empire.Core.Validation;

/// <summary>Source-compatible error-level structural, reference and playability checks for a level.</summary>
public sealed class BattleLevelValidator
{
    private readonly StateContent content;
    private readonly RuleReferenceRules rules;
    private readonly BattleReferenceCheckRegistry referenceChecks;
    private readonly GridRegistry grids;

    public BattleLevelValidator(RuleReferenceRules rules, BattleReferenceCheckRegistry referenceChecks, GridRegistry grids)
    {
        content = rules.Content.Source;
        this.rules = rules;
        this.referenceChecks = referenceChecks;
        this.grids = grids;
    }

    public IReadOnlyList<string> Issues(JsonObject level) => new Inspection(content, rules, referenceChecks, grids, level).Run();

    private sealed class Inspection
    {
        private readonly StateContent content;
        private readonly RuleReferenceRules rules;
        private readonly BattleReferenceCheckRegistry referenceChecks;
        private readonly JsonObject level;
        private readonly JsonObject? map;
        private readonly ITacticalGrid? grid;
        private readonly List<string> found = new();
        private readonly HashSet<string> said = new(StringComparer.Ordinal);
        private readonly HashSet<double> players = new();
        private readonly HashSet<string> unitKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> declarations = new(StringComparer.Ordinal);
        private readonly Dictionary<double, HashSet<string>> objectives = new();

        internal Inspection(StateContent content, RuleReferenceRules rules, BattleReferenceCheckRegistry referenceChecks, GridRegistry grids, JsonObject level)
        {
            this.content = content;
            this.rules = rules;
            this.referenceChecks = referenceChecks;
            this.level = level;
            var gridId = level["rules"]?["grid"] is { } namedGrid ? S(namedGrid) : "square4";
            grid = grids.TryGet(gridId);
            try { map = StateMap.Create(content, level); }
            catch (StoredDocumentException error) { Error(error.Message); }
            CollectDeclarations();
        }

        internal IReadOnlyList<string> Run()
        {
            if (!StoredSchema.Matches(level["schema"], StoredKind.Integer) || StateJson.Number(level["schema"], "schema") != Empire.Core.Engine.LevelNormalizer.Schema)
                Error($"Unsupported level schema; expected {Empire.Core.Engine.LevelNormalizer.Schema}.");
            if (level.ContainsKey("name") || level.ContainsKey("description") || level.ContainsKey("levelName"))
                Error("Level wording belongs to presentation metadata, not gameplay definitions.");
            if (RulePayloadReferences.Blank(OptionalText(level["id"]))) Error("关卡缺少 id");
            CheckCliffs();
            CheckDirectionalCover();
            CheckPlayers();
            CheckUnits();
            CheckCommanders();
            CheckStructures();
            CheckComposites();
            CheckZones();
            CheckEngagementRules();
            CheckDeployment();
            CheckOverlays();
            CheckObjectives();
            CheckTriggers();
            CheckTileOwners();
            CheckOpeningPosition();
            CheckRuleReferences();
            return found.ToArray();
        }

        private IEnumerable<JsonObject> Entries(string family) => Objects(level[family]);
        private IEnumerable<JsonObject> ScenarioEntries(string family) => Objects(level["scenario"]?[family]);
        private static IEnumerable<JsonObject> Objects(JsonNode? node) => StateJson.Objects(node, "level entries");
        private static IEnumerable<JsonNode?> Values(JsonNode? node) => node is null
            ? Array.Empty<JsonNode?>() : StateJson.Array(node, "level values");
        private static string S(JsonNode? node) => StateJson.Text(node, "level text");
        private static string S(JsonObject node, string name) => S(node[name]);
        private static double N(JsonNode? node) => StateJson.Number(node, "level number");
        private static string F(double number) => EcmaNumber.Format(number);
        private static string OptionalText(JsonNode? node) => node is null ? "" : S(node);
        private static JsonObject O(JsonNode? node) => StateJson.Object(node, "level object");
        private static string At(JsonObject cell) => $"{F(N(cell["x"]))},{F(N(cell["y"]))}";
        private static string Edge(JsonObject edge) => $"{At(O(edge["from"]))} -> {At(O(edge["to"]))}";
        private static bool Whole(double value) => double.IsFinite(value) && value == Math.Truncate(value);
        private static bool SharesEdge(JsonObject from, JsonObject to) =>
            Math.Abs(N(from["x"]) - N(to["x"])) + Math.Abs(N(from["y"]) - N(to["y"])) == 1;
        private bool InBounds(JsonObject cell, bool integers = true)
        {
            var x = N(cell["x"]); var y = N(cell["y"]);
            return (!integers || Whole(x) && Whole(y)) && x >= 0 && y >= 0 && x < N(level["width"]) && y < N(level["height"]);
        }
        private bool HasContent(string family, string id) => content.TryGet(family, id) is not null;
        private bool Has(string family, string id) => declarations[family].Contains(id);
        private bool AdmitsFacing(string direction) => grid is null || UnitDisposition.AdmitsFacing(grid, direction);
        private void Error(string message)
        {
            if (said.Add(message)) found.Add(message);
        }
        private JsonObject TerrainOn(int tile) => content.Get("terrains", S(map!["tiles"]![tile]));
        private static IEnumerable<JsonObject> ObjectivesOf(JsonObject level, JsonObject player) =>
            player["objectives"] is JsonArray { Count: > 0 } own ? Objects(own) : Objects(level["victory"]);

        private void CollectDeclarations()
        {
            foreach (var player in Entries("players"))
            {
                var id = N(player["id"]);
                if (id < 1) Error($"玩家 id 必须 >= 1（发现 {F(id)}）");
                if (!players.Add(id)) Error($"玩家 id 重复：{F(id)}");
            }
            foreach (var unit in Entries("units"))
            {
                var key = OptionalText(unit["key"]);
                if (key.Length > 0 && !unitKeys.Add(key)) Error($"单位 key 重复：{key}");
            }
            CollectIds("commanders", "指挥官", Entries("commanders"));
            CollectIds("structures", "结构", Entries("structures"));
            CollectIds("composites", "复合目标", Entries("composites"));
            CollectIds("zones", "区域", ScenarioEntries("zones"));
            CollectIds("engagementRules", "交战规则", ScenarioEntries("engagementRules"));
            CollectIds("overlays", "地形覆盖", ScenarioEntries("overlays"));
            CollectIds("triggers", "触发器", ScenarioEntries("triggers"));
            foreach (var player in Entries("players"))
            {
                var owner = N(player["id"]);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                objectives[owner] = ids;
                void Visit(JsonObject objective)
                {
                    var id = OptionalText(objective["id"]);
                    if (id.Length > 0 && !ids.Add(id)) Error($"玩家 {F(owner)} 的目标 id 重复：{id}");
                    foreach (var child in RulePayloadReferences.DeclaredObjectiveChildren(objective)) Visit(child);
                }
                foreach (var objective in ObjectivesOf(level, player)) Visit(objective);
            }
        }

        private void CollectIds(string family, string subject, IEnumerable<JsonObject> entries)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var id = S(entry, "id");
                if (RulePayloadReferences.Blank(id)) Error($"{subject}缺少 id");
                if (!ids.Add(id)) Error($"{subject} id 重复：{id}");
            }
            declarations[family] = ids;
        }

        private void CheckCliffs()
        {
            foreach (var cliff in Entries("cliffs"))
            {
                var from = O(cliff["from"]); var to = O(cliff["to"]);
                if (!InBounds(from) || !InBounds(to)) { Error($"悬崖边越界：{Edge(cliff)}"); continue; }
                if (!SharesEdge(from, to)) Error($"悬崖边必须连接相邻格：{Edge(cliff)}");
            }
        }

        private void CheckDirectionalCover()
        {
            foreach (var cover in Entries("directionalCover"))
            {
                var at = O(cover["at"]);
                if (!InBounds(at)) Error($"方向掩体越界：{At(at)}");
                foreach (var side in O(cover["sides"]).Select(pair => pair.Key))
                    if (!AdmitsFacing(side)) Error($"方向掩体 {At(at)} 的受保护边无效：{side}");
            }
        }

        private void CheckResourceAccounts(JsonNode? accounts, string owner)
        {
            foreach (var pair in accounts is null ? new JsonObject() : O(accounts))
            {
                var id = pair.Key; var account = O(pair.Value);
                if (RulePayloadReferences.Blank(id)) Error($"{owner} 的资源 id 不能为空");
                var current = RulePayloadReferences.Number(account["current"]);
                if (!double.IsFinite(current) || current < 0) Error($"{owner} 的资源 \"{id}\" 当前值必须 >= 0");
                if (account["capacity"] is not null || !account.ContainsKey("capacity"))
                {
                    var capacity = RulePayloadReferences.Number(account["capacity"]);
                    if (!double.IsFinite(capacity) || capacity < 0) Error($"{owner} 的资源 \"{id}\" 上限必须 >= 0 或 null");
                    if (current > capacity) Error($"{owner} 的资源 \"{id}\" 当前值不能超过上限");
                }
            }
        }

        private void CheckPlayers()
        {
            foreach (var player in Entries("players"))
            {
                CheckResourceAccounts(player["resources"], $"玩家 {F(N(player["id"]))}");
                if (player.ContainsKey("name")) Error("Player names belong to presentation metadata, not gameplay definitions.");
            }
            if (Entries("players").Count() < 2) Error("至少需要两名玩家");
        }

        private void CheckUnits()
        {
            var occupied = new HashSet<double>();
            foreach (var unit in Entries("units"))
            {
                var type = S(unit, "unit"); var owner = N(unit["owner"]);
                if (!HasContent("units", type)) Error($"未知兵种 \"{type}\"");
                if (!players.Contains(owner)) Error($"单位 {type}@{At(unit)} 的归属玩家 {F(owner)} 不存在");
                if (unit.ContainsKey("rank") && RulePayloadReferences.Number(unit["rank"]) is not (0 or 1 or 2)) Error($"单位 {type} 的军衔必须是 0、1 或 2");
                if (unit.ContainsKey("rankProgress") && RulePayloadReferences.Number(unit["rankProgress"]) is var progress &&
                    (!double.IsFinite(progress) || progress < 0)) Error($"单位 {type} 的军衔经验必须 >= 0");
                CheckResourceAccounts(unit["resources"], $"单位 {type}");
                if (unit["facing"] is { } facing && !AdmitsFacing(S(facing))) Error($"单位 {type} 的朝向无效：{S(facing)}");
                if (unit.ContainsKey("morale") && RulePayloadReferences.Number(unit["morale"]) is var morale &&
                    (!double.IsFinite(morale) || morale < 0)) Error($"单位 {type} 的士气必须 >= 0");
                if (unit["formation"] is { } form)
                {
                    var formation = S(form);
                    if (!HasContent("formations", formation)) Error($"单位 {type} 使用未知阵形 \"{formation}\"");
                    else if (!Values(content.TryGet("units", type)?["formations"]).Any(candidate => S(candidate) == formation))
                        Error($"单位 {type} 不能使用阵形 \"{formation}\"");
                }
                foreach (var waypoint in Objects(unit["directive"]?["waypoints"]))
                    if (!InBounds(waypoint)) Error($"单位 {type} 的巡逻点越界：{At(waypoint)}");
                CheckUnitCareer(unit, type);
                if (map is null) continue;
                if (!InBounds(unit, false)) { Error($"单位越界：{At(unit)}"); continue; }
                var tile = N(unit["y"]) * N(level["width"]) + N(unit["x"]);
                if (!occupied.Add(tile)) Error($"格子 {At(unit)} 上有多个单位");
                // A fractional array index fails in the source; casting it would silently select another tile.
                if (!Whole(tile)) throw new DomainInvariantException("unknown terrain \"undefined\"");
                var terrain = TerrainOn((int)tile);
                var definition = content.TryGet("units", type);
                if (definition is not null && terrain["cost"]?[S(definition, "movementClass")] is null)
                    Error($"Unit type \"{S(definition, "id")}\" cannot stand on terrain \"{S(terrain, "id")}\" at {At(unit)}.");
            }
        }

        private void CheckUnitCareer(JsonObject unit, string type)
        {
            if (unit["career"] is { } careerNode)
            {
                var id = S(careerNode); var career = content.TryGet("careers", id);
                if (career is null) Error($"单位 {type} 使用未知职业 \"{id}\"");
                else if (S(career, "unitType") != type) Error($"职业 {id} 与兵种 {type} 不匹配");
            }
            foreach (var career in Values(unit["unlockedCareers"]))
                if (!HasContent("careers", S(career))) Error($"单位 {type} 解锁了未知职业 \"{S(career)}\"");
            foreach (var pair in unit["careerMastery"] as JsonObject ?? new JsonObject())
            {
                if (!HasContent("careers", pair.Key)) Error($"单位 {type} 记录了未知职业熟练度 \"{pair.Key}\"");
                var mastery = RulePayloadReferences.Number(pair.Value);
                if (!double.IsFinite(mastery) || mastery < 0) Error($"单位 {type} 的职业熟练度必须 >= 0");
            }
        }

        private void CheckCommanders()
        {
            foreach (var commander in Entries("commanders"))
            {
                var id = S(commander, "id"); var key = S(commander, "unitKey");
                if (!unitKeys.Contains(key)) Error($"指挥官 {id} 引用了未知单位 key {key}");
                var radius = RulePayloadReferences.Number(commander["radius"]);
                if (!double.IsFinite(radius) || radius < 0) Error($"指挥官 {id} 的范围必须 >= 0");
                foreach (var tactic in Values(commander["tactics"]))
                    if (!HasContent("tactics", S(tactic))) Error($"指挥官 {id} 使用未知战术 \"{S(tactic)}\"");
            }
            foreach (var unit in Entries("units"))
            {
                var commander = OptionalText(unit["commander"]);
                if (commander.Length > 0 && !Has("commanders", commander))
                    Error($"单位 {(unit["key"] is { } key ? S(key) : At(unit))} 链接到未知指挥官 {commander}");
            }
        }

        private void CheckStructures()
        {
            foreach (var structure in Entries("structures"))
            {
                var id = S(structure, "id"); var type = S(structure, "type");
                if (!HasContent("structures", type)) Error($"未知结构 \"{type}\"");
                if (map is not null && !InBounds(structure, false)) Error($"结构越界：{id}@{At(structure)}");
                if (structure["owner"] is { } owner && N(owner) != 0 && !players.Contains(N(owner)))
                    Error($"结构 {id} 的归属玩家 {F(N(owner))} 不存在");
            }
        }

        private void CheckComposites()
        {
            foreach (var composite in Entries("composites"))
            {
                var id = S(composite, "id"); var parts = Values(composite["parts"]).ToArray();
                if (parts.Length == 0) Error($"复合目标 {id} 没有部件");
                foreach (var part in parts)
                    if (!Has("structures", S(part))) Error($"复合目标 {id} 引用了未知结构 {S(part)}");
                var threshold = composite["minimumNeutralized"] is { } value ? RulePayloadReferences.Number(value) : parts.Length;
                if (!Whole(threshold) || threshold < 1 || threshold > parts.Length) Error($"复合目标 {id} 的瘫痪阈值无效");
            }
        }

        private void CheckZones()
        {
            foreach (var zone in ScenarioEntries("zones"))
                foreach (var cell in Objects(zone["cells"]))
                    if (map is not null && !InBounds(cell, false)) Error($"区域 {S(zone, "id")} 包含越界格：{At(cell)}");
            foreach (var unit in Entries("units"))
            {
                var zone = OptionalText(unit["directive"]?["zone"]);
                if (zone.Length > 0 && !Has("zones", zone))
                    Error($"单位 {S(unit["key"] ?? unit["unit"])} 的战术指令引用了未知区域 {zone}");
            }
        }

        private void CheckEngagementRules()
        {
            foreach (var rule in ScenarioEntries("engagementRules"))
            {
                var id = S(rule, "id"); var zone = S(rule, "zone");
                if (!Has("zones", zone)) Error($"交战规则 {id} 引用了未知区域 {zone}");
                foreach (var owner in Values(rule["players"]))
                    if (!players.Contains(N(owner))) Error($"交战规则 {id} 引用了未知玩家 {F(N(owner))}");
            }
        }

        private void CheckDeployment()
        {
            if (level["deployment"] is not JsonObject deployment) return;
            var zones = Objects(deployment["zones"]).ToArray();
            var order = deployment["order"] is { } authoredOrder ? Values(authoredOrder).Select(N).ToArray()
                : zones.Select(zone => N(zone["player"])).ToArray();
            if (order.Length == 0) Error("部署阶段至少需要一名玩家");
            foreach (var owner in order) if (!players.Contains(owner)) Error($"部署顺序引用了未知玩家 {F(owner)}");
            var assigned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assignment in zones)
            {
                var owner = N(assignment["player"]); var zone = S(assignment, "zone");
                if (!players.Contains(owner)) Error($"部署区域引用了未知玩家 {F(owner)}");
                if (!Has("zones", zone)) Error($"部署区域引用了未知区域 {zone}");
                foreach (var key in Values(assignment["unitKeys"]).Select(S))
                {
                    var unit = Entries("units").FirstOrDefault(candidate => OptionalText(candidate["key"]) == key && candidate["key"] is not null);
                    if (unit is null) Error($"部署区域引用了未知单位 key {key}");
                    else if (N(unit["owner"]) != owner) Error($"部署单位 {key} 不属于玩家 {F(owner)}");
                    if (!assigned.Add(key)) Error($"部署单位 {key} 被分配到多个区域");
                }
            }
            foreach (var owner in zones.Select(zone => N(zone["player"])).Distinct())
                if (zones.Count(zone => N(zone["player"]) == owner && zone["unitKeys"] is null) > 1)
                    Error($"玩家 {F(owner)} 有多个未指定单位的部署区域");
        }

        private void CheckOverlays()
        {
            foreach (var overlay in ScenarioEntries("overlays"))
            {
                var id = S(overlay, "id"); var type = S(overlay, "type"); var zone = S(overlay, "zone");
                if (!HasContent("terrainOverlays", type)) Error($"未知地形覆盖 \"{type}\"");
                if (!Has("zones", zone)) Error($"地形覆盖 {id} 引用了未知区域 {zone}");
                if (overlay["remainingRounds"] is { } remaining && N(remaining) < 1) Error($"地形覆盖 {id} 的持续回合必须 >= 1");
            }
        }

        private void CheckReferences(string by, PayloadReferences cited)
        {
            void Unknown(string subject, string name) => Error($"{by} 引用了未知{subject} {name}");
            foreach (var id in cited.Zones) if (!Has("zones", id)) Unknown("区域", id);
            foreach (var id in cited.Players) if (!players.Contains(id)) Unknown("玩家", F(id));
            foreach (var id in cited.Structures) if (!Has("structures", id)) Unknown("结构", id);
            foreach (var id in cited.Composites) if (!Has("composites", id)) Unknown("复合目标", id);
            foreach (var aim in cited.Objectives)
                if (!objectives.TryGetValue(aim.Player, out var ids) || !ids.Contains(aim.Id)) Unknown("目标", $"{F(aim.Player)}:{aim.Id}");
            foreach (var id in cited.Statuses) if (!HasContent("statuses", id)) Unknown("状态", id);
            foreach (var id in cited.Terrains) if (!HasContent("terrains", id)) Unknown("地形", id);
            foreach (var id in cited.Overlays) if (!HasContent("terrainOverlays", id)) Unknown("地形覆盖", id);
            foreach (var id in cited.UnitTypes) if (!HasContent("units", id)) Unknown("兵种", id);
            foreach (var at in cited.Cells) if (!InBounds(at)) Error($"{by} 的位置越界：{At(at)}");
            foreach (var edge in cited.Edges)
            {
                var from = O(edge["from"]); var to = O(edge["to"]);
                if (!InBounds(from) || !InBounds(to) || !SharesEdge(from, to)) Error($"{by} 的边无效：{Edge(edge)}");
            }
            foreach (var fault in cited.Faults) Error($"{by}：{fault}");
            foreach (var condition in cited.Conditions) CheckCondition(by, condition);
        }

        private void CheckObjectives()
        {
            foreach (var player in Entries("players"))
                foreach (var objective in ObjectivesOf(level, player)) CheckObjective(objective);
        }

        private void CheckObjective(JsonObject objective)
        {
            var named = S(objective["id"] ?? objective["type"]);
            if (rules.Objectives.Has(S(objective, "type")))
                CheckReferences($"目标 {named}", rules.Objectives.References(objective));
            var children = RulePayloadReferences.DeclaredObjectiveChildren(objective).ToArray();
            if (objective["objectives"] is JsonArray && children.Length == 0) Error($"组合目标 {named} 不能为空");
            foreach (var child in children) CheckObjective(child);
        }

        private void CheckTriggers()
        {
            foreach (var trigger in ScenarioEntries("triggers"))
            {
                var by = $"触发器 {S(trigger, "id")}";
                CheckCondition(by, O(trigger["condition"]));
                foreach (var effect in Objects(trigger["effects"]))
                    if (rules.ScenarioEffects.Has(S(effect, "type")))
                        CheckReferences($"{by} 的效果 {S(effect, "type")}", rules.ScenarioEffects.References(effect));
                foreach (var fault in RulePayloadReferences.ScheduleFaults(trigger)) Error($"{by} 的{fault}");
            }
        }

        private void CheckCondition(string by, JsonObject condition)
        {
            var type = S(condition, "type");
            if (!rules.ScenarioConditions.Has(type)) return;
            CheckReferences($"{by} 的条件 {type}", rules.ScenarioConditions.References(condition));
            foreach (var child in rules.ScenarioConditions.Children(condition)) CheckCondition(by, child);
        }

        private void CheckTileOwners()
        {
            foreach (var owned in Entries("owners"))
            {
                var owner = N(owned["owner"]);
                if (owner != 0 && !players.Contains(owner)) Error($"建筑归属玩家 {F(owner)} 不存在");
            }
        }

        private void CheckOpeningPosition()
        {
            if (map is null) return;
            foreach (var player in Entries("players"))
            {
                var id = N(player["id"]);
                var hasUnits = Entries("units").Any(unit => N(unit["owner"]) == id);
                var hasProduction = Values(map["owners"]).Select((owner, tile) => (owner, tile))
                    .Any(entry => N(entry.owner) == id && Values(TerrainOn(entry.tile)["produces"]).Any());
                if (!hasUnits && !hasProduction) Error($"玩家 {F(id)}既没有单位也没有生产建筑，开局即败");
            }
        }

        private void CheckRuleReferences()
        {
            foreach (var issue in referenceChecks.LevelIssues(rules, level)) Error(issue);
        }
    }
}
