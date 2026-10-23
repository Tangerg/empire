using Empire.Checks;
using System.IO.Compression;
using System.Text.Json.Nodes;
using Empire.Core.Composition;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.State;
using Keys = Empire.Core.Composition.BattleCapabilities;

static class StateGridChecks
{
    internal static void Run(string root, Action<bool, string> check, Action<JsonNode?, JsonNode?, string> equal)
    {
        using var source = new GZipStream(File.OpenRead(Path.Combine(root,
            "tools/godot/engine-composition-checks/state-grid-oracle.json.gz")), CompressionMode.Decompress);
        var oracle = JsonNode.Parse(source)!.AsObject();
        var packs = EcmaJson.Parse(File.ReadAllText(Path.Combine(root, "godot/data/content-packs.json")))!.AsArray();
        foreach (var vector in oracle["cases"]!.AsArray().OfType<JsonObject>())
        {
            var id = EcmaJson.ReadString(vector["id"]);
            ITacticalGrid? resolved = null;
            var plugin = new EnginePlugin($"test.state-grid.{id}", 1, context =>
            {
                var grids = context.Require(Keys.Grids).Clone();
                resolved = new NorthGrid(grids.Get("square4"), id);
                if (grids.Has(id))
                    grids.Replace(resolved);
                else
                    grids.Register(resolved);
                context.Replace(Keys.Grids, grids);
            }, overrides: [Keys.Grids], requiresCapabilities: [Keys.Grids]);
            var engine = BattleEngineFactory.Create(new ReadOnlyContent(packs), [plugin]);
            check(ReferenceEquals(engine.Grids.Get(id), resolved) == vector["resolvedIdentity"]!.GetValue<bool>(), $"{id} resolved grid identity");
            equal(engine.RulesetManifest, FixtureRuleset.ExpectedNative(vector["manifest"]!.AsObject()), $"{id} named grid manifest");
            var level = vector["level"]!.AsObject();
            equal(engine.CreateState(level, 12345), vector["initial"], $"{id} composed initial facing");
            var battle = engine.CreateBattle(level, 12345);
            equal(battle.Snapshot(), vector["initial"], $"{id} composed battle initial facing");
            var recorder = engine.RecordBattle(battle.Snapshot());
            var action = vector["action"]!.AsObject();
            equal(new JsonArray(battle.Dispatch(action).Events.Select(e => e.DeepClone()).ToArray()), vector["events"], $"{id} spawn events");
            recorder.Record(action);
            var state = battle.Snapshot();
            equal(state, vector["state"], $"{id} spawned full state");
            var facings = new JsonArray(state["units"]!.AsArray().OfType<JsonObject>().Select(unit =>
            {
                var value = new JsonObject { ["id"] = unit["id"]!.DeepClone(), ["facing"] = unit["facing"]!.DeepClone() };
                if (unit["key"] is { } key)
                    value["key"] = key.DeepClone();
                return (JsonNode?)value;
            }).ToArray());
            equal(facings, vector["facings"], $"{id} initial/spawn default and authored facings");
            var save = engine.SaveBattle(state, "2026-10-21T00:00:00.000Z");
            equal(save, FixtureRuleset.NativeDocument(vector["save"]!.AsObject()), $"{id} grid save");
            equal(engine.LoadBattle(save), vector["loaded"], $"{id} grid load");
            var replay = recorder.Replay(state);
            equal(replay, FixtureRuleset.NativeDocument(vector["replay"]!.AsObject()), $"{id} grid replay document");
            equal(engine.ReplayBattle(level, replay).State, vector["replayed"], $"{id} grid replay state");
        }
    }

    private sealed class NorthGrid(ITacticalGrid geometry, string id) : ITacticalGrid
    {
        public string Id => id;
        public IReadOnlyList<DirectionDef> Directions => geometry.Directions;
        public string RestingFacing => "north";
        public int Distance(Coord a, Coord b) => geometry.Distance(a, b);
        public Coord Step(Coord at, string direction) => geometry.Step(at, direction);
        public string Opposite(string direction) => geometry.Opposite(direction);
        public string Toward(Coord from, Coord to) => geometry.Toward(from, to);
        public IReadOnlyList<Coord> Adjacent(Coord at) => geometry.Adjacent(at);
        public IReadOnlyList<Coord> Within(Coord at, int min, int max) => geometry.Within(at, min, max);
        public IReadOnlyList<Coord> Line(Coord from, Coord to) => geometry.Line(from, to);
        public TilePoint Center(Coord at) => geometry.Center(at);
        public Coord CellAt(TilePoint point) => geometry.CellAt(point);
        public IReadOnlyList<TilePoint> Outline() => geometry.Outline();
        public TilePoint Extent(GridExtent size) => geometry.Extent(size);
    }
}
