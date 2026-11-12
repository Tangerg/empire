#nullable enable
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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
            GD.Print($"GRAIN_ROAD_CHECKS_OK {assertions} assertions; canonical C08 unchanged, not visual acceptance.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private async Task Check()
    {
        var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-08");
        Require(EcmaJson.Sha256(level) == "c3895c5463000c022ceae230e700a5370e803e69c95812e2a468c0252e9604fd", "C08 art cannot change its canonical terrain, roster, objectives or ledger trigger.");
        var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
        var state = engine.CreateState(level);
        var unchanged = state.ToJsonString();
        var map = state["map"]!.AsObject();
        var scene = AuthoredScene.Load("res://data/scenes/c01-08.json");
        Require(scene.AssembledGroundPath == "res://data/scenes/c01-08-layered-ground.json" && scene.PaintedGroundPath is null,
            "C08 selects independent materialized terrain, not the generic tile renderer or a baked building painting.");
        var document = PresentationJson.Load<AssembledGroundDocument>(scene.AssembledGroundPath!);
        document.ValidateLevel(level);
        var details = document.Objects.Where(o => o.AssetId is "grain-stock" or "ledger-desk").ToArray();
        Require(details.Length == 5 && details.All(o => o.Pointer == "pass-through" && o.SelectionCell is null && !o.OwnsMapStructureBody),
            "Loading details and ledger landmark cannot own capture, occupancy or pointer semantics.");
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
            var walls = view.DepthEntries.Where(e => e.Node.Name.ToString().StartsWith("wall-", StringComparison.Ordinal)).ToArray();
            Require(walls.Length == 12, "Twelve independent segments describe the thirty canonical wall cells.");
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
            Require(state.ToJsonString() == unchanged, "Registration, rendering and picking cannot mutate canonical C08 facts.");
        }
        finally { RemoveChild(board); board.Free(); }
    }
}
