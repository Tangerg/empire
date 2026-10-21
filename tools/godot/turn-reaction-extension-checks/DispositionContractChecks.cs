using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Combat;
using Empire.Core.Composition;
using Empire.Core.Geometry;
using Empire.Core.Engine;
using Empire.Core.Persistence;
using Empire.Core.State;
using Keys = Empire.Core.Composition.BattleCapabilities;

static class DispositionContractChecks
{
    internal static JsonObject Level(string grid) => new()
    {
        ["schema"] = 2, ["id"] = "native.disposition", ["name"] = "Disposition contracts",
        ["width"] = 7, ["height"] = 7,
        ["terrain"] = new JsonArray(Enumerable.Repeat(".......", 7).Select(row => (JsonNode?)JsonValue.Create(row)).ToArray()),
        ["players"] = new JsonArray(
            new JsonObject { ["id"] = 1, ["name"] = "One", ["team"] = 1, ["controller"] = "human", ["color"] = "#ffffff" },
            new JsonObject { ["id"] = 2, ["name"] = "Two", ["team"] = 2, ["controller"] = "human", ["color"] = "#000000" }),
        ["units"] = new JsonArray(
            new JsonObject { ["unit"] = "soldier", ["owner"] = 1, ["x"] = 2, ["y"] = 3 },
            new JsonObject { ["unit"] = "soldier", ["owner"] = 2, ["x"] = 5, ["y"] = 3 }),
        ["rules"] = new JsonObject { ["grid"] = grid, ["fog"] = false, ["zoneOfControl"] = false },
        ["victory"] = new JsonArray(new JsonObject { ["id"] = "route", ["type"] = "routEnemies", ["label"] = "Route" }),
    };

    internal static void Run(JsonArray packs, Action<bool, string> check, Action<JsonNode?, JsonNode?, string> equal)
    {
        var catalog = new ReadOnlyContent(packs);
        var plugin = new EnginePlugin("test.disposition", 1, context =>
        {
            context.Replace(Keys.Grids, context.Require(Keys.Grids).Clone().Register(new RenamedGrid()));
            context.Replace(Keys.Reactions, context.Require(Keys.Reactions).Clone().Register(
                new("test.stance", "Test", false, 1, false, false, "Test")));
        }, overrides: [Keys.Grids, Keys.Reactions], requiresCapabilities: [Keys.Grids, Keys.Reactions]);
        var engine = BattleEngineFactory.Create(catalog, [plugin]);
        foreach (var grid in engine.Grids.All())
        {
            var level = Level(grid.Id);
            var battle = engine.CreateBattle(level, 123);
            var initial = battle.Snapshot();
            var recorder = engine.RecordBattle(initial);
            var reaction = new JsonObject { ["kind"] = "reaction", ["unit"] = 1, ["stance"] = "test.stance" };
            var receipt = battle.Dispatch(reaction);
            recorder.Record(reaction);
            check(StateJson.Text(battle.Snapshot()["units"]![0]!.AsObject(), "reaction") == "test.stance", grid.Id + " custom reaction command");
            foreach (var invalid in new[]
            {
                new JsonObject { ["kind"] = "reaction", ["unit"] = 1, ["stance"] = "absent" },
                new JsonObject { ["kind"] = "face", ["unit"] = 1, ["facing"] = "absent" },
                new JsonObject { ["kind"] = "face", ["unit"] = 1, ["facing"] = grid.Id == "hex" ? "north" : "hexEast" },
            })
            {
                var before = battle.Snapshot(); var hash = battle.StateHash;
                try { battle.Dispatch(invalid); throw new Exception("invalid disposition command committed"); }
                catch (Exception error) when (error is IllegalActionException or DomainInvariantException)
                { check(error is IllegalActionException, "invalid disposition is an illegal action"); }
                equal(battle.Snapshot(), before, "invalid disposition atomic state and event counters");
                check(battle.StateHash == hash, "invalid disposition atomic digest");
            }
            // A rejected command must not replace the last successful undo receipt.
            battle.Restore(receipt);
            equal(battle.Snapshot(), initial, "invalid disposition preserves undo checkpoint");
            battle.Dispatch(reaction);
            foreach (var direction in grid.Directions)
            {
                var action = new JsonObject { ["kind"] = "face", ["unit"] = 1, ["facing"] = direction.Id };
                var result = battle.Dispatch(action);
                recorder.Record(action);
                check(StateJson.Text(battle.Snapshot()["units"]![0]!.AsObject(), "facing") == direction.Id, grid.Id + " accepts declared direction " + direction.Id);
                check(result.Events.All(value => StateJson.Text(value, "type") == "facingChanged"), "facing event vocabulary unchanged");
            }
            var saved = engine.SaveBattle(battle.Snapshot(), "2026-10-21T00:00:00.000Z");
            var recreated = BattleEngineFactory.Create(catalog, [plugin]);
            equal(recreated.LoadBattle(saved), battle.Snapshot(), grid.Id + " accepted dispositions save round trip");
            var replay = recreated.ReplayBattle(level, recorder.Replay(battle.Snapshot()));
            check(replay.DivergedAt is null, grid.Id + " accepted dispositions replay");
            equal(replay.State, battle.Snapshot(), grid.Id + " accepted dispositions replay state");
            foreach (var key in new[] { "reaction", "facing" })
            {
                var corrupt = (JsonObject)saved.DeepClone();
                corrupt["state"]!["units"]![0]![key] = "absent";
                try { engine.LoadBattle(corrupt); throw new Exception("invalid saved disposition accepted"); }
                catch (StoredDocumentException error) { check(error.Message.Length > 0, "invalid saved " + key + " rejected"); }
                var invalidLevel = (JsonObject)level.DeepClone();
                invalidLevel["units"]![0]![key] = "absent";
                try { engine.CreateBattle(invalidLevel); throw new Exception("invalid authored disposition accepted"); }
                catch (BattleLevelException error) { check(error.Message.Length > 0, "invalid authored " + key + " rejected"); }
            }
            foreach (var container in new[] { "embarkedUnits", "markers" })
            {
                var archivedLevel = Level(grid.Id);
                archivedLevel["units"]!.AsArray().Add(new JsonObject { ["unit"] = "c01.supply-wagon", ["owner"] = 1, ["x"] = 2, ["y"] = 4 });
                var archived = engine.CreateState(archivedLevel, 123);
                var unit = archived["units"]![0]!.DeepClone();
                archived["units"]!.AsArray().RemoveAt(0);
                archived[container]!.AsArray().Add(container == "embarkedUnits"
                    ? new JsonObject { ["carrier"] = 3, ["unit"] = unit }
                    : new JsonObject { ["id"] = 1, ["fallenUnit"] = unit });
                archived["nextMarkerId"] = 2;
                var archivedSave = engine.SaveBattle(archived, "2026-10-21T00:00:00.000Z");
                var snapshot = archivedSave["state"]![container]![0]![container == "embarkedUnits" ? "unit" : "fallenUnit"]!.AsObject();
                snapshot["facing"] = "absent";
                try { engine.LoadBattle(archivedSave); throw new Exception("invalid archived facing accepted"); }
                catch (StoredDocumentException error)
                { check(error.Message.Contains("has a facing not admitted", StringComparison.Ordinal), container + " uses canonical facing admission"); }
            }
            var state = battle.Snapshot(); var actor = state["units"]![0]!.AsObject(); var events = new JsonArray();
            engine.Commands.TurnToward(state, actor, new(2, 3), new(2, 2), value => events.Add(value));
            check(StateJson.Text(actor, "facing") == grid.Toward(new(2, 3), new(2, 2)), grid.Id + " automatic facing uses composed tiling");
            var beforeInternal = (JsonObject)state.DeepClone();
            try { UnitDisposition.Face(grid, actor, "absent", value => events.Add(value)); throw new Exception("invalid internal facing accepted"); }
            catch (DomainInvariantException error) { check(error.Message.Contains("no direction", StringComparison.Ordinal), "internal facing invariant failure"); }
            equal(state, beforeInternal, "internal invalid facing has no write");
            try { UnitDisposition.React(engine.Reactions, actor, "absent", value => events.Add(value)); throw new Exception("invalid internal reaction accepted"); }
            catch (DomainInvariantException error) { check(error.Message.Contains("unknown reaction stance", StringComparison.Ordinal), "internal reaction invariant failure"); }
            equal(state, beforeInternal, "internal invalid reaction has no write");
            CheckSpawns(engine, level, grid, check, equal);
        }
    }

    private static void CheckSpawns(GrayFlagEngine engine, JsonObject level, ITacticalGrid grid,
        Action<bool, string> check, Action<JsonNode?, JsonNode?, string> equal)
    {
        var seed = new JsonObject
        {
            ["unit"] = "soldier", ["owner"] = 1, ["x"] = 1, ["y"] = 1,
            ["reaction"] = "test.stance", ["facing"] = grid.Directions[0].Id,
        };
        var state = engine.CreateState(level, 123);
        var initial = (JsonObject)state.DeepClone();
        var events = new JsonArray();
        foreach (var field in new[] { "reaction", "facing" })
        {
            var invalid = (JsonObject)seed.DeepClone();
            invalid[field] = "absent";
            try
            {
                engine.ScenarioCommands.Spawn(state, new JsonObject { ["units"] = new JsonArray(invalid) }, value => events.Add(value));
                throw new Exception("invalid scenario spawn disposition accepted");
            }
            catch (DomainInvariantException error)
            {
                check(error.Message.Contains(field == "reaction" ? "unknown reaction stance" : "no direction", StringComparison.Ordinal),
                    "invalid authored spawn " + field + " is a domain invariant");
            }
            equal(state, initial, "invalid spawn preserves all state including nextUnitId");
            check(events.Count == 0, "invalid spawn emits no events");
        }
        engine.ScenarioCommands.Spawn(state, new JsonObject { ["units"] = new JsonArray(seed), ["ready"] = true }, value => events.Add(value));
        var spawned = state["units"]!.AsArray()[^1]!.AsObject();
        check(StateJson.Text(spawned, "reaction") == "test.stance" && StateJson.Text(spawned, "facing") == grid.Directions[0].Id,
            grid.Id + " scenario spawn admits composed reaction and facing");
        check(StateJson.Integer(spawned, "id") == StateJson.Integer(initial, "nextUnitId") &&
            StateJson.Integer(state, "nextUnitId") == StateJson.Integer(initial, "nextUnitId") + 1,
            "only successful spawn consumes the next unit ID");
        check(events.Count == 1 && StateJson.Text(events[0]!.AsObject(), "type") == "unitSpawned", "spawn event vocabulary unchanged");
        equal(engine.LoadBattle(engine.SaveBattle(state, "2026-10-21T00:00:00.000Z")), state,
            grid.Id + " custom spawn disposition survives save/load");
    }

    sealed class RenamedGrid : ITacticalGrid
    {
        private readonly Square4Grid square = new();
        public string Id => "test.renamed-grid";
        public string Name => "Renamed directions";
        private static string Encode(string value) => "test." + value;
        private static string Decode(string value) => value[5..];
        public IReadOnlyList<DirectionDef> Directions => square.Directions.Select(value => new DirectionDef(Encode(value.Id), value.Name)).ToArray();
        public string RestingFacing => Encode(square.RestingFacing);
        public int Distance(Coord a, Coord b) => square.Distance(a, b);
        public Coord Step(Coord at, string direction) => square.Step(at, Decode(direction));
        public string Opposite(string direction) => Encode(square.Opposite(Decode(direction)));
        public string Toward(Coord from, Coord to) => Encode(square.Toward(from, to));
        public IReadOnlyList<Coord> Adjacent(Coord at) => square.Adjacent(at);
        public IReadOnlyList<Coord> Within(Coord at, int min, int max) => square.Within(at, min, max);
        public IReadOnlyList<Coord> Line(Coord from, Coord to) => square.Line(from, to);
        public TilePoint Center(Coord at) => square.Center(at);
        public Coord CellAt(TilePoint point) => square.CellAt(point);
        public IReadOnlyList<TilePoint> Outline() => square.Outline();
        public TilePoint Extent(GridExtent size) => square.Extent(size);
    }
}
