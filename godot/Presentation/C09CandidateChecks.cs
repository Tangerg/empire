#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Presentation.Tactical;
using Godot;
namespace Empire.Presentation;
public partial class C09CandidateChecks : Node
{
    public override async void _Ready()
    {
        BoardView? board = null;
        try
        {
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-09");
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var state = engine.CreateState(level);
            var before = EcmaJson.Stringify(state);
            var scene = AuthoredScene.Load("res://data/scenes/c01-09.json");
            if (!scene.RuntimeReady)
                throw new InvalidOperationException("C09 registered scene must be runtime ready.");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            board = new BoardView();
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.ConfigureDynamicState();
            board.RenderState(state);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var view = board.GetNode("AuthoredScene").GetChildren().OfType<AssembledGroundView>().Single();
            if (view.Sites.Count != 7 || view.Sites.Select(x => x.Cell).Distinct().Count() != 7 || view.Sites.Any(x => !x.OwnsMapStructureBody))
                throw new InvalidOperationException("All seven sites need one body owner.");
            if (board.RenderedStructureCount != 4 || board.RenderedUnitCount != 23)
                throw new InvalidOperationException($"Canonical actors/nodes missing: {board.RenderedUnitCount}/{board.RenderedStructureCount}");
            foreach (var site in view.Sites)
            {
                var entry = view.DepthEntries.Single(e => e.SelectionCell == site.Cell);
                var sprite = (Sprite2D)entry.Node;
                var rect = sprite.GetRect();
                var reachable = Enumerable.Range(1, 19).Any(y => Enumerable.Range(1, 19).Any(x =>
                {
                    var point = sprite.ToGlobal(rect.Position + rect.Size * new Vector2(x / 20f, y / 20f));
                    return entry.Visual!.ContainsPointer(point) && board.Pick(board.ToLocal(point)) == TacticalHit.Object(new(site.Cell.X, site.Cell.Y));
                }));
                if (!reachable)
                    throw new InvalidOperationException($"No visible clickable site body: {site.Cell}");
            }
            foreach (var node in level["structures"]!.AsArray().OfType<JsonObject>())
            {
                var cell = new Coord(node["x"]!.GetValue<int>(), node["y"]!.GetValue<int>());
                var hit = board.Pick(board.Projection.CellContact(cell));
                if (hit != TacticalHit.Object(new(cell.X, cell.Y)))
                    throw new InvalidOperationException($"Node contact cannot be selected: {cell}, got {hit}");
            }
            foreach (var unit in state["units"]!.AsArray().OfType<JsonObject>())
            {
                var id = unit["id"]!.GetValue<int>();
                var cell = new BoardCell(unit["x"]!.GetValue<int>(), unit["y"]!.GetValue<int>());
                var sprite = board.GetNode<Sprite2D>($"CanonicalProjection/Unit_{id}/AuthoredSprite");
                var rect = sprite.GetRect();
                var opaque = 0;
                var selectable = 0;
                for (var sy = 1; sy < 20; sy++)
                    for (var sx = 1; sx < 20; sx++)
                    {
                        var local = rect.Position + rect.Size * new Vector2(sx / 20f, sy / 20f);
                        if (!sprite.IsPixelOpaque(local))
                            continue;
                        opaque++;
                        if (board.Pick(board.ToLocal(sprite.ToGlobal(local))) == TacticalHit.Unit(cell, id))
                            selectable++;
                    }
                GD.Print($"C09_ACTOR_BODY {id}: {selectable}/{opaque} opaque samples selectable");
                if (opaque == 0 || selectable < opaque * .45f)
                    throw new InvalidOperationException($"Actor body too occluded: {id}, {selectable}/{opaque}");
            }
            var walls = view.DepthEntries.Where(e => e.Node.Name.ToString().StartsWith("Prison", StringComparison.Ordinal)).ToArray();
            var rows = level["terrain"]!.AsArray().Select(row => row!.GetValue<string>()).ToArray();
            // Hills and mountain are painted into continuous ground. Visual registration is manually reviewed;
            // do not allow the discarded standalone terrain bodies to reintroduce competing silhouettes.
            var ground = PresentationJson.Load<JsonObject>("res://data/scenes/c01-09-candidate-ground.json");
            if (ground["objects"]!.AsArray().OfType<JsonObject>().Any(item =>
                item["id"]!.GetValue<string>().StartsWith("Hill-", StringComparison.Ordinal)
                || item["id"]!.GetValue<string>().StartsWith("Mountain-", StringComparison.Ordinal)))
                throw new InvalidOperationException("Continuous painted hills must not have duplicate sprite bodies.");
            for (var y = 0; y < rows.Length; y++)
                for (var x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == '#' && !walls.Any(w => w.Visual!.Covers(board.ToGlobal(board.Projection.CellCenter(new Coord(x, y))))))
                        throw new InvalidOperationException($"Invisible wall footprint: {x},{y}");
            if (EcmaJson.Stringify(state) != before)
                throw new InvalidOperationException("Rendering mutated canonical state.");
            GD.Print("C09_CANDIDATE_SOURCE_RENDER_OK: seven unique site owners, four dynamic nodes, 23 actors, source-bound geometry, seven selectable site bodies, four selectable node contacts, all eight wall footprints visible, unchanged state. NOT visual or interaction acceptance.");
            board.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); board?.QueueFree(); GetTree().Quit(1); }
    }
}
