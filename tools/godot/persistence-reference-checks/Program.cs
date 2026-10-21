using Empire.Checks;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Persistence;
using Empire.Core.References;
using Empire.Core.Scenario;
using Empire.Core.State;
using Keys = Empire.Core.Composition.BattleCapabilities;

var root = Path.GetFullPath(args[0]);
JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, path)))!;
using var compressed = File.OpenRead(args[1]); using var zip = new GZipStream(compressed, CompressionMode.Decompress);
var oracle = JsonNode.Parse(zip)!.AsObject();
var packs = Read("godot/data/content-packs.json").AsArray();
var engine = new GrayFlagEngine(packs);
var ordinaryFixture = Read("godot/tests/oracle/grayflag-0.58.json");
var initial = ordinaryFixture["initialState"]!.AsObject();
var level = ordinaryFixture["level"]!.AsObject();
const string savedAt = "2026-01-01T00:00:00.000Z";
var passed = 0; var failed = 0;
void Check(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
}
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
void Equal(JsonNode expected, JsonNode actual, string message) => Assert(EcmaJson.DeepEquals(expected, actual), message + $" ({StateDigest.Hash(expected)} != {StateDigest.Hash(actual)})");
T Throws<T>(Action action, string? contains = null) where T : Exception
{
    try { action(); } catch (T error) { Assert(contains is null || error.Message.Contains(contains, StringComparison.Ordinal), error.Message); return error; }
    throw new Exception("expected " + typeof(T).Name);
}
RuleReferenceRules Rules(GrayFlagEngine value) => new()
{
    Content = value.Catalog, Grids = value.Grids, Abilities = value.Abilities, HitEffects = value.Combat.HitEffects,
    Objectives = value.Objectives, ScenarioConditions = value.ScenarioConditions, ScenarioEffects = value.ScenarioEffects,
    Reactions = value.Reactions, TurnOrders = value.TurnOrders, AreaShapes = value.AreaShapes, Directives = value.Directives,
};
JsonObject Wait(JsonObject state)
{
    var owner = state["currentPlayer"]!.GetValue<int>();
    var unit = state["units"]!.AsArray().OfType<JsonObject>().First(unit => unit["owner"]!.GetValue<int>() == owner && !unit["done"]!.GetValue<bool>());
    return new() { ["kind"] = "command", ["unit"] = unit["id"]!.DeepClone(), ["path"] = new JsonArray(new JsonObject { ["x"] = unit["x"]!.DeepClone(), ["y"] = unit["y"]!.DeepClone() }), ["command"] = new JsonObject { ["ability"] = "wait" } };
}

foreach (var entry in oracle["cases"]!.AsArray().OfType<JsonObject>()) Check("source differential: " + entry["name"], () =>
{
    var sourceSave = entry["save"]!.AsObject();
    var sourceBefore = sourceSave.ToJsonString();
    var save = FixtureRuleset.NativeDocument(sourceSave);
    var before = save.ToJsonString();
    var expectedError = entry["loadError"]?.GetValue<string>();
    if (expectedError is not null)
    {
        Assert(Throws<StoredDocumentException>(() => engine.LoadBattle(save)).Message == expectedError, "load refusal differs from source");
        var live = save["state"]!.AsObject(); var liveBefore = live.ToJsonString();
        Throws<DomainInvariantException>(() => engine.SaveBattle(live, savedAt), expectedError);
        Assert(live.ToJsonString() == liveBefore, "invalid save mutated live state");
    }
    else
    {
        var state = engine.LoadBattle(save);
        Assert(StateDigest.Hash(state) == entry["roundtripHash"]!.GetValue<string>(), "loaded hash differs from source");
        Equal(save["state"]!, engine.SaveBattle(state, savedAt)["state"]!, "roundtrip altered metadata");
        if (entry["action"] is JsonObject action)
        {
            var battle = engine.LoadSavedBattle(save); var hash = battle.StateHash;
            var actionError = entry["actionError"]?.GetValue<string>();
            if (actionError is null)
            {
                var receipt = battle.Dispatch(action);
                Equal(entry["events"]!, new JsonArray(receipt.Events.Select(e => (JsonNode)e.DeepClone()).ToArray()), "event stream differs from source");
            }
            else
            {
                Assert(Throws<DomainInvariantException>(() => battle.Dispatch(action)).Message == actionError, "action refusal differs from source");
                Assert(entry["atomic"]!.GetValue<bool>() && battle.StateHash == hash, "failed action was not atomic");
            }
            Equal(entry["after"]!, battle.Snapshot(), "post-action state differs from source");
            if (entry["postActionSaveError"] is { } failedSave)
                Assert(Throws<DomainInvariantException>(() => engine.SaveBattle(battle.Snapshot(), savedAt)).Message == failedSave.GetValue<string>(), "post-action save boundary differs from source");
            else Equal(battle.Snapshot(), engine.LoadBattle(engine.SaveBattle(battle.Snapshot(), savedAt)), "post-action roundtrip differs");
        }
    }
    Assert(save.ToJsonString() == before, "load modified input document");
    Assert(sourceSave.ToJsonString() == sourceBefore, "native identity declaration changed the source fixture");
});

foreach (var entry in oracle["catalogCases"]!.AsArray().OfType<JsonObject>()) Check("source catalog: " + entry["name"], () =>
{
    var catalogPacks = entry["packs"]!.AsArray(); var before = catalogPacks.ToJsonString();
    var expectedError = entry["error"]?.GetValue<string>();
    if (expectedError is not null)
    {
        var error = Throws<DomainInvariantException>(() => new GrayFlagEngine(catalogPacks));
        Assert(error.Message == expectedError, "catalog refusal differs from source: " + error.Message);
        Assert(error.GetType().Name.Replace("Exception", "Error", StringComparison.Ordinal) == entry["errorName"]!.GetValue<string>(), "catalog error category differs");
    }
    else
    {
        var custom = new GrayFlagEngine(catalogPacks); var save = FixtureRuleset.NativeDocument(entry["saved"]!.AsObject());
        Equal(save, custom.SaveBattle(initial, savedAt), "source catalog save differs");
        Equal(initial, custom.LoadBattle(save), "source catalog load differs");
    }
    Assert(catalogPacks.ToJsonString() == before, "catalog admission mutated input");
});
foreach (var entry in oracle["levelCases"]!.AsArray().OfType<JsonObject>()) Check("source authored level: " + entry["name"], () =>
{
    var authored = entry["level"]!.AsObject(); var before = authored.ToJsonString();
    Exception? caught = null; JsonObject? state = null;
    try { state = engine.CreateState(authored, 99); } catch (Exception error) when (error is StoredDocumentException or DomainInvariantException) { caught = error; }
    Assert(caught?.Message == entry["error"]?.GetValue<string>(), "level refusal differs from source: " + caught?.Message);
    Assert(caught?.GetType().Name.Replace("Exception", "Error", StringComparison.Ordinal) == entry["errorName"]?.GetValue<string>(), "level error category differs");
    if (state is not null) Equal(entry["state"]!, state, "source authored state differs");
    Assert(authored.ToJsonString() == before, "level admission mutated input");
});
foreach (var authored in Read("godot/data/grayflag-campaign-levels.json").AsArray().OfType<JsonObject>())
    Check("retained campaign roundtrip: " + authored["id"], () =>
    {
        var state = engine.CreateState(authored); var save = engine.SaveBattle(state, savedAt);
        Equal(state, engine.LoadBattle(save), "campaign roundtrip changed state");
    });

Check("default registry keys preserve source order", () => Assert(string.Join(",", engine.ReferenceChecks.Keys()) ==
    "abilities,hitEffects,areaShapes,reactions,directives,turnOrders,grids,objectives,scenarioConditions,scenarioEffects", "default check order differs"));
Check("check registry clone, replacement order, sealing and stable dedupe", () =>
{
    var checks = new BattleReferenceCheckRegistry()
        .Register(new("a", "标签", _ => []) { InState = (_, _) => [new("owner", "one"), new("owner", "one"), new("owner", "two")] })
        .Register(new("b", "标签", _ => []) { InState = (_, _) => [new("owner", "two"), new("owner", "three")] });
    var clone = checks.Clone(); clone.Replace(new("a", "标签", _ => []) { InState = (_, _) => [new("owner", "changed")] });
    Assert(string.Join(",", clone.Keys()) == "a,b", "replace moved check");
    Assert(checks.StateIssues(Rules(engine), initial).SequenceEqual(new[] { "owner 引用了未注册的标签「one」", "owner 引用了未注册的标签「two」", "owner 引用了未注册的标签「three」" }), "stable first-seen dedupe failed");
    Assert(clone.StateIssues(Rules(engine), initial)[0].Contains("changed", StringComparison.Ordinal), "replacement unused");
    Assert(checks.StateIssues(Rules(engine), initial)[0].Contains("one", StringComparison.Ordinal), "clone contaminated original");
    Throws<DomainInvariantException>(() => checks.Register(checks.Get("a")));
    checks.Seal(); Throws<DomainInvariantException>(() => checks.Replace(checks.Get("a")));
    Assert(!checks.Clone().IsSealed, "clone remained sealed");
});
Check("custom child owner controls recursive references and unknown stops traversal", () =>
{
    var handlers = engine.ScenarioConditions.Clone().Register(new("test.container", (_, _) => true)
    { Children = condition => [condition["payload"]!.AsObject()] });
    var state = initial.DeepClone().AsObject();
    state["scenario"]!["triggers"] = new JsonArray(new JsonObject { ["id"] = "nested", ["condition"] = new JsonObject
    { ["type"] = "test.container", ["payload"] = new JsonObject { ["type"] = "missing", ["conditions"] = new JsonArray(new JsonObject { ["type"] = "should-not-walk" }) } }, ["effects"] = new JsonArray() });
    var issues = engine.ReferenceChecks.StateIssues(Rules(engine) with { ScenarioConditions = handlers }, state);
    Assert(issues.Count == 1 && issues[0].Contains("「missing」", StringComparison.Ordinal), "tree bypassed actual owner or traversed unknown shape");
    handlers.Replace(new("test.container", (_, _) => true));
    Assert(engine.ReferenceChecks.StateIssues(Rules(engine) with { ScenarioConditions = handlers }, state).Count == 0, "replaced metadata not respected");
});
Check("handler references determine custom directive admission", () =>
{
    var effects = engine.ScenarioEffects.Clone().Register(new("test.issue-order", (_, _) => { })
    { References = effect => new PayloadReferences().Directive(effect["order"]!.GetValue<string>()) });
    var custom = BattleEngineFactory.Create(engine.Catalog, [new("test.directive-metadata", 1,
        context => context.Replace(Keys.ScenarioEffects, effects), overrides: [Keys.ScenarioEffects])]);
    var authored = level.DeepClone().AsObject();
    authored["scenario"]!["triggers"]!.AsArray().Add(new JsonObject { ["id"] = "custom-order", ["timing"] = "afterAction",
        ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 999 },
        ["effects"] = new JsonArray(new JsonObject { ["type"] = "test.issue-order", ["order"] = "absent-order" }) });
    Throws<StoredDocumentException>(() => custom.CreateState(authored), "未注册的常驻命令「absent-order」");
});
Check("custom check participates at content, level and state boundaries", () =>
{
    var contentCheck = engine.ReferenceChecks.Clone().Register(new("test.content", "目录声明", _ => [])
    { InContent = _ => [new("custom content", "absent")] });
    Throws<DomainInvariantException>(() => BattleEngineFactory.Create(engine.Catalog, [new("test.content-check", 1,
        context => context.Replace(Keys.ReferenceChecks, contentCheck), overrides: [Keys.ReferenceChecks])]), "custom content");
    var checks = engine.ReferenceChecks.Clone().Register(new("test.dynamic", "扩展声明", _ => [])
    {
        InLevel = (document, _) => document["probe"] is null ? [] : [new("custom level", "absent")],
        InState = (state, _) => state["probe"] is null ? [] : [new("custom state", "absent")],
    });
    var custom = BattleEngineFactory.Create(engine.Catalog, [new("test.dynamic-check", 1,
        context => context.Replace(Keys.ReferenceChecks, checks), overrides: [Keys.ReferenceChecks])]);
    var authored = level.DeepClone().AsObject(); authored["probe"] = true;
    Throws<StoredDocumentException>(() => custom.CreateState(authored), "custom level");
    var live = initial.DeepClone().AsObject(); live["probe"] = true;
    Throws<DomainInvariantException>(() => custom.SaveBattle(live, savedAt), "custom state");
    var save = custom.SaveBattle(initial, savedAt); save["state"]!["probe"] = true;
    Throws<StoredDocumentException>(() => custom.LoadBattle(save), "custom state");
});
Check("custom state check failures are classified at the save boundary", () =>
{
    var checks = engine.ReferenceChecks.Clone().Register(new("test.throwing", "故障", _ => [])
    { InState = (_, _) => throw new Exception("custom reference failure") });
    var custom = BattleEngineFactory.Create(engine.Catalog, [new("test.throwing-check", 1,
        context => context.Replace(Keys.ReferenceChecks, checks), overrides: [Keys.ReferenceChecks])]);
    var raw = engine.SaveBattle(initial, savedAt); raw["ruleset"] = custom.RulesetManifest;
    var error = Throws<StoredDocumentException>(() => custom.LoadBattle(raw), "checkRuleReferences");
    Assert(error.InnerException?.Message == "custom reference failure", "save boundary lost failure cause");
    Throws<DomainInvariantException>(() => custom.SaveBattle(initial, savedAt), "checkRuleReferences");
});
Check("named reader override sees actual environment and owns save and load", () =>
{
    var reader = new ProbeReader();
    var custom = BattleEngineFactory.Create(engine.Catalog, [new("test.save-reader", 3,
        context => context.Replace(Keys.Saves, reader), overrides: [Keys.Saves])]);
    var save = custom.SaveBattle(initial, savedAt);
    Equal(initial, custom.LoadBattle(save), "reader delegation changed state");
    Assert(reader.Calls == 2 && reader.Environment is { } environment && ReferenceEquals(environment.Rules.Content, custom.Catalog)
        && ReferenceEquals(environment.ReferenceChecks, custom.ReferenceChecks) && ReferenceEquals(environment.Rules.Objectives, custom.Objectives)
        && ReferenceEquals(environment.Rules.ScenarioConditions, custom.ScenarioConditions) && ReferenceEquals(environment.Rules.HitEffects, custom.Combat.HitEffects), "reader received copied ID tables or substitute owners");
    Assert(save["ruleset"]!["plugins"]!["test.save-reader"]!.GetValue<double>() == 3, "named reader omitted from manifest");
    reader.Refuse = true; var unchanged = initial.ToJsonString();
    Throws<StoredDocumentException>(() => custom.LoadBattle(save), "custom reader refusal");
    Throws<DomainInvariantException>(() => custom.SaveBattle(initial, savedAt), "custom reader refusal");
    Assert(initial.ToJsonString() == unchanged && reader.Calls == 4, "writer bypassed reader or changed state");
    Throws<StoredDocumentException>(() => engine.LoadBattle(save), "test.save-reader");
});
Check("named reference engine save and replay remain persistent", () =>
{
    var checks = engine.ReferenceChecks.Clone().Register(new("test.audit", "审计", _ => []) { InState = (_, _) => [] });
    var custom = BattleEngineFactory.Create(engine.Catalog, [new("test.named-references", 2,
        context => context.Replace(Keys.ReferenceChecks, checks), overrides: [Keys.ReferenceChecks])]);
    var battle = custom.CreateBattle(level, 99); var starting = battle.Snapshot(); var recorder = custom.RecordBattle(starting);
    var action = Wait(starting); battle.Dispatch(action); recorder.Record(action);
    var save = custom.SaveBattle(battle.Snapshot(), savedAt); Equal(battle.Snapshot(), custom.LoadBattle(save), "named save changed state");
    var result = custom.ReplayBattle(level, recorder.Replay(battle.Snapshot()));
    Assert(result.DivergedAt is null && StateDigest.Hash(result.State) == battle.StateHash, "named reference replay diverged");
});
Check("custom hit effect owns opaque status-named payload", () =>
{
    var customPacks = packs.DeepClone().AsArray();
    var weapon = customPacks.OfType<JsonObject>().SelectMany(pack => pack["weapons"]?.AsArray().OfType<JsonObject>() ?? []).First(value => value["id"]!.GetValue<string>() == "soldier_sword");
    weapon["hitEffects"] = new JsonArray(new JsonObject { ["type"] = "test.opaque", ["status"] = "not-a-status-id" });
    var effects = engine.Combat.HitEffects.Clone().Register(new("test.opaque", (_, _) => { }));
    var custom = BattleEngineFactory.Create(new ReadOnlyContent(customPacks), [new("test.opaque-hit-effect", 1,
        context => context.Replace(Keys.HitEffects, effects), overrides: [Keys.HitEffects])]);
    var save = custom.SaveBattle(initial, savedAt); Equal(initial, custom.LoadBattle(save), "opaque payload was treated as builtin status semantics");
});
Check("structural safeguards stay fail closed", () =>
{
    var save = engine.SaveBattle(initial, savedAt);
    foreach (var edit in new Action<JsonObject>[]
    {
        value => value["state"]!["map"]!["width"] = -1,
        value => value["state"]!["units"]![0]!["resources"]!["broken"] = new JsonObject { ["current"] = "wrong", ["capacity"] = null },
        value => value["state"]!["rules"]!["baseResourceGrants"]!.AsArray().Add(new JsonObject { ["resource"] = "unknown", ["amount"] = "wrong" }),
        value => value["state"]!["rules"]!["siteResourceOverrides"] = new JsonArray(),
        value => value["state"]!["nextUnitId"] = 1,
        value => value["state"]!["random"]!["counters"]!["broken"] = -1,
        value => value["state"]!.AsObject().Remove("embarkedUnits"),
    })
    {
        var bad = save.DeepClone().AsObject(); edit(bad); var before = bad.ToJsonString();
        Throws<StoredDocumentException>(() => engine.LoadBattle(bad)); Assert(bad.ToJsonString() == before, "structural rejection mutated input");
    }
});
if (failed > 0) throw new Exception($"{failed} reference/save reader checks failed; {passed} passed");
Console.WriteLine($"PASS {passed} reference/save reader differential and owner contract checks");

sealed class ProbeReader : IBattleSaveReader
{
    private readonly BattleSaveReader inner = new();
    public int Calls { get; private set; }
    public bool Refuse { get; set; }
    public BattleSaveEnvironment? Environment { get; private set; }
    public JsonObject Header(JsonNode? raw) => inner.Header(raw);
    public JsonObject Load(JsonNode? raw, BattleSaveEnvironment environment)
    {
        Calls++; Environment = environment;
        if (Refuse) throw new StoredDocumentException("custom reader refusal");
        return inner.Load(raw, environment);
    }
}
