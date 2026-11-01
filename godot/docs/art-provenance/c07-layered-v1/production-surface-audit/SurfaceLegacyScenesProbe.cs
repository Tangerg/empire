using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;

// Rendering only: never Execute, dispatch, AI, turn, move, capture, save or load campaign progress.
public partial class SurfaceLegacyScenesProbe : Node
{
 public override void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
 var levels=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json");var results=new JsonArray();
 for(int n=1;n<=6;n++){
  var id=$"c01-{n:00}";var level=levels.OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()==id);var state=engine.CreateState(level);var before=EcmaJson.Stringify(state);var scene=AuthoredScene.Load($"res://data/scenes/{id}.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
  var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);BoardGroundView? ground=null;
  board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();board.RenderState(state);
  int entries=ground!.DepthEntries.Count,sites=ground.Sites.Count;board.RenderState(state);
  bool unchanged=before==EcmaJson.Stringify(state);if(!unchanged||board.RenderingIssueCount!=0||entries!=ground.DepthEntries.Count||sites!=ground.Sites.Count)throw new Exception("Legacy initial render contract failed: "+id);
  results.Add(new JsonObject{{"level",id},{"stateUnchanged",unchanged},{"renderIssues",board.RenderingIssueCount},{"entries",entries},{"sites",sites},{"scope","initial static repeated render only; no actions or campaign progress"}});board.Free();
 }
 System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://formal-surface-legacy-static-result.json"),results.ToJsonString());GD.Print("SURFACE_LEGACY_STATIC_COMPLETE "+results.ToJsonString());GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
