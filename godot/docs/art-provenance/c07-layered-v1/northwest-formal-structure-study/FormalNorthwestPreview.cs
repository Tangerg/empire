using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Presentation;
using Godot;

public partial class FormalNorthwestPreview : Node2D
{
    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v => v["id"]!.GetValue<string>() == "c01-07");
            var state = engine.CreateState(level);
            var before = EcmaJson.Stringify(state);
            var variant = OS.GetEnvironment("NW_VARIANT");
            if (variant.Length == 0) variant = "stone";
            var scene = AuthoredScene.Load((variant == "baseline" || variant == "selected-northwest") ? "res://data/scenes/c01-07-selected-assembly.json" : $"res://northwest-{variant}-scene.json");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false };
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            board.ConfigureGround(rendering.Ground);
            board.ConfigureDynamicState();
            var shown = state.DeepClone().AsObject();
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(v => v["key"]!.GetValue<string>() == "campaign-torren").DeepClone().AsObject();
            unit["x"] = 8; unit["y"] = 8; unit["facing"] = "east";
            shown["units"] = new JsonArray(unit);
            board.RenderState(shown);
            var fullScene = OS.GetEnvironment("NW_FULL_SCENE") == "1";
            var zoom = fullScene ? .65f : OS.GetEnvironment("NW_DEFAULT_SCALE") == "1" ? 1.25f : 2.5f;
            board.Scale = Vector2.One * zoom;
            var overlay = new CanvasLayer(); AddChild(overlay);
            overlay.AddChild(new ColorRect { Color = new Color("#181818"), Size = new Vector2(1180, 72) });
            overlay.AddChild(new Label { Text = $"C07 NORTHWEST STATIC MATERIAL STUDY — {variant}\nFormal loader; fixed Torren back pose; leaf={((variant == "selected-northwest" || variant.EndsWith("leaf")) ? "new narrow design" : "absent")}; {(variant == "selected-northwest" ? "selected candidate, official unchanged" : "unselected candidate")}; no gameplay", Position = new Vector2(12, 12) });
            void Layout() => board.Position = GetViewportRect().Size / 2 + (fullScene ? new Vector2(0, 36) : Vector2.Zero)
                - board.Projection.CellContact(fullScene ? new(48, 32) : new(10, 7)) * zoom;
            GetViewport().SizeChanged += Layout;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetWindow().Size = new Vector2I(1180, 760);
            Layout();
            if (EcmaJson.Stringify(state) != before) throw new Exception("Static rendering changed state.");
            GD.Print("FORMAL_NORTHWEST_STATIC_READY issues=" + board.RenderingIssueCount);
            if (OS.GetEnvironment("NW_MEASURE") == "1")
            {
                for (var i = 0; i < 60; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var samples = new JsonArray();
                for (var i = 0; i < 120; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    samples.Add(new JsonObject {
                        ["drawCalls"] = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                        ["objects"] = Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame),
                        ["primitives"] = Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                        ["processSeconds"] = Performance.GetMonitor(Performance.Monitor.TimeProcess)
                    });
                }
                File.WriteAllText(ProjectSettings.GlobalizePath($"res://cost-{(fullScene ? "full-" : "")}{variant}.json"), new JsonObject {
                    ["scope"] = "Same viewport/camera/real detached actor; native Compatibility llvmpipe;60 warmup then120 frames; no gameplay or screenshot disk writes during sampling.",
                    ["variant"] = variant, ["zoom"] = zoom, ["fullScene"] = fullScene,
                    ["sceneNodeCount"] = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount), ["viewport"] = new JsonArray(GetWindow().Size.X, GetWindow().Size.Y),
                    ["stateUnchanged"] = EcmaJson.Stringify(state) == before, ["samples"] = samples
                }.ToJsonString(new() { WriteIndented = true }));
                GD.Print("NORTHWEST_STATIC_COST_COMPLETE " + variant);
                GetTree().Quit();
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
