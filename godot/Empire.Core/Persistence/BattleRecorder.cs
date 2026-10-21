using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.State;

namespace Empire.Core.Persistence;

public sealed class BattleRecorder
{
    public const int Schema = 3;
    private readonly JsonArray actions = new();
    private readonly JsonObject ruleset;
    private readonly string levelId;
    private readonly JsonNode seed;
    private readonly string initialStateHash;

    public BattleRecorder(JsonObject manifest, JsonObject initialState)
    {
        RulesetManifest.RequirePersistent(manifest);
        ruleset = (JsonObject)manifest.DeepClone();
        levelId = StateJson.Text(initialState, "levelId");
        seed = StateJson.Required(StateJson.Object(initialState["random"], "random"), "seed").DeepClone();
        initialStateHash = StateDigest.Hash(initialState);
    }

    public void Record(JsonObject action) => actions.Add(action.DeepClone());

    public JsonObject Replay(JsonObject finalState)
    {
        var finalLevel = StateJson.Text(finalState, "levelId");
        if (finalLevel != levelId)
            throw new DomainInvariantException($"cannot finish replay for \"{levelId}\" with state from \"{finalLevel}\"");
        return new JsonObject
        {
            ["schema"] = Schema,
            ["levelId"] = levelId,
            ["seed"] = seed.DeepClone(),
            ["ruleset"] = ruleset.DeepClone(),
            ["initialStateHash"] = initialStateHash,
            ["actions"] = actions.DeepClone(),
            ["finalStateHash"] = StateDigest.Hash(finalState),
        };
    }
}
