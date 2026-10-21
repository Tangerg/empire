using System.Text.Json.Nodes;
using Empire.Core.State;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Tactics;
using Empire.Core.Lifecycle;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Campaign;

public sealed class PendingBattleIdentityException(string expectedRequestId, string actualRequestId)
    : DomainInvariantException("pending battle identity does not match the campaign sequence and node")
{
    public string ExpectedRequestId { get; } = expectedRequestId;
    public string ActualRequestId { get; } = actualRequestId;
}

public sealed class CampaignRosterContentException(string campaignUnitId, string unitTypeId)
    : DomainInvariantException($"campaign roster unit \"{campaignUnitId}\" references unknown unit type \"{unitTypeId}\"")
{
    public string CampaignUnitId { get; } = campaignUnitId;
    public string UnitTypeId { get; } = unitTypeId;
}

/// <summary>Projects persistent roster state to and from the canonical battle snapshot.</summary>
public sealed class CampaignBattleBridge
{
    private readonly Func<string, JsonObject> resolveLevel;
    private readonly Func<JsonObject, JsonObject> normalizeLevel;
    private readonly StateContent content;
    private readonly IReadOnlyDictionary<string, string> dispositions;
    public CampaignBattleBridge(Func<string, JsonObject> resolveLevel, ReadOnlyContent catalog,
        Func<JsonObject, JsonObject>? normalizeLevel = null, IReadOnlyDictionary<string, string>? markerDispositions = null)
    {
        this.resolveLevel = resolveLevel; this.normalizeLevel = normalizeLevel ?? LevelNormalizer.Normalize;
        content = catalog.Source;
        dispositions = markerDispositions is null ? new Dictionary<string, string>(StringComparer.Ordinal)
        { ["corpse"] = "fallen", ["transport-loss"] = "fallen", ["routed"] = "routed", ["surrendered"] = "surrendered", ["withdrawn"] = "missing" }
            : new Dictionary<string, string>(markerDispositions, StringComparer.Ordinal);
    }
    public JsonObject Prepare(JsonObject definition, JsonObject state)
    {
        if (state["pendingBattle"] is JsonObject pending) throw new CampaignActionException($"campaign already has pending battle \"{Text(pending, "requestId")}\"");
        return ProjectRequest(definition, state, Number(state["battleSequence"], "battleSequence") + 1);
    }

    internal JsonObject RestorePending(JsonObject definition, JsonObject state)
    {
        var pending = state["pendingBattle"] as JsonObject
            ?? throw new CampaignActionException("campaign has no pending battle");
        var request = ProjectRequest(definition, state, Number(state["battleSequence"], "battleSequence"));
        if (Text(pending, "requestId") != Text(request, "id") || Text(pending, "node") != Text(request, "node") ||
            Text(pending, "level") != Text(request, "levelId"))
            throw new PendingBattleIdentityException(Text(request, "id"), Text(pending, "requestId"));
        return request;
    }

    private JsonObject ProjectRequest(JsonObject definition, JsonObject state, double sequence)
    {
        var id = Text(state, "currentNode");
        var node = Objects(definition["nodes"], "nodes").FirstOrDefault(candidate => Text(candidate, "id") == id);
        if (node is null || Text(node, "type") != "battle") throw new CampaignInvariantException($"campaign node \"{id}\" is not a battle");
        var level = normalizeLevel((JsonObject)resolveLevel(Text(node, "level")).DeepClone());
        var declared = Objects(node["rosterBindings"], "rosterBindings").Select(binding => (JsonObject)binding.DeepClone()).ToArray();
        foreach (var binding in declared)
        {
            var campaignUnit = Text(binding, "campaignUnit");
            var campaign = state["roster"]![campaignUnit] as JsonObject ?? throw new CampaignInvariantException($"unknown campaign roster unit \"{campaignUnit}\"");
            SeedLevelUnit(level, Text(binding, "levelUnitKey"), campaign);
        }
        var bindings = declared.Where(binding => Text(Object(state["roster"]![Text(binding, "campaignUnit")], "roster unit"), "disposition") == "available");
        return new JsonObject
        {
            ["id"] = $"{Text(definition, "id")}:{Text(node, "id")}:{EcmaNumber.Format(sequence)}",
            ["campaignId"] = definition["id"]!.DeepClone(), ["node"] = node["id"]!.DeepClone(),
            ["levelId"] = node["level"]!.DeepClone(), ["perspectivePlayer"] = node["perspectivePlayer"]?.DeepClone() ?? JsonValue.Create(1),
            ["level"] = level, ["rosterBindings"] = new JsonArray(bindings.Select(binding => (JsonNode)binding).ToArray()),
            ["context"] = new JsonObject { ["flags"] = state["flags"]!.DeepClone(), ["variables"] = state["variables"]!.DeepClone(), ["features"] = state["features"]!.DeepClone() },
        };
    }
    private void SeedLevelUnit(JsonObject level, string key, JsonObject campaign)
    {
        var units = Array(level["units"], "units");
        var unit = Objects(units, "units").FirstOrDefault(candidate => candidate["key"]?.GetValue<string>() == key)
            ?? throw new CampaignInvariantException($"level \"{Text(level, "id")}\" has no unit key \"{key}\"");
        if (Text(campaign, "disposition") != "available") { units.Remove(unit); return; }
        var type = Text(campaign, "unitType");
        var definition = content.TryGet("units", type) ?? throw new CampaignRosterContentException(Text(campaign, "id"), type);
        unit["unit"] = campaign["unitType"]!.DeepClone(); unit["owner"] = campaign["owner"]!.DeepClone();
        unit["hp"] = Math.Max(1, Round(Number(definition["maxHp"], "maxHp") * Number(campaign["hpRatio"], "hpRatio")));
        unit["morale"] = Math.Max(1, Round((definition["morale"]?["maximum"] is { } maximum ? Number(maximum, "maximum") : 100) * Number(campaign["moraleRatio"], "moraleRatio")));
        unit["rank"] = campaign["rank"]?.DeepClone() ?? JsonValue.Create(0);
        unit["rankProgress"] = campaign["rankProgress"]?.DeepClone() ?? JsonValue.Create(0);
        unit["resources"] = campaign["resources"]?.DeepClone() ?? new JsonObject();
        if (campaign["career"] is { } career) unit["career"] = career.DeepClone(); else unit.Remove("career");
        foreach (var name in new[] { "unlockedCareers", "learnedAbilities" })
            if (campaign[name] is { } value) unit[name] = value.DeepClone(); else unit.Remove(name);
        unit["careerMastery"] = campaign["careerMastery"]?.DeepClone() ?? new JsonObject();
    }
    public JsonObject Result(JsonObject request, JsonObject state, IEnumerable<JsonObject> events)
    {
        var conclusion = BattleConclusion.FromState(state);
        var outcome = conclusion is null ? "retreat" :
            conclusion.Team == TacticalQueries.TeamOf(state, Integer(request, "perspectivePlayer")) ? "victory" : "defeat";
        var signals = new JsonArray();
        foreach (var value in events)
            if (Text(value, "type") == "scenarioSignal") signals.Add(value["signal"]!.DeepClone());
        var units = new JsonArray(Objects(request["rosterBindings"], "rosterBindings")
            .Select(binding => (JsonNode)ProjectUnit(state, Text(binding, "campaignUnit"), Text(binding, "levelUnitKey"))).ToArray());
        return CampaignBattleResult.Create(Text(request, "id"), outcome, conclusion, Integer(state, "turn"),
            units, signals, Object(state["scenario"]!["eventCounts"], "event counts")).ToJson();
    }
    private JsonObject ProjectUnit(JsonObject state, string campaignUnit, string key)
    {
        var active = Objects(state["units"], "units").FirstOrDefault(unit => unit["key"]?.GetValue<string>() == key) ??
            Objects(state["embarkedUnits"], "embarkedUnits").Select(entry => Object(entry["unit"], "embarked unit")).FirstOrDefault(unit => unit["key"]?.GetValue<string>() == key);
        var marker = Objects(state["markers"], "markers").FirstOrDefault(candidate => candidate["fallenUnit"]?["key"]?.GetValue<string>() == key);
        var snapshot = active ?? marker?["fallenUnit"] as JsonObject ?? throw new CampaignInvariantException($"battle result cannot resolve roster unit key \"{key}\"");
        var disposition = active is not null ? "available" : marker is not null ? dispositions.GetValueOrDefault(Text(marker, "kind"), "missing") : "missing";
        var definition = content.Get("units", Text(snapshot, "type"));
        static double Ratio(double current, double maximum) => maximum > 0 ? Math.Clamp(current / maximum, 0, 1) : 1;
        return new JsonObject { ["campaignUnit"] = campaignUnit, ["disposition"] = disposition,
            ["hpRatio"] = Ratio(Number(snapshot["hp"], "hp"), Number(definition["maxHp"], "maxHp")),
            ["moraleRatio"] = Ratio(Number(snapshot["morale"]!["current"], "morale current"), Number(snapshot["morale"]!["maximum"], "morale maximum")),
            ["unitType"] = snapshot["type"]!.DeepClone(), ["rank"] = snapshot["rank"]!.DeepClone(), ["rankProgress"] = snapshot["rankProgress"]!.DeepClone(),
            ["resources"] = snapshot["resources"]!.DeepClone(), ["career"] = snapshot["career"]!["current"]?.DeepClone(),
            ["unlockedCareers"] = snapshot["career"]!["unlocked"]!.DeepClone(), ["careerMastery"] = snapshot["career"]!["mastery"]!.DeepClone(),
            ["learnedAbilities"] = snapshot["learnedAbilities"]!.DeepClone() };
    }
}
