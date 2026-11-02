using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
// Initial state only; no battle actions, replay, AI or saved-state writes.
public partial class ExistingLevelStaticProbe : Node
{
 public override void _Ready(){try{
 var results=new JsonArray();
 for(int number=1;number<=6;number++){
  string id=$"c01-{number:00}";var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()==id);
  var state=engine.CreateState(level);string before=EcmaJson.Stringify(state);
  var scene=AuthoredScene.Load("res://data/scenes/"+id+".json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
  var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
  BoardGroundView ground=null!;board.ConfigureGround((rules,s)=>{ground=rendering.Ground(rules,s);return ground;});board.ConfigureDynamicState();board.RenderState(state);
  JsonArray Picks(){var a=new JsonArray();int w=state["map"]!["width"]!.GetValue<int>(),h=state["map"]!["height"]!.GetValue<int>();for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=board.Projection.CellContact(new Coord(x,y));a.Add(board.Pick(p)?.ToString());}return a;}
  var first=Picks();board.RenderState(state);var second=Picks();if(first.ToJsonString()!=second.ToJsonString())throw new Exception(id+" repeated rendering changed picks");
  if(before!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception(id+" initial state/render invariant failed");
  results.Add(new JsonObject{{"level",id},{"stateHash",EcmaJson.Sha256(state)},{"pickHash",EcmaJson.Sha256(first)},{"samples",first.Count},{"entries",ground.DepthEntries.Count},{"issues",board.RenderingIssueCount},{"stateUnchanged",true}});
  RemoveChild(board);board.Free();GD.Print("INITIAL_STATIC_LEVEL_OK "+id);
 }
 File.WriteAllText(OS.GetEnvironment("EXISTING_STATIC_OUTPUT"),results.ToJsonString());GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
