using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Tactics;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(Path.Combine(root, path)))!;
var packs = Read("godot/data/content-packs.json").AsArray();
var grids = new GridRegistry().Register(new Square4Grid()).Register(new Square8Grid()).Register(new OddRowHexGrid());
var movement = new MovementRules(packs, grids);
var turns = new TurnOrderSystem(packs);
var commands = new TacticalCommands(movement, turns.MayAct, DirectiveArrivalRegistry.Core());
var cases = Read("godot/tests/oracle/action-cases.json").AsArray();
var passed = 0;
var failed = 0;

void Check(string name, Action test)
{
    try { test(); passed++; }
    catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error}"); }
}

void EqualJson(JsonNode? expected, JsonNode? actual, string subject)
{
    if (expected is null && actual is null) return;
    if (expected is null || actual is null || StateDigest.Hash(expected) != StateDigest.Hash(actual))
        throw new Exception($"{subject} mismatch\nExpected: {expected}\nActual: {actual}");
}

void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

JsonArray Integers(IEnumerable<int> values)
{
    var result = new JsonArray();
    foreach (var value in values) result.Add(value);
    return result;
}

Coord Point(JsonNode node) => new(node["x"]!.GetValue<int>(), node["y"]!.GetValue<int>());

Action<JsonObject> RecordingEmit(JsonObject state, JsonArray events) => message =>
{
    var kind = message["type"]!.GetValue<string>();
    var counts = state["scenario"]!["eventCounts"]!.AsObject();
    counts[kind] = (counts[kind]?.GetValue<int>() ?? 0) + 1;
    events.Add(message.DeepClone());
};

var waitCases = cases.OfType<JsonObject>().Where(entry =>
    entry["action"]!["kind"]!.GetValue<string>() == "command" &&
    entry["action"]!["command"]!["ability"]!.GetValue<string>() == "wait").ToArray();
foreach (var entry in waitCases)
{
    Check($"wait action {cases.IndexOf(entry)}", () =>
    {
        var state = (JsonObject)entry["before"]!.DeepClone();
        var events = new JsonArray();
        commands.ExecuteWait(state, entry["action"]!.AsObject(), RecordingEmit(state, events));
        EqualJson(entry["after"], state, "after state");
        EqualJson(entry["events"], events, "events");
    });
}
Console.WriteLine($"Checked {waitCases.Length} live TypeScript wait-action oracles.");

var vectors = Read("godot/tests/oracle/movement-vectors.json").AsArray();
foreach (var vector in vectors.OfType<JsonObject>())
{
    Check($"movement query {vector["name"]}", () =>
    {
        var state = vector["state"]!.AsObject();
        var before = StateDigest.Hash(state);
        var unit = TacticalQueries.RequireUnit(state, vector["unit"]!.GetValue<int>());
        var field = movement.ComputeMoveField(state, unit);
        var tiles = new JsonArray();
        foreach (var tile in field.Tiles.Values)
            tiles.Add(new JsonObject
            {
                ["index"] = tile.Index, ["x"] = tile.X, ["y"] = tile.Y,
                ["cost"] = tile.Cost, ["from"] = tile.From, ["free"] = tile.Free,
            });
        EqualJson(vector["tiles"], tiles, "ordered reachable tiles");
        EqualJson(vector["stops"], Integers(field.Stops), "ordered stopping points");
        foreach (var path in vector["paths"]!.AsArray().OfType<JsonObject>())
        {
            var target = Point(path["to"]!);
            var actualPath = movement.PathTo(field, state, target);
            EqualJson(path["path"], actualPath is null ? null : TacticalQueries.PathJson(actualPath), "canonical path");
            if (movement.MoveCostOf(field, state, target) != path["cost"]!.GetValue<int>())
                throw new Exception("move cost mismatch");
        }
        var origin = TacticalQueries.Position(unit);
        EqualJson(vector["attackTiles"], TacticalQueries.PathJson(movement.AttackTilesFrom(state, unit, origin)), "attack tiles");
        EqualJson(vector["attackTargets"], TacticalQueries.PathJson(movement.AttackTargetCoords(state, unit, origin)), "attack targets");
        EqualJson(vector["healTargets"], Integers(movement.HealTargetsFrom(state, unit, origin).Select(u => u["id"]!.GetValue<int>())), "heal targets");
        EqualJson(vector["threat"], Integers(movement.ThreatTiles(state, unit, field)), "threat union");
        foreach (var sight in vector["lineOfSight"]!.AsArray().OfType<JsonObject>())
            if (movement.HasDirectLineOfSight(state, origin, Point(sight["at"]!)) != sight["visible"]!.GetValue<bool>())
                throw new Exception("line of sight mismatch");
        if (StateDigest.Hash(state) != before) throw new Exception("tactical query mutated state");
    });
}
Console.WriteLine($"Checked {vectors.Count} ordered movement, targeting, sight and threat vectors.");

var sample = waitCases[0];
void Reject(string name, Action<JsonObject, JsonObject> edit)
{
    Check(name, () =>
    {
        var state = (JsonObject)sample["before"]!.DeepClone();
        var action = (JsonObject)sample["action"]!.DeepClone();
        edit(state, action);
        var before = StateDigest.Hash(state);
        Throws<IllegalActionException>(() => commands.ExecuteWait(state, action, _ => throw new Exception("rejection emitted an event")));
        if (StateDigest.Hash(state) != before) throw new Exception("rejection mutated state");
    });
}
Reject("empty path", (_, action) => action["path"] = new JsonArray());
Reject("wrong path origin", (_, action) => action["path"]![0] = new JsonObject { ["x"] = -1, ["y"] = -1 });
Reject("unknown actor", (_, action) => action["unit"] = 999999);
Reject("already spent actor", (state, action) => TacticalQueries.RequireUnit(state, action["unit"]!.GetValue<int>())["done"] = true);
Reject("wrong owner", (state, action) => TacticalQueries.RequireUnit(state, action["unit"]!.GetValue<int>())["owner"] = 2);
Reject("wait with a weapon", (_, action) => action["command"]!["weapon"] = "c01.ranger-bow");
Reject("occupied stopping point", (state, action) =>
{
    var unit = TacticalQueries.RequireUnit(state, action["unit"]!.GetValue<int>());
    var field = movement.ComputeMoveField(state, unit);
    var occupied = field.Tiles.Values.First(tile => !tile.Free);
    action["path"] = TacticalQueries.PathJson(new[] { TacticalQueries.Position(unit), new Coord(occupied.X, occupied.Y) });
});
Reject("unreachable destination", (state, action) =>
{
    var unit = TacticalQueries.RequireUnit(state, action["unit"]!.GetValue<int>());
    action["path"] = TacticalQueries.PathJson(new[] { TacticalQueries.Position(unit), new Coord(26, 16) });
});
Check("client path interior is nonauthoritative", () =>
{
    var state = (JsonObject)sample["before"]!.DeepClone();
    var action = (JsonObject)sample["action"]!.DeepClone();
    action["path"]![1] = new JsonObject { ["x"] = -999, ["y"] = 999 };
    var events = new JsonArray();
    commands.ExecuteWait(state, action, RecordingEmit(state, events));
    EqualJson(sample["after"], state, "canonical rerouting");
    EqualJson(sample["events"], events, "canonical rerouting events");
});
Console.WriteLine($"Tactical checks: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
