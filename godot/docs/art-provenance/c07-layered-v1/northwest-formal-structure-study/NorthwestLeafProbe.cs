using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;

public partial class NorthwestLeafProbe : Node
{
    public override void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v => v["id"]!.GetValue<string>() == "c01-07");
            var state = engine.CreateState(level);
            var digest = EcmaJson.Stringify(state);
            var negative = OS.GetEnvironment("NW_LEAF_NEGATIVE");
            if (negative is not ("" or "background" or "foreground")) throw new InvalidOperationException("Unknown leaf negative variant.");
            var scene = AuthoredScene.Load(negative.Length == 0 ? "res://northwest-platform-leaf-scene.json" : $"res://leaf-{negative}-scene.json");
            var loadClock = System.Diagnostics.Stopwatch.StartNew();
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false };
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            BoardGroundView? ground = null;
            board.ConfigureGround((rules, authored) => ground = rendering.Ground(rules, authored));
            loadClock.Stop();
            var groundLoadMilliseconds = loadClock.Elapsed.TotalMilliseconds;
            board.ConfigureDynamicState();
            var results = new JsonArray();
            foreach (var front in new[] { false, true })
            {
                int x = 9, y = front ? 9 : 7;
                if (level["terrain"]![y]!.GetValue<string>()[x] != 'p') throw new InvalidOperationException("Leaf display witness must remain on canonical paving.");
                var shown = state.DeepClone().AsObject();
                var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(v => v["key"]!.GetValue<string>() == "campaign-torren").DeepClone().AsObject();
                unit["x"] = x; unit["y"] = y; unit["facing"] = "east";
                shown["units"] = new JsonArray(unit);
                board.RenderState(shown);
                int id = unit["id"]!.GetValue<int>();
                var actor = board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");
                var sprite = actor.GetNode<Sprite2D>("AuthoredSprite");
                var leaves = ground!.DepthEntries.Where(d => d.Node.Name.ToString().StartsWith("northwest-leaf-study__", StringComparison.Ordinal)).ToArray();
                var rows = new JsonArray();
                for (float py = 94.25f; py < 137; py++)
                    for (float px = 153.25f; px < 165; px++)
                    {
                        var point = new Vector2(px, py);
                        var global = board.ToGlobal(point);
                        var covering = leaves.Where(d => d.Visual!.Covers(global)).ToArray();
                        if (covering.Length == 0 || !sprite.IsPixelOpaque(sprite.ToLocal(global))) continue;
                        var normal = board.Pick(point);
                        foreach (var leaf in leaves) leaf.Node.Visible = false;
                        var withoutLeaf = board.Pick(point);
                        foreach (var leaf in leaves) leaf.Node.Visible = true;
                        // Exclude ground/stone-hidden pixels: only a genuine leaf-exclusive witness qualifies.
                        if (withoutLeaf != TacticalHit.Unit(new(x, y), id)) continue;
                        rows.Add(new JsonObject {
                            ["point"] = new JsonArray(px, py), ["actorZ"] = actor.ZIndex,
                            ["leafZ"] = new JsonArray(covering.Select(v => (JsonNode)JsonValue.Create(v.Node.ZIndex)!).ToArray()),
                            ["actorPickable"] = normal == TacticalHit.Unit(new(x, y), id),
                            ["withoutLeafActorPickable"] = true,
                            ["pick"] = normal?.ToString()
                        });
                    }
                results.Add(new JsonObject { ["front"] = front, ["cell"] = new JsonArray(x, y), ["witnesses"] = rows });
            }
            var output = new JsonObject { ["scope"] = "Detached real Torren display poses only; hiding leaf is a render-only ownership control, never a game command.", ["poses"] = results, ["stateUnchanged"] = digest == EcmaJson.Stringify(state), ["issues"] = board.RenderingIssueCount, ["negative"] = negative,
                ["coldConfigureIncludingSceneRenderingMilliseconds"] = groundLoadMilliseconds,
                ["totalDepthEntries"] = ground!.DepthEntries.Count,
                ["gateDepthEntries"] = ground.DepthEntries.Count(d => d.Node.Name.ToString().StartsWith("northwest-gate-study__", StringComparison.Ordinal)),
                ["leafDepthEntries"] = ground.DepthEntries.Count(d => d.Node.Name.ToString().StartsWith("northwest-leaf-study__", StringComparison.Ordinal)) };
            File.WriteAllText(ProjectSettings.GlobalizePath(negative.Length == 0 ? "res://northwest-leaf-actual-poses.json" : $"res://northwest-leaf-{negative}-negative-result.json"), output.ToJsonString(new() { WriteIndented = true }));
            foreach (var pose in results)
            {
                var rows = pose!["witnesses"]!.AsArray();
                if (rows.Count == 0 || rows.Any(v => v!["actorPickable"]!.GetValue<bool>() != pose["front"]!.GetValue<bool>()))
                    throw new InvalidOperationException("Independent northwest leaf front/back render and Pick contract failed.");
            }
            if (digest != EcmaJson.Stringify(state) || board.RenderingIssueCount != 0) throw new InvalidOperationException("Static leaf inspection changed state or reported issues.");
            GD.Print("NORTHWEST_LEAF_ACTUAL_POSES_COMPLETE");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
