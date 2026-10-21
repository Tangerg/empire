using System.Text.Json.Nodes;
using Empire.Core.References;
using Empire.Core.State;

namespace Empire.Core.Persistence;

public sealed record BattleSaveEnvironment(RuleReferenceRules Rules,
    BattleReferenceCheckRegistry ReferenceChecks, JsonObject RulesetManifest, BattleSaveFeatures StructuralGuards);

public interface IBattleSaveReader
{
    JsonObject Header(JsonNode? raw);
    JsonObject Load(JsonNode? raw, BattleSaveEnvironment environment);
}

/// <summary>The replaceable admission capability; no per-load environment is retained.</summary>
public sealed class BattleSaveReader : IBattleSaveReader
{
    public JsonObject Header(JsonNode? raw) => BattleSaveInspection.RequireHeader(StoredSchema.Read("battle save", BattlePersistence.Schema, raw));
    public JsonObject Load(JsonNode? raw, BattleSaveEnvironment environment) => new BattleSaveInspection(environment).ReadDocument(raw);
}

internal sealed partial class BattleSaveInspection
{
    private const int Schema = BattlePersistence.Schema;
    private readonly StateContent content;
    private readonly JsonObject manifest;
    private readonly BattleSaveEnvironment environment;
    internal BattleSaveInspection(BattleSaveEnvironment environment)
    {
        this.environment = environment;
        content = environment.Rules.Content.Source;
        manifest = environment.RulesetManifest;
    }

    internal JsonObject ReadDocument(JsonNode? raw)
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
            try
            {
                check.Apply(save);
            }
            catch (StoredDocumentException) { throw; }
            catch (Exception error)
            {
                throw new StoredDocumentException($"战斗存档无法读取：校验「{check.Id}」时遇到损坏数据", error);
            }
        }
        return save;
    }

    internal static JsonObject RequireHeader(JsonObject save)
    {
        if (save["battle"] is JsonObject legacy && legacy.ContainsKey("levelName"))
            throw new StoredDocumentException("Level titles belong to presentation metadata, not battle headers.");
        if (save["battle"] is not JsonObject header ||
            !StoredSchema.Matches(header["levelId"], StoredKind.String) ||
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
        if (content.TryGet(family, id) is null)
            StoredSchema.Reject($"{owner} 引用了目录里没有的「{id}」");
    }

    private void CheckRuleset(JsonObject save)
    {
        RulesetManifest.ValidateStored(save["ruleset"]);
        var differences = RulesetManifest.Differences(manifest, save["ruleset"]!.AsObject());
        if (differences.Count > 0)
            StoredSchema.Reject(string.Join("；", differences));
    }

    private static void CheckMetadata(JsonObject save)
    {
        if (!StoredSchema.ValidTimestamp(StateJson.Text(save, "savedAt")))
            StoredSchema.Reject("保存时间缺失或损坏");
        var header = RequireHeader(save);
        var state = State(save);
        foreach (var key in new[] { "levelId", "turn", "phase" })
            if (!Empire.Core.Determinism.EcmaJson.DeepEquals(header[key], state[key]))
                StoredSchema.Reject("战斗摘要与战斗状态不一致");
    }

    private void CheckMap(JsonObject save)
    {
        var map = State(save)["map"]!.AsObject();
        if (!StoredSchema.Matches(map["width"], StoredKind.Integer) || !StoredSchema.Matches(map["height"], StoredKind.Integer) ||
            StateJson.Number(map["width"], "width") < 1 || StateJson.Number(map["height"], "height") < 1)
            StoredSchema.Reject("地图尺寸不合法");
        var width = StateJson.Integer(map, "width");
        var height = StateJson.Integer(map, "height");
        if ((long)width * height != map["tiles"]!.AsArray().Count)
            StoredSchema.Reject("地图格数与尺寸不符");
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
