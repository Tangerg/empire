using System.Text.Json.Nodes;
using Empire.Core.Campaign;
using Empire.Core.Engine;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Presentation.Feedback;

namespace Empire.Application;

/// <summary>Application flow, delegating campaign and battle mutations to their canonical aggregates.</summary>
public sealed class GrayFlagCampaign
{
    private readonly JsonObject definition;
    private readonly GrayFlagEngine engine;
    private readonly CampaignBattleBridge bridge;
    private readonly IReadOnlyDictionary<string, JsonObject> levels;
    public bool AtCamp { get; private set; }
    public JsonObject BattleLevel() => (JsonObject)levels[Runtime.Node()["level"]!.GetValue<string>()].DeepClone();
    public CampaignRuntime Runtime { get; private set; }
    public GrayFlagSession? Battle { get; private set; }
    public JsonObject? Request { get; private set; }

    public GrayFlagCampaign(JsonObject definition, JsonArray levels, JsonArray packs, GrayFlagEngine engine, JsonObject? initialState = null)
    {
        this.definition = Empire.Core.Determinism.EcmaJson.Normalize(definition)!.AsObject();
        this.engine = engine;
        var catalog = levels.OfType<JsonObject>().ToDictionary(level => level["id"]!.GetValue<string>(), StringComparer.Ordinal);
        this.levels = catalog;
        bridge = new CampaignBattleBridge(id => catalog.TryGetValue(id, out var level) ? (JsonObject)level.DeepClone()
            : throw new InvalidOperationException($"Unknown campaign level {id}"), packs);
        Runtime = new CampaignRuntime(definition, initialState);
        if (Runtime.Snapshot()["pendingBattle"] is not null)
            throw new InvalidOperationException("A campaign-only resume cannot reconstruct a pending battle. Load the complete application save instead.");
    }

    public void BeginBattle()
    {
        if (Battle is not null)
        {
            if (!AtCamp) throw new InvalidOperationException("A battle is already open.");
            RestartBattle(); return;
        }
        var before = Runtime.Snapshot();
        try
        {
            var request = Candidate01CampaignPolicy.ApplyBattleContext(Runtime.BeginBattle(bridge), Runtime.Snapshot());
            var battle = new GrayFlagSession(engine, request["level"]!.AsObject());
            Request = request; Battle = battle;
        }
        catch { Runtime = new CampaignRuntime(definition, before); throw; }
    }

    public (string Outcome, CampaignBattleSummary Summary) BattleSummary()
    {
        if (Battle is null || Request is null || Battle.Snapshot()["phase"]?.GetValue<string>() != "over")
            throw new InvalidOperationException("The battle has not concluded.");
        var state = Battle.Snapshot();
        var events = Battle.Events;
        var result = bridge.Result(Request, state, events);
        var level = Request["level"]!.AsObject();
        var chapter = level["extra"]?["chapter"]?.GetValue<int>() ?? 1;
        if (chapter == 0) chapter = 1;
        return (result["outcome"]!.GetValue<string>(), CampaignBattleSummary.Create(
            level["name"]!.GetValue<string>(), chapter, Request["perspectivePlayer"]!.GetValue<int>(),
            EcmaJson.ToElement(state), engine.UnitTypeName,
            result["signals"]!.AsArray().Select(signal => signal!.GetValue<string>()), BattleEventFacts.StrikeCount(events)));
    }

    public void FinishBattle()
    {
        if (Battle is null || Request is null || Battle.Snapshot()["phase"]!.GetValue<string>() != "over")
            throw new InvalidOperationException("The battle has not concluded.");
        var result = bridge.Result(Request, Battle.Snapshot(), Battle.Events);
        Runtime.CompleteBattle(Candidate01CampaignPolicy.ApplyBattleResult(result, Runtime.Snapshot()));
        Battle = null; Request = null; AtCamp = false;
    }

    public void ExitBattle()
    {
        if (Battle is null || Request is null) throw new InvalidOperationException("No battle to leave.");
        RestartBattle();
        AtCamp = true;
    }

    public void RestartBattle()
    {
        if (Request is null) throw new InvalidOperationException("No battle to restart.");
        Battle = new GrayFlagSession(engine, Request["level"]!.AsObject());
        AtCamp = false;
    }

    public JsonObject Save() => new()
    {
        ["schema"] = 1, ["atCamp"] = AtCamp,
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
        var atCamp = document["atCamp"]?.GetValue<bool>() ?? false;
        if (atCamp && (request is null || savedBattle is null)) throw new InvalidOperationException("Camp return has no retained battle request.");
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
        Runtime = restored; Request = request is null ? null : (JsonObject)request.DeepClone(); Battle = battle; AtCamp = atCamp;
    }
}
