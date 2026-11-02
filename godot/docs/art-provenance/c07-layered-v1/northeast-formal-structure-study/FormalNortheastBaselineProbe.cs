using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;

public partial class FormalNortheastBaselineProbe : Node
{
    public override void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>()
                .Single(v => v["id"]!.GetValue<string>() == "c01-07");
            var state = engine.CreateState(level);
            var digest = EcmaJson.Stringify(state);
            var scene = AuthoredScene.Load("res://northeast-baseline-scene.json");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false };
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            BoardGroundView? ground = null;
            board.ConfigureGround((rules, authored) => ground = rendering.Ground(rules, authored));
            board.ConfigureDynamicState();
            var empty = state.DeepClone().AsObject();
            empty["units"] = new JsonArray();
            board.RenderState(empty);
            var visuals = ground!.DepthEntries.Where(v => v.Node.Name.ToString().Contains("northeast-static-study"))
                .Select(v => v.Visual!).ToArray();
            if (visuals.Length != 168) throw new Exception("Frozen baseline support count changed.");
            bool Covers(Vector2 point) => visuals.Any(v => v.Covers(board.ToGlobal(point)));
            var regions = new JsonArray();
            var union = new HashSet<Vector2>();
            foreach (var region in PresentationJson.Load<JsonObject>("res://northeast-gate-original-road-regions.json")["regions"]!.AsArray())
            {
                var polygon = region!["polygon"]!.AsArray().Select(v => Point(v!)).ToArray();
                int count = 0, covered = 0;
                for (int y = (int)polygon.Min(v => v.Y); y <= polygon.Max(v => v.Y); y++)
                for (int x = (int)polygon.Min(v => v.X); x <= polygon.Max(v => v.X); x++)
                {
                    var point = new Vector2(x, y);
                    if (!InsideInclusive(point, polygon)) continue;
                    count++; union.Add(point);
                    if (Covers(point)) covered++;
                }
                if (count != region["integerLatticeSampleCount"]!.GetValue<int>() || covered != 0)
                    throw new Exception("Frozen continuous opening contract changed.");
                regions.Add(new JsonObject { ["id"] = region["id"]!.DeepClone(), ["count"] = count, ["covered"] = covered });
            }
            if (union.Count != 1103) throw new Exception("Frozen road union count changed.");
            var source = PresentationJson.Load<JsonObject>("res://northeast-frozen-v2-sections.json");
            var texture = GD.Load<Texture2D>(source["textureResourcePath"]!.GetValue<string>());
            var sampler = new Sprite2D { Texture = texture, Centered = false, Visible = false };
            AddChild(sampler);
            var scale = source["scale"]!.GetValue<float>();
            var translation = Point(source["translation"]!);
            int samples = 0, mismatch = 0;
            for (float y = .41f; y < texture.GetHeight(); y += 17)
            for (float x = .37f; x < texture.GetWidth(); x += 17)
            {
                var pixel = new Vector2(x, y); samples++;
                if (sampler.IsPixelOpaque(pixel) != Covers(pixel * scale + translation)) mismatch++;
            }
            if (mismatch != 0) throw new Exception("Frozen formal alpha ownership changed.");
            var target = board.Pick(new(1417, 130));
            var approach = board.Pick(new(1320, 184));
            if (target != TacticalHit.Object(new(83, 11)) || approach != TacticalHit.Ground(new(82, 11)))
                throw new Exception("Frozen station or approach contract changed.");
            var lookout = new JsonArray();
            foreach (var point in new[] { new Vector2(1416, 59), new Vector2(1431, 66), new Vector2(1427, 67), new Vector2(1435, 78) })
                lookout.Add(new JsonObject { ["source"] = Pair(point), ["formalCovered"] = Covers(point) });
            if (lookout.Any(v => !v!["formalCovered"]!.GetValue<bool>()))
                throw new Exception("Frozen failed lookout baseline changed: two blocked holes and two wood positives must remain covered.");
            bool front = OS.GetEnvironment("C07_GATE_FRONT") == "1";
            int px = front ? 84 : 85, py = front ? 9 : 8;
            if (level["terrain"]![py]!.GetValue<string>()[px] != 'p') throw new Exception("Fixed diagnostic pose changed.");
            var shown = state.DeepClone().AsObject();
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(v => v["key"]!.GetValue<string>() == "raider-1").DeepClone().AsObject();
            unit["x"] = px; unit["y"] = py; unit["facing"] = "east";
            shown["units"] = new JsonArray(unit);
            board.RenderState(shown);
            int id = unit["id"]!.GetValue<int>();
            var sprite = board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}").GetNode<Sprite2D>("AuthoredSprite");
            var witnesses = new JsonArray();
            foreach (var point in new[] { new Vector2(1348, 121), new Vector2(1349, 122) })
            {
                bool opaque = sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(point)));
                bool pickable = board.Pick(point) == TacticalHit.Unit(new(px, py), id);
                witnesses.Add(new JsonObject { ["source"] = Pair(point), ["actorOpaque"] = opaque, ["actorPickable"] = pickable });
                if (!opaque || pickable != front) throw new Exception("Actual wolf-rider front/back contract changed.");
            }
            if (digest != EcmaJson.Stringify(state) || board.RenderingIssueCount != 0)
                throw new Exception("Static state or rendering issue invariant failed.");
            var result = new JsonObject {
                ["scope"] = "Unselected frozen failed NE v2 through formal loader; detached real wolf-rider poses. No gameplay commands or save writers. Two lookout holes deliberately remain failed.",
                ["stateUnchanged"] = true, ["issues"] = board.RenderingIssueCount, ["front"] = front,
                ["roadUnionCount"] = union.Count, ["roadRegions"] = regions, ["ownershipSamples"] = samples,
                ["ownershipMismatch"] = mismatch, ["gateTarget"] = target?.ToString(), ["approach"] = approach?.ToString(),
                ["lookoutFixedSamples"] = lookout, ["witnesses"] = witnesses
            };
            File.WriteAllText(ProjectSettings.GlobalizePath($"res://formal-baseline-{(front ? "front" : "back")}-result.json"), result.ToJsonString(new() { WriteIndented = true }));
            GD.Print("FORMAL_NORTHEAST_FAILED_BASELINE_VERIFIED");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static Vector2 Point(JsonNode value) => new(value[0]!.GetValue<float>(), value[1]!.GetValue<float>());
    private static JsonArray Pair(Vector2 point) => new(point.X, point.Y);
    private static bool InsideInclusive(Vector2 point, Vector2[] polygon)
    {
        for (int i = 0; i < polygon.Length; i++)
        {
            var start = polygon[i]; var edge = polygon[(i + 1) % polygon.Length] - start; var offset = point - start;
            if (MathF.Abs(edge.Cross(offset)) < .001f && offset.Dot(edge) >= 0 && offset.Dot(edge) <= edge.LengthSquared()) return true;
        }
        return Geometry2D.IsPointInPolygon(point, polygon);
    }
}
