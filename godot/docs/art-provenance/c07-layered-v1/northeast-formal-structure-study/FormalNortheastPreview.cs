using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Engine;
using Empire.Presentation;
using Godot;

public partial class FormalNortheastPreview : Node2D
{
    public override async void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v => v["id"]!.GetValue<string>() == "c01-07");
            var state = engine.CreateState(level);
            var before = EcmaJson.Stringify(state);
            var candidate = OS.GetEnvironment("NE_INTERFACE") == "1";
            var leaf = OS.GetEnvironment("NE_LEAF") == "1";
            var fullScene = OS.GetEnvironment("NE_FULL_SCENE") == "1";
            var costBaseline = OS.GetEnvironment("NE_COST_BASELINE") == "1";
            var scene = AuthoredScene.Load(costBaseline ? "res://data/scenes/c01-07-selected-assembly.json" : leaf ? "res://northeast-leaf-scene.json" : candidate ? "res://northeast-interface-scene.json" : "res://northeast-baseline-scene.json");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false };
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            BoardGroundView? ground = null;
            board.ConfigureGround((rules, authored) => ground = rendering.Ground(rules, authored));
            board.ConfigureDynamicState();
            var shown = state.DeepClone().AsObject();
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(v => v["key"]!.GetValue<string>() == "raider-1").DeepClone().AsObject();
            unit["x"] = 85; unit["y"] = leaf ? (OS.GetEnvironment("C07_GATE_FRONT") == "1" ? 9 : 7) : 8; unit["facing"] = "east";
            shown["units"] = new JsonArray(unit);
            board.RenderState(shown);
            var zoom = fullScene ? .65f : OS.GetEnvironment("NE_DEFAULT_ZOOM") == "1" ? 1.25f : 2.5f;
            board.Scale = Vector2.One * zoom;
            var overlay = new CanvasLayer(); AddChild(overlay);
            overlay.AddChild(new ColorRect { Color = new Color("#181818"), Size = new Vector2(1180, 72) });
            overlay.AddChild(new Label { Text = fullScene ? $"C07 FULL SELECTED STATIC COST STUDY — {(costBaseline ? "CURRENT63/FALLBACK NE" : "UNSELECTED65/NEW NE+LEAF")}\nSame static camera/actor; incomplete scene; no gameplay; official unchanged" : leaf ? "C07 NORTHEAST WHOLE GATE + INDEPENDENT LEFT LEAF — UNSELECTED\nStatic detached legal pose; new hidden joinery; original road contract; no gameplay" : candidate ? "C07 NORTHEAST NEW FRAME/FLOOR INTERFACE — UNSELECTED\nBounded new design, not pixel restoration; leaf absent; detached wolf rider; no gameplay" : "C07 NORTHEAST FROZEN FAILED BASELINE\nFormal loader; two lookout holes unresolved; detached wolf rider; unselected / no gameplay", Position = new Vector2(12, 12) });
            void Layout() => board.Position = GetViewportRect().Size / 2 + (fullScene ? new Vector2(0,36) : Vector2.Zero) - board.Projection.CellContact(fullScene ? new(48,32) : new(85,7)) * zoom;
            GetViewport().SizeChanged += Layout;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetWindow().Size = new Vector2I(1180, 760);
            Layout();
            if (EcmaJson.Stringify(state) != before) throw new Exception("Static rendering changed state.");
            File.WriteAllText(ProjectSettings.GlobalizePath("res://preview-projection.json"), new JsonObject { ["position"] = new JsonArray(board.Position.X,board.Position.Y), ["scale"] = zoom }.ToJsonString());
            GD.Print("FORMAL_NORTHEAST_STATIC_READY issues=" + board.RenderingIssueCount);
            if (OS.GetEnvironment("NE_MEASURE") == "1")
            {
                for (int i=0;i<60;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                var samples=new JsonArray();
                for (int i=0;i<120;i++)
                {
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    samples.Add(new JsonObject {
                        ["drawCalls"]=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
                        ["objects"]=Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame),
                        ["primitives"]=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                        ["processSeconds"]=Performance.GetMonitor(Performance.Monitor.TimeProcess)});
                }
                var variant=costBaseline?"baseline63":"candidate65";
                File.WriteAllText(ProjectSettings.GlobalizePath($"res://whole-cost-{variant}.json"),new JsonObject {
                    ["scope"]="Full selected static scene, .65 fixed camera; baseline has NE generic fallback, NOT equal geometry/quality.60warmup/120samples; llvmpipe; no gameplay or disk capture during sampling.",
                    ["variant"]=variant,["depthEntries"]=ground!.DepthEntries.Count,
                    ["sceneNodes"]=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
                    ["stateUnchanged"]=EcmaJson.Stringify(state)==before,["samples"]=samples
                }.ToJsonString(new(){WriteIndented=true}));
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var nativeFrame=GetViewport().GetTexture().GetImage();
                if(nativeFrame.SavePng(ProjectSettings.GlobalizePath($"res://whole-cost-{variant}.png"))!=Error.Ok) throw new Exception("Native scene capture failed.");
                GD.Print("NE_WHOLE_STATIC_COST_COMPLETE "+variant);GetTree().Quit();return;
            }

            if (OS.GetEnvironment("NE_ALPHA_CAPTURE") == "1")
            {
                // Diagnostic copy of existing draw nodes, not a second geometry compiler or a performance scene.
                var isolated = new SubViewport { Size = GetWindow().Size, TransparentBg = true,
                    World2D = new World2D(), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(isolated);
                var pieces = ground!.DepthEntries.Where(v => v.Node.Name.ToString().Contains("northeast-static-study"))
                    .Select(v => v.Node).OrderBy(v => v.ZIndex).ThenBy(v => v.GetIndex()).ToArray();
                foreach (var piece in pieces)
                {
                    var copy = (Node2D)piece.Duplicate();
                    isolated.AddChild(copy);
                    copy.Transform = piece.GlobalTransform;
                }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var mainPixels = GetViewport().GetTexture().GetImage();
                mainPixels.SavePng(ProjectSettings.GlobalizePath($"res://gpu-main-{(candidate ? "interface" : "baseline")}.png"));
                using var pixels = isolated.GetTexture().GetImage();
                pixels.SavePng(ProjectSettings.GlobalizePath($"res://gpu-alpha-{(candidate ? "interface" : "baseline")}.png"));
                GD.Print($"NE_ISOLATED_GPU_ALPHA_CAPTURE_COMPLETE pieces={pieces.Length}");
                GetTree().Quit();
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
