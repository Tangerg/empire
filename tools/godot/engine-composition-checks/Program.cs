using Empire.Checks;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using System.Text.Json;
using Empire.Core.Ai;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;
using Keys = Empire.Core.Composition.BattleCapabilities;

var root = Path.GetFullPath(args.Length > 1 ? args[1] : ".");
using var source = new GZipStream(File.OpenRead(args[0]), CompressionMode.Decompress);
var oracle = JsonNode.Parse(source)!.AsObject();
var packs = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
ReadOnlyContent Catalog() => new(packs);
var checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
void Equal(JsonNode? actual, JsonNode? expected, string label) => Check(EcmaJson.DeepEquals(actual, expected), $"{label}: {actual} != {expected}");
JsonObject Events(IReadOnlyList<JsonObject> events) => new() { ["events"] = new JsonArray(events.Select(v => v.DeepClone()).ToArray()) };
JsonObject? Error(Action call) { try { call(); return null; } catch (Exception error) { return new() { ["name"] = error.GetType().Name, ["message"] = error.Message }; } }
void Reject(Action call, string label) => Check(Error(call) is not null, label);
EnginePlugin[] Plugins(double version, List<string> trace) => [
    new("test.root-extension", version, context =>
    {
        trace.Add("extension");
        var resources = context.Require(Keys.Resources);
        context.Replace(Keys.Abilities, context.Require(Keys.Abilities).Clone().Register(new("root-pulse")
        {
            Priority = 12.5, Execute = (_, query, _, emit) =>
            {
                var subject = new BattleResources.Subject("unit", query.Unit);
                var amount = resources.Spend(subject, "root_energy", 1.5);
                resources.Announce(subject, "root_energy", -amount, emit);
                var variables = query.State["scenario"]!["variables"]!.AsObject();
                variables["pulses"] = StateJson.Number(variables, "pulses", 0) + 1;
                emit(new() { ["type"]="scenarioSignal", ["signal"]="root-pulse" });
                if (StateJson.IsTrue(variables["fail"])) throw new DomainInvariantException("root pulse failed");
            }
        }));
        context.Replace(Keys.ActionHandlers, context.Require(Keys.ActionHandlers).Clone().Register("rootGrant", (context, action) =>
        {
            var unit = context.State["units"]!.AsArray().OfType<JsonObject>().Single(u => StateJson.Integer(u,"id") == 2);
            unit["learnedAbilities"] = new JsonArray("root-pulse");
            unit["resources"]!["root_energy"] = new JsonObject { ["current"] = 4, ["capacity"] = 5 };
            unit["done"] = false; context.State["scenario"]!["variables"]!["fail"] = StateJson.IsTrue(action["fail"]);
            context.Emit(new() { ["type"]="scenarioSignal", ["signal"]="root-grant" });
        }));
    }, overrides: [Keys.Abilities, Keys.ActionHandlers], requiresCapabilities: [Keys.Abilities,Keys.ActionHandlers,Keys.Resources]),
    new("test.root-resources", 1, context =>
    {
        trace.Add("resources");
        var resources = context.Require(Keys.Resources).Clone();
        resources.Adapters.Register(new("root_energy", "unit", false, 0.25, subject =>
            subject.Holder["resources"]?["root_energy"] is JsonObject account ? new ResourceAccount(
                () => new(StateJson.Number(account,"current",0), StateJson.Number(account,"capacity",0)), value => account["current"] = value) : null));
        context.Replace(Keys.Resources, resources);
    }, overrides: [Keys.Resources], requiresCapabilities: [Keys.Resources]),
    new("test.root-ai", 1, context =>
    {
        trace.Add("ai");
        context.Replace(Keys.AiIntents, context.Require(Keys.AiIntents).Clone().Register(new("root-choice", -1000,
            _ => new() { ["kind"]="rootGrant" })));
    }, overrides: [Keys.AiIntents], requiresCapabilities: [Keys.AiIntents, Keys.Abilities]),
];
var level = oracle["level"]!.AsObject();
foreach (var vector in oracle["cases"]!.AsArray().OfType<JsonObject>())
{
    var version = StateJson.Number(vector, "version", 1);
    var trace = new List<string>();
    var catalog = Catalog();
    var engine = BattleEngineFactory.Create(catalog, Plugins(version, trace));
    Check(ReferenceEquals(catalog, engine.Catalog), "resolved catalog identity");
    Equal(new JsonArray(trace.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()), vector["trace"], "install-time consumer order");
    Equal(engine.RulesetManifest, FixtureRuleset.ExpectedNative(vector["manifest"]!.AsObject()), "actual composed manifest");
    var battle = engine.CreateBattle(level);
    Equal(battle.Snapshot(), vector["initial"], "initial");
    var recorder = engine.RecordBattle(battle.Snapshot());
    var grant = vector["grant"]!.AsObject();
    Equal(Events(battle.Dispatch(grant).Events)["events"], vector["grantEvents"], "custom action events");
    recorder.Record(grant);
    var state = battle.Snapshot();
    var actor = state["units"]!.AsArray().OfType<JsonObject>().Single(u => StateJson.Integer(u, "id") == 2);
    var options = new JsonArray(engine.CommandsAt(state, actor, TacticalQueries.Position(actor)).Select(option => (JsonNode?)new JsonObject { ["ability"] = option.Ability, ["key"] = option.Key }).ToArray());
    Equal(options, vector["options"], "custom ability menu");
    var pulse = vector["pulse"]!.AsObject();
    Equal(Events(battle.Dispatch(pulse).Events)["events"], vector["pulseEvents"], "composed ability resource events");
    recorder.Record(pulse);
    Equal(battle.Snapshot(), vector["final"], "custom ability final state");
    Check(battle.StateHash == StateJson.Text(vector, "hash"), "final digest");
    var save = engine.SaveBattle(battle.Snapshot(), "2026-10-20T00:00:00.000Z");
    Equal(save, FixtureRuleset.NativeDocument(vector["save"]!.AsObject()), "named save");
    Equal(engine.LoadBattle(save), vector["loaded"], "named load");
    var replay = recorder.Replay(battle.Snapshot());
    Equal(replay, FixtureRuleset.NativeDocument(vector["replay"]!.AsObject()), "named recording");
    var replayed = engine.ReplayBattle(level, replay);
    Equal(replayed.State, vector["replayed"], "named replay");
    Check(replayed.DivergedAt is null, "replay nond divergence");
    Equal(engine.Ai.ChooseAction(vector["initial"]!.AsObject()), vector["ai"], "resolved AI intents");
    battle.Dispatch(vector["failGrant"]!.AsObject());
    Equal(battle.Snapshot(), vector["beforeFailure"], "failure setup");
    Equal(Error(() => battle.Dispatch(pulse)), vector["failure"], "custom refusal");
    Equal(battle.Snapshot(), vector["afterFailure"], "rollback custom side effects/events");
    var resumed = engine.LoadSavedBattle(save);
    resumed.Dispatch(vector["failGrant"]!.AsObject());
    Equal(resumed.Snapshot(), vector["beforeFailure"], "restored battle uses resolved handlers/pipeline");
    if (vector["wrongVersion"] is not null)
        Equal(Error(() => BattleEngineFactory.Create(Catalog(), Plugins(version + 1, [])).LoadBattle(save)), vector["wrongVersion"], "version mismatch");
    Reject(() => engine.Abilities.Register(new("late")), "abilities sealed");
    Reject(() => engine.ActionHandlers.Register("late", (_, _) => { }), "actions sealed");
    Reject(() => engine.Resources.Adapters.Register(new("late", "unit", true, 1, _ => null)), "resources sealed");
    Reject(() => engine.Ai.Intents.Register(new("late", 1, _ => null)), "AI sealed");
}
var plain = BattleEngineFactory.Create(Catalog());
Equal(plain.RulesetManifest, FixtureRuleset.ExpectedNative(oracle["defaultManifest"]!.AsObject()), "explicit native default manifest identity");
Equal(new BattleEngine(packs).RulesetManifest, plain.RulesetManifest, "convenience constructor same composition");
Check(!plain.Abilities.Has("root-pulse") && !plain.Resources.Adapters.Has("root_energy") && !plain.ActionHandlers.Has("rootGrant"), "two-engine isolation");
var another = BattleEngineFactory.Create(Catalog());
Check(!ReferenceEquals(plain.Abilities, another.Abilities) && !ReferenceEquals(plain.Grids, another.Grids) && !ReferenceEquals(plain.Resources, another.Resources) && !ReferenceEquals(plain.Ai.Intents, another.Ai.Intents), "fresh default rule graphs");
var resourcesOverride = plain.Resources.Clone();
var anonymous = BattleEngineFactory.Create(Catalog(), [BattleEngineFactory.OverridePlugin(Keys.Resources, resourcesOverride)]);
Check(ReferenceEquals(anonymous.Resources, resourcesOverride), "anonymous override resolves exact object");
Equal(anonymous.RulesetManifest, FixtureRuleset.ExpectedNative(oracle["anonymousManifest"]!.AsObject()), "anonymous manifest");
Equal(Error(() => anonymous.SaveBattle(anonymous.CreateState(level))), oracle["anonymousSave"], "anonymous cannot save");
Equal(Error(() => anonymous.RecordBattle(anonymous.CreateState(level))), oracle["anonymousRecord"], "anonymous cannot record");
var bad = new EnginePlugin("test.bad", 1, c => c.Replace(Keys.Resources, new BattleResources(new BattleResourceRules(DefaultResourcePolicies.Adapters.Clone(), DefaultResourcePolicies.Subjects.Clone()))), requiresCapabilities: [Keys.Resources]);
Reject(() => BattleEngineFactory.Create(Catalog(), [bad]), "declaration mismatch reaches engine factory");
Reject(() => BattleEngineFactory.Create(Catalog(), [DefaultBattlePlugins.TacticalRules]), "duplicate default plugin reaches engine factory");
var p = Plugins(1, []);
Reject(() => BattleEngineFactory.Create(Catalog(), [p[1], new EnginePlugin("test.competing", 1, c => c.Replace(Keys.Resources, new BattleResources(new BattleResourceRules(DefaultResourcePolicies.Adapters.Clone(), DefaultResourcePolicies.Subjects.Clone()))), overrides: [Keys.Resources])]), "competing overrides reach engine factory");
var detached = (JsonArray)packs.DeepClone();
var readOnly = new ReadOnlyContent(detached);
detached.OfType<JsonObject>().First(pack => pack["units"] is JsonArray units && units.Count > 0)["units"]!.AsArray().Clear();
Check(readOnly.Definitions("units").Count > 0, "catalog detaches caller input");
foreach (var vector in oracle["randomCases"]!.AsArray().OfType<JsonObject>())
{
    var random = new TestBattleRandom();
    var engine = BattleEngineFactory.Create(Catalog(), Plugins(1, []).Append(new EnginePlugin("test.root-random", 1,
        context => context.Replace(Keys.Random, random), overrides: [Keys.Random])));
    Check(ReferenceEquals(engine.Random, random), "resolved full-state random identity");
    var randomLevel = vector["level"]!.AsObject();
    var battle = engine.CreateBattle(randomLevel);
    Equal(battle.Snapshot(), vector["before"], "random initial");
    var recorder = engine.RecordBattle(battle.Snapshot());
    var action = vector["action"]!.AsObject();
    JsonNode? events = null;
    var error = Error(() => { events = Events(battle.Dispatch(action).Events)["events"]!.DeepClone(); recorder.Record(action); });
    Equal(error, vector["failure"], "scenario random failure");
    Equal(events, vector["events"], "scenario random events");
    Equal(battle.Snapshot(), vector["state"], "full-state random mutations/rollback");
    var save = engine.SaveBattle(battle.Snapshot(), "2026-10-20T00:00:00.000Z");
    Equal(save, FixtureRuleset.NativeDocument(vector["save"]!.AsObject()), "random save");
    Equal(engine.LoadBattle(save), vector["loaded"], "random load");
    var replay = recorder.Replay(battle.Snapshot());
    Equal(replay, FixtureRuleset.NativeDocument(vector["replay"]!.AsObject()), "random recording");
    Equal(engine.ReplayBattle(randomLevel, replay).State, vector["replayed"], "random replay");
}
var spatial = oracle["spatial"]!.AsObject();
var peace = BattleEngineFactory.Create(Catalog(), [new EnginePlugin("test.root-space",1,context=>context.Replace(Keys.Space,
    new PeaceSpace(context.Require(Keys.Content),context.Require(Keys.Grids))),overrides:[Keys.Space],requiresCapabilities:[Keys.Content,Keys.Grids])]);
var spaceInput = spatial["input"]!.AsObject();
var spaceActor = spaceInput["units"]!.AsArray().OfType<JsonObject>().Single(u => StateJson.Integer(u, "id") == 2);
JsonArray OptionKeys(BattleEngine engine) => new(engine.CommandsAt(spaceInput, spaceActor, TacticalQueries.Position(spaceActor)).Select(o => (JsonNode?)JsonValue.Create(o.Key)).ToArray());
Equal(OptionKeys(plain), spatial["defaultOptions"], "default spatial options");
Equal(OptionKeys(peace), spatial["customOptions"], "resolved spatial options");
var peaceBattle = peace.LoadSavedBattle(peace.SaveBattle(spaceInput));
Equal(Error(() => peaceBattle.Dispatch(spatial["attack"]!.AsObject())), spatial["error"], "resolved spatial execution");
Equal(peaceBattle.Snapshot(), spatial["after"], "spatial refusal rollback");
var combatVector = oracle["combat"]!.AsObject();
var combatCatalog = new ReadOnlyContent(combatVector["packs"]!.AsArray());
CombatModifierPipeline? resolvedModifiers = null;
WeaponHitEffectHandlerRegistry? resolvedEffects = null;
var combatEngine = BattleEngineFactory.Create(combatCatalog, [new EnginePlugin("test.root-combat",1,context=>
{
    var catalog=context.Require(Keys.Content);
    resolvedModifiers=context.Require(Keys.CombatModifiers).Register(new("root-bonus",999,query=>
    {
        Check(ReferenceEquals(query.Content,catalog),"modifier resolved catalog identity");
        return [new("root-bonus","root-bonus","root","final","add",7)];
    }));context.Replace(Keys.CombatModifiers,resolvedModifiers);
    resolvedEffects=context.Require(Keys.HitEffects).Clone().Register(new("rootMark",(query,_)=>
    {
        Check(ReferenceEquals(query.Content,catalog),"hit-effect resolved catalog identity");
        query.State["scenario"]!["variables"]!["rootHit"]=query.Target["id"]!.DeepClone();query.Emit(new(){["type"]="scenarioSignal",["signal"]="root-hit"});
    }));context.Replace(Keys.HitEffects,resolvedEffects);
},overrides:[Keys.CombatModifiers,Keys.HitEffects],requiresCapabilities:[Keys.Content,Keys.CombatModifiers,Keys.HitEffects])]);
Check(ReferenceEquals(resolvedModifiers, combatEngine.Combat.Forecasts.Modifiers) && ReferenceEquals(resolvedEffects, combatEngine.Combat.HitEffects), "resolved combat policy identity");
var combatState = combatVector["input"]!.AsObject();
var combatUnits = combatState["units"]!.AsArray().OfType<JsonObject>().ToArray();
var forecast = combatEngine.Combat.Forecasts.Forecast(combatState, combatUnits[0], combatUnits[1]);
var forecastJson = JsonSerializer.SerializeToNode(forecast, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
if (forecastJson["reaction"] is JsonObject reaction && reaction["protectedUnit"] is null) reaction.Remove("protectedUnit");
Equal(forecastJson, combatVector["forecast"], "composed modifier forecast");
var combatBattle = combatEngine.LoadSavedBattle(combatEngine.SaveBattle(combatState));
Equal(Events(combatBattle.Dispatch(combatVector["action"]!.AsObject()).Events)["events"], combatVector["events"], "composed hit effects events");
Equal(combatBattle.Snapshot(), combatVector["state"], "composed combat state");
Equal(combatEngine.SaveBattle(combatBattle.Snapshot(), "2026-10-20T00:00:00.000Z"), FixtureRuleset.NativeDocument(combatVector["save"]!.AsObject()), "composed hit kind save admission");
Reject(() => resolvedModifiers!.Register(new("late", 0, _ => [])), "modifier pipeline sealed");
Reject(() => resolvedEffects!.Register(new("late", (_, _) => { })), "hit effects sealed");
var flag = new CapabilityKey<string>("mod.root.flag");
var openTrace = new List<string>();
var openEngine = BattleEngineFactory.Create(Catalog(), [
    new EnginePlugin("test.open-consumer",1,c=>openTrace.Add($"consumer:{c.Require(flag)}"),requiresCapabilities:[flag]),
    new EnginePlugin("test.open-override",1,c=>{openTrace.Add("override");c.Replace(flag,"replacement");},overrides:[flag]),
    new EnginePlugin("test.open-provider",1,c=>{openTrace.Add("provider");c.Provide(flag,"original");},provides:[flag]),
]);
Equal(new JsonArray(openTrace.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()), oracle["openTrace"], "open capability root ordering");
Equal(openEngine.RulesetManifest, FixtureRuleset.ExpectedNative(oracle["openManifest"]!.AsObject()), "open capability plugin identity");
Check(oracle["capture"]!["capturedOriginal"]!.GetValue<bool>(), "source stale local-grid capture is explicitly characterized");
InstallationContractChecks.Run(packs, level, Check, Equal);
AllCapabilitiesChecks.Run(root, Check, Equal);
StateGridChecks.Run(root, Check, Equal);
Console.WriteLine($"PASS {checks} real composed-engine source checks");

sealed class TestBattleRandom : IBattleRandomSource
{
    private readonly DefaultBattleRandomSource defaults = new();
    public string Id => "root-random";
    public double Next(JsonObject state, string stream) => defaults.Next(state, stream);
    public double Peek(JsonObject state, string stream) => defaults.Peek(state, stream);
    public double Int(JsonObject state, string stream, double bound) => defaults.Int(state, stream, bound);
    public T Pick<T>(JsonObject state, string stream, IReadOnlyList<T> items) => defaults.Pick(state, stream, items);
    public bool Chance(JsonObject state, string stream, double percent)
    {
        state["random"]!["seed"] = (uint)(StateJson.Number(state["random"]!["seed"], "seed") + 17);
        var counters = state["random"]!["counters"]!.AsObject();
        counters[stream] = StateJson.Number(counters, stream, 0) + 3;
        counters["other-stream"] = 7;
        state["scenario"]!["variables"]!["randomSeen"] = percent;
        if (StateJson.IsTrue(state["scenario"]!["variables"]!["failRandom"]))
            throw new DomainInvariantException("random policy failed");
        return true;
    }
}
sealed class PeaceSpace(ReadOnlyContent content, Empire.Core.Geometry.GridRegistry grids) : DefaultTacticalSpace(content, grids)
{
    public override IReadOnlyList<Empire.Core.Geometry.Coord> AttackTargetCoords(JsonObject state, JsonObject unit, Empire.Core.Geometry.Coord from, JsonObject weapon) => [];
}
