using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;

JsonNode Read(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = path.EndsWith(".gz", StringComparison.Ordinal)
        ? new StreamReader(new GZipStream(stream, CompressionMode.Decompress)) : new StreamReader(stream);
    return EcmaJson.Parse(reader.ReadToEnd())!;
}
var fixture = Read(args[0]).AsObject();
var packs = fixture["packs"]!.AsArray();
var engine = new BattleEngine(packs);
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception(name);
    checks++;
}
void Equal(JsonNode? actual, JsonNode? expected, string name) => Check(EcmaJson.DeepEquals(actual, expected), name + $"\nactual: {actual}\nexpected: {expected}");
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, json);
JsonObject Unit(JsonObject state) => state["units"]![0]!.AsObject();
JsonObject State(string type) => fixture["commandLabels"]!.AsArray().OfType<JsonObject>()
    .First(row => StateJson.Text(Unit(row["state"]!.AsObject()), "type") == type)["state"]!.DeepClone().AsObject();
JsonObject Order(JsonObject unit, string ability, Coord? target = null, Coord? destination = null) => new()
{
    ["kind"] = "command",
    ["unit"] = unit["id"]!.DeepClone(),
    ["path"] = TacticalQueries.PathJson(destination is { } to ? [TacticalQueries.Position(unit), to] : [TacticalQueries.Position(unit)]),
    ["command"] = new JsonObject { ["ability"] = ability, ["target"] = target is { } at ? TacticalQueries.CoordinateJson(at) : null },
};
(AbilityCommands Commands, BattleCombat Combat) Consumers(ITacticalSpace space)
{
    var movement = new TacticalCommands(new(engine.Grids, space, engine.Reactions, engine.Turns.MayAct, engine.Directives));
    var combat = new BattleCombat(new BattleCombatRules(engine.Catalog, engine.Grids, engine.Resources, space, engine.AreaShapes,
        DefaultCombatModifiers.Create(), DefaultWeaponHitEffects.Create(), engine.Reactions, engine.RankProgression, engine.UnitDepartures));
    return (new(engine.AbilityRules with
    {
        Space = space,
        Combat = combat
    }, movement), combat);
}
void Refused(AbilityCommands commands, JsonObject state, JsonObject action, string name)
{
    var battle = new Battle(new BattleTransactionRules(new BattleActionRegistry().Register("command", commands.Execute), null), state);
    var before = battle.Snapshot();
    var refused = false;
    try
    {
        battle.Dispatch(action);
    }
    catch (IllegalActionException) { refused = true; }
    Check(refused, name + " was accepted");
    Equal(battle.Snapshot(), before, name + " leaked state");
}

Check(typeof(ITacticalSpace).GetMethods().Length == 10, "spatial contract grew non-source methods");
Check(typeof(BattleEngine).GetProperty("Space")!.PropertyType == typeof(ITacticalSpace), "engine binds concrete spatial policy");
Check(typeof(AbilityRules).GetProperty("Space")!.PropertyType == typeof(ITacticalSpace), "abilities bind concrete spatial policy");
var defaults = new DefaultTacticalSpace(new TacticalSpaceRules(engine.Catalog, engine.Grids));
var soldier = State("soldier");
var actor = Unit(soldier);
var at = TacticalQueries.Position(actor);
var baseline = Consumers(defaults);
Check(baseline.Commands.Options(soldier, actor, at).Any(option => option.Ability == "attack"), "pacifist fixture has no default attack");
var pacifist = new PacifistSpace(new TacticalSpaceRules(engine.Catalog, engine.Grids));
var pacifistConsumers = Consumers(pacifist);
Check(!pacifistConsumers.Commands.Options(soldier, actor, at).Any(option => option.Ability == "attack"), "pacifist menu bypass");
Check(pacifistConsumers.Combat.AttackTargets(soldier, actor, at).Count == 0, "combat query bypass");
Refused(pacifistConsumers.Commands, soldier, Order(actor, "attack", new(2, 1)), "pacifist authoritative attack");
Check(defaults.ThreatTiles(soldier, actor).Count > 0 && pacifist.ThreatTiles(soldier, actor).SetEquals(defaults.ThreatTiles(soldier, actor)),
    "source attack-target override must not rewrite inherited threat");
var cleric = State("cleric");
var healer = Unit(cleric);
Check(baseline.Commands.Options(cleric, healer, TacticalQueries.Position(healer)).Any(option => option.Ability == "heal"), "heal fixture has no default heal");
Check(!pacifistConsumers.Commands.Options(cleric, healer, TacticalQueries.Position(healer)).Any(option => option.Ability == "heal"), "custom heal menu bypass");
Refused(pacifistConsumers.Commands, cleric, Order(healer, "heal", new(1, 2)), "custom heal execution");

var isolated = new IndependentOverrides(new TacticalSpaceRules(engine.Catalog, engine.Grids));
Check(isolated.ComputeMoveField(soldier, actor).Tiles.Count == 0, "move field override did not execute");
Check(isolated.ThreatTiles(soldier, actor).SetEquals(defaults.ThreatTiles(soldier, actor)), "inherited threat called overridden move field");
Check(isolated.VisibleTiles(soldier, 1).Count == 0, "visible tiles override did not execute");
Check(isolated.VisibleUnits(soldier, 1).Select(unit => StateJson.Integer(unit, "id")).SequenceEqual(defaults.VisibleUnits(soldier, 1).Select(unit => StateJson.Integer(unit, "id"))),
    "inherited visible units called overridden visible tiles or visibility predicate");
Check(!isolated.IsUnitVisible(soldier, 1, actor), "individual visibility override did not execute");
Check(isolated.Board(soldier).Distance(new(0, 0), new(1, 1)) == 1 && defaults.Board(soldier).Distance(new(0, 0), new(1, 1)) == 2,
    "board override fixture failed");
Check(isolated.AttackTargetCoords(soldier, actor, at, engine.Combat.Forecasts.PrimaryWeapon(actor)).SequenceEqual(
    defaults.AttackTargetCoords(soldier, actor, at, engine.Combat.Forecasts.PrimaryWeapon(actor))), "inherited attack used overridden board");

var dict = new Dictionary<int, ReachableTile>();
var stops = new HashSet<int> { 3 };
var field = new MoveField(at, StateJson.Integer(actor, "id"), dict, stops);
Check(ReferenceEquals(dict, field.Tiles) && ReferenceEquals(stops, field.Stops), "MoveField copied extension-owned identity");
dict[3] = new(3, 3, 0, 5, -1, false);
stops.Add(4);
Check(field.Tiles.ContainsKey(3) && field.Stops.SetEquals(new[] { 3, 4 }), "MoveField mutation is not observable");
Check(field.Stops.Contains(3) && !field.Tiles[3].Free, "MoveField silently recomputed custom stops");
dict[3].Cost = 7;
Check(field.Tiles[3].Cost == 7, "MoveField lost tile identity or mutability");
field.Origin = new(5, 6);
field.Unit = 73;
field.Tiles = new();
field.Stops = new();
Check(field.Origin == new Coord(5, 6) && field.Unit == 73 && field.Tiles.Count == 0 && field.Stops.Count == 0,
    "MoveField introduced readonly restrictions absent from source data contract");

var teleport = new TeleportSpace(new TacticalSpaceRules(engine.Catalog, engine.Grids));
var teleportConsumers = Consumers(teleport);
soldier["rules"]!["zoneOfControl"] = false;
var destination = new Coord(0, 0);
var movementBattle = new Battle(new BattleTransactionRules(new BattleActionRegistry().Register("command", teleportConsumers.Commands.Execute), null), soldier);
var movementReceipt = movementBattle.Dispatch(Order(actor, "wait", destination: destination));
Check(TacticalQueries.Position(Unit(movementBattle.Snapshot())) == destination, "custom move field did not reach execution");
var move = movementReceipt.Events.Single(value => StateJson.Text(value, "type") == "move");
Equal(move["path"], TacticalQueries.PathJson([at, new(0, 1), destination]), "custom canonical path discarded");
Check(teleport.MoveCalls == 1 && teleport.PathCalls == 1, "movement validation recomputed default policy");
Refused(Consumers(isolated).Commands, soldier, Order(actor, "wait", destination: destination), "empty replacement move field");
var noStop = new TeleportSpace(new TacticalSpaceRules(engine.Catalog, engine.Grids), stop: false);
Refused(Consumers(noStop).Commands, soldier, Order(actor, "wait", destination: destination), "custom independent stop set");

var gridsKey = new CapabilityKey<GridRegistry>("grids");
var spaceKey = new CapabilityKey<ITacticalSpace>("space");
var consumerKey = new CapabilityKey<ITacticalSpace>("consumer");
var originalGrids = new GridRegistry().Register(new Square4Grid());
var replacementGrids = new GridRegistry().Register(new DiagonalSquare4());
var captured = new SrpgMicrokernel()
    .Use(new("consumer", 1, c => c.Provide(consumerKey, c.Require(spaceKey)), provides: [consumerKey], requiresCapabilities: [spaceKey]))
    .Use(new("replace-grids", 1, c => c.Replace(gridsKey, replacementGrids), overrides: [gridsKey]))
    .Use(new("space", 1, c => c.Provide<ITacticalSpace>(spaceKey, new DefaultTacticalSpace(new TacticalSpaceRules(engine.Catalog, c.Require(gridsKey)))),
        provides: [spaceKey], requiresCapabilities: [gridsKey]))
    .Use(new("grids", 1, c => c.Provide(gridsKey, originalGrids), provides: [gridsKey]))
    .Compose();
Check(captured.Require(gridsKey).Get("square4").Distance(new(0, 0), new(1, 1)) == 1, "grid capability replacement failed");
Check(captured.Require(consumerKey).Board(soldier).Distance(new(0, 0), new(1, 1)) == 1, "spatial installer missed the declared grid replacement");
Check(ReferenceEquals(((DefaultTacticalSpace)captured.Require(consumerKey)).Grids, replacementGrids), "spatial installer copied the resolved registry");
replacementGrids.Replace(new Square4Grid());
Check(captured.Require(consumerKey).Board(soldier).Distance(new(0, 0), new(1, 1)) == 2, "resolved registry reference became snapshot");
var replacement = new TeleportSpace(new TacticalSpaceRules(engine.Catalog, engine.Grids));
var fullReplacement = new SrpgMicrokernel()
    .Use(new("consumer", 1, c => c.Provide(consumerKey, c.Require(spaceKey)), provides: [consumerKey], requiresCapabilities: [spaceKey]))
    .Use(new("space-replacement", 1, c => c.Replace<ITacticalSpace>(spaceKey, replacement), overrides: [spaceKey]))
    .Use(new("default-space", 1, c => c.Provide<ITacticalSpace>(spaceKey, defaults), provides: [spaceKey]))
    .Compose();
Check(ReferenceEquals(fullReplacement.Require(consumerKey), replacement), "install-time consumer captured pre-override spatial policy");
Check(defaults.Board(soldier).Distance(new(0, 0), new(1, 1)) == 2, "replacement mutated isolated default");

var records = Read(args[1]).AsArray();
foreach (var entry in records.OfType<JsonObject>())
{
    var state = entry["state"]!.AsObject();
    var unit = TacticalQueries.RequireUnit(state, StateJson.Integer(entry, "unit"));
    var name = StateJson.Text(entry, "name");
    var computed = defaults.ComputeMoveField(state, unit);
    Equal(Node(computed.Tiles.Values), entry["tiles"], name + " source move tiles");
    Equal(Node(computed.Stops), entry["stops"], name + " source stops");
    Equal(Node(defaults.ThreatTiles(state, unit, computed)), entry["threat"], name + " source threat");
    Equal(Node(defaults.AttackTargetCoords(state, unit, TacticalQueries.Position(unit), engine.Combat.Forecasts.PrimaryWeapon(unit))), entry["attackTargets"], name + " source attack targets");
    Equal(Node(defaults.HealTargetsFrom(state, unit, TacticalQueries.Position(unit)).Select(value => StateJson.Integer(value, "id"))), entry["healTargets"], name + " source heal targets");
}
var visibility = Read(args[2]).AsObject();
foreach (var item in visibility["cases"]!.AsArray().OfType<JsonObject>())
{
    var state = item["state"]!.AsObject();
    var viewer = StateJson.Integer(item, "viewer");
    Equal(Node(defaults.VisibleTiles(state, viewer)), item["tiles"], "recorded source visible tiles");
    Equal(Node(defaults.VisibleUnits(state, viewer).Select(unit => StateJson.Integer(unit, "id"))), item["units"], "recorded source visible units");
    Equal(Node(StateJson.Objects(state["units"], "units").Select(unit => defaults.IsUnitVisible(state, viewer, unit))), item["individual"], "recorded source unit visibility");
}
foreach (var item in visibility["casts"]!.AsArray().OfType<JsonObject>())
{
    var state = item["state"]!.AsObject();
    Equal(Node(baseline.Combat.IncomingCasts(state).Select(pair => new { pair.Key.X, pair.Key.Y, Remaining = pair.Value })), item["incoming"], "recorded source incoming cast projection");
}
var whole = new WholeSpace(teleport);
var wholeConsumers = Consumers(whole);
Check(!wholeConsumers.Commands.Options(soldier, actor, at).Any(option => option.Ability == "attack"), "plain interface replacement failed menu");
Check(wholeConsumers.Combat.AttackTargets(soldier, actor, at).Count == 0, "plain interface replacement failed combat query");
Refused(wholeConsumers.Commands, soldier, Order(actor, "attack", new(2, 1)), "plain interface authoritative attack");
Check(whole.ThreatTiles(soldier, actor).SetEquals(new[] { 17 }), "plain interface threat replacement");
Check(whole.HostileControlZone(soldier, actor).SetEquals(new[] { 23 }), "plain interface control replacement");
Check(whole.VisibleTiles(soldier, 1).SetEquals(new[] { 11 }), "plain interface tile visibility replacement");
Check(whole.VisibleUnits(soldier, 1).Count == 1 && ReferenceEquals(whole.VisibleUnits(soldier, 1)[0], actor), "plain interface actor visibility identity");
Check(!whole.IsUnitVisible(soldier, 2, actor), "plain interface visibility predicate replacement");
var wholeBattle = new Battle(new BattleTransactionRules(new BattleActionRegistry().Register("command", wholeConsumers.Commands.Execute), null), soldier);
wholeBattle.Dispatch(Order(actor, "wait", destination: destination));
Check(TacticalQueries.Position(Unit(wholeBattle.Snapshot())) == destination, "plain interface movement/path replacement");
var projected = visibility["casts"]![0]!["state"]!.AsObject();
var boardPolicy = Consumers(isolated).Combat;
var expectedProjection = new Dictionary<Coord, double>();
foreach (var cast in boardPolicy.ActiveCasts(projected))
{
    var weapon = engine.Catalog.Definition("weapons", cast.Weapon)!.Value;
    foreach (var cell in engine.AreaShapes.Coverage(isolated.Board(projected), cast.Origin, cast.Target, weapon.GetProperty("area").GetString()!))
        expectedProjection[cell] = cast.Remaining;
}
Equal(Node(boardPolicy.IncomingCasts(projected).Select(pair => new { pair.Key.X, pair.Key.Y, Remaining = pair.Value })),
    Node(expectedProjection.Select(pair => new { pair.Key.X, pair.Key.Y, Remaining = pair.Value })), "incoming projection bypassed spatial board policy");
Console.WriteLine($"PASS {checks} spatial contract, consumer, rollback, independent-default, resolved-grid, and recorded source comparisons");
Console.WriteLine("These checks establish spatial consumer behavior; engine composition and save/replay integration have separate suites.");

sealed class PacifistSpace(TacticalSpaceRules rules) : DefaultTacticalSpace(rules)
{
    public override IReadOnlyList<Coord> AttackTargetCoords(JsonObject state, JsonObject unit, Coord from, JsonObject weapon) => [];
    public override IReadOnlyList<JsonObject> HealTargetsFrom(JsonObject state, JsonObject unit, Coord from) => [];
}
sealed class IndependentOverrides(TacticalSpaceRules rules) : DefaultTacticalSpace(rules)
{
    public override MoveField ComputeMoveField(JsonObject state, JsonObject unit) => new(TacticalQueries.Position(unit), StateJson.Integer(unit, "id"), []);
    public override IReadOnlySet<int> VisibleTiles(JsonObject state, int viewer) => new HashSet<int>();
    public override bool IsUnitVisible(JsonObject state, int viewer, JsonObject unit, IReadOnlySet<int>? seen = null) => false;
    public override TacticalBoard Board(JsonObject state) => new(new Square8Grid(), state["map"]!.AsObject());
}
sealed class TeleportSpace(TacticalSpaceRules rules, bool stop = true) : DefaultTacticalSpace(rules)
{
    public int MoveCalls
    {
        get; private set;
    }
    public int PathCalls
    {
        get; private set;
    }
    public override MoveField ComputeMoveField(JsonObject state, JsonObject unit)
    {
        MoveCalls++;
        return new(TacticalQueries.Position(unit), StateJson.Integer(unit, "id"), new()
        {
            [0] = new(0, 0, 0, 0, -1, true)
        }, stop ? [0] : []);
    }
    public override IReadOnlyList<Coord>? PathTo(MoveField field, JsonObject state, Coord destination)
    {
        PathCalls++;
        return destination == new Coord(0, 0) ? [field.Origin, new(0, 1), destination] : null;
    }
}
sealed class DiagonalSquare4 : SquareTiling
{
    public override string Id => "square4";
    public override IReadOnlyList<DirectionDef> Directions => new Square8Grid().Directions;
    public override int Distance(Coord a, Coord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

sealed class WholeSpace(ITacticalSpace inner) : ITacticalSpace
{
    public MoveField ComputeMoveField(JsonObject state, JsonObject unit) => inner.ComputeMoveField(state, unit);
    public IReadOnlyList<Coord>? PathTo(MoveField field, JsonObject state, Coord destination) => inner.PathTo(field, state, destination);
    public IReadOnlySet<int> ThreatTiles(JsonObject state, JsonObject unit, MoveField? field = null) => new HashSet<int> { 17 };
    public IReadOnlySet<int> HostileControlZone(JsonObject state, JsonObject unit) => new HashSet<int> { 23 };
    public IReadOnlyList<Coord> AttackTargetCoords(JsonObject state, JsonObject unit, Coord from, JsonObject weapon) => [];
    public IReadOnlyList<JsonObject> HealTargetsFrom(JsonObject state, JsonObject unit, Coord from) => [];
    public TacticalBoard Board(JsonObject state) => inner.Board(state);
    public IReadOnlySet<int> VisibleTiles(JsonObject state, int viewer) => new HashSet<int> { 11 };
    public bool IsUnitVisible(JsonObject state, int viewer, JsonObject unit, IReadOnlySet<int>? seen = null) => viewer == StateJson.Integer(unit, "owner");
    public IReadOnlyList<JsonObject> VisibleUnits(JsonObject state, int viewer) => [state["units"]![0]!.AsObject()];
}
