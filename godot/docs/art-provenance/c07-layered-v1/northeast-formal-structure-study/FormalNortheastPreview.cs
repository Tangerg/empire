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
            var scene = AuthoredScene.Load(candidate ? "res://northeast-interface-scene.json" : "res://northeast-baseline-scene.json");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false };
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            BoardGroundView? ground = null;
            board.ConfigureGround((rules, authored) => ground = rendering.Ground(rules, authored));
            board.ConfigureDynamicState();
            var shown = state.DeepClone().AsObject();
            var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(v => v["key"]!.GetValue<string>() == "raider-1").DeepClone().AsObject();
            unit["x"] = 85; unit["y"] = 8; unit["facing"] = "east";
            shown["units"] = new JsonArray(unit);
            board.RenderState(shown);
            var zoom = 2.5f;
            board.Scale = Vector2.One * zoom;
            var overlay = new CanvasLayer(); AddChild(overlay);
            overlay.AddChild(new ColorRect { Color = new Color("#181818"), Size = new Vector2(1180, 72) });
            overlay.AddChild(new Label { Text = candidate ? "C07 NORTHEAST NEW FRAME/FLOOR INTERFACE — UNSELECTED\nBounded new design, not pixel restoration; leaf absent; detached wolf rider; no gameplay" : "C07 NORTHEAST FROZEN FAILED BASELINE\nFormal loader; two lookout holes unresolved; detached wolf rider; unselected / no gameplay", Position = new Vector2(12, 12) });
            void Layout() => board.Position = GetViewportRect().Size / 2 - board.Projection.CellContact(new(85, 7)) * zoom;
            GetViewport().SizeChanged += Layout;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetWindow().Size = new Vector2I(1180, 760);
            Layout();
            if (EcmaJson.Stringify(state) != before) throw new Exception("Static rendering changed state.");
            File.WriteAllText(ProjectSettings.GlobalizePath("res://preview-projection.json"), new JsonObject { ["position"] = new JsonArray(board.Position.X,board.Position.Y), ["scale"] = zoom }.ToJsonString());
            GD.Print("FORMAL_NORTHEAST_STATIC_READY issues=" + board.RenderingIssueCount);
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
                using var pixels = isolated.GetTexture().GetImage();
                pixels.SavePng(ProjectSettings.GlobalizePath($"res://gpu-alpha-{(candidate ? "interface" : "baseline")}.png"));
                GD.Print($"NE_ISOLATED_GPU_ALPHA_CAPTURE_COMPLETE pieces={pieces.Length}");
                GetTree().Quit();
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
