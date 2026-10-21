using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Empire.Core.Transactions;
using Empire.Core.Transport;

var root = Path.GetFullPath(args.Single());
var packs = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
var original = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "godot/tests/oracle/grayflag-initial-raw.json")))!["state"]!.AsObject();
var engine = new GrayFlagEngine(packs);
var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
void Reject(Action action, string name)
{
    try { action(); } catch (DomainInvariantException) { checks++; return; }
    throw new Exception(name);
}
JsonObject Point(int x, int y) => new() { ["x"] = x, ["y"] = y };
int Index(JsonObject state, Coord at) => at.Y * state["map"]!["width"]!.GetValue<int>() + at.X;
int Capture(JsonObject state, Coord at) => state["map"]!["captureProgress"]![Index(state, at)]!.GetValue<int>();
JsonObject Unit(JsonObject state, int id = 1) => TacticalQueries.RequireUnit(state, id);
JsonObject State()
{
    var state = original.DeepClone().AsObject();
    var first = state["units"]![0]!.DeepClone().AsObject();
    first["id"] = 1; first["type"] = "soldier"; first["owner"] = 1;
    first["x"] = 1; first["y"] = 1; first["hp"] = 50; first["capture"] = 17;
    first["done"] = false; first["facing"] = "north"; first["commanderId"] = null;
    first["statuses"] = new JsonArray();
    first["directive"] = new JsonObject { ["mode"] = "patrol", ["cursor"] = 0,
        ["waypoints"] = new JsonArray(Point(1, 2), Point(1, 1)) };
    var second = first.DeepClone().AsObject(); second["id"] = 2; second["key"] = "carrier";
    second["x"] = 2; second["type"] = "c01.supply-wagon";
    state["units"] = new JsonArray(first, second);
    state["currentPlayer"] = 1; state["phase"] = "playing";
    state["commanders"] = new JsonArray(); state["structures"] = new JsonArray();
    state["markers"] = new JsonArray(); state["pendingCasts"] = new JsonArray(); state["embarkedUnits"] = new JsonArray();
    state["rules"]!["grid"] = "square4"; state["rules"]!["moraleEnabled"] = false;
    state["scenario"]!["overlays"] = new JsonArray(); state["scenario"]!["triggers"] = new JsonArray();
    var tiles = state["map"]!["tiles"]!.AsArray();
    for (var index = 0; index < tiles.Count; index++)
    {
        tiles[index] = "plain"; state["map"]!["captureProgress"]![index] = 37;
        state["map"]!["elevation"]![index] = 0;
    }
    state["map"]!["cliffs"] = new JsonArray();
    return state;
}
var origin = new Coord(1, 1); var destination = new Coord(1, 2);
{
    var state = State(); var unit = Unit(state); var before = unit.DeepClone().AsObject();
    var moved = new BattleRelocation(state).MoveUnit(1, destination);
    Check(ReferenceEquals(unit, moved) && TacticalQueries.Position(unit) == destination, "move updates the canonical live occupant");
    Check(Capture(state, origin) == 0 && Capture(state, destination) == 37 && unit["capture"]!.GetValue<int>() == 0,
        "move clears only origin capture");
    before["x"] = destination.X; before["y"] = destination.Y; before["capture"] = 0;
    Check(JsonNode.DeepEquals(unit, before), "relocation must not own facing readiness or patrol policy");
    var snapshot = state.DeepClone(); new BattleRelocation(state).MoveUnit(1, destination);
    Check(JsonNode.DeepEquals(state, snapshot), "same-cell movement is inert");
}
foreach (var target in new[] { new Coord(-1, 1), new Coord(999, 0), new Coord(2, 1) })
{
    var state = State(); var before = state.DeepClone();
    Reject(() => new BattleRelocation(state).MoveUnit(1, target), "invalid relocation destination accepted");
    Check(JsonNode.DeepEquals(state, before), "invalid destination cleared capture or moved a unit");
}
{
    var state = State(); state["map"]!["captureProgress"] = new JsonArray(); var before = state.DeepClone();
    Reject(() => new BattleRelocation(state).MoveUnit(1, destination), "missing capture storage accepted");
    Check(JsonNode.DeepEquals(state, before), "invalid capture storage partially moved a unit");
    state = State();
    state["map"]!["captureProgress"] = new JsonArray(Enumerable.Range(0, Index(state, origin) + 1)
        .Select(_ => (JsonNode?)JsonValue.Create(37)).ToArray());
    before = state.DeepClone();
    Reject(() => new BattleRelocation(state).MoveUnit(1, destination), "missing destination capture storage accepted");
    Check(JsonNode.DeepEquals(state, before), "missing destination capture storage partially moved a unit");

}
{
    var state = State(); var unit = Unit(state); var before = unit.DeepClone();
    var snapshot = new BattleRelocation(state).RemoveForEmbark(1);
    Check(TacticalQueries.UnitById(state, 1) is null && Capture(state, origin) == 0, "boarding leaves the origin cell");
    Check(snapshot["capture"]!.GetValue<int>() == 0, "boarding snapshot capture reset");
    Check(JsonNode.DeepEquals(unit, before) && !ReferenceEquals(snapshot, unit), "boarding retains detached snapshot semantics");
    Check(!snapshot["done"]!.GetValue<bool>() && snapshot["facing"]!.GetValue<string>() == "north",
        "boarding relocation does not impose readiness or facing policy");
    snapshot["directive"]!["cursor"] = 1;
    Check(unit["directive"]!["cursor"]!.GetValue<int>() == 0, "boarding snapshot owns nested state");
}
{
    var state = State(); var unit = Unit(state); var before = unit.DeepClone();
    new BattleRelocation(state).RemoveUnit(1);
    Check(TacticalQueries.UnitById(state, 1) is null && Capture(state, origin) == 0, "departure clears origin capture");
    Check(JsonNode.DeepEquals(unit, before), "departure preserves historical unit snapshot fields");
    var saved = state.DeepClone(); new BattleRelocation(state).RemoveUnit(1);
    Check(JsonNode.DeepEquals(state, saved), "repeated departure is inert");
}
{
    var state = State(); var events = new List<JsonObject>(); var partingShots = 0;
    state["rules"]!["zoneOfControl"] = true;
    engine.Commands.ApplyMovement(state, new JsonObject { ["unit"] = 1,
        ["path"] = new JsonArray(Point(1, 1), Point(1, 2)) }, events.Add, (draft, unit, from, to, emit) =>
    {
        partingShots++;
        Check(from == origin && to == destination && Capture(draft, origin) == 0 && TacticalQueries.Position(unit) == destination,
            "parting-shot policy sees completed relocation");
        Check(events[^1]["type"]!.GetValue<string>() == "move", "movement event precedes parting shots");
        emit(new JsonObject { ["type"] = "parting-observation" });
    });
    Check(partingShots == 1 && Unit(state)["directive"]!["cursor"]!.GetValue<int>() == 1 &&
        Unit(state)["facing"]!.GetValue<string>() == "south" && !Unit(state)["done"]!.GetValue<bool>(),
        "voluntary caller retains patrol facing and readiness policies");
    Check(events.Select(e => e["type"]!.GetValue<string>()).SequenceEqual(new[] { "facingChanged", "move", "parting-observation" }),
        "voluntary movement event order");
}
{
    var state = State(); state["units"]!.AsArray().RemoveAt(1); var events = new List<JsonObject>();
    engine.Combat.ForceMove(state, 1, new Coord(0, 1), "push", 1, 0, events.Add);
    Check(TacticalQueries.Position(Unit(state)) == new Coord(2, 1) && Capture(state, origin) == 0,
        "forced movement uses live relocation");
    Check(Unit(state)["facing"]!.GetValue<string>() == "east" && Unit(state)["directive"]!["cursor"]!.GetValue<int>() == 0 &&
        !Unit(state)["done"]!.GetValue<bool>(), "forced movement keeps separate facing patrol and readiness policy");
    Check(events.Count == 1 && events[0]["type"]!.GetValue<string>() == "forcedMove", "forced movement facing remains silent");
}
{
    var state = State(); var events = new List<JsonObject>();
    state["scenario"]!["zones"]!["teleport"] = new JsonArray(Point(1, 2));
    engine.ScenarioCommands.TeleportSelected(state, new JsonObject { ["selector"] = new JsonObject { ["ids"] = new JsonArray(1) }, ["zone"] = "teleport" }, events.Add);
    Check(TacticalQueries.Position(Unit(state)) == destination && Capture(state, origin) == 0, "teleport uses live relocation");
    Check(Unit(state)["facing"]!.GetValue<string>() == "north" && Unit(state)["directive"]!["cursor"]!.GetValue<int>() == 0 &&
        !Unit(state)["done"]!.GetValue<bool>(), "teleport preserves facing readiness and patrol");
    Check(events.Count == 1 && events[0]["mode"]!.GetValue<string>() == "teleport", "teleport owns its event policy");
}
{
    var state = State(); state["phase"] = "deployment";
    state["scenario"]!["zones"]!["deploy"] = new JsonArray(Point(1, 1), Point(2, 1));
    state["deployment"] = new JsonObject { ["assignments"] = new JsonArray(new JsonObject {
        ["player"] = 1, ["zone"] = "deploy", ["unitIds"] = new JsonArray(1, 2) }) };
    var events = new List<JsonObject>(); engine.Deployment.Deploy(state, 1, new Coord(2, 1), events.Add);
    Check(TacticalQueries.Position(Unit(state)) == new Coord(2, 1) && TacticalQueries.Position(Unit(state, 2)) == origin,
        "deployment swaps both placements");
    Check(Capture(state, origin) == 37 && Capture(state, new Coord(2, 1)) == 37,
        "deployment placement must preserve map capture");
    Check(Unit(state)["capture"]!.GetValue<int>() == 0 && Unit(state, 2)["capture"]!.GetValue<int>() == 0 &&
        Unit(state)["facing"]!.GetValue<string>() == "north" && Unit(state)["directive"]!["cursor"]!.GetValue<int>() == 0,
        "deployment resets unit capture without movement policy");
    Check(events.Count == 1 && events[0]["type"]!.GetValue<string>() == "unitDeployed", "deployment event order");
}
{
    var state = State(); var events = new List<JsonObject>(); engine.Transport.Embark(state, 1, 2, events.Add);
    var snapshot = state["embarkedUnits"]![0]!["unit"]!.AsObject();
    Check(snapshot["done"]!.GetValue<bool>() && snapshot["capture"]!.GetValue<int>() == 0 && Capture(state, origin) == 0,
        "transport caller finishes boarding and uses canonical capture reset");
    state["map"]!["captureProgress"]![Index(state, origin)] = 63;
    engine.Transport.Disembark(state, 2, 1, new Coord(2, 2), events.Add);
    Check(Capture(state, origin) == 63 && Capture(state, new Coord(2, 2)) == 37,
        "passenger placement must not clear historical origin or destination capture");
    Check(Unit(state)["done"]!.GetValue<bool>() && Unit(state)["facing"]!.GetValue<string>() == "north" &&
        events.Select(e => e["type"]!.GetValue<string>()).SequenceEqual(new[] { "unitEmbarked", "unitDisembarked" }),
        "transport preserves distinct boarding landing and event policies");
}
{
    var state = State(); var fallen = Unit(state).DeepClone().AsObject(); state["units"]!.AsArray().RemoveAt(0);
    var marker = new JsonObject { ["id"] = 7, ["kind"] = "withdrawn", ["at"] = Point(1, 1), ["fallenUnit"] = fallen };
    state["markers"]!.AsArray().Add(marker); var events = new List<JsonObject>();
    state["commanders"]!.AsArray().Add(new JsonObject { ["unitId"] = 1, ["owner"] = 1 });
    var before = state.DeepClone();
    Reject(() => BattleUnitReturn.Restore(engine.Catalog, state, marker, new UnitReturnRequest(destination, Owner: 999), events.Add),
        "return accepted an unknown player owner");
    Check(JsonNode.DeepEquals(state, before) && events.Count == 0, "invalid return owner mutated units markers commander or events");
    BattleUnitReturn.Restore(engine.Catalog, state, marker, new UnitReturnRequest(destination), events.Add);
    Check(Capture(state, origin) == 37 && Capture(state, destination) == 37 && Unit(state)["capture"]!.GetValue<int>() == 0,
        "return placement preserves map capture while resetting unit capture");
    Check(Unit(state)["done"]!.GetValue<bool>() && events.Select(e => e["type"]!.GetValue<string>()).SequenceEqual(new[] { "markerRemoved", "unitRevived" }),
        "return readiness and event policy remain explicit");
}
{
    var state = State(); var observed = false;
    engine.Combat.Damage.Resolve(state, 1, 100, false, _ => new JsonObject { ["type"] = "hit" }, _ =>
    {
        observed = true;
        Check(Capture(state, origin) == 0 && TacticalQueries.UnitById(state, 1) is null &&
            state["markers"]![0]!["fallenUnit"]!["capture"]!.GetValue<int>() == 17,
            "lethal departure preserves corpse snapshot before ordered reports");
    });
    Check(observed, "lethal departure reports events");
}
{
    var state = State();
    var registry = new BattleActionRegistry().Register("bad-relocation", (draft, _, emit) =>
    {
        draft["turn"] = 999; emit(new JsonObject { ["type"] = "draft-only" });
        new BattleRelocation(draft).MoveUnit(1, new Coord(2, 1));
    });
    var battle = new Battle(state, registry); var before = battle.Snapshot(); var hash = battle.StateHash;
    Reject(() => battle.Dispatch(new JsonObject { ["kind"] = "bad-relocation" }), "transaction accepted invalid relocation");
    Check(JsonNode.DeepEquals(before, battle.Snapshot()) && hash == battle.StateHash, "invalid relocation committed draft state or events");
}
Console.WriteLine($"PASS {checks} external-consumer relocation assertions: capture ownership, atomic validation, distinct movement/placement policies and transaction rollback");
