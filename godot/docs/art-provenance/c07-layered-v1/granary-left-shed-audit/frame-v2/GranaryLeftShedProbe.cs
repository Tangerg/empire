using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;

public partial class GranaryLeftShedProbe : Node
{
    public override void _Ready()
    {
        try
        {
            bool wrongRoofSupport=OS.GetEnvironment("C07_SHED_POSTS_USE_ROOF") == "1";
            var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");
            var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);
            var scene=AuthoredScene.Load("res://data/scenes/c01-07-granary-preview.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
            var board=new BoardView{AnimationsEnabled=false};AddChild(board);
            board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
            var geometry=PresentationJson.Load<JsonObject>("res://granary-left-shed-geometries.json");
            if(wrongRoofSupport)foreach(var component in geometry["components"]!.AsArray().OfType<JsonObject>())
                if(component["namePrefix"]!.GetValue<string>().Contains("PostColumn"))component["supportLine"]=geometry["components"]![0]!["supportLine"]!.DeepClone();
            BoardGroundView? ground=null;
            board.ConfigureGround((rules,authored)=>{
                BoardGroundView current=rendering.Ground(rules,authored);
                foreach(var name in new[]{"granary-canopy-geometry.json","granary-bin-geometry.json"})current=new TexturedPropGround(current,PresentationJson.Load<JsonObject>("res://"+name));
                foreach(var box in PresentationJson.Load<JsonObject>("res://granary-door-stock-geometries.json")["containers"]!.AsArray().OfType<JsonObject>())current=new TexturedPropGround(current,box);
                foreach(var component in geometry["components"]!.AsArray().OfType<JsonObject>())current=new TexturedPropGround(current,component);
                return ground=current;
            });
            board.ConfigureDynamicState();var empty=state.DeepClone().AsObject();empty["units"]=new JsonArray();board.RenderState(empty);
            var entries=ground!.DepthEntries.Where(d=>d.Node.Name.ToString().StartsWith("GranaryShed")).ToArray();
            bool Covers(Vector2 p)=>entries.Any(d=>d.Visual!.Covers(board.ToGlobal(p)));
            var materials=new JsonArray();foreach(var key in new[]{"materialPositives","materialNegatives"})foreach(var point in geometry[key]!.AsArray())
            {var p=new Vector2(point![0]!.GetValue<float>(),point[1]!.GetValue<float>());materials.Add(new JsonObject{["source"]=point.DeepClone(),["expected"]=key=="materialPositives",["actual"]=Covers(p)});}
            var contacts=new JsonArray();foreach(var(name,y)in new[]{("Left",170f),("Near",213f),("Right",191f),("Rear",148f)})
            {var values=entries.Where(d=>d.Node.Name.ToString().StartsWith("GranaryShed"+name+"PostColumn")).Select(d=>d.LogicalContact.Y*16/BoardProjection.TileSize).ToArray();contacts.Add(new JsonObject{["post"]=name,["sourceFootY"]=y,["observedOriginal"]=name is "Left" or "Near",["entryCount"]=values.Length,["actualDepthMinimum"]=values.Min(),["actualDepthMaximum"]=values.Max(),["ownsIndependentFoot"]=values.All(v=>Math.Abs(v-y)<.0001f)});}
            var door=board.Pick(new(568,162));var approach=board.Pick(new(584,232));
            var result=new JsonObject{["scope"]="STATIC source/structural contract; original legal-role roof intersection NOT established, no old roof bug or gameplay claim",["wrongRoofSupportNegative"]=wrongRoofSupport,["stateUnchanged"]=digest==EcmaJson.Stringify(state),["issues"]=board.RenderingIssueCount,["materials"]=materials,["postContacts"]=contacts,["standingEntries"]=entries.Length,["doorPick"]=door?.ToString(),["approachPick"]=approach?.ToString()};
            string tag=wrongRoofSupport?"roof-support-negative":"candidate";System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://granary-left-shed-{tag}-result.json"),result.ToJsonString());
            if(materials.Any(v=>v!["expected"]!.GetValue<bool>()!=v["actual"]!.GetValue<bool>()))throw new Exception("Granary left shed source material/foot exclusion failed");
            if(contacts.Any(v=>!v!["ownsIndependentFoot"]!.GetValue<bool>()))throw new Exception("Granary shed posts must own actual or declared individual feet, not roof envelope");
            if(door!=TacticalHit.Object(new(35,14))||approach!=TacticalHit.Ground(new(36,14)))throw new Exception("Shed must preserve existing site/approach");
            if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static state/rendering contract failed");
            GD.Print("GRANARY_LEFT_SHED_STRUCTURAL_STATIC_CONTRACT_COMPLETE");GetTree().Quit();
        }
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}
    }
}
