using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;

public partial class GranaryLargeGrainProbe : Node
{
    public override void _Ready()
    {
        try
        {
            bool background=OS.GetEnvironment("C07_LARGE_GRAIN_BACKGROUND")=="1";
            var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");
            var state=engine.CreateState(level);string digest=EcmaJson.Stringify(state);
            var scene=AuthoredScene.Load(background?"res://granary-bale-background-negative-scene.json":"res://data/scenes/c01-07-granary-stock-preview.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
            var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
            BoardGroundView? ground=null;
            board.ConfigureGround((rules,authored)=>{BoardGroundView current=rendering.Ground(rules,authored);
                foreach(var name in new[]{"granary-canopy-geometry.json","granary-bin-geometry.json"})current=new TexturedPropGround(current,PresentationJson.Load<JsonObject>("res://"+name));
                foreach(var box in PresentationJson.Load<JsonObject>("res://granary-door-stock-geometries.json")["containers"]!.AsArray().OfType<JsonObject>())current=new TexturedPropGround(current,box);
                foreach(var component in PresentationJson.Load<JsonObject>("res://granary-left-shed-geometries.json")["components"]!.AsArray().OfType<JsonObject>())current=new TexturedPropGround(current,component);
                return ground=current;});
            board.ConfigureDynamicState();var empty=state.DeepClone().AsObject();empty["units"]=new JsonArray();board.RenderState(empty);
            IEnumerable<TerrainDepthEntry> Bales()=>ground!.DepthEntries.Where(d=>d.Node.Name.ToString().StartsWith("granary-large-grain"));
            bool Covers(Vector2 p)=>Bales().Any(d=>d.Visual!.Covers(board.ToGlobal(p)));
            var materials=new JsonArray();foreach(var(expected,points)in new[]{(true,new[]{new Vector2(388,215),new Vector2(394,213),new Vector2(396,212),new Vector2(388,210),new Vector2(390,211),new Vector2(402,185),new Vector2(403,185)}),(false,new[]{new Vector2(388,217),new Vector2(394,215),new Vector2(396,214),new Vector2(402,178),new Vector2(414,208),new Vector2(396,216),new Vector2(406,221),new Vector2(424,216)})})foreach(var p in points)materials.Add(new JsonObject{["source"]=new JsonArray(p.X,p.Y),["expected"]=expected,["actual"]=Covers(p)});
            // A pole is a separate foreground owner, not a request to carve a fictitious hole through reconstructed bales.
            var polePoint=new Vector2(408,190);var globalPole=board.ToGlobal(polePoint);
            var pole=ground!.DepthEntries.Where(d=>d.Node.Name.ToString().StartsWith("GranaryCanopyColumn")&&d.Visual!.Covers(globalPole)).ToArray();
            var baleAtPole=Bales().Where(d=>d.Visual!.Covers(globalPole)).ToArray();
            bool poleInFront=pole.Length>0&&baleAtPole.Length>0&&pole.Max(d=>d.Node.ZIndex)>baleAtPole.Max(d=>d.Node.ZIndex);
            var wolf=state["units"]!.AsArray().OfType<JsonObject>().Single(u=>u["key"]!.GetValue<string>()=="raider-1");var poses=new JsonArray();
            foreach(var(x,y,visible,points)in new[]{(24,15,true,new[]{new Vector2(388,210),new Vector2(390,211)}),(26,12,false,new[]{new Vector2(402,185),new Vector2(403,185)})})
            {
                if(level["terrain"]![y]!.GetValue<string>()[x]!='p')throw new Exception("Bale static pose must remain canonical p");
                var unit=wolf.DeepClone().AsObject();unit["x"]=x;unit["y"]=y;unit["facing"]="east";int id=unit["id"]!.GetValue<int>();var shown=state.DeepClone().AsObject();shown["units"]=new JsonArray(unit);board.RenderState(shown);
                var sprite=board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}").GetNode<Sprite2D>("AuthoredSprite");var samples=new JsonArray();
                foreach(var p in points)samples.Add(new JsonObject{["source"]=new JsonArray(p.X,p.Y),["actorOpaque"]=sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(p))),["baleMaterial"]=Covers(p),["actorVisible"]=board.Pick(p)==TacticalHit.Unit(new(x,y),id)});
                poses.Add(new JsonObject{["cell"]=new JsonArray(x,y),["expectedVisible"]=visible,["basis"]=visible?"Original visible lower-front feet protect this real-role foreground":"NEW reconstructed hidden east support contract only; original east foot obscured, NOT old wrong-order proof",["samples"]=samples});
            }
            var result=new JsonObject{["scope"]="STATIC new bale reconstruction; original back ordering unresolved, no commands/save/gameplay",["backgroundNegative"]=background,["stateUnchanged"]=digest==EcmaJson.Stringify(state),["issues"]=board.RenderingIssueCount,["materials"]=materials,["separatePole408190InFront"]=poleInFront,["poleZ"]=new JsonArray(pole.Select(d=>(JsonNode)JsonValue.Create(d.Node.ZIndex)!).ToArray()),["baleZAtPole"]=new JsonArray(baleAtPole.Select(d=>(JsonNode)JsonValue.Create(d.Node.ZIndex)!).ToArray()),["poses"]=poses};
            System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://granary-large-grain-{(background?"background-negative":"candidate")}-result.json"),result.ToJsonString());
            if(materials.Any(v=>v!["expected"]!.GetValue<bool>()!=v["actual"]!.GetValue<bool>()))throw new Exception("Granary large bale fixed visible material/ground contract failed");
            if(!poleInFront)throw new Exception("Separate canopy post must remain in front of new bale volume");
            foreach(var p in poses)if(p!["samples"]!.AsArray().Any(v=>!v!["actorOpaque"]!.GetValue<bool>()||!v["baleMaterial"]!.GetValue<bool>()||v["actorVisible"]!.GetValue<bool>()!=p["expectedVisible"]!.GetValue<bool>()))throw new Exception("Granary new bale real-role front or authored-east depth contract failed");
            if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static state/rendering contract failed");
            GD.Print("GRANARY_LARGE_BALE_STATIC_NEW_CONTRACT_COMPLETE");GetTree().Quit();
        }
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}
    }
}
