using System.Text.Json.Nodes;
using Empire.Core.Campaign;
using Empire.Core.Engine;

namespace Empire.Application;

/// <summary>Application flow, delegating campaign and battle mutations to their canonical aggregates.</summary>
public sealed class GrayFlagCampaign
{
    private readonly JsonObject definition;
    private readonly GrayFlagEngine engine;
    private readonly CampaignBattleBridge bridge;
    public CampaignRuntime Runtime { get; private set; }
    public GrayFlagSession? Battle { get; private set; }
    public JsonObject? Request { get; private set; }

    public GrayFlagCampaign(JsonObject definition, JsonArray levels, JsonArray packs, GrayFlagEngine engine)
    {
        this.definition = Empire.Core.Determinism.EcmaJson.Normalize(definition)!.AsObject();
        this.engine = engine;
        var catalog = levels.OfType<JsonObject>().ToDictionary(level => level["id"]!.GetValue<string>(), StringComparer.Ordinal);
        bridge = new CampaignBattleBridge(id => catalog.TryGetValue(id, out var level) ? (JsonObject)level.DeepClone()
            : throw new InvalidOperationException($"Unknown campaign level {id}"), packs);
        Runtime = new CampaignRuntime(definition);
    }

    public void BeginBattle()
    {
        if (Battle is not null) throw new InvalidOperationException("A battle is already open.");
        var before = Runtime.Snapshot();
        try
        {
            var request = Candidate01CampaignPolicy.ApplyBattleContext(Runtime.BeginBattle(bridge), Runtime.Snapshot());
            var battle = new GrayFlagSession(engine, request["level"]!.AsObject());
            Request = request; Battle = battle;
        }
        catch { Runtime = new CampaignRuntime(definition, before); throw; }
    }

    public void FinishBattle()
    {
        if (Battle is null || Request is null || Battle.Snapshot()["phase"]!.GetValue<string>() != "over")
            throw new InvalidOperationException("The battle has not concluded.");
        var result = bridge.Result(Request, Battle.Snapshot(), Battle.Events);
        Runtime.CompleteBattle(Candidate01CampaignPolicy.ApplyBattleResult(result, Runtime.Snapshot()));
        Battle = null; Request = null;
    }

    public void RestartBattle()
    {
        if (Request is null) throw new InvalidOperationException("No battle to restart.");
        Battle = new GrayFlagSession(engine, Request["level"]!.AsObject());
    }

    public JsonObject Save() => new()
    {
        ["schema"] = 1,
        ["campaign"] = CampaignSave.Create(definition, Runtime.Snapshot()),
        ["request"] = Request?.DeepClone(),
        ["battle"] = Battle?.Save(),
        ["events"] = Battle is null ? new JsonArray() : new JsonArray(Battle.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()),
    };

    public void Load(JsonObject document)
    {
        document = Empire.Core.Determinism.EcmaJson.Normalize(document)!.AsObject();
        if (document["schema"]?.GetValue<int>() != 1) throw new InvalidOperationException("Unsupported application save schema.");
        var state = CampaignSave.Load(document["campaign"], definition)["state"]!.AsObject();
        var restored = new CampaignRuntime(definition, state);
        var request = document["request"] as JsonObject;
        var savedBattle = document["battle"];
        if (document["events"] is not JsonArray recordedEvents || recordedEvents.Any(value => value is not JsonObject || value["type"] is not JsonValue type || !type.TryGetValue<string>(out var eventType) || string.IsNullOrEmpty(eventType)))
            throw new InvalidOperationException("Saved event history is malformed.");
        if ((request is null) != (savedBattle is null) || (state["pendingBattle"] is null) != (request is null))
            throw new InvalidOperationException("Campaign and battle save do not match.");
        GrayFlagSession? battle = null;
        if (request is not null)
        {
            if (request["id"]?.GetValue<string>() != state["pendingBattle"]!["requestId"]?.GetValue<string>()
                || request["node"]?.GetValue<string>() != state["pendingBattle"]!["node"]?.GetValue<string>()
                || request["levelId"]?.GetValue<string>() != state["pendingBattle"]!["level"]?.GetValue<string>())
                throw new InvalidOperationException("Battle request identity mismatch.");
            battle = GrayFlagSession.FromSave(engine, savedBattle!, recordedEvents);
            if (battle.Snapshot()["levelId"]?.GetValue<string>() != request["levelId"]?.GetValue<string>())
                throw new InvalidOperationException("Battle level identity mismatch.");
        }
        Runtime = restored; Request = request is null ? null : (JsonObject)request.DeepClone(); Battle = battle;
    }
}
