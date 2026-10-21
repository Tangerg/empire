using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Actions;
using Empire.Core.Abilities;
using Empire.Core.Transactions;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Lifecycle;
using Empire.Core.Persistence;
using Empire.Core.References;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Tactics;
using static Empire.Core.State.StateJson;

using var file = File.OpenRead(args[0]);
using var reader = args[0].EndsWith(".gz", StringComparison.Ordinal) ? new StreamReader(new GZipStream(file, CompressionMode.Decompress)) : new StreamReader(file);
var fixture = EcmaJson.Parse(reader.ReadToEnd())!.AsObject();
var packs = fixture["packs"]!.AsArray();
var engine = new GrayFlagEngine(packs);
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, options);
void Equal(JsonNode? actual, JsonNode? expected, string name) => Check(EcmaJson.DeepEquals(actual, expected), name + $"\nactual: {actual}\nexpected: {expected}");
void Refuses(Action operation, string message)
{
    try { operation(); } catch (DomainInvariantException error) { Check(error.Message.Contains(message, StringComparison.Ordinal), "wrong error: " + error.Message); return; }
    throw new Exception("Expected rejection: " + message);
}
ReactionBehavior Definition(JsonObject value) => new(Text(value, "id"), Text(value, "name"), IsTrue(value["intercepts"]), Number(value, "incomingMultiplier", 1), IsTrue(value["retaliates"]), IsTrue(value["conservesResources"]), Text(value, "hint"));
ReactionRegistry Reactions() { var result = ReactionRegistry.Core(); foreach (var item in Objects(fixture["customReactions"], "custom reactions")) result.Register(Definition(item)); return result; }
BattleCombat ComposedCombat(JsonArray contentPacks, ReactionRegistry reactions) => BattleEngineFactory.Create(new ReadOnlyContent(contentPacks),
    [new EnginePlugin("test.turn-reaction.reactions", 1, context => context.Replace(BattleCapabilities.Reactions, reactions), overrides: [BattleCapabilities.Reactions])]).Combat;
Equal(Node(TurnOrderRegistry.Core().All().Select(value => new { value.Id, value.Name })), fixture["defaults"]!["turnOrders"], "default turn metadata");
Equal(Node(ReactionRegistry.Core().All().Select(value => new { value.Id, value.Name, value.Intercepts, value.IncomingMultiplier, value.Retaliates, value.ConservesResources, value.Hint })), fixture["defaults"]!["reactions"], "default reaction metadata and hints");
foreach (var item in Objects(fixture["turns"], "turn cases"))
{
    var state = item["before"]!.DeepClone().AsObject();
    var config = item["config"]!.AsObject();
    var registry = TurnOrderRegistry.Core();
    if (IsTrue(config["custom"])) registry.Register(new ReversePolicy(config));
    var turns = new TurnOrderSystem(new(engine.Catalog, registry));
    var events = new JsonArray(); var hashes = new JsonArray();
    void Emit(JsonObject value) { events.Add(value); var counts = state["scenario"]!["eventCounts"]!.AsObject(); var type = Text(value, "type"); counts[type] = Number(counts, type, 0) + 1; hashes.Add(StateDigest.Hash(state)); }
    var life = new BattleLifecycle(new(engine.Catalog, turns, engine.Scenarios, engine.Victory, engine.Combat, engine.Resources, new StatusLifecycle(engine.Catalog, new StatusBehaviorRegistry(), engine.Combat)), state, Emit);
    switch (Text(item, "operation")) { case "start": life.Start(); break; case "beginPlaying": life.BeginPlaying(); break; case "advance": life.AdvanceTurn(); break; }
    var name = Text(item, "name"); var beforeQueries = StateDigest.Hash(state);
    var policy = turns.Active(state); var units = Objects(state["units"], "units").ToArray();
    var query = Node(new { Active = policy.Id, policy.Name, CanAct = units.Select(unit => turns.CanAct(state, unit)), MayAct = units.Select(unit => turns.MayAct(state, unit)), Actors = turns.Actors(state).Select(unit => Integer(unit, "id")), Preview = turns.Preview(state, config["count"] is null ? 5 : Integer(config, "count")) });
    Check(StateDigest.Hash(state) == beforeQueries, name + " queries mutated state");
    Equal(query, item["query"], name + " policy queries");
    Equal(events, item["events"], name + " ordered events");
    Equal(hashes, item["hashes"], name + " event-time state digests");
    Equal(state, item["after"], name + " full state");
    Check(StateDigest.Hash(state) == Text(item, "hash"), name + " final state digest");
}
foreach (var item in Objects(fixture["reactions"], "reaction cases"))
{
    var state = item["before"]!.DeepClone().AsObject(); var config = item["config"]!.AsObject();
    var customPacks = packs.DeepClone().AsArray();
    if (IsTrue(config["weaponCost"])) Objects(customPacks, "packs").SelectMany(pack => Objects(pack["weapons"], "weapons")).Single(weapon => Text(weapon, "id") == "soldier_sword")["cooldown"] = 2;
    var registry = Reactions();
    if (IsTrue(config["replaceGuard"])) registry.Replace(registry.Get("guard") with { Name = "替换防御", IncomingMultiplier = 0.25, Retaliates = true });
    var combat = ComposedCombat(customPacks, registry);
    var attacker = state["units"]![0]!.AsObject(); var defender = state["units"]![1]!.AsObject();
    var name = Text(item, "name"); var beforeForecast = StateDigest.Hash(state);
    var result = Node(combat.Forecasts.Forecast(state, attacker, defender));
    if (result?["reaction"] is JsonObject reaction && reaction["protectedUnit"] is null) reaction.Remove("protectedUnit");
    Check(StateDigest.Hash(state) == beforeForecast, name + " forecast mutated state");
    Equal(result, item["result"], name + " complete forecast");
    var events = new JsonArray(); var hashes = new JsonArray();
    if (IsTrue(config["execute"]))
    {
        var plan = combat.Forecast(state, attacker, new(3, 3));
        combat.ExecutePlan(state, plan, value => { events.Add(value); var counts = state["scenario"]!["eventCounts"]!.AsObject(); var type = Text(value, "type"); counts[type] = Number(counts, type, 0) + 1; hashes.Add(StateDigest.Hash(state)); });
    }
    Equal(events, item["events"], name + " ordered events");
    Equal(hashes, item["hashes"], name + " event-time state digests");
    Equal(state, item["after"], name + " full state");
    Check(StateDigest.Hash(state) == Text(item, "hash"), name + " final state digest");
}
foreach (var item in Objects(fixture["actions"], "action cases"))
{
    var turns = new TurnOrderSystem(new(engine.Catalog, TurnOrderRegistry.Core().Register(new ReversePolicy(new JsonObject()))));
    var combat = ComposedCombat(packs, Reactions());
    var commands = new TacticalCommands(new(engine.Grids, engine.Space, combat.Forecasts.Reactions), turns.MayAct, engine.Directives);
    var abilities = new AbilityCommands(engine.AbilityRules with { Combat = combat }, commands);
    var actionRules = new ActionRules(commands, engine.Deployment, engine.Transport, engine.Progression, abilities, engine.Recruitment, engine.Support, combat,
        (state, emit) => new BattleLifecycle(new(engine.Catalog, turns, engine.Scenarios, engine.Victory, combat, engine.Resources, new StatusLifecycle(engine.Catalog, new StatusBehaviorRegistry(), combat)), state, emit));
    var battle = new Battle(item["before"]!.AsObject(), DefaultActionHandlers.Create(), new ActionExecutionPipeline(actionRules));
    var events = new JsonArray(); JsonObject? error = null;
    try { foreach (var value in battle.Dispatch(item["action"]!.AsObject()).Events) events.Add(value.DeepClone()); }
    catch (Exception caught) when (caught is DomainInvariantException or IllegalActionException)
    { error = new JsonObject { ["kind"] = caught.GetType().Name.Replace("Exception", "", StringComparison.Ordinal), ["message"] = caught.Message }; }
    var name = Text(item, "name");
    if (name == "unknown stance action")
    {
        Check(item["error"] is null && Text(item["after"]!["units"]![2]!.AsObject(), "reaction") == "absent",
            "frozen source characterizes invalid reaction commit");
        Check(Text(error!, "kind") == "IllegalAction" && Text(error!, "message").Contains("unknown reaction stance", StringComparison.Ordinal),
            "unknown stance action native typed refusal");
        Equal(events, new JsonArray(), "unknown stance action has no events");
        Equal(battle.Snapshot(), item["before"], "unknown stance action native rollback");
        continue;
    }
    Equal(error, item["error"], name + " typed action failure"); Equal(events, item["events"], name + " action events");
    Equal(battle.Snapshot(), item["after"], name + " action/rollback state"); Check(battle.StateHash == Text(item, "hash"), name + " action digest");
}
var turnsRegistry = TurnOrderRegistry.Core(); var originalSide = turnsRegistry.Get("side");
Refuses(() => turnsRegistry.Register(originalSide), "already registered");
turnsRegistry.Seal(); Refuses(() => turnsRegistry.Replace(originalSide), "sealed");
var turnsCopy = turnsRegistry.Clone();
Check(!turnsCopy.IsSealed && ReferenceEquals(turnsCopy.Get("side"), originalSide), "turn shallow mutable clone");
turnsCopy.Replace(new ReversePolicy(new JsonObject { ["id"] = "side" }));
turnsCopy.Replace(new ReversePolicy(new JsonObject { ["id"] = "custom" }));
Check(turnsCopy.Keys().SequenceEqual(new[] { "side", "initiative", "custom" }) && ReferenceEquals(turnsRegistry.Get("side"), originalSide), "turn replacement/upsert order and isolation");
Check(!turnsRegistry.Has("custom") && turnsRegistry.TryGet("custom") is null, "turn ask contract"); Refuses(() => turnsRegistry.Get("custom"), "unknown turn order");
var reactionsRegistry = Reactions(); var originalGuard = reactionsRegistry.Get("guard");
Refuses(() => reactionsRegistry.Register(originalGuard), "already registered"); reactionsRegistry.Seal(); Refuses(() => reactionsRegistry.Replace(originalGuard), "sealed");
var reactionsCopy = reactionsRegistry.Clone(); Check(!reactionsCopy.IsSealed && ReferenceEquals(reactionsCopy.Get("guard"), originalGuard), "reaction shallow mutable clone");
reactionsCopy.Replace(originalGuard with { IncomingMultiplier = 0.1 }); reactionsCopy.Replace(originalGuard with { Id = "custom" });
Check(reactionsCopy.Keys()[1] == "guard" && reactionsCopy.Keys()[^1] == "custom" && ReferenceEquals(reactionsRegistry.Get("guard"), originalGuard), "reaction replacement/upsert order and isolation");
Refuses(() => reactionsRegistry.Get("absent"), "unknown reaction stance");
var mutable = TurnOrderRegistry.Core(); var dynamicTurns = new TurnOrderSystem(new(engine.Catalog, mutable));
var sample = fixture["turns"]![0]!["before"]!.DeepClone().AsObject();
mutable.Replace(new ReversePolicy(new JsonObject { ["id"] = "side", ["independent"] = true }));
Check(dynamicTurns.Preview(sample, 7).SequenceEqual(new[] { 3, 7, 1 }), "turn consumer snapshotted registry");
Check(ReferenceEquals(dynamicTurns.Actors(sample)[0], sample["units"]![1]), "actor query lost unit identity");
var mutableReactions = ReactionRegistry.Core(); var dynamicForecast = new CombatForecasts(engine.Catalog, engine.Grids, engine.Resources, engine.Combat.Forecasts.Modifiers, mutableReactions);
var replacement = originalGuard with { IncomingMultiplier = 0.17 }; mutableReactions.Replace(replacement);
Check(ReferenceEquals(dynamicForecast.Reaction("guard"), replacement), "forecast consumer snapshotted registry");
Check(ReferenceEquals(dynamicForecast.Reactions, mutableReactions), "forecast lost supplied registry identity");
var turnKey = new CapabilityKey<TurnOrderRegistry>("turnOrders"); var reactionKey = new CapabilityKey<ReactionRegistry>("reactions");
var consumerKey = new CapabilityKey<TurnOrderSystem>("test.turn-consumer"); var forecastKey = new CapabilityKey<CombatForecasts>("test.reaction-consumer");
var resolved = new SrpgMicrokernel()
    .Use(new("consumer", 1, c => { c.Provide(consumerKey, new(new(engine.Catalog, c.Require(turnKey)))); c.Provide(forecastKey, new(engine.Catalog, engine.Grids, engine.Resources, engine.Combat.Forecasts.Modifiers, c.Require(reactionKey))); }, provides: [consumerKey, forecastKey], requiresCapabilities: [turnKey, reactionKey]))
    .Use(new("replacement", 1, c => { c.Replace(turnKey, turnsCopy); c.Replace(reactionKey, reactionsCopy); }, overrides: [turnKey, reactionKey]))
    .Use(new("defaults", 1, c => { c.Provide(turnKey, turnsRegistry); c.Provide(reactionKey, reactionsRegistry); }, provides: [turnKey, reactionKey]))
    .Compose();
Check(ReferenceEquals(resolved.Require(consumerKey).Policies, turnsCopy), "install-time turn replacement bypass");
Check(ReferenceEquals(resolved.Require(forecastKey).Reactions, reactionsCopy), "install-time reaction replacement bypass");
var referenceRules = new RuleReferenceRules
{
    Content = engine.Catalog, Grids = engine.Grids, Abilities = engine.Abilities, HitEffects = engine.Combat.HitEffects,
    Objectives = engine.Objectives, ScenarioConditions = engine.ScenarioConditions, ScenarioEffects = engine.ScenarioEffects,
    Reactions = reactionsCopy, TurnOrders = turnsCopy, AreaShapes = engine.AreaShapes, Directives = engine.Directives,
};
Check(engine.ReferenceChecks.Get("turnOrders").Known(referenceRules).Contains("custom") &&
    engine.ReferenceChecks.Get("reactions").Known(referenceRules).Contains("custom"), "reference ids lost custom registration");
Check(!TurnOrderRegistry.Core().Has("custom") && !ReactionRegistry.Core().Has("custom"), "custom registry leaked into new defaults");
sample["phase"] = "deployment"; sample["turnOrder"]!["policy"] = "absent";
Check(!dynamicTurns.MayAct(sample, sample["units"]![0]!.AsObject()), "phase gate must short-circuit unknown policy");
sample["phase"] = "playing"; Refuses(() => dynamicTurns.MayAct(sample, sample["units"]![0]!.AsObject()), "unknown turn order");
DispositionContractChecks.Run(packs, Check, Equal);
ReactionContractChecks.Run(packs, fixture["reactions"]!.AsArray(), Check, Equal);
Console.WriteLine($"PASS {checks} original-source turn/reaction vectors, event-time hashes, registry/reference identity and install-time consumers");
Console.WriteLine("Native contracts cover invalid disposition rollback, all shipped/custom facings, handler-owned support policy, and composed save/replay.");

sealed class ReversePolicy(JsonObject config) : ITurnOrderPolicy
{
    public string Id => config["id"]?.GetValue<string>() ?? "reverse";
    public string Name => "倒序自定义";
    private static JsonObject[] Units(JsonObject state) => Objects(state["units"], "units").ToArray();
    public JsonObject InitialState(JsonObject state, ReadOnlyContent content) => new() { ["policy"] = IsTrue(config["switchToSide"]) ? "side" : Id, ["activeUnit"] = null,
        ["data"] = new JsonObject { ["step"] = 0, ["movement"] = content.Definition("units", Text(Units(state)[0], "type"))!.Value.GetProperty("movement").GetDouble() } };
    public TurnHandoff Begin(JsonObject state, TurnOrderContext context)
    {
        context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = $"{Id}.begin.{Text(state["turnOrder"]!.AsObject(), "policy")}" });
        var actor = Units(state)[^1]; state["turnOrder"]!["data"]!["step"] = 1;
        return new(Integer(actor, "owner"), Integer(actor, "id"), false);
    }
    public TurnHandoff Advance(JsonObject state, TurnOrderContext context)
    {
        var data = state["turnOrder"]!["data"]!.AsObject(); var step = Integer(data, "step") + 1; data["step"] = step;
        context.Emit(new JsonObject { ["type"] = "scenarioSignal", ["signal"] = $"{Id}.advance.{step}" });
        if (IsTrue(config["exhaust"]) && step >= 3) return new(Integer(state, "currentPlayer"), null, true, true);
        var units = Units(state); var actor = units[(units.Length - step % units.Length) % units.Length];
        return new(Integer(actor, "owner"), Integer(actor, "id"), step % 2 == 0);
    }
    public bool CanAct(JsonObject state, JsonObject unit) => state["turnOrder"]!["activeUnit"] is { } active && Integer(active, "activeUnit") == Integer(unit, "id") && !IsTrue(unit["done"]);
    public IReadOnlyList<JsonObject> Actors(JsonObject state) => IsTrue(config["independent"]) ? [Units(state)[1], Units(state)[0]] : Units(state).Where(unit => CanAct(state, unit)).ToArray();
    public IReadOnlyList<int> Preview(JsonObject state, ReadOnlyContent content, int count)
    {
        var units = Units(state);
        if (IsTrue(config["independent"])) return [(int)content.Definition("units", Text(units[0], "type"))!.Value.GetProperty("movement").GetDouble(), count, Integer(units[0], "id")];
        var step = Integer(state["turnOrder"]!["data"]!.AsObject(), "step");
        return Enumerable.Range(0, Math.Max(0, count)).Select(index => Integer(units[(units.Length - (step + index) % units.Length) % units.Length], "id")).ToArray();
    }
}
