using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Composition;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;
using Keys = Empire.Core.Composition.BattleCapabilities;

static class InstallationContractChecks
{
    internal static void Run(JsonArray sourcePacks, JsonObject sourceLevel,
        Action<bool, string> check, Action<JsonNode?, JsonNode?, string> equal)
    {
        var packs = (JsonArray)sourcePacks.DeepClone();
        var actorDefinition = packs.OfType<JsonObject>().SelectMany(pack => pack["units"]?.AsArray().OfType<JsonObject>() ?? [])
            .Single(unit => StateJson.Text(unit, "id") == "c01.swordsman");
        actorDefinition["movement"] = 1;
        actorDefinition["vision"] = 1;
        var catalog = new ReadOnlyContent(packs);
        foreach (var gridId in new[] { "square4", "test.diagonal" })
        {
            var level = (JsonObject)sourceLevel.DeepClone();
            level["width"] = 6;
            level["height"] = 6;
            level["terrain"] = new JsonArray(Enumerable.Repeat("......", 6).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
            level["elevation"] = new JsonArray(Enumerable.Range(0, 36).Select(_ => (JsonNode?)JsonValue.Create(0)).ToArray());
            level["owners"] = new JsonArray();
            level["commanders"] = new JsonArray();
            level["scenario"] = new JsonObject { ["triggers"] = new JsonArray() };
            level["units"] = new JsonArray(
                new JsonObject { ["unit"] = "c01.swordsman", ["owner"] = 1, ["x"] = 1, ["y"] = 1 },
                new JsonObject { ["unit"] = "c01.swordsman", ["owner"] = 2, ["x"] = 4, ["y"] = 4 });
            var objectives = new JsonArray(new JsonObject { ["id"] = "route", ["type"] = "routEnemies", ["label"] = "route" });
            level["victory"] = objectives.DeepClone();
            foreach (var player in level["players"]!.AsArray().OfType<JsonObject>()) player["objectives"] = objectives.DeepClone();
            level["rules"]!["grid"] = gridId;
            level["rules"]!["fog"] = true;
            level["rules"]!["zoneOfControl"] = false;
            GridRegistry? displaced = null;
            KernelPluginContext? retained = null;
            var plugin = new EnginePlugin($"test.diagonal-grid.{gridId}", 1, context =>
            {
                retained = context;
                displaced = context.Require(Keys.Grids);
                var grids = displaced.Clone().Replace(new DiagonalGrid(gridId));
                context.Replace(Keys.Grids, grids);
            }, overrides: [Keys.Grids], requiresCapabilities: [Keys.Grids]);
            var engine = BattleEngineFactory.Create(catalog, [plugin]);
            var space = (DefaultTacticalSpace)engine.Space;
            check(ReferenceEquals(space.Content, catalog), $"{gridId}: spatial content identity");
            check(ReferenceEquals(space.Grids, engine.Grids) && !ReferenceEquals(space.Grids, displaced), $"{gridId}: replacement captured before spatial installation");
            displaced!.Replace(new Square4Grid());
            try { engine.Grids.Replace(new DiagonalGrid(gridId)); throw new Exception("resolved grid registry remained writable"); }
            catch (DomainInvariantException error)
            {
                check(error.Message == "tactical grid registry is sealed after composition", $"{gridId}: resolved registry sealing");
            }
            var battle = engine.CreateBattle(level, 12345);
            var initial = battle.Snapshot();
            var actor = initial["units"]![0]!.AsObject();
            var field = engine.Space.ComputeMoveField(initial, actor);
            check(field.Tiles[14].Cost == 1, $"{gridId}: replacement diagonal movement cost");
            var path = engine.Space.PathTo(field, initial, new(2, 2));
            check(path is not null && path.SequenceEqual(new[] { new Coord(1, 1), new Coord(2, 2) }), $"{gridId}: canonical diagonal path");
            var seen = engine.Space.VisibleTiles(initial, 1);
            check(seen.Count == 9 && seen.Contains(14), $"{gridId}: replacement geometry reaches fog visibility");
            if (gridId == "square4")
            {
                var defaults = BattleEngineFactory.Create(catalog);
                var baseline = defaults.CreateState(level, 12345);
                check(!defaults.Space.ComputeMoveField(baseline, baseline["units"]![0]!.AsObject()).Tiles.ContainsKey(14), "default movement distinguishes the replacement");
                check(!defaults.Space.VisibleTiles(baseline, 1).Contains(14), "default fog distinguishes the replacement");
            }
            var move = new JsonObject
            {
                ["kind"] = "command", ["unit"] = actor["id"]!.DeepClone(),
                ["path"] = new JsonArray(new JsonObject { ["x"] = 1, ["y"] = 1 }, new JsonObject { ["x"] = 2, ["y"] = 2 }),
                ["command"] = new JsonObject { ["ability"] = "wait" },
            };
            var recorder = engine.RecordBattle(initial);
            var moved = battle.Dispatch(move);
            recorder.Record(move);
            check(moved.Events.Any(item => StateJson.Text(item, "type") == "move"), $"{gridId}: movement command emitted its event");
            check(TacticalQueries.Position(battle.Snapshot()["units"]![0]!.AsObject()) == new Coord(2, 2), $"{gridId}: actual movement uses the replacement");
            var save = engine.SaveBattle(battle.Snapshot(), "2026-10-21T00:00:00.000Z");
            var restoredEngine = BattleEngineFactory.Create(catalog, [plugin]);
            var resumed = restoredEngine.LoadSavedBattle(save);
            equal(resumed.Snapshot(), battle.Snapshot(), $"{gridId}: named plugin save round trip");
            var endTurn = new JsonObject { ["kind"] = "endTurn" };
            equal(new JsonArray(battle.Dispatch(endTurn).Events.Select(item => item.DeepClone()).ToArray()),
                new JsonArray(resumed.Dispatch(endTurn).Events.Select(item => item.DeepClone()).ToArray()), $"{gridId}: resumed events");
            recorder.Record(endTurn);
            equal(resumed.Snapshot(), battle.Snapshot(), $"{gridId}: resumed state");
            var replay = restoredEngine.ReplayBattle(level, recorder.Replay(battle.Snapshot()));
            check(replay.DivergedAt is null, $"{gridId}: replay must not diverge");
            equal(replay.State, battle.Snapshot(), $"{gridId}: replay exact final state");
            foreach (var misuse in new Action[]
            {
                () => retained!.Replace(Keys.Grids, GridRegistry.Core()),
                () => retained!.Provide(new CapabilityKey<string>("test.late"), "late"),
                () => retained!.Require(Keys.Resources),
            })
            {
                try { misuse(); throw new Exception("retained engine context remained usable"); }
                catch (DomainInvariantException error) { check(error.Message.EndsWith("installation context is closed", StringComparison.Ordinal), "closed engine context fails explicitly"); }
            }
            equal(engine.SaveBattle(initial, "2026-10-21T00:00:00.000Z")["ruleset"], engine.RulesetManifest, "retained context cannot corrupt persisted composition identity");
        }
    }

    private sealed class DiagonalGrid(string id) : SquareTiling
    {
        private readonly Square8Grid geometry = new();
        public override string Id => id;
        public override IReadOnlyList<DirectionDef> Directions => geometry.Directions;
        public override int Distance(Coord a, Coord b) => geometry.Distance(a, b);
        public override string Toward(Coord from, Coord to) => geometry.Toward(from, to);
    }
}
