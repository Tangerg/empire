using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;

public partial class FormalGranaryBinProbe : Node
{
    public override void _Ready()
    {
        try
        {
            bool original = false;
            bool west = OS.GetEnvironment("C07_BIN_WEST") == "1";
            bool flat = OS.GetEnvironment("C07_BIN_FLAT_LEFT") == "1";
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json")
                .OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-07");
            var state = engine.CreateState(level);
            string digest = EcmaJson.Stringify(state);
            var scene = AuthoredScene.Load(flat ? "res://formal-granary-bin-flat-left-scene.json" : (OS.GetEnvironment("C07_STATIC_SCENE") is var sceneOverride&&sceneOverride.Length>0?sceneOverride:"res://data/scenes/c01-07-granary-bin-surface-preview.json"));
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false };
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            var geometry = PresentationJson.Load<JsonObject>("res://granary-bin-geometry.json");
            BoardGroundView? ground = null;
            board.ConfigureGround((rules, authored) => ground = rendering.Ground(rules, authored));
            board.ConfigureDynamicState();
            var empty = state.DeepClone().AsObject(); empty["units"] = new JsonArray(); board.RenderState(empty);
            bool BinCovers(Vector2 p) => ground!.DepthEntries.Any(d => d.Node.Name.ToString().StartsWith("granary-low-bin__surface_") && d.Visual!.Covers(board.ToGlobal(p)));
            var material = new JsonArray();
            if (!original) foreach (var key in new[] { "materialPositives", "materialNegatives" }) foreach (var point in geometry[key]!.AsArray())
            {
                var p = new Vector2(point![0]!.GetValue<float>(), point[1]!.GetValue<float>());
                material.Add(new JsonObject { ["source"] = point.DeepClone(), ["expected"] = key == "materialPositives", ["actual"] = BinCovers(p) });
            }
            var door = board.Pick(new(568, 162)); var approach = board.Pick(new(584, 232));
            var torren = state["units"]!.AsArray().OfType<JsonObject>().Single(u => u["key"]!.GetValue<string>() == "campaign-torren");
            var poses = new JsonArray();
            foreach (int y in new[] { 14, 15 })
            {
                if (level["terrain"]![y]!.GetValue<string>()[27] != 'p') throw new Exception("Bin actual-role pose must remain canonical p");
                var unit = torren.DeepClone().AsObject(); unit["x"] = 27; unit["y"] = y; unit["facing"] = west ? "west" : "east";
                int id = unit["id"]!.GetValue<int>(); var shown = state.DeepClone().AsObject(); shown["units"] = new JsonArray(unit); board.RenderState(shown);
                var sprite = board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}").GetNode<Sprite2D>("AuthoredSprite");
                var points = new JsonArray();
                foreach (var p in (west ? new[] { new Vector2(432, 228), new Vector2(432, 229) } : new[] { new Vector2(432, 224), new Vector2(432, 225) }))
                    points.Add(new JsonObject { ["source"] = new JsonArray(p.X, p.Y), ["actorOpaque"] = sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(p))), ["actorVisible"] = board.Pick(p) == TacticalHit.Unit(new(27, y), id), ["binMaterial"] = BinCovers(p) });
                poses.Add(new JsonObject { ["cell"] = new JsonArray(27, y), ["expectedActorVisible"] = original || y == 15, ["points"] = points });
            }
            var result = new JsonObject { ["scope"] = "STATIC actual C07 Torren back/front, no commands/saves/gameplay", ["original"] = original, ["flatLeftNegative"] = flat, ["actualFacing"] = west ? "west" : "east", ["stateUnchanged"] = digest == EcmaJson.Stringify(state), ["issues"] = board.RenderingIssueCount, ["materials"] = material, ["poses"] = poses, ["doorPick"] = door?.ToString(), ["approachPick"] = approach?.ToString() };
            string tag = (original ? "original" : flat ? "flat-left-negative" : "candidate") + (west ? "-west" : "");
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath((OS.GetEnvironment("C07_STATIC_RESULT_TAG") is var outputTag&&outputTag.Length>0?"res://"+outputTag+"-":"res://")+$"formal-granary-bin-{tag}-result.json"), result.ToJsonString());
            if (material.Any(v => v!["expected"]!.GetValue<bool>() != v["actual"]!.GetValue<bool>())) throw new Exception("Granary bin fixed material contract failed");
            foreach (var pose in poses) if (pose!["points"]!.AsArray().Any(p => !p!["actorOpaque"]!.GetValue<bool>() || p["actorVisible"]!.GetValue<bool>() != pose["expectedActorVisible"]!.GetValue<bool>())) throw new Exception("Granary bin actual-role sloping-depth contract failed");
            if (door != TacticalHit.Object(new(35,14)) || approach != TacticalHit.Ground(new(36,14))) throw new Exception("Granary bin must not capture existing site/approach");
            if (digest != EcmaJson.Stringify(state) || board.RenderingIssueCount != 0) throw new Exception("Static state/rendering contract failed");
            GD.Print("FORMAL_GRANARY_BIN_STATIC_COMPLETE"); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
}
