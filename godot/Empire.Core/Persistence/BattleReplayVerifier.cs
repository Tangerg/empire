using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.State;
using Empire.Core.Transactions;

namespace Empire.Core.Persistence;

public sealed record ReplayOutcome(JsonObject State, int? DivergedAt, string Reason, ActionRefusal? Refusal = null);

public static class BattleReplayVerifier
{
    public static ReplayOutcome Verify(
        JsonObject manifest, JsonObject level, JsonNode? raw,
        Func<JsonObject, uint, Battle> createBattle)
    {
        RulesetManifest.RequirePersistent(manifest);
        if (raw is not JsonObject original) throw new StoredDocumentException("battle replay must be an object");
        var replay = (JsonObject)original.DeepClone();
        RequireShape(replay);
        var seedValue = StateJson.Number(replay["seed"], "seed");
        if (seedValue < 0 || seedValue > uint.MaxValue || seedValue != Math.Truncate(seedValue))
            throw new StoredDocumentException("battle replay has invalid seed");
        var seed = (uint)seedValue;
        Battle Create() => createBattle((JsonObject)level.DeepClone(), seed);
        ReplayOutcome Refuse(string reason) => new(Create().Snapshot(), 0, reason);
        var schema = StateJson.Number(replay["schema"], "schema");
        if (schema != BattleRecorder.Schema) return Refuse($"不支持回放 schema {replay["schema"]!.ToJsonString()}");
        var levelId = StateJson.Text(level, "id");
        var replayLevel = StateJson.Text(replay, "levelId");
        if (replayLevel != levelId) return Refuse($"回放关卡 {replayLevel} 与 {levelId} 不匹配");
        RulesetManifest.ValidateStored(replay["ruleset"]);
        var differences = RulesetManifest.Differences(manifest, replay["ruleset"]!.AsObject());
        if (differences.Count > 0) return Refuse(string.Join("；", differences));
        var battle = Create();
        if (battle.StateHash != StateJson.Text(replay, "initialStateHash"))
            return new(battle.Snapshot(), 0, "回放初始状态摘要不匹配");
        var actions = replay["actions"]!.AsArray();
        var expectedHash = StateJson.Text(replay, "finalStateHash");
        for (var index = 0; index < actions.Count; index++)
        {
            if (actions[index] is not JsonObject action)
                throw new StoredDocumentException($"battle replay action {index} must be an object");
            try { battle.Dispatch(action); }
            catch (IllegalActionException error) { return new(battle.Snapshot(), index, "Action refused during replay.", error.Refusal); }
            if (StateJson.Text(battle.Snapshot(), "phase") != "over") continue;
            if (index != actions.Count - 1) return new(battle.Snapshot(), index + 1, "对局提前结束");
            return battle.StateHash == expectedHash
                ? new(battle.Snapshot(), null, "")
                : new(battle.Snapshot(), actions.Count, "回放最终状态摘要不匹配");
        }
        return battle.StateHash == expectedHash
            ? new(battle.Snapshot(), null, "")
            : new(battle.Snapshot(), actions.Count, "回放最终状态摘要不匹配");
    }

    private static void RequireShape(JsonObject replay)
    {
        foreach (var (field, kind) in new (string, StoredKind)[]
        {
            ("schema", StoredKind.Integer), ("levelId", StoredKind.String), ("seed", StoredKind.Number),
            ("ruleset", StoredKind.Object), ("initialStateHash", StoredKind.String),
            ("actions", StoredKind.Array), ("finalStateHash", StoredKind.String),
        })
            if (!StoredSchema.Matches(replay[field], kind))
                throw new StoredDocumentException($"battle replay has invalid {field}");
    }
}
