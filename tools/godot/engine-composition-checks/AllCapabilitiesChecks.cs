using Empire.Checks;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Objectives;
using Empire.Core.Persistence;
using Empire.Core.Progression;
using Empire.Core.References;
using Empire.Core.Scenario;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Transactions;
using Keys = Empire.Core.Composition.BattleCapabilities;

static class AllCapabilitiesChecks
{
    internal static void Run(string root, Action<bool, string> check, Action<JsonNode?, JsonNode?, string> equal)
    {
        using var source = new GZipStream(File.OpenRead(Path.Combine(root, "tools/godot/engine-composition-checks/all-capabilities-oracle.json.gz")), CompressionMode.Decompress);
        var oracle = JsonNode.Parse(source)!.AsObject();
        var vectors = oracle["cases"]!.AsArray().OfType<JsonObject>();
        var packs = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
        var recipe = new JsonArray(new[] { DefaultBattlePlugins.ContentFor(new ReadOnlyContent(packs)) }.Concat(DefaultBattlePlugins.Rules).Select(plugin => (JsonNode?)new JsonObject
        {
            ["id"] = plugin.Id,
            ["version"] = plugin.Version,
            ["provides"] = new JsonArray(plugin.Provides.Select(key => (JsonNode?)JsonValue.Create(key.Id)).ToArray()),
            ["requiresCapabilities"] = new JsonArray(plugin.RequiresCapabilities.Select(key => (JsonNode?)JsonValue.Create(key.Id)).ToArray()),
        }).ToArray());
        var expectedRecipe = (JsonArray)oracle["recipe"]!.DeepClone();
        var tactical = expectedRecipe.OfType<JsonObject>().Single(plugin => StateJson.Text(plugin, "id") == "engine.tactical-rules");
        check(StateJson.Integer(tactical, "version") == 1, "source tactical recipe version");
        tactical["version"] = 2;
        var provides = tactical["provides"]!.AsArray();
        provides.Remove(provides.Single(key => key!.GetValue<string>() == "space"));
        equal(tactical["requiresCapabilities"], new JsonArray("content"), "source tactical content capture");
        tactical["requiresCapabilities"] = new JsonArray();
        expectedRecipe.Insert(expectedRecipe.IndexOf(tactical) + 1, new JsonObject
        {
            ["id"] = "engine.tactical-space",
            ["version"] = 1,
            ["provides"] = new JsonArray("space"),
            ["requiresCapabilities"] = new JsonArray("content", "grids"),
        });
        equal(recipe, expectedRecipe, "explicit native six-plugin 24-capability installation recipe");
        JsonObject? Error(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception error) { return new() { ["name"] = error.GetType().Name, ["message"] = error.Message }; }
        }
        foreach (var vector in vectors)
        {
            var catalog = new ReadOnlyContent(packs);
            var trace = new List<string>();
            void Same(ReadOnlyContent value) => check(ReferenceEquals(catalog, value), "all-capability callback catalog identity");
            var reader = new RootReader(Same, trace);
            StatusBehaviorRegistry? statuses = null;
            IRankProgressionPolicy? progression = null;
            TurnOrderRegistry? turns = null;
            ReactionRegistry? reactions = null;
            UnitDepartureHandlerRegistry? departures = null;
            BattleReferenceCheckRegistry? references = null;
            ScenarioConditionHandlerRegistry? conditions = null;
            ScenarioEffectHandlerRegistry? effects = null;
            ObjectiveHandlerRegistry? objectives = null;
            var plugin = new EnginePlugin("test.root-all-capabilities", 3, context =>
            {
                statuses = context.Require(Keys.StatusBehaviors).Clone().Register(new("inspired", context =>
                {
                    Same(context.Content);
                    var variables = context.State["scenario"]!["variables"]!.AsObject();
                    variables["statusCalls"] = StateJson.Number(variables, "statusCalls", 0) + 1;
                    context.Emit(new()
                    {
                        ["type"] = "scenarioSignal",
                        ["signal"] = "root-status"
                    });
                    if (StateJson.IsTrue(variables["failStatus"]))
                        throw new DomainInvariantException("root status failed");
                }));
                context.Replace(Keys.StatusBehaviors, statuses);
                progression = new ThresholdRankProgressionPolicy(1, 3);
                context.Replace(Keys.Progression, progression);
                turns = context.Require(Keys.TurnOrders).Clone().Register(new RootTurns(Same));
                context.Replace(Keys.TurnOrders, turns);
                reactions = context.Require(Keys.Reactions).Clone().Register(new("root-guard", false, 0.25, false, false));
                context.Replace(Keys.Reactions, reactions);
                departures = context.Require(Keys.UnitDepartures).Clone().Register(new("root.departure", context =>
                {
                    Same(context.Rules.Content);
                    var variables = context.State["scenario"]!["variables"]!.AsObject();
                    variables["departed"] = context.Unit["id"]!.DeepClone();
                    context.Emit(new()
                    {
                        ["type"] = "scenarioSignal",
                        ["signal"] = "root-departure"
                    });
                    if (StateJson.IsTrue(variables["failDeparture"]))
                        throw new DomainInvariantException("root departure failed");
                }));
                context.Replace(Keys.UnitDepartures, departures);
                references = context.Require(Keys.ReferenceChecks).Clone().Register(new("root-reference", "root marker", _ => ["allowed"])
                {
                    InContent = value => { Same(value); trace.Add("content"); return []; },
                    InLevel = (level, rules) => { Same(rules.Content); trace.Add("level"); return [new("root-level", level["scenario"]?["variables"]?["rootCheck"]?.GetValue<string>() ?? "allowed")]; },
                    InState = (state, rules) => { Same(rules.Content); trace.Add("state"); return [new("root-state", state["scenario"]?["variables"]?["rootCheck"]?.GetValue<string>() ?? "allowed")]; },
                });
                context.Replace(Keys.ReferenceChecks, references);
                context.Replace(Keys.Saves, reader);
                conditions = context.Require(Keys.ScenarioConditions).Clone().Register(new("rootCondition", (context, _) => { Same(context.Content); return true; }));
                context.Replace(Keys.ScenarioConditions, conditions);
                effects = context.Require(Keys.ScenarioEffects).Clone().Register(new("rootEffect", (context, _) =>
                {
                    Same(context.Content);
                    var variables = context.State["scenario"]!["variables"]!.AsObject();
                    variables["effectCalls"] = StateJson.Number(variables, "effectCalls", 0) + 1;
                    context.Emit(new()
                    {
                        ["type"] = "scenarioSignal",
                        ["signal"] = "root-effect"
                    });
                }));
                context.Replace(Keys.ScenarioEffects, effects);
                // Objective extensions own semantic assessments; their labels/progress prose belong to presentation.
                objectives = context.Require(Keys.Objectives).Clone().Register(new("rootHold", (context, _) =>
                {
                    Same(context.Content);
                    return new ObjectiveAssessment("pending", new JsonObject { ["remaining"] = 1 });
                }));
                context.Replace(Keys.Objectives, objectives);
                context.Replace(Keys.ActionHandlers, context.Require(Keys.ActionHandlers).Clone().Register("rootPrepare", (context, _) =>
                {
                    var units = context.State["units"]!.AsArray().OfType<JsonObject>().ToArray();
                    var unit = units.Single(u => StateJson.Integer(u, "id") == 2);
                    unit["statuses"] = new JsonArray(new JsonObject { ["id"] = "inspired", ["remaining"] = 2, ["stacks"] = 1 });
                    unit["reaction"] = "root-guard";
                    context.Rules.Combat.Effects.AwardRankProgress(unit, 4, context.Emit);
                    var target = units.First(u => StateJson.Integer(u, "owner") == 2);
                    var id = StateJson.Integer(target, "id");
                    context.Rules.Combat.Damage.Resolve(context.State, id, 999, false, blow => new() { ["type"] = "statusTick", ["unit"] = id, ["status"] = "inspired", ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter }, context.Emit);
                }));
            }, overrides: [Keys.StatusBehaviors, Keys.Progression, Keys.TurnOrders, Keys.Reactions, Keys.UnitDepartures, Keys.ReferenceChecks, Keys.Saves, Keys.ScenarioConditions, Keys.ScenarioEffects, Keys.Objectives, Keys.ActionHandlers],
              requiresCapabilities: [Keys.Content, Keys.StatusBehaviors, Keys.TurnOrders, Keys.Reactions, Keys.UnitDepartures, Keys.ReferenceChecks, Keys.ScenarioConditions, Keys.ScenarioEffects, Keys.Objectives, Keys.ActionHandlers]);
            var engine = BattleEngineFactory.Create(catalog, [plugin]);
            var identity = new JsonObject
            {
                ["statusBehaviors"] = ReferenceEquals(engine.StatusBehaviors, statuses),
                ["progression"] = ReferenceEquals(engine.RankProgression, progression),
                ["turnOrders"] = ReferenceEquals(engine.TurnOrders, turns),
                ["reactions"] = ReferenceEquals(engine.Reactions, reactions),
                ["unitDepartures"] = ReferenceEquals(engine.UnitDepartures, departures),
                ["referenceChecks"] = ReferenceEquals(engine.ReferenceChecks, references),
                ["saves"] = ReferenceEquals(engine.Saves, reader),
                ["scenarioConditions"] = ReferenceEquals(engine.ScenarioConditions, conditions),
                ["scenarioEffects"] = ReferenceEquals(engine.ScenarioEffects, effects),
                ["objectives"] = ReferenceEquals(engine.Objectives, objectives),
            };
            equal(identity, vector["identity"], "all capability resolved identity");
            equal(engine.RulesetManifest, FixtureRuleset.ExpectedNative(vector["manifest"]!.AsObject()), "all capability actual manifest");
            var level = vector["level"]!.AsObject();
            var battle = engine.CreateBattle(level);
            equal(battle.Snapshot(), vector["initial"], "custom turn start");
            var semanticState = battle.Snapshot();
            var semanticObjective = new JsonObject { ["id"] = "root-hold-probe", ["type"] = "rootHold" };
            var ownerId = semanticState["players"]![0]!["id"]!.GetValue<int>();
            var assessment = engine.Objectives.Assess(engine.Victory.Rules, semanticState, ownerId, semanticObjective);
            var progress = engine.Objectives.Project(engine.Victory.Rules, semanticState, ownerId, semanticObjective);
            check(assessment.Outcome == "pending" && progress.Status == "active" && progress.Kind == "rootHold", "resolved custom objective semantic assessment");
            equal(EcmaJson.Parse(assessment.Facts.GetRawText()), new JsonObject { ["remaining"] = 1 }, "resolved custom objective facts");
            equal(EcmaJson.Parse(progress.Facts.GetRawText()), new JsonObject { ["remaining"] = 1 }, "resolved custom objective projected facts");
            semanticObjective["type"] = "changed";
            check(EcmaJson.ReadString(EcmaJson.Parse(progress.Definition.GetRawText())!["type"]) == "rootHold", "objective projection detaches definition");
            var forecastState = battle.Snapshot();
            var forecastUnits = forecastState["units"]!.AsArray().OfType<JsonObject>().ToArray();
            var defender = forecastUnits.Single(u => StateJson.Integer(u, "id") == 2);
            defender["reaction"] = "root-guard";
            var forecast = engine.Combat.Forecasts.Forecast(forecastState, forecastUnits.First(u => StateJson.Integer(u, "owner") == 2), defender);
            var forecastJson = JsonSerializer.SerializeToNode(forecast, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
            if (forecastJson["reaction"] is JsonObject reaction && reaction["protectedUnit"] is null)
                reaction.Remove("protectedUnit");
            equal(forecastJson, vector["forecast"], "composed custom reaction forecast");
            equal(engine.Ai.ChooseAction(battle.Snapshot()), vector["ai"], "composed custom objective AI");
            var recorder = engine.RecordBattle(battle.Snapshot());
            foreach (var step in vector["steps"]!.AsArray().OfType<JsonObject>())
            {
                equal(battle.Snapshot(), step["before"], "all capability step input");
                JsonNode? events = null;
                var action = step["action"]!.AsObject();
                var failure = Error(() => { events = new JsonArray(battle.Dispatch(action).Events.Select(v => v.DeepClone()).ToArray()); recorder.Record(action); });
                equal(failure, step["failure"], "all capability step error");
                equal(events, step["events"], "all capability step events");
                equal(battle.Snapshot(), step["after"], "all capability step state/rollback");
            }
            var save = engine.SaveBattle(battle.Snapshot(), "2026-10-20T00:00:00.000Z");
            equal(save, FixtureRuleset.NativeDocument(vector["save"]!.AsObject()), "all capability live save");
            equal(engine.LoadBattle(save), vector["loaded"], "all capability load");
            var replay = recorder.Replay(battle.Snapshot());
            equal(replay, FixtureRuleset.NativeDocument(vector["replay"]!.AsObject()), "all capability replay document");
            equal(engine.ReplayBattle(level, replay).State, vector["replayed"], "all capability replay state");
            var invalidLevel = (JsonObject)level.DeepClone();
            invalidLevel["scenario"]!["variables"]!["rootCheck"] = "missing";
            equal(Error(() => engine.CreateState(invalidLevel)), vector["levelFailure"], "composed level reference admission");
            var invalidState = battle.Snapshot();
            invalidState["scenario"]!["variables"]!["rootCheck"] = "missing";
            equal(Error(() => engine.SaveBattle(invalidState)), vector["stateFailure"], "composed live state reference admission");
            var refused = (JsonObject)save.DeepClone();
            refused["state"]!["scenario"]!["variables"]!["failRead"] = true;
            equal(Error(() => engine.LoadBattle(refused)), vector["readFailure"], "composed reader refusal");
            var seals = new JsonObject
            {
                ["statusBehaviors"] = Error(() => statuses!.Replace(statuses.Get(statuses.Keys()[0]))) is not null,
                ["turnOrders"] = Error(() => turns!.Replace(turns.Get(turns.Keys()[0]))) is not null,
                ["reactions"] = Error(() => reactions!.Replace(reactions.Get(reactions.Keys()[0]))) is not null,
                ["unitDepartures"] = Error(() => departures!.Replace(departures.Get(departures.Keys()[0]))) is not null,
                ["referenceChecks"] = Error(() => references!.Replace(references.Get(references.Keys()[0]))) is not null,
                ["scenarioConditions"] = Error(() => conditions!.Replace(conditions.Get(conditions.Keys()[0]))) is not null,
                ["scenarioEffects"] = Error(() => effects!.Replace(effects.Get(effects.Keys()[0]))) is not null,
                ["objectives"] = Error(() => objectives!.Replace(objectives.Get(objectives.Keys()[0]))) is not null,
            };
            equal(seals, vector["seals"], "all registries sealed at actual root");
            equal(new JsonArray(trace.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()), vector["trace"], "reference and reader phase invocation trace");
        }
    }

    private sealed class RootReader(Action<ReadOnlyContent> same, List<string> trace) : IBattleSaveReader
    {
        private readonly BattleSaveReader defaults = new();
        public JsonObject Header(JsonNode? raw) => defaults.Header(raw);
        public JsonObject Load(JsonNode? raw, BattleSaveEnvironment environment)
        {
            same(environment.Rules.Content);
            trace.Add("reader");
            var result = defaults.Load(raw, environment);
            if (StateJson.IsTrue(result["state"]!["scenario"]!["variables"]!["failRead"]))
                throw new StoredDocumentException("root reader refused");
            return result;
        }
    }
    private sealed class RootTurns(Action<ReadOnlyContent> same) : ITurnOrderPolicy
    {
        private readonly SideTurnOrder defaults = new();
        public string Id => "root-turn";
        public JsonObject InitialState(JsonObject state, ReadOnlyContent content)
        {
            same(content);
            state["scenario"]!["variables"]!["turnInitial"] = 1;
            return new()
            {
                ["policy"] = Id,
                ["activeUnit"] = null,
                ["data"] = new JsonObject()
            };
        }
        public TurnHandoff Begin(JsonObject state, TurnOrderContext context)
        {
            same(context.Content);
            state["scenario"]!["variables"]!["turnBegin"] = 1;
            return defaults.Begin(state, context);
        }
        public TurnHandoff Advance(JsonObject state, TurnOrderContext context)
        {
            same(context.Content);
            var variables = state["scenario"]!["variables"]!.AsObject();
            variables["turnAdvance"] = StateJson.Number(variables, "turnAdvance", 0) + 1;
            return defaults.Advance(state, context);
        }
        public bool CanAct(JsonObject state, JsonObject unit) => defaults.CanAct(state, unit);
        public IReadOnlyList<JsonObject> Actors(JsonObject state) => defaults.Actors(state);
        public IReadOnlyList<int> Preview(JsonObject state, ReadOnlyContent content, int count) => defaults.Preview(state, content, count);
    }
}
