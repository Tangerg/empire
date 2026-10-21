using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Engine;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Presentation.Feedback;
using Empire.Localization;

namespace Empire.Application;

/// <summary>Owns the campaign/battle lifecycle; request facts are derived from the canonical campaign.</summary>
public sealed class GrayFlagCampaign
{
    private readonly JsonObject definition;
    private readonly GrayFlagEngine engine;
    private readonly GameText text;
    private readonly ActionRefusalText refusals;
    private readonly CampaignBattleBridge bridge;
    private readonly IReadOnlyDictionary<string, JsonObject> levels;
    private CampaignRuntime runtime;
    private PendingBattle? pending;
    private sealed record PendingBattle(JsonObject Request, GrayFlagSession Session, bool AtCamp);

    public bool AtCamp => pending?.AtCamp == true;
    public bool HasPendingBattle => pending is not null;
    public GrayFlagSession? Battle => pending?.Session;
    public JsonObject Snapshot() => runtime.Snapshot();
    public JsonObject Node() => runtime.Node();
    public JsonArray Choices() => runtime.Choices();
    public JsonObject? RequestSnapshot() => pending?.Request.DeepClone() as JsonObject;
    public JsonObject BattleLevel() => (JsonObject)(pending?.Request["level"] ?? levels[Node()["level"]!.GetValue<string>()]).DeepClone();
    public void Advance() => runtime.Advance();
    public void Choose(string id) => runtime.Choose(id);

    public GrayFlagCampaign(JsonObject definition, JsonArray levels, GrayFlagEngine engine, GameText text, ActionRefusalText refusals, JsonObject? initialState = null)
    {
        this.definition = EcmaJson.Normalize(definition)!.AsObject();
        this.engine = engine;
        this.text = text;
        this.refusals = refusals;
        this.levels = levels.OfType<JsonObject>().ToDictionary(level => level["id"]!.GetValue<string>(),
            level => (JsonObject)level.DeepClone(), StringComparer.Ordinal);
        bridge = new CampaignBattleBridge(id => this.levels.TryGetValue(id, out var level) ? (JsonObject)level.DeepClone()
            : throw new CampaignInvariantException($"Unknown campaign level {id}"), engine.Catalog);
        runtime = new CampaignRuntime(this.definition, initialState);
        if (runtime.Snapshot()["pendingBattle"] is not null)
            throw new CampaignActionException("A campaign-only resume cannot reconstruct a pending battle. Load the complete application save instead.");
    }

    public void BeginBattle()
    {
        if (pending is not null)
        {
            if (!AtCamp) throw new CampaignActionException("A battle is already open.");
            RestartBattle();
            return;
        }
        var nextRuntime = new CampaignRuntime(definition, runtime.Snapshot());
        var request = Candidate01CampaignPolicy.ApplyBattleContext(nextRuntime.BeginBattle(bridge), nextRuntime.Snapshot());
        var next = new PendingBattle(request, new GrayFlagSession(engine, request["level"]!.AsObject(), text, refusals), false);
        runtime = nextRuntime;
        pending = next;
    }

    public (string Outcome, CampaignBattleSummary Summary) BattleSummary()
    {
        var current = RequireConcludedBattle();
        var state = current.Session.Snapshot();
        var events = current.Session.Events;
        var result = bridge.Result(current.Request, state, events);
        var level = current.Request["level"]!.AsObject();
        var chapter = level["extra"]?["chapter"]?.GetValue<int>() ?? 1;
        if (chapter == 0) chapter = 1;
        return (result["outcome"]!.GetValue<string>(), CampaignBattleSummary.Create(
            level["name"]!.GetValue<string>(), chapter, current.Request["perspectivePlayer"]!.GetValue<int>(),
            EcmaJson.ToElement(state), engine.UnitTypeName,
            result["signals"]!.AsArray().Select(signal => signal!.GetValue<string>()), BattleEventFacts.StrikeCount(events)));
    }

    public void FinishBattle()
    {
        var current = RequireConcludedBattle();
        var result = bridge.Result(current.Request, current.Session.Snapshot(), current.Session.Events);
        runtime.CompleteBattle(Candidate01CampaignPolicy.ApplyBattleResult(result, runtime.Snapshot()));
        pending = null;
    }

    public void ExitBattle() => ResetBattle(atCamp: true);
    public void RestartBattle() => ResetBattle(atCamp: false);

    private void ResetBattle(bool atCamp)
    {
        var current = pending ?? throw new CampaignActionException("No battle to restart.");
        pending = new(current.Request, new GrayFlagSession(engine, current.Request["level"]!.AsObject(), text, refusals), atCamp);
    }

    private PendingBattle RequireConcludedBattle()
    {
        if (pending is not { AtCamp: false } current || current.Session.Snapshot()["phase"]!.GetValue<string>() != "over")
            throw new CampaignActionException("The battle has not concluded.");
        return current;
    }

    public JsonObject Save() => new()
    {
        ["schema"] = ApplicationSaveDocument.Schema,
        ["campaign"] = CampaignSave.Create(definition, runtime.Snapshot()),
        ["mode"] = pending is null ? "campaign" : AtCamp ? "camp" : "battle",
        ["battle"] = pending is { AtCamp: false } ? pending.Session.Save() : null,
        ["events"] = pending is { AtCamp: false }
            ? new JsonArray(pending.Session.Events.Select(value => (JsonNode)value.DeepClone()).ToArray()) : new JsonArray(),
    };

    public void Load(JsonNode? document)
    {
        var saved = ApplicationSaveDocument.Read(document, definition);
        var nextRuntime = new CampaignRuntime(definition, saved.CampaignState);
        PendingBattle? next = null;
        if (saved.Location != CampaignLocation.Campaign)
        {
            JsonObject request;
            try { request = nextRuntime.PendingBattleRequest(bridge); }
            catch (PendingBattleIdentityException error)
            { throw new StoredDocumentException("Saved pending battle identity is invalid.", error); }
            catch (CampaignRosterContentException error)
            { throw new StoredDocumentException("Saved campaign roster references unknown content.", error); }
            request = Candidate01CampaignPolicy.ApplyBattleContext(request, nextRuntime.Snapshot());
            var opening = new GrayFlagSession(engine, request["level"]!.AsObject(), text, refusals);
            var session = saved.Location == CampaignLocation.Camp
                ? opening
                : GrayFlagSession.FromSave(engine, saved.Battle!, text, refusals, saved.Events);
            var state = session.Snapshot();
            if (state["levelId"]?.GetValue<string>() != request["levelId"]!.GetValue<string>() ||
                !EcmaJson.DeepEquals(state["map"]?["width"], request["level"]!["width"]) ||
                !EcmaJson.DeepEquals(state["map"]?["height"], request["level"]!["height"]))
                throw new StoredDocumentException("Saved battle does not match the campaign level.");
            next = new(request, session, saved.Location == CampaignLocation.Camp);
        }
        runtime = nextRuntime;
        pending = next;
    }
}
