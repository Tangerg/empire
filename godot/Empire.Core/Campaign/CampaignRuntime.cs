using System.Text.Json.Nodes;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Campaign;

/// <summary>Canonical aggregate for cross-node changes and persistent roster projection.</summary>
public sealed class CampaignAggregate
{
    private readonly JsonObject definition;
    private readonly JsonObject state;
    private readonly CampaignRules rules;
    private readonly Dictionary<string, JsonObject> nodes;
    public CampaignAggregate(JsonObject definition, JsonObject state, CampaignRules rules)
    {
        this.definition = definition; this.state = state; this.rules = rules;
        CampaignValidation.ValidateDefinition(definition);
        var inspection = new CampaignNodeInspection(definition);
        foreach (var node in Objects(definition["nodes"], "nodes")) rules.Nodes.Validate(inspection, node);
        CampaignValidation.ValidateState(definition, state);
        nodes = Objects(definition["nodes"], "nodes").ToDictionary(node => Text(node, "id"), StringComparer.Ordinal);
    }
    public JsonObject Node() => nodes.GetValueOrDefault(Text(state, "currentNode")) ??
        throw new CampaignInvariantException($"unknown current campaign node \"{Text(state, "currentNode")}\"");
    public void Apply(JsonArray? effects = null) { foreach (var effect in Objects(effects, "effects")) rules.Effects.Apply(state, effect); }
    public void MoveTo(string next, string? completed = null)
    {
        if (!nodes.ContainsKey(next)) throw new CampaignInvariantException($"unknown campaign node \"{next}\"");
        completed ??= Text(state, "currentNode");
        if (!CampaignConditionRegistry.Contains(state["completedNodes"], completed)) Array(state["completedNodes"], "completedNodes").Add(completed);
        state["currentNode"] = next;
    }
    public void ProjectBattleResult(JsonObject result)
    {
        foreach (var value in Objects(result["units"], "units"))
        {
            if (state["roster"]![Text(value, "campaignUnit")] is not JsonObject unit) continue;
            foreach (var key in new[] { "disposition", "hpRatio", "moraleRatio", "unitType", "rank", "rankProgress", "resources", "career", "unlockedCareers", "careerMastery", "learnedAbilities" })
                unit[key] = value[key]?.DeepClone();
        }
    }
}

/// <summary>Deterministic application-facing campaign writer; failed commands roll back atomically.</summary>
public sealed class CampaignRuntime
{
    private readonly JsonObject definition;
    private readonly JsonObject state;
    private readonly CampaignAggregate aggregate;
    private readonly CampaignRules rules;
    public JsonObject Definition => (JsonObject)definition.DeepClone();
    public JsonObject State => Snapshot();
    public CampaignRuntime(JsonObject definition, JsonObject? state = null, CampaignRules? rules = null)
    {
        this.definition = Empire.Core.Determinism.EcmaJson.Normalize(definition)!.AsObject();
        this.state = state is null ? CampaignValidation.CreateState(this.definition) : Empire.Core.Determinism.EcmaJson.Normalize(state)!.AsObject();
        this.rules = (rules ?? CampaignRules.Create()).FrozenClone();
        aggregate = new CampaignAggregate(this.definition, this.state, this.rules);
    }
    public JsonObject Node() => (JsonObject)aggregate.Node().DeepClone();
    public JsonObject Snapshot() => (JsonObject)state.DeepClone();
    public JsonObject PendingBattleRequest(CampaignBattleBridge bridge)
    {
        RequireActive();
        return bridge.RestorePending(definition, state);
    }
    public JsonObject Advance() => Transaction(() =>
    {
        RequireActive();
        var node = aggregate.Node();
        rules.Nodes.Advance(new CampaignNodeAdvance(aggregate.Apply, next => aggregate.MoveTo(next), status =>
        { CompleteNode(Text(node, "id")); state["status"] = status; }, api =>
            throw new CampaignActionException($"{Text(node, "type")} node requires {api}")), node);
        return Node();
    });
    public JsonArray Choices()
    {
        var node = aggregate.Node();
        if (Text(node, "type") != "choice") return new();
        return new JsonArray(Objects(node["choices"], "choices")
            .Where(choice => choice["condition"] is not JsonObject condition || rules.Conditions.Evaluate(state, condition))
            .Select(choice => (JsonNode)new JsonObject { ["id"] = choice["id"]!.DeepClone(), ["next"] = choice["next"]!.DeepClone() }).ToArray());
    }
    public JsonObject Choose(string id) => Transaction(() =>
    {
        RequireActive(); var node = aggregate.Node();
        if (Text(node, "type") != "choice") throw new CampaignActionException("current campaign node is not a choice");
        var selected = Objects(node["choices"], "choices").FirstOrDefault(choice => Text(choice, "id") == id)
            ?? throw new CampaignActionException($"unknown choice \"{id}\"");
        if (selected["condition"] is JsonObject condition && !rules.Conditions.Evaluate(state, condition))
            throw new CampaignActionException($"choice \"{id}\" is not currently available");
        aggregate.Apply(node["effects"] as JsonArray); aggregate.Apply(selected["effects"] as JsonArray);
        aggregate.MoveTo(Text(selected, "next")); return Node();
    });
    public JsonObject BeginBattle(CampaignBattleBridge bridge) => Transaction(() =>
    {
        RequireActive(); var node = aggregate.Node();
        if (Text(node, "type") != "battle") throw new CampaignActionException("current campaign node is not a battle");
        aggregate.Apply(node["effects"] as JsonArray);
        var request = bridge.Prepare(definition, state);
        state["battleSequence"] = Number(state["battleSequence"], "battleSequence") + 1;
        state["pendingBattle"] = new JsonObject { ["requestId"] = request["id"]!.DeepClone(), ["node"] = node["id"]!.DeepClone(), ["level"] = node["level"]!.DeepClone() };
        return request;
    });
    public JsonObject? CompleteBattle(JsonObject result) => Transaction<JsonObject?>(() =>
    {
        RequireActive(); var node = aggregate.Node();
        if (state["pendingBattle"] is not JsonObject pending || Text(node, "type") != "battle") throw new CampaignActionException("campaign has no pending battle");
        if (Text(pending, "requestId") != Text(result, "requestId") || Text(pending, "node") != Text(node, "id"))
            throw new CampaignActionException("battle result does not match pending request");
        aggregate.ProjectBattleResult(result);
        var outcome = Text(result, "outcome");
        aggregate.Apply(node["outcomeEffects"]?[outcome] as JsonArray);
        Array(state["battleHistory"], "battleHistory").Add(new JsonObject { ["requestId"] = result["requestId"]!.DeepClone(),
            ["node"] = node["id"]!.DeepClone(), ["level"] = node["level"]!.DeepClone(), ["outcome"] = outcome,
            ["turns"] = result["turns"]!.DeepClone(), ["signals"] = result["signals"]!.DeepClone() });
        state["pendingBattle"] = null;
        var next = node["next"]?[outcome]?.GetValue<string>();
        if (string.IsNullOrEmpty(next)) { CompleteNode(Text(node, "id")); state["status"] = outcome == "victory" ? "completed" : "failed"; return null; }
        aggregate.MoveTo(next); return Node();
    });
    private void RequireActive() { if (Text(state, "status") != "active") throw new CampaignActionException($"campaign is {Text(state, "status")}"); }
    private void CompleteNode(string id)
    { if (!CampaignConditionRegistry.Contains(state["completedNodes"], id)) Array(state["completedNodes"], "completedNodes").Add(id); }
    private T Transaction<T>(Func<T> operation)
    {
        var before = Snapshot();
        try { return operation(); }
        catch
        {
            state.Clear();
            foreach (var pair in before) state[pair.Key] = pair.Value?.DeepClone();
            throw;
        }
    }
}
