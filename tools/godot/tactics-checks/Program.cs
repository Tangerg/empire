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
var directives = UnitDirectiveRegistry.Core().Seal();
var space = new DefaultTacticalSpace(packs, grids);
var commands = new TacticalCommands(new(space.Content, grids, space), turns.MayAct, directives);
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

var directiveVectors = Read("godot/tests/oracle/directive-vectors.json").AsArray();
foreach (var vector in directiveVectors.OfType<JsonObject>())
{
    Check($"directive behavior {vector["name"]}", () =>
    {
        var state = (JsonObject)vector["state"]!.DeepClone();
        var unit = TacticalQueries.RequireUnit(state, vector["unit"]!.GetValue<int>());
        var before = StateDigest.Hash(state);
        foreach (var pull in vector["pulls"]!.AsArray().OfType<JsonObject>())
            if (directives.Pull(state, unit, Point(pull["at"]!), movement.Grid(state)) != pull["value"]!.GetValue<double>())
                throw new Exception("directive pull mismatch");
        if (directives.Engagement(unit) != vector["engagement"]!.GetValue<double>() ||
            directives.FightPenalty(unit) != vector["fightPenalty"]!.GetValue<double>())
            throw new Exception("directive combat factors mismatch");
        if (StateDigest.Hash(state) != before) throw new Exception("directive appraisal mutated state");
        foreach (var at in vector["arrivalPath"]!.AsArray()) directives.ArriveAt(unit, Point(at!));
        EqualJson(vector["afterDirective"], unit["directive"], "directive arrival");
    });
}
Console.WriteLine($"Checked {directiveVectors.Count} directive appraisal and arrival vectors.");
Check("directive registry extension, replacement, clone and seal", () =>
{
    var custom = new UnitDirectiveBehavior("test.forage", survey => survey.At.Y * 40, 0.5, -20,
        (unit, at) => unit["capture"] = at.X);
    var registry = directives.Clone().Register(custom);
    var state = (JsonObject)vectors[0]!["state"]!.DeepClone();
    var unit = state["units"]![0]!.AsObject();
    unit["directive"]!["mode"] = custom.Id;
    if (registry.Pull(state, unit, new Coord(0, 1), movement.Grid(state)) != 40 ||
        registry.Engagement(unit) != 0.5 || registry.FightPenalty(unit) != -20)
        throw new Exception("custom directive not dispatched");
    registry.ArriveAt(unit, new Coord(3, 0));
    if (unit["capture"]!.GetValue<int>() != 3) throw new Exception("custom arrival not dispatched");
    Throws<DomainInvariantException>(() => registry.Register(custom));
    registry.Replace(custom with { Engagement = 0.8 });
    if (registry.Engagement(unit) != 0.8 || directives.Has(custom.Id))
        throw new Exception("registry replacement or clone isolation failed");
    registry.Seal();
    Throws<DomainInvariantException>(() => registry.Replace(custom));
    Throws<DomainInvariantException>(() => directives.Get("missing"));
});

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
Check("off-board destination cannot alias a reachable flattened index", () =>
{
    var vector = vectors.OfType<JsonObject>().Single(entry => entry["name"]!.GetValue<string>() == "terrain-and-occupancy");
    var state = (JsonObject)vector["state"]!.DeepClone();
    var unitId = vector["unit"]!.GetValue<int>();
    var unit = TacticalQueries.RequireUnit(state, unitId);
    var origin = TacticalQueries.Position(unit);
    var field = movement.ComputeMoveField(state, unit);
    var width = state["map"]!["width"]!.GetValue<int>();
    var valid = new Coord(0, 1);
    if (movement.PathTo(field, state, valid) is null)
        throw new Exception("regression requires reachable row-adjacent alias");
    var before = StateDigest.Hash(state);
    foreach (var outside in new[] { new Coord(width, 0), new Coord(-1, 1), new Coord(width, -1) })
    {
        if (movement.PathTo(field, state, outside) is not null ||
            movement.MoveCostOf(field, state, outside) is not null)
            throw new Exception($"off-board query resolved {outside}");
        var action = new JsonObject
        {
            ["kind"] = "command",
            ["unit"] = unitId,
            ["path"] = TacticalQueries.PathJson(new[] { origin, outside }),
            ["command"] = new JsonObject { ["ability"] = "wait" },
        };
        Throws<IllegalActionException>(() => commands.ExecuteWait(state, action, _ => throw new Exception("rejection emitted an event")));
        if (StateDigest.Hash(state) != before) throw new Exception("off-board rejection mutated state");
    }
});
Console.WriteLine($"Tactical checks: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
