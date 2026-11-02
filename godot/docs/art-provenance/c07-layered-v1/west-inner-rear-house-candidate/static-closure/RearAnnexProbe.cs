using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;
public partial class RearAnnexProbe : Node
{
 public override async void _Ready(){try{
  var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");
  var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);var scenePath=OS.GetEnvironment("REAR_ANNEX_SCENE");if(scenePath.Length==0)scenePath="res://west-inner-rear-house-study.json";
  var scene=AuthoredScene.Load(scenePath);var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);BoardGroundView ground=null!;board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();
  var rows=new JsonArray();
  foreach(var (x,y,facing) in new[]{(24,22,"east"),(24,22,"west"),(21,26,"east"),(21,26,"west"),(26,24,"west")}){
   if(level["terrain"]![y]!.GetValue<string>()[x]!='p')throw new Exception("Annex display pose must be original legal paving");
   var unit=state["units"]!.AsArray().OfType<JsonObject>().Single(u=>u["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();unit["x"]=x;unit["y"]=y;unit["facing"]=facing;int id=unit["id"]!.GetValue<int>();var shown=state.DeepClone().AsObject();shown["units"]=new JsonArray(unit);board.RenderState(shown);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   var actor=board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");var sprite=actor.GetNode<Sprite2D>("AuthoredSprite");var overlap=new JsonArray();var owns=ground.DepthEntries.Where(v=>v.Visual is not null&&v.Node.Name.ToString().StartsWith("west-inner-rear-house-main__")).ToArray();
   for(int py=y*16+8-40;py<=y*16+8;py++)for(int px=x*16+8-24;px<=x*16+8+24;px++){
    var point=new Vector2(px,py);if(!sprite.IsPixelOpaque(sprite.ToLocal(point)))continue;var covered=owns.Where(v=>v.Visual!.GlobalBounds.HasPoint(point)&&v.Visual.Covers(point)).ToArray();if(covered.Length==0)continue;
    overlap.Add(new JsonObject{{"point",new JsonArray(px,py)},{"materialZ",covered.Max(v=>v.Node.ZIndex)},{"actorZ",actor.ZIndex},{"foreground",covered.Any(v=>v.Node.ZIndex>actor.ZIndex)},{"unitPicked",board.Pick(point)==TacticalHit.Unit(new(x,y),id)},{"hit",board.Pick(point)?.ToString()}});
   }
   // Frozen actual-alpha witnesses, chosen from the retained discovery output.
   (int X,int Y,bool Front)? witness=(x,y,facing) switch {
    (24,22,"east") => (399,356,false),
    (21,26,"east") => (357,400,true),
    (26,24,"west") => (418,378,false),
    _ => null
   };
   if(witness is {} w){
    var sample=overlap.OfType<JsonObject>().SingleOrDefault(v=>v["point"]![0]!.GetValue<int>()==w.X&&v["point"]![1]!.GetValue<int>()==w.Y);
    if(sample is null)throw new Exception($"Annex actual alpha witness absent at {w.X},{w.Y}");
    if(sample["foreground"]!.GetValue<bool>()==w.Front||sample["unitPicked"]!.GetValue<bool>()!=w.Front)
     throw new Exception($"Annex frozen depth/pick contract failed at {w.X},{w.Y}; front={w.Front}");
   }
   rows.Add(new JsonObject{{"pose",new JsonArray(x,y)},{"facing",facing},{"overlaps",overlap}});
  }
  var output=OS.GetEnvironment("REAR_ANNEX_OUTPUT");if(output.Length==0)output="res://rear-annex-current-overlap-scan.json";File.WriteAllText(ProjectSettings.GlobalizePath(output),new JsonObject{{"scope","Real initial Torren appearances at fixed legal display poses only; no commands/AI/save. Three frozen actual-alpha front/back/side witnesses; retained empty west-facing front is not a positive control."},{"scene",scenePath},{"stateUnchanged",digest==EcmaJson.Stringify(state)},{"issues",board.RenderingIssueCount},{"rows",rows}}.ToJsonString());
  if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static annex invariant changed");GD.Print("REAR_ANNEX_FROZEN_CONTRACTS_OK");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
