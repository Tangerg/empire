using Empire.Checks;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Persistence;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, path)))!;
var packs = Read("godot/data/content-packs.json").AsArray();
var engine = new GrayFlagEngine(packs);
var manifest = engine.RulesetManifest;
var persistence = engine.Persistence;
var fixture = Read("godot/tests/oracle/grayflag-0.58.json").AsObject();
var good = FixtureRuleset.NativeDocument(fixture["finalSave"]!.AsObject());
var initial = fixture["initialState"]!.AsObject();
var passed = 0;
var failed = 0;

void Check(string name, Action test)
{
    try { test(); passed++; }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
}
void Equal(JsonNode expected, JsonNode actual, string subject)
{
    if (StateDigest.Hash(expected) != StateDigest.Hash(actual))
        throw new Exception($"{subject} mismatch: {StateDigest.Hash(expected)} != {StateDigest.Hash(actual)}");
}
void Throws<T>(Action test, string? contains = null) where T : Exception
{
    try { test(); }
    catch (T error)
    {
        if (contains is not null && !error.Message.Contains(contains, StringComparison.Ordinal))
            throw new Exception($"Expected error containing '{contains}', got '{error.Message}'");
        return;
    }
    throw new Exception($"Expected {typeof(T).Name}");
}
void Reject(string name, Action<JsonObject> edit, string? message = null)
{
    Check(name, () =>
    {
        var corrupt = (JsonObject)good.DeepClone();
        edit(corrupt);
        var original = StateDigest.Hash(corrupt);
        Throws<StoredDocumentException>(() => persistence.Load(corrupt), message);
        if (StateDigest.Hash(corrupt) != original) throw new Exception("rejected load mutated its input");
    });
}
void AdmitMetadata(string name, Action<JsonObject> edit)
{
    Check(name, () =>
    {
        var save = (JsonObject)good.DeepClone(); edit(save); var before = save.ToJsonString();
        var loaded = persistence.Load(save);
        Equal(save["state"]!, loaded, "accepted metadata load");
        Equal(save, persistence.Save(loaded, save["savedAt"]!.GetValue<string>()), "accepted metadata roundtrip");
        if (save.ToJsonString() != before) throw new Exception("accepted metadata load mutated input");
    });
}
JsonObject LiveSave() => persistence.Save(initial, "2026-01-01T00:00:00.000Z");

Check("declared native assembly identity", () => Equal(good["ruleset"]!, manifest, "manifest"));
Check("obsolete source assembly identity is rejected", () =>
    Throws<StoredDocumentException>(() => persistence.Load(fixture["finalSave"])));
foreach (var level in Read("godot/data/grayflag-campaign-levels.json").AsArray().OfType<JsonObject>())
{
    Check($"campaign opening save {level["id"]}", () =>
    {
        var state = engine.CreateState(level);
        var document = engine.SaveBattle(state, "2026-01-01T00:00:00.000Z");
        Equal(state, engine.LoadBattle(document), "campaign opening round trip");
    });
}
foreach (var aggression in new[] { "0.35", "0.58", "0.8" })
{
    var oracle = Read($"godot/tests/oracle/grayflag-{aggression}.json").AsObject();
    oracle["finalSave"] = FixtureRuleset.NativeDocument(oracle["finalSave"]!.AsObject());
    oracle["replay"] = FixtureRuleset.NativeDocument(oracle["replay"]!.AsObject());
    Check($"source final save {aggression}", () =>
    {
        Equal(oracle["finalSave"]!["state"]!, persistence.Load(oracle["finalSave"]), "loaded final state");
        Equal(oracle["finalSave"]!, persistence.Save(oracle["finalSave"]!["state"]!.AsObject(),
            oracle["finalSave"]!["savedAt"]!.GetValue<string>()), "saved document");
    });
    Check($"source replay and recorder {aggression}", () =>
    {
        var level = oracle["level"]!.AsObject();
        var replay = oracle["replay"]!.AsObject();
        var result = engine.ReplayBattle(level, replay);
        if (result.DivergedAt is not null) throw new Exception($"Diverged at {result.DivergedAt}: {result.Reason}");
        Equal(oracle["finalSave"]!["state"]!, result.State, "replayed final state");
        var recorder = new BattleRecorder(manifest, oracle["initialState"]!.AsObject());
        foreach (var action in replay["actions"]!.AsArray().OfType<JsonObject>()) recorder.Record(action);
        Equal(replay, recorder.Replay(result.State), "source recorder document");
    });
    Check($"midbattle resume {aggression}", () =>
    {
        var replay = oracle["replay"]!.AsObject();
        var original = engine.CreateBattle(oracle["level"]!.AsObject(), replay["seed"]!.GetValue<uint>());
        var actions = replay["actions"]!.AsArray().OfType<JsonObject>().ToArray();
        var split = actions.Length / 2;
        foreach (var action in actions.Take(split)) original.Dispatch(action);
        var document = engine.SaveBattle(original.Snapshot(), "2026-01-01T00:00:00.000Z");
        var resumed = engine.LoadSavedBattle(JsonNode.Parse(document.ToJsonString()));
        Equal(original.Snapshot(), resumed.Snapshot(), "resumed midpoint");
        foreach (var action in actions.Skip(split))
        {
            original.Dispatch(action);
            resumed.Dispatch(action);
            if (original.StateHash != resumed.StateHash) throw new Exception("resume diverged after an action");
        }
        Equal(oracle["finalSave"]!["state"]!, resumed.Snapshot(), "resumed final state");
    });
}

Check("save, load and header own detached data", () =>
{
    var state = (JsonObject)initial.DeepClone();
    var save = persistence.Save(state, "2026-01-01T00:00:00.000Z");
    var hash = StateDigest.Hash(save);
    state["units"]![0]!["hp"] = 1;
    if (StateDigest.Hash(save) != hash) throw new Exception("live state mutated save");
    var loaded = persistence.Load(save);
    loaded["units"]![0]!["hp"] = 2;
    var header = persistence.Header(save);
    header["turn"] = 999;
    if (StateDigest.Hash(save) != hash) throw new Exception("loaded state/header mutated save");
    save["state"]!["units"]![0]!["hp"] = 3;
    if (loaded["units"]![0]!["hp"]!.GetValue<int>() != 2) throw new Exception("save mutated loaded state");
});
Check("save exact top-level ownership", () =>
{
    if (!LiveSave().Select(pair => pair.Key).SequenceEqual(new[] { "schema", "battle", "ruleset", "savedAt", "state" }))
        throw new Exception("save contains session state or wrong boundary fields");
});
Check("header does not accept or load damaged battle content", () =>
{
    var damaged = (JsonObject)good.DeepClone();
    damaged["state"] = null;
    Equal(good["battle"]!, persistence.Header(damaged), "independent header");
    Throws<StoredDocumentException>(() => persistence.Load(damaged));
});
Check("engine manifest is detached", () =>
{
    var exposed = engine.RulesetManifest;
    exposed["plugins"]!["engine.content"] = 999;
    Equal(good["ruleset"]!, engine.RulesetManifest, "engine manifest ownership");
});
foreach (var field in good["state"]!.AsObject().Select(pair => pair.Key))
    Reject($"missing state field {field}", save => save["state"]!.AsObject().Remove(field), $"战斗的「{field}」缺失或损坏");
Reject("missing map tiles", save => save["state"]!["map"]!.AsObject().Remove("tiles"), "地图的「tiles」");
Reject("missing random seed", save => save["state"]!["random"]!.AsObject().Remove("seed"), "随机流的「seed」");
Reject("null unit", save => save["state"]!["units"]![0] = null);
Reject("header mismatch", save => save["battle"]!["turn"] = 999, "摘要与战斗状态不一致");
Reject("invalid timestamp", save => save["savedAt"] = "not-a-date", "保存时间");
Reject("unknown content unit", save => save["state"]!["units"]![0]!["type"] = "missing-unit", "目录里没有");
Reject("unknown weapon state", save => save["state"]!["units"]![0]!["weaponState"]!["unknown"] = new JsonObject(), "武器");
Reject("unknown directive", save => save["state"]!["units"]![0]!["directive"]!["mode"] = "unknown", "常驻命令");
Reject("unknown reaction", save => save["state"]!["units"]![0]!["reaction"] = "unknown", "反应姿态");
Reject("unknown learned ability", save => save["state"]!["units"]![0]!["learnedAbilities"]!.AsArray().Add("unknown"), "能力");
Reject("unknown grid", save => save["state"]!["rules"]!["grid"] = "unknown", "棋盘几何");
Reject("unknown turn policy", save => save["state"]!["turnOrder"]!["policy"] = "unknown", "行动顺序策略");
Reject("unknown objective", save => save["state"]!["players"]![0]!["objectives"]![0]!["type"] = "unknown", "目标类型");
AdmitMetadata("unknown nested condition stays stored until evaluated", save => save["state"]!["players"]![0]!["objectives"]![0]!["condition"]!["type"] = "unknown");
Reject("unknown scenario effect", save => save["state"]!["scenario"]!["triggers"]![0]!["effects"]![0]!["type"] = "unknown", "场景效果");
Reject("wrong pack version", save => save["ruleset"]!["contentPacks"]!["empire.common"] = 999, "content pack \"empire.common\" version");
Reject("unknown plugin", save => save["ruleset"]!["plugins"]!["other"] = 1, "not part of the current ruleset");
Reject("missing plugin", save => save["ruleset"]!["plugins"]!.AsObject().Remove("engine.content"), "is missing");
Reject("fractional plugin version", save => save["ruleset"]!["plugins"]!["engine.content"] = 1.5, "版本");
Reject("offboard live unit", save => save["state"]!["units"]![0]!["x"] = 9999, "位置");
Reject("bad row-major array length", save => save["state"]!["map"]!["owners"]!.AsArray().RemoveAt(0), "格数");
Reject("negative random counter", save => save["state"]!["random"]!["counters"]!["bad"] = -1, "计数器");
Reject("fractional actor clock", save => save["state"]!["actorTurns"] = 1.5, "actorTurns");
Reject("next unit id collision", save => save["state"]!["nextUnitId"] = 1, "后续标识");
AdmitMetadata("unknown resource account remains inert", save => save["state"]!["players"]![0]!["resources"]!["unknown"] =
    new JsonObject { ["current"] = 1, ["capacity"] = null });
AdmitMetadata("unknown upkeep resource stays stored until granted", save => save["state"]!["rules"]!["baseResourceGrants"]!.AsArray().Add(
    new JsonObject { ["resource"] = "unknown", ["amount"] = 1 }));
Reject("negative resource balance", save => save["state"]!["players"]![0]!["resources"]!["funds"]!["current"] = -1, "资源数值");
Reject("stale deployment after battle start", save =>
{
    save["state"]!["deployment"] = new JsonObject { ["order"] = new JsonArray(1), ["currentIndex"] = 0, ["assignments"] = new JsonArray() };
}, "部署");
Reject("old schema", save => save["schema"] = 0, "unsupported battle save schema 0");
Reject("future schema", save => save["schema"] = 7, "unsupported battle save schema 7");
Check("invalid schema and document type", () =>
{
    Throws<StoredDocumentException>(() => persistence.Load(null), "must be an object");
    Throws<StoredDocumentException>(() => persistence.Load(new JsonArray()), "must be an object");
    Throws<StoredDocumentException>(() => persistence.Load(new JsonObject()), "invalid schema");
    Throws<StoredDocumentException>(() => persistence.Load(new JsonObject { ["schema"] = double.NaN }), "invalid schema");
    var nonfinite = (JsonObject)good.DeepClone();
    nonfinite["state"]!["turn"] = double.PositiveInfinity;
    Throws<StoredDocumentException>(() => persistence.Load(nonfinite), "turn");
});
Check("invalid live state cannot be saved", () =>
{
    var state = (JsonObject)initial.DeepClone();
    state["units"]![0]!["type"] = "missing";
    Throws<DomainInvariantException>(() => persistence.Save(state), "cannot save invalid live battle");
    Throws<DomainInvariantException>(() => persistence.Save(initial, "not-a-date"), "invalid timestamp");
});
Check("persistent manifests refuse anonymous rules and unversioned packs", () =>
{
    var anonymous = (JsonObject)manifest.DeepClone();
    anonymous["plugins"]!["engine.override.random"] = 1;
    Throws<DomainInvariantException>(() => new BattleRecorder(anonymous, initial), "unversioned override");
    var unversioned = (JsonObject)manifest.DeepClone();
    unversioned["contentPacks"] = new JsonObject();
    Throws<DomainInvariantException>(() => new BattleRecorder(unversioned, initial), "no versioned content pack");
});
Check("recorder owns action and output documents", () =>
{
    var action = (JsonObject)fixture["replay"]!["actions"]![0]!.DeepClone();
    var expected = (JsonObject)action.DeepClone();
    var recorder = new BattleRecorder(manifest, initial);
    recorder.Record(action);
    action["kind"] = "changed";
    var replay = recorder.Replay(initial);
    Equal(expected, replay["actions"]![0]!, "recorded action");
    replay["actions"]![0]!["kind"] = "changed-again";
    Equal(expected, recorder.Replay(initial)["actions"]![0]!, "replay snapshot");
    var foreign = (JsonObject)initial.DeepClone();
    foreign["levelId"] = "foreign";
    Throws<DomainInvariantException>(() => recorder.Replay(foreign), "cannot finish replay");
});
void Divergence(string name, Action<JsonObject> edit, int at, string reason)
{
    Check(name, () =>
    {
        var replay = FixtureRuleset.NativeDocument(fixture["replay"]!.AsObject());
        edit(replay);
        var result = BattleReplayVerifier.Verify(manifest, fixture["level"]!.AsObject(), replay,
            (level, seed) => engine.CreateBattle(level, seed));
        if (result.DivergedAt != at || !result.Reason.Contains(reason, StringComparison.Ordinal))
            throw new Exception($"Unexpected divergence {result.DivergedAt}: {result.Reason}");
    });
}
var actionCount = fixture["replay"]!["actions"]!.AsArray().Count;
Divergence("replay schema", replay => replay["schema"] = 7, 0, "schema 7");
Divergence("replay level", replay => replay["levelId"] = "other", 0, "不匹配");
Divergence("replay manifest", replay => replay["ruleset"]!["plugins"]!["engine.content"] = 2, 0, "version");
Divergence("replay initial hash", replay => replay["initialStateHash"] = "00000000", 0, "初始状态");
Divergence("replay illegal action", replay => replay["actions"]![0]!["kind"] = "unknown", 0, "行动类型");
Divergence("replay final hash", replay => replay["finalStateHash"] = "00000000", actionCount, "最终状态");
Divergence("replay early end", replay => replay["actions"]!.AsArray().Add(new JsonObject { ["kind"] = "endTurn" }), actionCount, "提前结束");
Check("replay invariant failures are not disguised as divergence", () =>
{
    Throws<DomainInvariantException>(() => BattleReplayVerifier.Verify(manifest, fixture["level"]!.AsObject(),
        fixture["replay"], (_, _) => throw new DomainInvariantException("intentional defect")), "intentional defect");
});
Console.WriteLine($"Persistence checks: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
