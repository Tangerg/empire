using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Campaign;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Objectives;
using Empire.Core.References;
using Empire.Core.State;
using Empire.Core.Scenario;
using Empire.Core.Validation;
using Empire.Localization;
using Empire.Presentation.Feedback;
using Keys = Empire.Core.Composition.BattleCapabilities;

if (args.Length != 2) throw new ArgumentException("Expected repository root and validated catalog.json paths.");
var root = Path.GetFullPath(args[0]);
var messages = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[1]))
    ?? throw new InvalidOperationException("The validated localization catalog must be an object.");
var packs = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
var checks = 0;
void Check(bool value, string label)
{
    checks++;
    if (!value)
        throw new InvalidOperationException(label);
}
void Reject<T>(Action operation, string label) where T : Exception
{
    try
    {
        operation();
    }
    catch (T) { Check(true, label); return; }
    Check(false, label);
}
JsonObject Copy(JsonObject value) => value.DeepClone().AsObject();
JsonObject Player(JsonObject state, int id) => state["players"]!.AsArray().OfType<JsonObject>()
    .Single(player => player["id"]!.GetValue<int>() == id);
JsonObject Objective(JsonObject state, int player) => Player(state, player)["objectives"]![0]!.AsObject();
JsonArray Events(IEnumerable<JsonObject> events) => new(events.Select(value => value.DeepClone()).ToArray());

// Native-authored semantic input, independent of source snapshots and compatibility adapters.
var level = EcmaJson.Parse("""
{
  "schema": 3,
  "id": "test.objective-outcomes",
  "width": 3,
  "height": 2,
  "terrain": ["...", "..."],
  "players": [
    {
      "id": 1, "team": 1, "color": "#336699", "controller": "human", "resources": {},
      "objectives": [
        {"id": "mission", "type": "all", "objectives": [
          {"id": "score", "type": "score", "variable": "points", "atLeast": 2},
          {"id": "signal", "type": "test.signal", "target": 7}
        ]}
      ]
    },
    {
      "id": 2, "team": 2, "color": "#993333", "controller": "ai", "resources": {},
      "objectives": [{"id": "score", "type": "score", "variable": "enemyPoints", "atLeast": 99}]
    }
  ],
  "units": [
    {"key": "first-guard", "unit": "soldier", "owner": 1, "x": 0, "y": 0},
    {"key": "second-guard", "unit": "soldier", "owner": 2, "x": 2, "y": 1}
  ],
  "scenario": {"variables": {"points": 2, "enemyPoints": 0, "signal": 7}},
  "rules": {"fog": false, "healOnOwnedBuilding": false}
}
""")!.AsObject();
const string metadata = """
{
  "schema": 3,
  "levels": {
    "test.objective-outcomes": {
      "actors": {},
      "title": {"key": "test.level.title"},
      "description": {"key": "test.level.description"},
      "signals": {},
      "players": {
        "1": {
          "name": {"key": "test.player.guard"},
          "objectives": {
            "mission": {"key": "test.objective.mission"},
            "score": {"key": "test.objective.score"},
            "signal": {"generated": true}
          }
        },
        "2": {
          "name": {"key": "test.player.rival"},
          "objectives": {"score": {"key": "test.objective.enemy_score"}}
        }
      }
    }
  }
}
""";

var assessments = 0;
BattleEngine Compose() => BattleEngineFactory.Create(new ReadOnlyContent(packs),
[
    new EnginePlugin("test.objective-outcomes", 1, context =>
        context.Replace(Keys.Objectives, context.Require(Keys.Objectives).Clone().Register(
            new ObjectiveHandler("test.signal", (query, definition) =>
            {
                assessments++;
                var observed = query.State["scenario"]!["variables"]!["signal"]!.GetValue<int>();
                var target = definition["target"]!.GetValue<int>();
                return new ObjectiveAssessment(observed >= target ? "success" : "pending",
                    new JsonObject { ["observed"] = observed, ["target"] = target });
            }))), overrides: [Keys.Objectives], requiresCapabilities: [Keys.Objectives]),
]);
var engine = Compose();
var peer = Compose();
var defaultPlayerInput = Copy(level);
defaultPlayerInput.Remove("players");
var defaultPlayerLevel = LevelNormalizer.Normalize(defaultPlayerInput);
var defaultPlayerBattle = engine.CreateBattle(defaultPlayerLevel, 2048);
Check(defaultPlayerBattle.Snapshot()["players"]!.AsArray().OfType<JsonObject>().All(player => !player.ContainsKey("name")),
    "native default-player normalization produces semantic identities accepted by battle admission");

GameText Catalog(bool alternate)
{
    var values = new Dictionary<string, string>(messages, StringComparer.Ordinal)
    {
        ["test.player.guard"] = alternate ? "Silver guard" : "Copper watch",
        ["test.player.rival"] = alternate ? "Mountain rivals" : "River rivals",
        ["test.objective.mission"] = alternate ? "Protect the record" : "Secure the archive",
        ["test.objective.score"] = alternate ? "Gather two records" : "Collect two reports",
        ["test.objective.enemy_score"] = alternate ? "Gather ninety-nine records" : "Collect ninety-nine reports",
        ["test.objective.signal.description"] = alternate ? "Raise beacon {0}" : "Establish signal {0}",
        ["test.objective.signal.progress"] = alternate ? "Beacon reading {0} of {1}" : "Signal {0}/{1}",
        ["test.signal.found"] = alternate ? "Record located" : "Archive discovered",
        ["test.level.title"] = alternate ? "Alternate mission" : "Semantic objective fixture",
        ["test.level.description"] = alternate ? "Alternate briefing" : "Native semantic fixture briefing",
        ["battle.conclusion.objective_completed"] = alternate ? "Goal reached by {0}: {1}" : "{0} completed {1}",
        ["test.content.unit"] = alternate ? "Silver infantry" : "Copper infantry",
        ["test.content.weapon"] = alternate ? "Silver sword" : "Copper sword",
        ["test.content.ability"] = alternate ? "Strike" : "Attack",
        ["test.content.hint"] = alternate ? "Strike the rival" : "Attack the enemy",
        ["test.content.bonus.first"] = alternate ? "First advantage" : "First bonus",
        ["test.content.bonus.second"] = alternate ? "Second advantage" : "Second bonus",
        ["test.content.modifier"] = alternate ? "Resolve reading {0}" : "Resolve {0}",
    }.ToFrozenDictionary(StringComparer.Ordinal);
    return new GameText(key => values.GetValueOrDefault(key), CultureInfo.InvariantCulture);
}
ObjectiveText Formatters(GameText text)
{
    var result = ObjectiveText.CreateDefault(text).Register(new ObjectiveTextFormatter("test.signal",
        ["test.objective.signal.description", "test.objective.signal.progress"],
        (catalog, definition, _) => catalog.Format("test.objective.signal.description", definition.GetProperty("target").GetInt32()),
        (catalog, objectiveProgress, _) => catalog.Format("test.objective.signal.progress",
            objectiveProgress.Facts.GetProperty("observed").GetInt32(), objectiveProgress.Facts.GetProperty("target").GetInt32())));
    result.ValidateCoverage(engine.Objectives.Keys());
    return result.Seal();
}
var firstCatalog = Catalog(false);
var secondCatalog = Catalog(true);
var firstFormatters = Formatters(firstCatalog);
var secondFormatters = Formatters(secondCatalog);
var textDocument = BattleTextDocument.Parse(new(new JsonArray(level.DeepClone()), engine.Objectives, engine.ScenarioEffects), metadata);
var firstText = new BattleText(new(firstCatalog, firstFormatters), textDocument);
var secondText = new BattleText(new(secondCatalog, secondFormatters), textDocument);
Check(firstText.LevelTitle("test.objective-outcomes") == "Semantic objective fixture" &&
    secondText.LevelTitle("test.objective-outcomes") == "Alternate mission" &&
    firstText.LevelDescription("test.objective-outcomes") != secondText.LevelDescription("test.objective-outcomes"),
    "level title and briefing follow the injected catalog without entering gameplay definitions");
var declaredSignalLevel = Copy(level);
declaredSignalLevel["scenario"]!["triggers"] = new JsonArray(new JsonObject
{
    ["id"] = "test.record",
    ["condition"] = new JsonObject { ["type"] = "turnAtLeast", ["turn"] = 99 },
    ["effects"] = new JsonArray(new JsonObject { ["type"] = "test.record", ["signal"] = "archive_found" }),
});
var signalHandlers = engine.ScenarioEffects.Clone().Register(new ScenarioEffectHandler("test.record", (context, effect) =>
    context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = effect["signal"]!.DeepClone() }))
{
    Signals = effect => new[] { effect["signal"]!.GetValue<string>() }
}).Seal();
Reject<LocalizationException>(() => BattleTextDocument.Parse(new(new JsonArray(declaredSignalLevel.DeepClone()), engine.Objectives, signalHandlers), metadata),
    "extension-declared signal requires presentation metadata without a built-in effect-kind switch");
var signalMetadata = EcmaJson.Parse(metadata)!.AsObject();
signalMetadata["levels"]!["test.objective-outcomes"]!["signals"]!["archive_found"] = new JsonObject { ["key"] = "test.signal.found" };
var signalDocument = BattleTextDocument.Parse(new(new JsonArray(declaredSignalLevel.DeepClone()), engine.Objectives, signalHandlers), EcmaJson.Stringify(signalMetadata));
var signalText = new BattleText(new(firstCatalog, firstFormatters), signalDocument);
Check(signalText.SignalName("test.objective-outcomes", "archive_found") == "Archive discovered", "semantic signal renders through its registered metadata");
Reject<LocalizationException>(() => signalText.SignalName("test.objective-outcomes", "undeclared"), "unknown signal never leaks a raw identifier to the result screen");

var mutableFacts = new JsonObject { ["nested"] = new JsonObject { ["values"] = new JsonArray(7) } };
var assessment = new ObjectiveAssessment("pending", mutableFacts);
mutableFacts["nested"]!["values"]![0] = 99;
Check(assessment.Facts.GetProperty("nested").GetProperty("values")[0].GetInt32() == 7,
    "assessment facts detach nested caller data");
var consumerFacts = EcmaJson.Parse(assessment.Facts.GetRawText())!.AsObject();
consumerFacts["nested"]!["values"]!.AsArray().Clear();
Check(assessment.Facts.GetProperty("nested").GetProperty("values").GetArrayLength() == 1,
    "assessment fact projections cannot mutate the original");
Check(engine.Objectives.Has("test.signal") && engine.Objectives.IsSealed,
    "an external plugin registers a custom objective through composition");
Reject<LocalizationException>(() => ObjectiveText.CreateDefault(firstCatalog).ValidateCoverage(engine.Objectives.Keys()),
    "missing extension presentation is rejected rather than silently falling back");

var battle = engine.CreateBattle(level, 2048);
var otherBattle = peer.CreateBattle(level, 2048);
var initial = battle.Snapshot();
var projectionSource = Copy(initial);
var progress = engine.Victory.Objectives(projectionSource, 1).Single();
var score = progress.Children.Single(child => child.Id == "score");
var signal = progress.Children.Single(child => child.Id == "signal");
Check(progress.Status == "active" && progress.Facts.GetProperty("completed").GetInt32() == 0
    && progress.Facts.GetProperty("total").GetInt32() == 2
    && firstText.ObjectiveProgress(progress) == firstCatalog.Format("objective.progress.all", 0, 2),
    "composite progress renders handler-owned counts");
foreach (var kind in new[] { "any", "all", "sequence" })
{
    var alternative = Copy(initial);
    Objective(alternative, 1)["type"] = kind;
    var alternativeProgress = engine.Victory.Objectives(alternative, 1).Single();
    Check(firstText.ObjectiveProgress(alternativeProgress) == firstCatalog.Format("objective.progress." + kind, 0, 2),
        $"{kind} progress describes its own completion semantics without changing handler-owned counts");
}
Check(score.Facts.GetProperty("current").GetProperty("kind").GetString() == "finite"
    && score.Facts.GetProperty("current").GetProperty("value").GetInt32() == 2
    && score.Facts.GetProperty("target").GetInt32() == 2
    && firstText.ObjectiveProgress(score) == firstCatalog.Format("objective.progress.score", 2, 2),
    "score progress renders typed numeric facts");
Objective(projectionSource, 1)["objectives"]![0]!["atLeast"] = 200;
Objective(projectionSource, 1)["objectives"]!.AsArray().Clear();
Check(score.Definition.GetProperty("atLeast").GetInt32() == 2 && progress.Children.Count == 2
    && progress.Definition.GetProperty("objectives").GetArrayLength() == 2,
    "progress definitions and child projections detach nested gameplay definitions");
projectionSource["scenario"]!["variables"]!["points"] = 900;
var projectedFacts = EcmaJson.Parse(score.Facts.GetRawText())!.AsObject();
projectedFacts["current"]!["value"] = 800;
Check(score.Facts.GetProperty("current").GetProperty("value").GetInt32() == 2,
    "progress facts remain isolated from state changes and consumer copies");
Reject<NotSupportedException>(() => ((IList<ObjectiveProgress>)progress.Children).Clear(),
    "progress children cannot be changed through a mutable interface");
var callsBeforeFormatting = assessments;
Check(firstText.ObjectiveDescription("test.objective-outcomes", 1, signal) == "Establish signal 7"
    && firstText.ObjectiveProgress(signal) == "Signal 7/7" && assessments == callsBeforeFormatting,
    "custom registered formatting reads immutable facts without reevaluating rules");

foreach (var (status, outcome) in new[]
{
    ("completed", "success"), ("failed", "failure"), ("inactive", "pending"), ("cancelled", "pending"),
})
{
    var terminalState = Copy(initial);
    Player(terminalState, 1)["objectiveStates"]!["signal"]!["status"] = status;
    var definition = Objective(terminalState, 1)["objectives"]![1]!.AsObject();
    var before = assessments;
    var terminal = engine.Objectives.Project(engine.Victory.Rules, terminalState, 1, definition);
    var result = engine.Objectives.Assess(engine.Victory.Rules, terminalState, 1, definition);
    Check(result.Outcome == outcome && !result.Facts.EnumerateObject().Any()
        && firstFormatters.Progress(terminal) == firstCatalog.Format("objective.status." + status)
        && assessments == before, $"terminal {status} bypasses assessment and uses its status translation");
}

var recorder = engine.RecordBattle(initial);
var otherRecorder = peer.RecordBattle(otherBattle.Snapshot());
var action = new JsonObject { ["kind"] = "endTurn" };
var receipt = battle.Dispatch(action);
recorder.Record(action);
var otherReceipt = otherBattle.Dispatch(action);
otherRecorder.Record(action);
var final = battle.Snapshot();
var conclusion = BattleConclusion.FromState(final)!;
Check(final["phase"]!.GetValue<string>() == "over" && conclusion.Kind == "objectiveCompleted"
    && conclusion.Team == 1 && conclusion.Player == 1 && conclusion.Objective == "mission",
    "real endTurn action reaches BattleLifecycle objective completion with both teams alive");
var gameOver = receipt.Events.Single(entry => entry["type"]!.GetValue<string>() == "gameOver");
Check(gameOver.Count == 2 && EcmaJson.DeepEquals(gameOver["conclusion"], conclusion.ToJson())
    && !final.ContainsKey("winnerTeam") && !final.ContainsKey("endReason"),
    "gameOver publishes only the canonical semantic conclusion");
var committedHash = battle.StateHash;
gameOver["conclusion"]!["objective"] = "consumer-change";
Check(battle.StateHash == committedHash
    && receipt.Events.Single(entry => entry["type"]!.GetValue<string>() == "gameOver")["conclusion"]!["objective"]!.GetValue<string>() == "mission",
    "event conclusion projections cannot mutate receipt or committed state");
var detachedState = battle.Snapshot();
detachedState["conclusion"]!["team"] = 999;
var detachedConclusion = conclusion.ToJson();
detachedConclusion["objective"] = "consumer-change";
Check(battle.StateHash == committedHash && conclusion.Objective == "mission"
    && BattleConclusion.FromState(battle.Snapshot())!.Team == 1, "state and conclusion JSON projections are detached");

var otherFinal = otherBattle.Snapshot();
var save = engine.SaveBattle(final, "2026-10-21T00:00:00.000Z");
var otherSave = peer.SaveBattle(otherFinal, "2026-10-21T00:00:00.000Z");
var replay = recorder.Replay(final);
var otherReplay = otherRecorder.Replay(otherFinal);
var finalElement = EcmaJson.ToElement(final);
var otherElement = EcmaJson.ToElement(otherFinal);
Check(firstText.PlayerName("test.objective-outcomes", 1) == "Copper watch"
    && secondText.PlayerName("test.objective-outcomes", 1) == "Silver guard", "catalog substitution changes player wording");
Check(firstText.ObjectiveDescription("test.objective-outcomes", 1, progress) == "Secure the archive"
    && secondText.ObjectiveDescription("test.objective-outcomes", 1, progress) == "Protect the record",
    "catalog substitution changes authored objective wording");
Check(firstText.DescribeConclusion(finalElement) == "Copper watch completed Secure the archive"
    && secondText.DescribeConclusion(otherElement) == "Goal reached by Silver guard: Protect the record",
    "catalog substitution changes outcome wording without changing semantic identities");
callsBeforeFormatting = assessments;
Check(secondText.ObjectiveProgress(signal) == "Beacon reading 7 of 7"
    && firstText.DescribeConclusion(finalElement, EcmaJson.ToElement(receipt.Events.Single(entry => entry["type"]!.GetValue<string>() == "gameOver")["conclusion"]))
        == firstText.DescribeConclusion(finalElement)
    && assessments == callsBeforeFormatting, "both outcome surfaces and custom progress format without recounting rules");
Check(StateDigest.Hash(final) == StateDigest.Hash(otherFinal) && battle.StateHash == committedHash,
    "different presentation catalogs leave full state hashes equal");
Check(StateDigest.Hash(Events(receipt.Events)) == StateDigest.Hash(Events(otherReceipt.Events)),
    "different presentation catalogs leave full event hashes equal");
Check(StateDigest.Hash(save) == StateDigest.Hash(otherSave), "different presentation catalogs leave full save hashes equal");
Check(StateDigest.Hash(replay) == StateDigest.Hash(otherReplay), "different presentation catalogs leave full replay hashes equal");
var loaded = engine.LoadBattle(save);
var replayed = engine.ReplayBattle(level, replay);
Check(StateDigest.Hash(loaded) == committedHash && replayed.DivergedAt is null
    && StateDigest.Hash(replayed.State) == committedHash, "save/load and replay preserve the complete concluded state hash");

void RejectConclusion(Action<JsonObject> change, string label)
{
    var invalid = Copy(final);
    change(invalid);
    Reject<DomainInvariantException>(() => BattleConclusion.FromState(invalid), label);
}
RejectConclusion(state => state["conclusion"] = new JsonObject { ["kind"] = "translatedVictory", ["team"] = null },
    "unknown conclusion kind is rejected even with otherwise valid fields");
RejectConclusion(state => state["conclusion"]!["reason"] = "Localized victory", "unexpected conclusion prose field is rejected");
RejectConclusion(state => state["conclusion"]!.AsObject().Remove("team"), "missing conclusion field is rejected");
RejectConclusion(state => state["conclusion"]!["team"] = 77, "unknown winning team is rejected");
RejectConclusion(state => state["phase"] = "playing", "phase and conclusion mismatch is rejected");
RejectConclusion(state => state["conclusion"]!["player"] = 77, "unknown winning player is rejected");
RejectConclusion(state => state["conclusion"]!["player"] = 2, "winning player must belong to winning team");
RejectConclusion(state => state["conclusion"]!["objective"] = "missing", "unknown winning objective is rejected");
RejectConclusion(state => Player(state, 1)["objectiveStates"]!["mission"]!["status"] = "active",
    "winning objective must actually be completed");
void RejectSave(Action<JsonObject> change, string label)
{
    var invalid = Copy(save);
    change(invalid);
    Reject<StoredDocumentException>(() => engine.LoadBattle(invalid), label);
}
RejectSave(document => document["schema"] = 1, "old save schema 1 is rejected without migration");
RejectSave(document => document["schema"] = 2, "previous native battle save schema is rejected without migration");
RejectSave(document => document["battle"]!["levelName"] = "Obsolete title", "save header rejects obsolete title even under the current schema");
RejectSave(document => document["state"]!["levelName"] = "Obsolete title", "save state rejects obsolete title even under the current schema");
var obsoleteLevel = Copy(level);
obsoleteLevel["name"] = "Obsolete title";
Reject<StoredDocumentException>(() => LevelNormalizer.Normalize(obsoleteLevel), "level normalizer rejects superseded wording fields");
Reject<BattleLevelException>(() => engine.CreateBattle(obsoleteLevel), "direct battle admission rejects superseded wording fields");
Check(!final.ContainsKey("levelName") && !save["battle"]!.AsObject().ContainsKey("levelName"), "no title is persisted in native battle facts");
var extraField = Copy(final);
extraField["levelName"] = "Unexpected fact";
Check(StateDigest.Hash(extraField) != StateDigest.Hash(final), "digest no longer silently ignores a field named levelName");
RejectSave(document => document["state"]!["endReason"] = "旧的胜利描述", "current save shape rejects legacy outcome prose");
RejectSave(document => Player(document["state"]!.AsObject(), 1)["name"] = "旧的玩家名称",
    "current save shape rejects player-name prose");
RejectSave(document => Objective(document["state"]!.AsObject(), 1)["objectives"]![0]!["label"] = "旧的目标描述",
    "current save shape rejects nested objective-label prose");

var campaignDefinition = EcmaJson.Parse("""
{
  "schema": 1,
  "id": "test.objective-result-admission",
  "version": 1,
  "start": "archive",
  "contentPacks": {"empire.common": 1, "empire.ancient-empires": 1, "candidate-01": 1},
  "nodes": [
    {"id": "archive", "type": "battle", "level": "test.objective-outcomes", "perspectivePlayer": 1,
     "rosterBindings": [], "next": {}}
  ]
}
""")!.AsObject();
var campaign = new CampaignRuntime(CampaignRules.Create(), campaignDefinition);
var bridge = new CampaignBattleBridge(new CampaignBattleBridgeRules(levelId => levelId == "test.objective-outcomes" ? Copy(level)
    : throw new InvalidOperationException("Unknown fixture level."), engine.Catalog, Empire.Core.Engine.LevelNormalizer.Normalize, GrayFlagCampaignPolicy.MarkerDispositions));
var request = campaign.BeginBattle(bridge);
var pendingCampaign = campaign.Snapshot();
var campaignBattle = engine.CreateBattle(request["level"]!.AsObject(), 2048);
var campaignReceipt = campaignBattle.Dispatch(action);
var battleResult = bridge.Result(request, campaignBattle.Snapshot(), campaignReceipt.Events);
campaign.CompleteBattle(battleResult);
var completedCampaign = campaign.Snapshot();
Check(completedCampaign["status"]!.GetValue<string>() == "completed"
    && completedCampaign["pendingBattle"] is null
    && completedCampaign["battleHistory"]!.AsArray().Count == 1
    && completedCampaign["battleHistory"]![0]!["outcome"]!.GetValue<string>() == "victory",
    "campaign accepts the actual bridge result from its requested concluded native battle");
var rejectingCampaign = new CampaignRuntime(CampaignRules.Create(), campaignDefinition, pendingCampaign);
var beforeRejectedResult = rejectingCampaign.Snapshot();
var obsoleteResult = Copy(battleResult);
obsoleteResult["reason"] = "旧的胜利描述";
obsoleteResult["winnerTeam"] = 1;
var rejectedResult = false;
try
{
    rejectingCampaign.CompleteBattle(obsoleteResult);
}
catch (CampaignInvariantException) { rejectedResult = true; }
Check(rejectedResult && EcmaJson.DeepEquals(beforeRejectedResult, rejectingCampaign.Snapshot())
    && StateDigest.Hash(beforeRejectedResult) == StateDigest.Hash(rejectingCampaign.Snapshot()),
    "campaign rejects obsolete result prose atomically without consuming the pending battle");

var modifierFacts = new JsonObject { ["nested"] = new JsonObject { ["readings"] = new JsonArray(7) } };
var customModifier = new CombatModifier("test.resolve", "test.resolve", "test", "power", "multiply", 1.25, modifierFacts);
modifierFacts["nested"]!["readings"]![0] = 99;
var detachedModifierFacts = EcmaJson.Parse(customModifier.Facts.GetRawText())!.AsObject();
detachedModifierFacts["nested"]!["readings"]!.AsArray().Clear();
Check(customModifier.Facts.GetProperty("nested").GetProperty("readings").GetArrayLength() == 1
    && customModifier.Facts.GetProperty("nested").GetProperty("readings")[0].GetInt32() == 7,
    "combat modifier facts deeply detach provider input and consumer projections");

var contentPacks = packs.DeepClone().AsArray();
JsonObject Sword(JsonArray source) => source.OfType<JsonObject>().Where(pack => pack["weapons"] is JsonArray)
    .SelectMany(pack => pack["weapons"]!.AsArray().OfType<JsonObject>())
    .Single(weapon => weapon["id"]!.GetValue<string>() == "soldier_sword");
Sword(contentPacks)["bonuses"] = new JsonArray(
    new JsonObject { ["id"] = "first", ["targetTag"] = "infantry", ["multiplier"] = 1.5 },
    new JsonObject { ["id"] = "second", ["targetTag"] = "infantry", ["multiplier"] = 2 });
var modifierEvaluations = 0;
BattleEngine ComposeContent(JsonArray source) => BattleEngineFactory.Create(new ReadOnlyContent(source),
[
    new EnginePlugin("test.content-presentation", 1, context =>
    {
        context.Replace(Keys.Abilities, context.Require(Keys.Abilities).Clone().Register(new AbilityDefinition("test.inspect", AbilityTargetKind.Self)));
        context.Replace(Keys.CombatModifiers, context.Require(Keys.CombatModifiers).Register(new CombatModifierProvider(
            "test.resolve", 550, _ => { modifierEvaluations++; return new[] { customModifier }; })));
    }, overrides: [Keys.Abilities, Keys.CombatModifiers], requiresCapabilities: [Keys.Abilities, Keys.CombatModifiers]),
]);
var contentEngine = ComposeContent(contentPacks);
var contentDefinitions = new ContentTextDefinitions(contentEngine.Catalog, contentEngine.Abilities,
    contentEngine.Reactions, contentEngine.Resources.Adapters, contentEngine.Grids);
var contentMetadata = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/grayflag-content-text.json")))!.AsObject();
contentMetadata["content"]!["units"]!["soldier"]!["name"] = "test.content.unit";
contentMetadata["content"]!["weapons"]!["soldier_sword"]!["name"] = "test.content.weapon";
contentMetadata["content"]!["weapons"]!["soldier_sword"]!["bonuses"] = new JsonObject
{
    ["first"] = "test.content.bonus.first",
    ["second"] = "test.content.bonus.second"
};
contentMetadata["abilities"]!["attack"] = new JsonObject { ["name"] = "test.content.ability", ["hint"] = "test.content.hint" };
Reject<LocalizationException>(() => ContentTextDocument.Parse(contentDefinitions, EcmaJson.Stringify(contentMetadata)),
    "metadata coverage follows the composed ability registry, including external additions");
contentMetadata["abilities"]!["test.inspect"] = contentMetadata["abilities"]!["attack"]!.DeepClone();
foreach (var (label, entries) in new (string, Func<JsonObject, JsonObject>)[]
{
    ("content identities", document => document["content"]!["units"]!.AsObject()),
    ("abilities", document => document["abilities"]!.AsObject()),
    ("reactions", document => document["reactions"]!.AsObject()),
    ("resources", document => document["resources"]!.AsObject()),
    ("grid directions", document => document["directions"]!["square4"]!.AsObject()),
    ("weapon bonuses", document => document["content"]!["weapons"]!["soldier_sword"]!["bonuses"]!.AsObject()),
})
{
    var missing = Copy(contentMetadata);
    entries(missing).Remove(entries(missing).First().Key);
    Reject<LocalizationException>(() => ContentTextDocument.Parse(contentDefinitions, EcmaJson.Stringify(missing)),
        $"content metadata rejects missing {label}");
    var extra = Copy(contentMetadata);
    entries(extra)["test.undeclared"] = entries(extra).First().Value!.DeepClone();
    Reject<LocalizationException>(() => ContentTextDocument.Parse(contentDefinitions, EcmaJson.Stringify(extra)),
        $"content metadata rejects undeclared {label}");
}
var contentDocument = ContentTextDocument.Parse(contentDefinitions, EcmaJson.Stringify(contentMetadata));
contentMetadata["content"]!["weapons"]!["soldier_sword"]!["bonuses"]!["first"] = "test.content.bonus.second";
contentMetadata["abilities"]!["attack"]!["name"] = "test.content.weapon";
contentMetadata["directions"]!["square4"]!.AsObject().Clear();
var firstContentText = new ContentText(firstCatalog, contentDocument);
var secondContentText = new ContentText(secondCatalog, contentDocument);
Check(firstContentText.AbilityName("attack") == "Attack"
    && firstContentText.WeaponBonus("soldier_sword", "first") == "First bonus"
    && firstContentText.DirectionName("square4", "north") == firstCatalog.Format("content.directions.square4.north"),
    "nested content metadata remains isolated from caller edits");
Reject<LocalizationException>(() => CombatModifierText.CreateDefault(new(firstCatalog, firstContentText))
    .ValidateCoverage(contentEngine.Combat.ModifierKinds), "composed modifier kinds require registered presentation coverage");
CombatModifierText ModifierFormatters(GameText text, ContentText names)
{
    var result = CombatModifierText.CreateDefault(new(text, names)).Register(new CombatModifierFormatter("test.resolve",
        ["test.content.modifier"], (rules, facts) => rules.Messages.Format("test.content.modifier",
            facts.GetProperty("nested").GetProperty("readings")[0].GetInt32())));
    result.ValidateCoverage(contentEngine.Combat.ModifierKinds);
    return result.Seal();
}
var firstModifierText = ModifierFormatters(firstCatalog, firstContentText);
var secondModifierText = ModifierFormatters(secondCatalog, secondContentText);
var contentLevel = Copy(level);
foreach (var player in contentLevel["players"]!.AsArray().OfType<JsonObject>()) player.Remove("objectives");
contentLevel["units"]![1]!["x"] = 1;
contentLevel["units"]![1]!["y"] = 0;
var contentBattle = contentEngine.CreateBattle(contentLevel, 2048);
var contentState = contentBattle.Snapshot();
var attacker = contentState["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["owner"]!.GetValue<int>() == 1);
var defender = contentState["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["owner"]!.GetValue<int>() == 2);
var forecast = contentEngine.Combat.Forecasts.Forecast(contentState, attacker, defender, weapon: "soldier_sword");
var command = contentEngine.CommandsAt(contentState, attacker, new(0, 0)).Single(option => option.Weapon == "soldier_sword");
var stateBeforeText = StateDigest.Hash(contentState);
var saveBeforeText = StateDigest.Hash(contentEngine.SaveBattle(contentState, "2026-10-21T00:00:00.000Z"));
var forecastBeforeText = JsonSerializer.Serialize(forecast);
var evaluationsBeforeText = modifierEvaluations;
Check(firstContentText.UnitName("soldier") == "Copper infantry" && secondContentText.UnitName("soldier") == "Silver infantry"
    && firstContentText.CommandName(command) != secondContentText.CommandName(command)
    && firstContentText.CommandHint(command) != secondContentText.CommandHint(command),
    "one semantic command and content identity render through two injected catalogs");
Check(firstModifierText.Describe(customModifier).Contains("Resolve 7", StringComparison.Ordinal)
    && secondModifierText.Describe(customModifier).Contains("Resolve reading 7", StringComparison.Ordinal)
    && !forecast.Strike.Modifiers.Select(firstModifierText.Describe).SequenceEqual(forecast.Strike.Modifiers.Select(secondModifierText.Describe))
    && modifierEvaluations == evaluationsBeforeText,
    "custom provider presentation reads the same immutable facts without reevaluating combat");
Check(StateDigest.Hash(contentState) == stateBeforeText && contentBattle.StateHash == stateBeforeText
    && StateDigest.Hash(contentEngine.SaveBattle(contentState, "2026-10-21T00:00:00.000Z")) == saveBeforeText
    && JsonSerializer.Serialize(forecast) == forecastBeforeText
    && JsonSerializer.Serialize(contentEngine.Combat.Forecasts.Forecast(contentState, attacker, defender, weapon: "soldier_sword")) == forecastBeforeText,
    "catalog substitution preserves complete battle state, save, and forecast gameplay");
var bonuses = forecast.Strike.Modifiers.Where(modifier => modifier.Kind == "core.weapon-target-tags").ToArray();
Check(bonuses.Length == 2 && bonuses.Select(modifier => modifier.Id).Distinct().Count() == 2
    && bonuses.Select(modifier => modifier.Facts.GetProperty("bonusId").GetString()).SequenceEqual(new[] { "first", "second" }),
    "bonuses sharing a target tag retain their explicit distinct identities");
for (var removed = 0; removed < 2; removed++)
{
    var singleBonusPacks = contentPacks.DeepClone().AsArray();
    Sword(singleBonusPacks)["bonuses"]!.AsArray().RemoveAt(removed);
    var singleBonusEngine = ComposeContent(singleBonusPacks);
    Check(singleBonusEngine.Combat.Forecasts.ComputeDamage(contentState, attacker, defender, weapon: "soldier_sword").Damage < forecast.Strike.Damage,
        $"same-target bonus {removed + 1} contributes to actual damage independently");
}
var duplicateBonusPacks = contentPacks.DeepClone().AsArray();
Sword(duplicateBonusPacks)["bonuses"]![1]!["id"] = "first";
var duplicateBonusContent = new ReadOnlyContent(duplicateBonusPacks);
var duplicateBonusRules = new RuleReferenceRules
{
    Content = duplicateBonusContent,
    Grids = contentEngine.Grids,
    Abilities = contentEngine.Abilities,
    HitEffects = contentEngine.Combat.HitEffects,
    Objectives = contentEngine.Objectives,
    ScenarioConditions = contentEngine.ScenarioConditions,
    ScenarioEffects = contentEngine.ScenarioEffects,
    Reactions = contentEngine.Reactions,
    TurnOrders = contentEngine.TurnOrders,
    AreaShapes = contentEngine.AreaShapes,
    Directives = contentEngine.Directives,
};
Reject<DomainInvariantException>(() => new BattleContentValidator(new BattleContentValidationRules(duplicateBonusRules, contentEngine.ReferenceChecks)).Validate(),
    "BattleContentValidator owns duplicate weapon bonus identity rejection");

var logState = Copy(contentState);
var embarked = Copy(attacker);
embarked["id"] = 101;
var fallen = Copy(defender);
fallen["id"] = 102;
logState["embarkedUnits"] = new JsonArray(new JsonObject { ["unit"] = embarked });
logState["markers"] = new JsonArray(new JsonObject { ["fallenUnit"] = fallen });
var logNames = new BattleLogNames(unit => firstContentText.UnitName(unit.GetProperty("type").GetString()!), firstContentText.UnitName, firstContentText.CareerName, firstContentText.ResourceName,
    id => firstText.PlayerName("test.objective-outcomes", id));
var logContext = new BattleLogContext(logNames, EcmaJson.ToElement(contentState), EcmaJson.ToElement(logState));
Check(logContext.UnitName(attacker["id"]!.GetValue<int>()) == "Copper infantry"
    && logContext.UnitName(101) == "Copper infantry" && logContext.UnitName(102) == "Copper infantry",
    "battle log resolves canonical live, embarked, and fallen unit identities through injected names");
Reject<LocalizationException>(() => logContext.UnitName(999), "battle log rejects unavailable actor identity without a guessed name");
var removedLogState = Copy(logState);
removedLogState["markers"]!.AsArray().Clear();
Check(new BattleLogContext(logNames, EcmaJson.ToElement(logState), EcmaJson.ToElement(removedLogState)).UnitName(102) == "Copper infantry",
    "receipt identity survives explicit marker removal between before and after snapshots");
removedLogState["units"]![0]!["type"] = "test.missing-content";
Reject<LocalizationException>(() => new BattleLogContext(logNames, EcmaJson.ToElement(logState), EcmaJson.ToElement(removedLogState))
    .UnitName(attacker["id"]!.GetValue<int>()), "identity lookup never conceals a missing canonical content mapping");

// A supported trigger may create, use and erase a unit before its receipt snapshot.
var transientLevel = Copy(contentLevel);
transientLevel["scenario"]!["triggers"] = EcmaJson.Parse("""
[
  {"id":"transient-resource", "timing":"afterAction", "condition":{"type":"turnAtLeast","turn":1}, "effects":[
    {"type":"spawnUnits","ready":true,"units":[{"key":"transient","unit":"soldier","owner":1,"x":1,"y":1,
      "resources":{"momentum":{"current":0,"capacity":10}}}]},
    {"type":"changeUnitOwner","selector":{"keys":["transient"]},"owner":2},
    {"type":"changeUnitResource","selector":{"keys":["transient"]},"resource":"momentum","amount":3},
    {"type":"withdrawUnits","selector":{"keys":["transient"]},"leaveCorpse":false},
    {"type":"removeMarkers","selector":{"kind":"withdrawn"}}
  ]}
]
""");
var transientBattle = contentEngine.CreateBattle(transientLevel, 2048);
var transientBefore = transientBattle.Snapshot();
var transientReceipt = transientBattle.Dispatch(new JsonObject { ["kind"] = "endTurn" });
var transientAfter = transientBattle.Snapshot();
var creation = transientReceipt.Events.Single(value => value["type"]!.GetValue<string>() == "unitSpawned");
var transientId = creation["unit"]!.GetValue<int>();
Check(creation["unitType"]!.GetValue<string>() == "soldier" && creation["owner"]!.GetValue<int>() == 1
    && !transientBefore["units"]!.AsArray().OfType<JsonObject>().Any(unit => unit["id"]!.GetValue<int>() == transientId)
    && !transientAfter["units"]!.AsArray().OfType<JsonObject>().Any(unit => unit["id"]!.GetValue<int>() == transientId)
    && transientAfter["markers"]!.AsArray().Count == 0,
    "real creation event retains emission-time identity after same-action transfer, resource change and erasure");
var resourceIndex = transientReceipt.Events.ToList().FindIndex(value => value["type"]!.GetValue<string>() == "resourceChanged"
    && value["subject"]?["id"]?.GetValue<int>() == transientId);
Check(resourceIndex > transientReceipt.Events.ToList().IndexOf(creation), "creation precedes its transient actor-bearing resource event");
var eventText = BattleEventText.CreateDefault(new(firstCatalog, firstText));
BattleLogContext TransientContext() => new(logNames, EcmaJson.ToElement(transientBefore), EcmaJson.ToElement(transientAfter));
var eventValues = transientReceipt.Events.Select(EcmaJson.ToElement).ToArray();
var eventLines = eventText.DescribeAll(TransientContext(), eventValues);
Check(eventLines[resourceIndex].Contains("Copper infantry", StringComparison.Ordinal)
    && eventLines[resourceIndex].Contains(firstContentText.ResourceName("momentum"), StringComparison.Ordinal),
    "ordered log projection names a valid transient actor from creation facts, not a guessed placeholder");
Reject<LocalizationException>(() => eventText.DescribeAll(TransientContext(), new[] { eventValues[resourceIndex] }),
    "an actor-bearing event cannot look ahead or silently invent missing identity");
Reject<LocalizationException>(() => eventText.DescribeAll(TransientContext(), new[] { EcmaJson.ToElement(creation), EcmaJson.ToElement(creation) }),
    "repeated creation identity is rejected instead of overwritten");

foreach (var (kind, key) in new[] { ("structureDisabled", "battle.event.structure_disabled"), ("structureEnabled", "battle.event.structure_enabled") })
{
    var lifecycle = EcmaJson.ToElement(new JsonObject { ["type"] = kind, ["structure"] = "a-structure", ["at"] = new JsonObject { ["x"] = 4, ["y"] = 7 } });
    Check(eventText.Describe(TransientContext(), lifecycle) == firstCatalog.Format(key, 4, 7),
        $"{kind} uses the injected locale catalog, not an English event identifier");
}

Console.WriteLine($"Objective/outcome native contract checks passed: {checks} assertions.");
