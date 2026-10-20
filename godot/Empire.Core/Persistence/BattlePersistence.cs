using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Persistence;

public sealed partial class BattlePersistence
{
    public const int Schema = 1;
    private readonly StateContent content;
    private readonly JsonObject manifest;
    private readonly BattleSaveFeatures features;

    public BattlePersistence(JsonArray contentPacks, JsonObject rulesetManifest, BattleSaveFeatures features)
    {
        content = new StateContent((JsonArray)contentPacks.DeepClone());
        manifest = (JsonObject)rulesetManifest.DeepClone();
        this.features = features.Snapshot();
    }

    public JsonObject Header(JsonNode? raw) => RequireHeader(StoredSchema.Read("battle save", Schema, raw));

    public JsonObject Save(JsonObject state, string? savedAt = null)
    {
        RulesetManifest.RequirePersistent(manifest);
        savedAt ??= DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        if (!StoredSchema.ValidTimestamp(savedAt))
            throw new DomainInvariantException("cannot save battle with an invalid timestamp");
        var document = new JsonObject
        {
            ["schema"] = Schema,
            ["battle"] = new JsonObject
            {
                ["levelId"] = state["levelId"]?.DeepClone(), ["levelName"] = state["levelName"]?.DeepClone(),
                ["turn"] = state["turn"]?.DeepClone(), ["phase"] = state["phase"]?.DeepClone(),
            },
            ["ruleset"] = manifest.DeepClone(), ["savedAt"] = savedAt, ["state"] = state.DeepClone(),
        };
        try { return ReadDocument(document); }
        catch (StoredDocumentException error)
        { throw new DomainInvariantException($"cannot save invalid live battle: {error.Message}", error); }
    }

    public JsonObject Load(JsonNode? raw)
    {
        RulesetManifest.RequirePersistent(manifest);
        return (JsonObject)ReadDocument(raw)["state"]!.DeepClone();
    }

    private JsonObject ReadDocument(JsonNode? raw)
    {
        var save = StoredSchema.Read("battle save", Schema, raw);
        RequireHeader(save);
        foreach (var check in new (string Id, Action<JsonObject> Apply)[]
        {
            ("checkShape", CheckShape), ("checkRuleset", CheckRuleset), ("checkMetadata", CheckMetadata),
            ("checkMap", CheckMap), ("checkUnits", CheckUnits), ("checkBattlefield", CheckBattlefield),
            ("checkRuleReferences", CheckRuleReferences), ("checkNativeState", CheckNativeState),
        })
        {
            try { check.Apply(save); }
            catch (StoredDocumentException) { throw; }
            catch (Exception error) when (error is DomainInvariantException or InvalidOperationException or
                ArgumentException or KeyNotFoundException or IndexOutOfRangeException or FormatException or OverflowException)
            { throw new StoredDocumentException($"战斗存档无法读取：校验「{check.Id}」时遇到损坏数据", error); }
        }
        return save;
    }

    private static JsonObject RequireHeader(JsonObject save)
    {
        if (save["battle"] is not JsonObject header ||
            !StoredSchema.Matches(header["levelId"], StoredKind.String) ||
            !StoredSchema.Matches(header["levelName"], StoredKind.String) ||
            !StoredSchema.Matches(header["turn"], StoredKind.Integer) ||
            StateJson.Number(header["turn"], "turn") < 1 ||
            !StoredSchema.Matches(header["phase"], StoredKind.String) ||
            StateJson.Text(header, "phase") is not ("deployment" or "playing" or "over"))
            throw new StoredDocumentException("battle save has no battle header");
        return (JsonObject)header.DeepClone();
    }

    private static JsonObject State(JsonObject save) => StateJson.Object(save["state"], "state");

    private void RequireContent(string family, string id, string owner)
    {
        if (content.TryGet(family, id) is null) StoredSchema.Reject($"{owner} 引用了目录里没有的「{id}」");
    }

    private void CheckRuleset(JsonObject save)
    {
        RulesetManifest.ValidateStored(save["ruleset"]);
        var differences = RulesetManifest.Differences(manifest, save["ruleset"]!.AsObject());
        if (differences.Count > 0) StoredSchema.Reject(string.Join("；", differences));
    }

    private static void CheckMetadata(JsonObject save)
    {
        if (!StoredSchema.ValidTimestamp(StateJson.Text(save, "savedAt"))) StoredSchema.Reject("保存时间缺失或损坏");
        var header = RequireHeader(save);
        var state = State(save);
        foreach (var key in new[] { "levelId", "levelName", "turn", "phase" })
            if (!JsonNode.DeepEquals(header[key], state[key])) StoredSchema.Reject("战斗摘要与战斗状态不一致");
    }

    private void CheckMap(JsonObject save)
    {
        var map = State(save)["map"]!.AsObject();
        if (!StoredSchema.Matches(map["width"], StoredKind.Integer) || !StoredSchema.Matches(map["height"], StoredKind.Integer) ||
            StateJson.Number(map["width"], "width") < 1 || StateJson.Number(map["height"], "height") < 1)
            StoredSchema.Reject("地图尺寸不合法");
        var width = StateJson.Integer(map, "width");
        var height = StateJson.Integer(map, "height");
        if ((long)width * height != map["tiles"]!.AsArray().Count) StoredSchema.Reject("地图格数与尺寸不符");
        foreach (var terrain in map["tiles"]!.AsArray())
            RequireContent("terrains", StateJson.Text(terrain, "terrain"), "地图");
        foreach (var key in new[] { "owners", "captureProgress", "elevation" })
            if (map[key]!.AsArray().Count != map["tiles"]!.AsArray().Count)
                StoredSchema.Reject($"地图的「{key}」格数与尺寸不符");
    }

    private static IEnumerable<(JsonObject Unit, string Owner)> AllUnits(JsonObject state)
    {
        foreach (var unit in StateJson.Objects(state["units"], "units"))
            yield return (unit, $"单位 {unit["key"]?.ToString() ?? unit["id"]?.ToString()}");
        foreach (var entry in StateJson.Objects(state["embarkedUnits"], "embarkedUnits"))
        {
            var unit = StateJson.Object(entry["unit"], "passenger");
            yield return (unit, $"载具乘员 {unit["key"]?.ToString() ?? unit["id"]?.ToString()}");
        }
        foreach (var marker in StateJson.Objects(state["markers"], "markers"))
            if (marker["fallenUnit"] is { } fallen)
            {
                var unit = StateJson.Object(fallen, "fallen unit");
                yield return (unit, $"离场单位 {unit["key"]?.ToString() ?? marker["id"]?.ToString()}");
            }
    }

    private void CheckUnits(JsonObject save)
    {
        foreach (var (unit, owner) in AllUnits(State(save)))
        {
            RequireUnitShape(unit, owner);
            RequireContent("units", StateJson.Text(unit, "type"), owner);
            foreach (var weapon in unit["weaponState"]!.AsObject())
                RequireContent("weapons", weapon.Key, owner);
            foreach (var status in StateJson.Objects(unit["statuses"], "statuses"))
                RequireContent("statuses", StateJson.Text(status, "id"), owner);
            if (unit["career"]!["current"] is { } career && StateJson.Text(career, "career").Length > 0)
                RequireContent("careers", StateJson.Text(career, "career"), owner);
            if (unit["formation"] is { } formation && StateJson.Text(formation, "formation").Length > 0)
                RequireContent("formations", StateJson.Text(formation, "formation"), owner);
        }
    }

    private void CheckBattlefield(JsonObject save)
    {
        var state = State(save);
        foreach (var structure in StateJson.Objects(state["structures"], "structures"))
            RequireContent("structures", StateJson.Text(structure, "type"), $"结构 {StateJson.Text(structure, "id")}");
        foreach (var overlay in StateJson.Objects(state["scenario"]!["overlays"], "overlays"))
            RequireContent("terrainOverlays", StateJson.Text(overlay, "type"), $"覆盖层 {StateJson.Text(overlay, "id")}");
        foreach (var commander in StateJson.Objects(state["commanders"], "commanders"))
            foreach (var tactic in StateJson.Array(commander["tactics"], "tactics"))
                RequireContent("tactics", StateJson.Text(tactic, "tactic"), $"指挥官 {StateJson.Text(commander, "id")}");
    }
}
