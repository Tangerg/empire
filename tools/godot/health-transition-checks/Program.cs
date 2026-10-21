using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Abilities;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Tactics;
using Empire.Core.Transactions;
using Empire.Core.Transport;

var root = Path.GetFullPath(args.Single());
var packs = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
var original = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "godot/tests/oracle/grayflag-initial-raw.json")))!["state"]!.AsObject();
var engine = new BattleEngine(packs);
var catalog = engine.Catalog;
var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
void Reject(Action action, string name)
{
    try
    {
        action();
    }
    catch (DomainInvariantException) { checks++; return; }
    throw new Exception(name);
}
double Hp(JsonObject unit) => unit["hp"]!.GetValue<double>();
JsonObject Unit(double hp = 50, string type = "soldier") => new() { ["type"] = type, ["hp"] = hp };
JsonObject State()
{
    var state = original.DeepClone().AsObject();
    var unit = state["units"]![0]!.DeepClone().AsObject();
    unit["id"] = 1;
    unit["type"] = "soldier";
    unit["hp"] = 50;
    unit["x"] = 0;
    unit["y"] = 0;
    unit["commanderId"] = null;
    state["units"] = new JsonArray(unit);
    state["commanders"] = new JsonArray();
    state["pendingCasts"] = new JsonArray();
    state["embarkedUnits"] = new JsonArray();
    state["markers"] = new JsonArray();
    state["rules"]!["moraleEnabled"] = false;
    state["scenario"]!["triggers"] = new JsonArray();
    return state;
}
foreach (var invalid in new[] { -1d, -0.1, double.NaN, double.NegativeInfinity, double.PositiveInfinity })
{
    foreach (var nonlethal in new[] { false, true })
    {
        var state = State();
        var before = state.DeepClone();
        var events = new List<JsonObject>();
        Reject(() => engine.Combat.Damage.Resolve(state, 1, invalid, nonlethal,
            _ => throw new Exception("invalid damage reached report factory"), events.Add), "invalid aggregate damage accepted");
        Check(JsonNode.DeepEquals(state, before) && events.Count == 0, "invalid aggregate damage mutated state or emitted");
    }
    var unit = Unit();
    var saved = unit.DeepClone();
    Reject(() => new UnitHealth(catalog, unit).Heal(invalid), "invalid healing accepted");
    Check(JsonNode.DeepEquals(unit, saved), "invalid healing mutated unit");
    Reject(() => new UnitHealth(catalog, unit).RestoreHealth(invalid), "invalid restore accepted");
    Check(JsonNode.DeepEquals(unit, saved), "invalid restore mutated unit");
}
{
    var unit = Unit();
    var health = new UnitHealth(catalog, unit);
    Check(health.Damage(0.49) == 0 && Hp(unit) == 50, "damage rounds below half down");
    Check(health.Damage(0.5) == 1 && Hp(unit) == 49, "damage rounds half up");
    Check(health.Damage(double.MaxValue, true) == 48 && Hp(unit) == 1, "nonlethal damage retains one health");
    Check(health.Damage(1, true) == 0 && Hp(unit) == 1, "nonlethal damage at one is inert");
    Check(health.Damage(double.MaxValue) == 1 && Hp(unit) == 0, "lethal damage caps at current health");
    Check(health.Heal(0.49) == 0 && Hp(unit) == 0, "healing rounds below half down");
    Check(health.Heal(0.5) == 1 && Hp(unit) == 1, "healing rounds half up");
    Check(health.Heal(double.MaxValue) == 99 && Hp(unit) == 100, "healing caps at canonical maximum");
    Check(health.Heal(0) == 0 && Hp(unit) == 100, "zero healing is inert");
}
foreach (var strength in new[] { 0d, 100.01, 999 })
{
    var unit = Unit();
    var before = unit.DeepClone();
    Reject(() => new UnitHealth(catalog, unit).RestoreHealth(strength), "out-of-bounds restore accepted");
    Check(JsonNode.DeepEquals(unit, before), "out-of-bounds restore mutated unit");
}
{
    var unit = Unit(0);
    var health = new UnitHealth(catalog, unit);
    Reject(() => health.RestoreHealth(), "dead snapshot returned without a revival policy");
    health.RestoreHealth(0.1);
    Check(Hp(unit) == 1, "explicit revival rounds to at least one");
    health.RestoreHealth(99.6);
    Check(Hp(unit) == 100, "explicit revival rounds within canonical maximum");
    unit["hp"] = 25.25;
    health.RestoreHealth();
    Check(Hp(unit) == 25.25, "omitted return strength preserves snapshot exactly");
}
foreach (var badHp in new[] { -1d, 101, double.NaN, double.PositiveInfinity })
{
    var unit = Unit(badHp);
    Reject(() => new UnitHealth(catalog, unit).Damage(0), "invalid existing health accepted by damage");
    Reject(() => new UnitHealth(catalog, unit).Heal(0), "invalid existing health accepted by healing");
    Reject(() => new UnitHealth(catalog, unit).ChangeType("archer"), "invalid existing health accepted by type change");
    Check(unit["type"]!.GetValue<string>() == "soldier" && Hp(unit).Equals(badHp), "invalid health preflight changed type or health");
}
var careerPacks = packs.DeepClone().AsArray();
JsonObject Definition(string family, string id) => careerPacks.OfType<JsonObject>()
    .SelectMany(pack => pack[family]?.AsArray().OfType<JsonObject>() ?? []).Single(item => item["id"]!.GetValue<string>() == id);
Definition("units", "archer")["maxHp"] = 80;
Definition("careers", "ranger")["costs"] = new JsonArray(new JsonObject { ["resource"] = "funds", ["amount"] = 40 });
var careerEngine = new BattleEngine(careerPacks);
{
    var unit = Unit(50);
    new UnitHealth(careerEngine.Catalog, unit).ChangeType("archer");
    Check(Hp(unit) == 40 && unit["type"]!.GetValue<string>() == "archer", "type change preserves health ratio");
    var before = unit.DeepClone();
    Reject(() => new UnitHealth(catalog, unit).ChangeType("absent-type"), "unknown type accepted");
    Check(JsonNode.DeepEquals(unit, before), "unknown type changed health or type");
}
{
    var fractionalPacks = careerPacks.DeepClone().AsArray();
    var archer = fractionalPacks.OfType<JsonObject>().SelectMany(pack => pack["units"]?.AsArray().OfType<JsonObject>() ?? [])
        .Single(item => item["id"]!.GetValue<string>() == "archer");
    archer["maxHp"] = 1.5;
    var unit = Unit(0, "archer");
    var health = new UnitHealth(new ReadOnlyContent(fractionalPacks), unit);
    health.RestoreHealth(1.5);
    Check(Hp(unit) == 1.5, "restore rounding never exceeds fractional maximum");
    Reject(() => health.RestoreHealth(1.6), "restore input above fractional maximum accepted");
    archer["maxHp"] = 0;
    unit = Unit();
    var before = unit.DeepClone();
    Reject(() => new UnitHealth(new ReadOnlyContent(fractionalPacks), unit).ChangeType("archer"), "invalid target maximum accepted");
    Check(JsonNode.DeepEquals(unit, before), "invalid target maximum mutated current type or health");
}
foreach (var hp in new[] { 0d, 200 })
{
    var state = State();
    var unit = state["units"]![0]!.AsObject();
    unit["hp"] = hp;
    unit["rank"] = 1;
    unit["career"] = new JsonObject
    {
        ["current"] = "militia",
        ["unlocked"] = new JsonArray("militia", "ranger"),
        ["mastery"] = new JsonObject { ["militia"] = 120 }
    };
    state["players"]![0]!["resources"]!["funds"]!["current"] = 200;
    var before = state.DeepClone();
    var events = new List<JsonObject>();
    Reject(() => careerEngine.Progression.Careers.Change(state, unit, "ranger", events.Add), "invalid career health accepted");
    Check(JsonNode.DeepEquals(state, before) && events.Count == 0, "career health failure spent costs or emitted before validation");
}
{
    var state = State();
    var unit = state["units"]![0]!.AsObject();
    unit["hp"] = 85;
    var events = new List<JsonObject>();
    engine.Abilities.Get("heal").Execute(engine.AbilityRules, new AbilityQuery(state, unit, new Coord(0, 0), false), new Coord(0, 0), events.Add);
    Check(Hp(unit) == 100 && events[0]["type"]!.GetValue<string>() == "heal" && events[0]["amount"]!.GetValue<double>() == 15,
        "registered healing ability uses bounded entity transition");
}
{
    var state = State();
    var unit = state["units"]![0]!.AsObject();
    unit["owner"] = 2;
    unit["hp"] = 50;
    unit["statuses"] = new JsonArray();
    state["currentPlayer"] = 1;
    state["phase"] = "playing";
    state["rules"]!["healOnOwnedBuilding"] = true;
    state["map"]!["tiles"]![0] = "village";
    state["map"]!["owners"]![0] = 2;
    var events = new List<JsonObject>();
    var rules = new BattleLifecycleRules(catalog, engine.Turns, engine.Scenarios, engine.Victory,
        engine.Combat, engine.Resources, new StatusLifecycle(catalog, engine.StatusBehaviors, engine.Combat));
    var expected = new MovementRules(new MovementRuleServices(catalog, engine.Grids)).Battlefield(state).Cell(new Coord(0, 0)).Heal;
    new BattleLifecycle(rules, state, events.Add).AdvanceTurn();
    Check(Hp(unit) == 50 + expected && events.Any(e => e["type"]!.GetValue<string>() == "regen" &&
        e["amount"]!.GetValue<double>() == expected), "lifecycle regeneration uses canonical cell healing and entity transition");
}
(JsonObject State, JsonObject Marker) ReturnState(double hp)
{
    var state = State();
    var fallen = state["units"]![0]!.DeepClone().AsObject();
    fallen["hp"] = hp;
    state["units"] = new JsonArray();
    var marker = new JsonObject
    {
        ["id"] = 7,
        ["kind"] = "corpse",
        ["owner"] = 1,
        ["at"] = new JsonObject { ["x"] = 0, ["y"] = 0 },
        ["fallenUnit"] = fallen
    };
    state["markers"] = new JsonArray(marker);
    return (state, marker);
}
foreach (var strength in new[] { -1d, 0, 101, double.NaN, double.PositiveInfinity })
{
    var (state, marker) = ReturnState(0);
    var before = state.DeepClone();
    var events = new List<JsonObject>();
    Reject(() => BattleUnitReturn.Restore(catalog, state, marker, new UnitReturnRequest(new Coord(0, 0), Hp: strength), events.Add),
        "return accepted invalid explicit strength");
    Check(JsonNode.DeepEquals(state, before) && events.Count == 0, "invalid return changed marker, units, or events");
}
{
    var (state, marker) = ReturnState(25.25);
    var events = new List<JsonObject>();
    var returned = BattleUnitReturn.Restore(catalog, state, marker, new UnitReturnRequest(new Coord(0, 0)), events.Add)!;
    Check(Hp(returned) == 25.25 && events.Select(e => e["type"]!.GetValue<string>()).SequenceEqual(new[] { "markerRemoved", "unitRevived" }),
        "snapshot return preserves health and ordered events");
    (state, marker) = ReturnState(0);
    events.Clear();
    var before = state.DeepClone();
    Reject(() => BattleUnitReturn.Restore(catalog, state, marker, new UnitReturnRequest(new Coord(0, 0)), events.Add),
        "return revived corpse without explicit health policy");
    Check(JsonNode.DeepEquals(state, before) && events.Count == 0, "omitted dead return mutated snapshot or events");

    engine.ScenarioCommands.ReviveMarkers(state, new JsonObject { ["selector"] = new JsonObject { ["ids"] = new JsonArray(7) }, ["hpPercent"] = 0.25 }, events.Add);
    Check(Hp(state["units"]![0]!.AsObject()) == 25 && events.Count == 2, "scenario corpse revival supplies explicit canonical strength");
    (state, marker) = ReturnState(25.25);
    marker["kind"] = "withdrawn";
    events.Clear();
    state["scenario"]!["zones"]!["return-zone"] = new JsonArray(new JsonObject { ["x"] = 1, ["y"] = 1 });
    state["map"]!["tiles"]![state["map"]!["width"]!.GetValue<int>() + 1] = "plain";
    engine.ScenarioCommands.RestoreWithdrawn(state, new JsonObject { ["selector"] = new JsonObject { ["ids"] = new JsonArray(7) }, ["zone"] = "return-zone" }, events.Add);
    Check(Hp(state["units"]![0]!.AsObject()) == 25.25 && events.Count == 2, "withdrawn scenario return preserves valid snapshot strength");

}
{
    var state = State();
    state["phase"] = "playing";
    var actions = new BattleActionRegistry().Register("invalid-health", (draft, _, emit) =>
    {
        draft["turn"] = 999;
        emit(new JsonObject { ["type"] = "draft-only" });
        engine.Combat.Damage.Resolve(draft, 1, double.PositiveInfinity, true, _ => new JsonObject(), emit);
    });
    var battle = new Battle(new BattleTransactionRules(actions, null), state);
    var before = battle.Snapshot();
    var hash = battle.StateHash;
    Reject(() => battle.Dispatch(new JsonObject { ["kind"] = "invalid-health" }), "transaction accepted invalid damage");
    Check(JsonNode.DeepEquals(battle.Snapshot(), before) && battle.StateHash == hash, "health failure committed draft or event counts");
}
Console.WriteLine($"PASS {checks} external-consumer health transition assertions: finite inputs, bounds, canonical ability/return/career paths and transaction rollback");
