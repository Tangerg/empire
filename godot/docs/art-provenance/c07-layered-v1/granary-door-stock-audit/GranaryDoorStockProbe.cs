using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;

public partial class GranaryDoorStockProbe : Node
{
    public override void _Ready()
    {
        try
        {
            bool late = OS.GetEnvironment("C07_STOCK_LATE_DEPTH") == "1";
            bool spill = OS.GetEnvironment("C07_STOCK_CORRIDOR_SPILL") == "1";
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x => x["id"]!.GetValue<string>() == "c01-07");
            var state = engine.CreateState(level); string digest = EcmaJson.Stringify(state);
            var scene = AuthoredScene.Load("res://data/scenes/c01-07-granary-preview.json");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false }; AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            var geometry = PresentationJson.Load<JsonObject>("res://granary-door-stock-geometries.json");
            if (late) foreach (var box in geometry["containers"]!.AsArray()) foreach (var p in box!["supportLine"]!.AsArray()) p![1] = 260;
            if (spill)
            {
                var box = geometry["containers"]![1]!;
                box["supportLine"]!.AsArray().Add(new JsonArray(532,219));
                box["members"]!.AsArray().Add(new JsonObject {
                    ["id"] = "deliberate-small-corridor-intrusion-between-cell-centres",
                    ["polygon"] = new JsonArray(new JsonArray(530,225),new JsonArray(531,225),new JsonArray(531,227),new JsonArray(530,227)),
                    ["uv"] = new JsonArray(new JsonArray(1180,500),new JsonArray(1220,500),new JsonArray(1220,760),new JsonArray(1180,760)),["tint"]="ffffff" });
            }
            geometry = JsonNode.Parse(geometry.ToJsonString())!.AsObject();
            BoardGroundView? ground = null;
            board.ConfigureGround((rules, authored) => {
                BoardGroundView current = rendering.Ground(rules, authored);
                foreach (var name in new[] { "granary-canopy-geometry.json", "granary-bin-geometry.json" }) current = new TexturedPropGround(current, PresentationJson.Load<JsonObject>("res://" + name));
                foreach (var box in geometry["containers"]!.AsArray().OfType<JsonObject>()) current = new TexturedPropGround(current, box);
                return ground = current;
            });
            board.ConfigureDynamicState();
            var empty = state.DeepClone().AsObject(); empty["units"] = new JsonArray(); board.RenderState(empty);
            bool StockCovers(Vector2 p) => ground!.DepthEntries.Any(d => d.Node.Name.ToString().StartsWith("GranaryStock") && d.Visual!.Covers(board.ToGlobal(p)));
            var materials = new JsonArray();
            foreach (string key in new[] { "materialPositives", "materialNegatives" }) foreach (var point in geometry[key]!.AsArray())
            {
                var p = new Vector2(point![0]!.GetValue<float>(),point[1]!.GetValue<float>());
                materials.Add(new JsonObject { ["source"] = point.DeepClone(), ["expected"] = key == "materialPositives", ["actual"] = StockCovers(p) });
            }
            int corridorCovered = 0; var firstCovered = new JsonArray();
            for (int y=224;y<240;y++) for (int x=528;x<592;x++) if (StockCovers(new(x+.5f,y+.5f)))
            { corridorCovered++; if(firstCovered.Count<8) firstCovered.Add(new JsonArray(x+.5f,y+.5f)); }
            var door=board.Pick(new(568,162)); var approach=board.Pick(new(584,232));
            var unit=state["units"]!.AsArray().OfType<JsonObject>().Single(u=>u["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();
            if(level["terrain"]![14]!.GetValue<string>()[33]!='p') throw new Exception("Canonical stock front pose changed");
            unit["x"]=33;unit["y"]=14;unit["facing"]="east";int id=unit["id"]!.GetValue<int>();
            var shown=state.DeepClone().AsObject();shown["units"]=new JsonArray(unit);board.RenderState(shown);
            var sprite=board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}").GetNode<Sprite2D>("AuthoredSprite");
            var front=new JsonArray();foreach(var p in new[]{new Vector2(535,215),new Vector2(539,211)}) front.Add(new JsonObject {
                ["source"]=new JsonArray(p.X,p.Y),["actorOpaque"]=sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(p))),["stockMaterial"]=StockCovers(p),["actorVisible"]=board.Pick(p)==TacticalHit.Unit(new(33,14),id)});
            var result=new JsonObject { ["scope"]="STATIC independently grounded door stock, actual C07 front-role protection, NOT a claimed old rear-role bug or gameplay",["lateDepthNegative"]=late,["corridorSpillNegative"]=spill,["stateUnchanged"]=digest==EcmaJson.Stringify(state),["issues"]=board.RenderingIssueCount,["materials"]=materials,["corridorSamples"]=1024,["corridorCovered"]=corridorCovered,["firstCovered"]=firstCovered,["frontActualTorren"]=front,["doorPick"]=door?.ToString(),["approachPick"]=approach?.ToString() };
            string tag=late?"late-depth-negative":spill?"corridor-spill-negative":"candidate";
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://granary-door-stock-{tag}-result.json"),result.ToJsonString());
            if(materials.Any(v=>v!["expected"]!.GetValue<bool>()!=v["actual"]!.GetValue<bool>())) throw new Exception("Granary door stock fixed material contract failed");
            if(corridorCovered!=0)throw new Exception("Granary door stock full entry-strip exclusion failed");
            if(front.Any(v=>!v!["actorOpaque"]!.GetValue<bool>()||!v["stockMaterial"]!.GetValue<bool>()||!v["actorVisible"]!.GetValue<bool>()))throw new Exception("Granary door stock actual-role front visibility failed");
            if(door!=TacticalHit.Object(new(35,14))||approach!=TacticalHit.Ground(new(36,14)))throw new Exception("Stock must preserve granary door and approach picking");
            if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static state/rendering contract failed");
            GD.Print("GRANARY_DOOR_STOCK_STATIC_CONTRACT_COMPLETE");GetTree().Quit();
        }
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}
    }
}
