using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Objectives;
using Empire.Core.State;
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
    if (!value) throw new InvalidOperationException(label);
}
void Reject<T>(Action operation, string label) where T : Exception
{
    try { operation(); }
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
  "schema": 2,
  "id": "test.objective-outcomes",
  "name": "Semantic objective fixture",
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
  "schema": 1,
  "levels": {
    "test.objective-outcomes": {
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
GrayFlagEngine Compose() => BattleEngineFactory.Create(new ReadOnlyContent(packs),
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
        ["battle.conclusion.objective_completed"] = alternate ? "Goal reached by {0}: {1}" : "{0} completed {1}",
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
var textDocument = BattleTextDocument.Parse(metadata, new JsonArray(level.DeepClone()), engine.Objectives);
var firstText = new BattleText(firstCatalog, textDocument, firstFormatters);
var secondText = new BattleText(secondCatalog, textDocument, secondFormatters);

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
    && firstText.ObjectiveProgress(progress) == firstCatalog.Format("objective.progress.stages", 0, 2),
    "composite progress renders handler-owned counts");
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
var campaign = new CampaignRuntime(campaignDefinition);
var bridge = new CampaignBattleBridge(levelId => levelId == "test.objective-outcomes" ? Copy(level)
    : throw new InvalidOperationException("Unknown fixture level."), engine.Catalog);
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
var rejectingCampaign = new CampaignRuntime(campaignDefinition, pendingCampaign);
var beforeRejectedResult = rejectingCampaign.Snapshot();
var obsoleteResult = Copy(battleResult);
obsoleteResult["reason"] = "旧的胜利描述";
obsoleteResult["winnerTeam"] = 1;
var rejectedResult = false;
try { rejectingCampaign.CompleteBattle(obsoleteResult); }
catch (CampaignInvariantException) { rejectedResult = true; }
Check(rejectedResult && EcmaJson.DeepEquals(beforeRejectedResult, rejectingCampaign.Snapshot())
    && StateDigest.Hash(beforeRejectedResult) == StateDigest.Hash(rejectingCampaign.Snapshot()),
    "campaign rejects obsolete result prose atomically without consuming the pending battle");

Console.WriteLine($"Objective/outcome native contract checks passed: {checks} assertions.");
