#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Application;
using Empire.Localization;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

public partial class GrainRoadChecks : Node
{
    private int assertions;
    private void Require(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
    public override async void _Ready()
    {
        try
        {
            await Check();
            PaintedPickingChecks.CheckOpenMouth(this, Require);
            GD.Print($"GRAIN_ROAD_CHECKS_OK {assertions} assertions; reviewed courtyard layout; not visual acceptance.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task Check()
    {
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-08");
        Require(EcmaJson.Sha256(level) == "1f4ca03f82ad5f66ef0386d154697b1cb770eb3b0fc054bc7844e756b7e7712c", "C08 must match the reviewed courtyard terrain and unchanged roster, objectives and ledger trigger.");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var state = engine.CreateState(level);
        var unchanged = state.ToJsonString();
        var map = state["map"]!.AsObject();
        var scene = AuthoredScene.Load("res://data/scenes/c01-08.json");
        Require(scene.AssembledGroundPath == "res://data/scenes/c01-08-a2-ground.json" && scene.PaintedGroundPath is null,
            "C08 selects independent materialized terrain, not the generic tile renderer or a baked building painting.");
        var document = PresentationJson.Load<AssembledGroundDocument>(scene.AssembledGroundPath!);
        document.ValidateLevel(level);
        var decorations = document.Objects.Where(o => !o.OwnsMapStructureBody).ToArray();
        Require(decorations.Length == 12 && decorations.All(o => o.Pointer == "pass-through" && o.SelectionCell is null),
            "Courtyard boundaries cannot own capture, occupancy or pointer semantics.");
        CheckWallMovement(engine, level);
        var rendering = BoardSceneRendering.Load(scene, level, map);
        var board = new BoardView();
        AddChild(board);
        try
        {
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.RenderState(state);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var view = board.GetNode("AuthoredScene").GetChildren().OfType<AssembledGroundView>().Single();
            var rows = level["terrain"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
            var cells = Enumerable.Range(0, 14).SelectMany(y => Enumerable.Range(0, 22).Select(x => new Coord(x, y))).ToArray();
            var sites = cells.Where(c => "vBq".Contains(rows[c.Y][c.X])).ToHashSet();
            Require(board.SurfaceReady && board.RenderingIssueCount == 0 && view.Sites.Count == 16
                && sites.SetEquals(view.Sites.Select(s => s.Cell)) && view.Sites.All(s => s.OwnsMapStructureBody),
                "All sixteen canonical sites have exactly one independent building owner and no generic duplicate.");
            Require(board.RenderedStructureCount == 0 && board.RenderedUnitCount == 21, "C08 preserves all units and replaces only site body drawings.");
            foreach (var c in new[] { new Coord(10, 7), new Coord(11, 7) })
                Require(board.Pick(board.Projection.CellCenter(c)) == TacticalHit.Ground(new(c.X, c.Y)),
                    "Both central ledger trigger cells remain visibly unobstructed and directly selectable.");
            var walls = view.DepthEntries.Where(e => e.Node.Name.ToString().StartsWith("a2-wall-", StringComparison.Ordinal)).ToArray();
            Require(walls.Length == 12, "Twelve joined segments describe the thirty-six reviewed courtyard wall cells.");
            foreach (var cell in cells.Where(c => rows[c.Y][c.X] == '#'))
                Require(walls.Any(w => w.Visual!.Covers(board.ToGlobal(board.Projection.CellCenter(cell)))),
                    $"Canonical wall {cell} must have visible registered wall material, not an invisible blocker.");
            foreach (var site in sites)
            {
                var entry = view.DepthEntries.Single(e => e.SelectionCell == site);
                var sprite = (Sprite2D)entry.Node;
                var rect = sprite.GetRect();
                var reachable = Enumerable.Range(1, 19).Any(y => Enumerable.Range(1, 19).Any(x =>
                {
                    var point = sprite.ToGlobal(rect.Position + rect.Size * new Vector2(x / 20f, y / 20f));
                    return entry.Visual!.ContainsPointer(point) && board.Pick(board.ToLocal(point)) == TacticalHit.Object(new(site.X, site.Y));
                }));
                Require(reachable, $"Site {site} must retain visible, clickable facade pixels after all layers and units are mounted.");
            }
            var door = board.ToGlobal(board.Projection.CellCenter(new Coord(5, 9)));
            Require(walls.All(w => !w.Visual!.Covers(door)), "The southwest escape door must not look blocked by wall material.");
            await CheckVillageOccupant(board, state);
            Require(state.ToJsonString() == unchanged, "Registration, rendering and picking cannot mutate canonical C08 facts.");
        }
        finally { RemoveChild(board); board.Free(); }
    }

    private void CheckWallMovement(BattleEngine engine, JsonObject level)
    {
        var rows = level["terrain"]!.AsArray().Select(row => row!.GetValue<string>()).ToArray();
        var walls = rows.SelectMany((row, y) => row.Select((symbol, x) => (symbol, x, y)))
            .Where(cell => cell.symbol == '#').Select(cell => new BoardCell(cell.x, cell.y)).ToArray();
        Require(walls.Length == 36, "The reviewed courtyard layout has thirty-six blocked wall cells.");
        var rules = new BattleSessionRules(engine,
            BattleTextChecks.Session(engine, GodotTextCatalog.LoadSimplifiedChinese()), new JsonArray());
        var session = new GrayFlagSession(rules, level);
        var queries = session.GetTacticalQueries(0);
        var actors = queries.Actors().ToArray();
        Require(actors.Length > 0, "Movement checks require native selectable actors.");
        foreach (var actor in actors)
        {
            var stops = queries.MoveStops(actor);
            foreach (var cell in walls)
                Require(!stops.Contains(cell), $"Wall {cell} cannot become a movement stop for actor {actor}.");
        }
    }

    private async Task CheckVillageOccupant(BoardView board, JsonObject initial)
    {
        // This is a render-only occlusion fixture, not a battle continuation or a legal move claim.
        // Keep it local so this regression does not require a private campaign save.
        var fixture = initial.DeepClone().AsObject();
        var actor = fixture["units"]!.AsArray().OfType<JsonObject>().Single(unit => unit["id"]!.GetValue<int>() == 5).DeepClone().AsObject();
        actor["x"] = 15;
        actor["y"] = 7;
        actor["facing"] = "east";
        fixture["units"] = new JsonArray(actor);
        var before = fixture.ToJsonString();
        board.RenderState(fixture);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var sprite = board.GetNode<Sprite2D>("CanonicalProjection/Unit_5/AuthoredSprite");
        var rect = sprite.GetRect();
        var opaque = 0;
        var exposed = 0;
        for (var y = rect.Position.Y + .5f; y < rect.End.Y; y += 8)
            for (var x = rect.Position.X + .5f; x < rect.End.X; x += 8)
            {
                var local = new Vector2(x, y);
                if (!sprite.IsPixelOpaque(local))
                    continue;
                opaque++;
                if (board.Pick(board.ToLocal(sprite.ToGlobal(local))) == TacticalHit.Unit(new(15, 7), 5))
                    exposed++;
            }
        Require(opaque > 0 && exposed * 2 > opaque,
            $"Occupied village must expose selectable actor body rather than only a displaced flag: {exposed}/{opaque}.");
        Require(fixture.ToJsonString() == before, "Rendering and picking cannot advance the fixture state.");
    }
}
