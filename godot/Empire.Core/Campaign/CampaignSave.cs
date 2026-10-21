using System.Globalization;
using System.Text.Json.Nodes;
using Empire.Core.Persistence;

namespace Empire.Core.Campaign;

public static class CampaignSave
{
    public const int Schema = 1;

    public static JsonObject Create(JsonObject definition, JsonObject state, string? savedAt = null)
    {
        CampaignValidation.ValidateDefinition(definition);
        savedAt ??= DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        if (!StoredSchema.ValidTimestamp(savedAt))
            throw new CampaignInvariantException("cannot save campaign with an invalid timestamp");
        if (!CampaignValidation.Same(state["definitionId"], definition["id"]) ||
            !CampaignValidation.Same(state["definitionVersion"], definition["version"]))
            throw new CampaignInvariantException("cannot save state for a different campaign definition");
        CampaignValidation.ValidateState(definition, state);
        return new JsonObject
        {
            ["schema"] = Schema,
            ["campaign"] = new JsonObject
            {
                ["id"] = definition["id"]!.DeepClone(),
                ["version"] = definition["version"]!.DeepClone(),
            },
            ["contentPacks"] = definition["contentPacks"]!.DeepClone(),
            ["savedAt"] = savedAt,
            ["state"] = state.DeepClone(),
        };
    }

    public static JsonObject Load(JsonNode? raw, JsonObject definition)
    {
        var save = StoredSchema.Read("campaign save", Schema, raw);
        CampaignValidation.ValidateDefinition(definition);
        RequireShape(save);
        var campaign = save["campaign"]!.AsObject();
        if (!CampaignValidation.Same(campaign["id"], definition["id"]) ||
            !CampaignValidation.Same(campaign["version"], definition["version"]))
            Reject("definition identity/version mismatch");
        var expectedPacks = definition["contentPacks"]!.AsObject();
        var storedPacks = save["contentPacks"]!.AsObject();
        foreach (var id in expectedPacks.Select(entry => entry.Key).Union(storedPacks.Select(entry => entry.Key), StringComparer.Ordinal))
            if (!CampaignValidation.Same(storedPacks[id], expectedPacks[id]))
                Reject($"content pack mismatch: \"{id}\"");
        var state = save["state"]!.AsObject();
        if (!CampaignValidation.Same(state["definitionId"], definition["id"]) ||
            !CampaignValidation.Same(state["definitionVersion"], definition["version"]))
            Reject("state identity/version mismatch");
        try
        {
            CampaignValidation.ValidateState(definition, state);
        }
        catch (CampaignInvariantException error)
        {
            throw new StoredDocumentException($"campaign save state is invalid: {error.Message}", error);
        }
        return save;
    }

    private static void RequireShape(JsonObject save)
    {
        Require(save, "save", ("schema", StoredKind.Integer), ("campaign", StoredKind.Object),
            ("contentPacks", StoredKind.Object), ("savedAt", StoredKind.String), ("state", StoredKind.Object));
        Require(save["campaign"], "campaign", ("id", StoredKind.String), ("version", StoredKind.Integer));
        if (!StoredSchema.ValidTimestamp(save["savedAt"]!.GetValue<string>()))
            Reject("savedAt is invalid");
        var state = save["state"]!.AsObject();
        Require(state, "state", ("definitionId", StoredKind.String), ("definitionVersion", StoredKind.Integer),
            ("currentNode", StoredKind.String), ("status", StoredKind.String), ("flags", StoredKind.Array),
            ("variables", StoredKind.Object), ("resources", StoredKind.Object), ("relations", StoredKind.Object),
            ("features", StoredKind.Array), ("roster", StoredKind.Object), ("completedNodes", StoredKind.Array),
            ("battleHistory", StoredKind.Array), ("pendingBattle", StoredKind.NullableObject), ("battleSequence", StoredKind.Integer));
        foreach (var (id, version) in save["contentPacks"]!.AsObject())
            if (!CampaignValidation.Named(id) || !CampaignValidation.Integer(version) || CampaignValidation.Number(version) < 1)
                Reject($"contentPacks.{id} is invalid");
        RequireStringArray(state["flags"]!.AsArray(), "state.flags");
        RequireStringArray(state["features"]!.AsArray(), "state.features");
        RequireStringArray(state["completedNodes"]!.AsArray(), "state.completedNodes");
        RequireFiniteRecord(state["resources"]!.AsObject(), "state.resources");
        RequireFiniteRecord(state["relations"]!.AsObject(), "state.relations");
        foreach (var (key, value) in state["variables"]!.AsObject())
            if (!CampaignValidation.Named(key) || !CampaignValidation.ScenarioValue(value))
                Reject($"state.variables.{key} is invalid");
        foreach (var (id, value) in state["roster"]!.AsObject())
            if (value is not JsonObject unit || !StoredSchema.Matches(unit["id"], StoredKind.String) ||
                !StoredSchema.Matches(unit["hpRatio"], StoredKind.Number) || !StoredSchema.Matches(unit["moraleRatio"], StoredKind.Number))
                Reject($"state.roster.{id} is invalid");
        var history = state["battleHistory"]!.AsArray();
        for (var index = 0; index < history.Count; index++)
        {
            if (history[index] is not JsonObject record || !StoredSchema.Matches(record["requestId"], StoredKind.String) ||
                !StoredSchema.Matches(record["node"], StoredKind.String) || !StoredSchema.Matches(record["level"], StoredKind.String) ||
                !StoredSchema.Matches(record["outcome"], StoredKind.String) || !StoredSchema.Matches(record["turns"], StoredKind.Integer) ||
                !StoredSchema.Matches(record["signals"], StoredKind.Array))
                Reject($"state.battleHistory.{index} is invalid");
            RequireStringArray(history[index]!["signals"]!.AsArray(), $"state.battleHistory.{index}.signals");
        }
        if (state["pendingBattle"] is JsonObject pending &&
            (!StoredSchema.Matches(pending["requestId"], StoredKind.String) || !StoredSchema.Matches(pending["node"], StoredKind.String) ||
                !StoredSchema.Matches(pending["level"], StoredKind.String)))
            Reject("state.pendingBattle is invalid");
    }

    private static void Require(JsonNode? value, string owner, params (string Field, StoredKind Kind)[] shape)
    {
        if (value is not JsonObject aggregate)
            Reject($"{owner} must be an object");
        else
            foreach (var (field, kind) in shape)
                if (!aggregate.ContainsKey(field) || !StoredSchema.Matches(aggregate[field], kind))
                    Reject($"{owner}.{field} is missing or invalid");
    }

    private static void RequireStringArray(JsonArray value, string owner)
    {
        if (value.Any(entry => !StoredSchema.Matches(entry, StoredKind.String)))
            Reject($"{owner} must contain strings");
    }

    private static void RequireFiniteRecord(JsonObject value, string owner)
    {
        foreach (var (key, entry) in value)
            if (!CampaignValidation.Named(key) || !CampaignValidation.Finite(entry))
                Reject($"{owner}.{key} is invalid");
    }

    private static void Reject(string message) => throw new StoredDocumentException($"campaign save {message}");
}
