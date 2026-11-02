using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;
public partial class CentralSelectedResponsibilityProbe:Node {
 public override void _Ready(){try{
 var e=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));var l=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v=>v["id"]!.GetValue<string>()=="c01-07");var s=e.CreateState(l);var digest=EcmaJson.Stringify(s);var sc=AuthoredScene.Load("res://data/scenes/c01-07-selected-assembly.json");var r=BoardSceneRendering.Load(sc,l,s["map"]!.AsObject());var b=new BoardView{AnimationsEnabled=false};AddChild(b);b.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),e.Grids.Get("square4"),e.Catalog,r.Plane),l,sc);BoardGroundView? g=null;b.ConfigureGround((rules,scene)=>g=r.Ground(rules,scene));b.ConfigureDynamicState();var empty=s.DeepClone().AsObject();empty["units"]=new JsonArray();b.RenderState(empty);var rows=new JsonArray();
 foreach(var cell in PresentationJson.Load<JsonObject>("res://central-selected-responsibility-input.json")["blockedCells"]!.AsArray()){
 int x=cell![0]!.GetValue<int>(),y=cell[1]!.GetValue<int>();if(l["terrain"]![y]!.GetValue<string>()[x]!='#')throw new Exception("Responsibility source cell is not blocked");var p=new Vector2(x*16+8,y*16+8);var owners=new JsonArray();foreach(var v in g!.DepthEntries.Where(v=>v.Visual is not null&&v.Visual.GlobalBounds.HasPoint(p)&&v.Visual.Covers(p)))owners.Add(v.Node.Name.ToString());rows.Add(new JsonObject{{"cell",cell.DeepClone()},{"point",new JsonArray(p.X,p.Y)},{"owners",owners}});
 }
 var result=new JsonObject{{"scope","Only actual formal-renderer material at 50 blocked cell centres; NOT foot support, whole-cell coverage or gameplay. Missing centres remain responsibility questions, not automatic need for solid fill."},{"stateUnchanged",digest==EcmaJson.Stringify(s)},{"issues",b.RenderingIssueCount},{"rows",rows}};File.WriteAllText(ProjectSettings.GlobalizePath("res://central-selected-responsibility-result.json"),result.ToJsonString());if(digest!=EcmaJson.Stringify(s)||b.RenderingIssueCount!=0)throw new Exception("Static responsibility state/render issue");GD.Print("CENTRAL_SELECTED_RESPONSIBILITY_COMPLETE");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
