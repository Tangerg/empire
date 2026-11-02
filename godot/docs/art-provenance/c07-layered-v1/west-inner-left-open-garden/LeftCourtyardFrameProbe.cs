using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;
public partial class LeftCourtyardFrameProbe : Node
{
 public override async void _Ready(){try{
  var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");
  var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);var scenePath=OS.GetEnvironment("LEFT_FRAME_SCENE");if(scenePath.Length==0)scenePath="res://left-courtyard-frame-study.json";
  var scene=AuthoredScene.Load(scenePath);var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);BoardGroundView ground=null!;board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();
  var rows=new JsonArray();
  foreach(var (x,y,facing) in new[]{(23,31,"east"),(22,31,"east"),(21,27,"east"),(28,29,"west")}){
   if(level["terrain"]![y]!.GetValue<string>()[x]!='p')throw new Exception("Annex display pose must be original legal paving");
   var unit=state["units"]!.AsArray().OfType<JsonObject>().Single(u=>u["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();unit["x"]=x;unit["y"]=y;unit["facing"]=facing;int id=unit["id"]!.GetValue<int>();var shown=state.DeepClone().AsObject();shown["units"]=new JsonArray(unit);board.RenderState(shown);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   var actor=board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");var sprite=actor.GetNode<Sprite2D>("AuthoredSprite");var overlap=new JsonArray();var owns=ground.DepthEntries.Where(v=>v.Visual is not null&&v.Node.Name.ToString().StartsWith("left-courtyard-open-frame__")).ToArray();
   for(int py=y*16+8-40;py<=y*16+8;py++)for(int px=x*16+8-24;px<=x*16+8+24;px++){
    var point=new Vector2(px,py);if(!sprite.IsPixelOpaque(sprite.ToLocal(point)))continue;var covered=owns.Where(v=>v.Visual!.GlobalBounds.HasPoint(point)&&v.Visual.Covers(point)).ToArray();if(covered.Length==0)continue;
    overlap.Add(new JsonObject{{"point",new JsonArray(px,py)},{"materialZ",covered.Max(v=>v.Node.ZIndex)},{"actorZ",actor.ZIndex},{"foreground",covered.Any(v=>v.Node.ZIndex>actor.ZIndex)},{"unitPicked",board.Pick(point)==TacticalHit.Unit(new(x,y),id)},{"hit",board.Pick(point)?.ToString()}});
   }
   (int X,int Y)? witness=(x,y) switch {(23,31)=>(392,485),(22,31)=>(361,471),_=>null};
   if(witness is {} w){var sample=overlap.OfType<JsonObject>().SingleOrDefault(v=>v["point"]![0]!.GetValue<int>()==w.X&&v["point"]![1]!.GetValue<int>()==w.Y);
    if(sample is null||sample["foreground"]!.GetValue<bool>()||!sample["unitPicked"]!.GetValue<bool>())throw new Exception($"Left frame fixed actual front alpha/Pick failed at {w.X},{w.Y}");}
   rows.Add(new JsonObject{{"pose",new JsonArray(x,y)},{"facing",facing},{"overlaps",overlap}});
  }
  var material=new JsonArray();
  foreach(var (px,py,expected) in new[]{(393,465,true),(393,472,true),(377,460,false),(381,466,false),(385,447,false),(408,447,false)}){
   var q=new Vector2(px,py);var owners=ground.DepthEntries.Where(v=>v.Visual is not null&&v.Node.Name.ToString().StartsWith("left-courtyard-open-frame__")&&v.Visual.GlobalBounds.HasPoint(q)&&v.Visual.Covers(q)).ToArray();
   material.Add(new JsonObject{{"point",new JsonArray(px,py)},{"expectedWood",expected},{"woodOwners",owners.Length}});
   if((owners.Length>0)!=expected)throw new Exception($"Left open frame wood/empty contract failed at {px},{py}; expectedWood={expected}");
  }
  var roots=new JsonArray();
  if(OS.GetEnvironment("LEFT_FRAME_ROOTS")=="1")foreach(var pair in new[]{("left-vine",new Vector2(368.7f,472.28f)),("near-vine",new Vector2(389.7f,483.28f))}){
   var vine=ground.DepthEntries.Single(v=>v.Node.Name.ToString()=="left-courtyard-"+pair.Item1);
   var soil=ground.DepthEntries.Where(v=>v.Visual is not null&&v.Node.Name.ToString().StartsWith("left-courtyard-soil__")&&v.Visual.GlobalBounds.HasPoint(pair.Item2)&&v.Visual.Covers(pair.Item2)).ToArray();
   bool root=vine.Visual!.Covers(pair.Item2),before=soil.Length>0&&soil.All(v=>v.Node.ZIndex<vine.Node.ZIndex),pass=!vine.Visual.ContainsPointer(pair.Item2);
   roots.Add(new JsonObject{{"instance",pair.Item1},{"point",new JsonArray(pair.Item2.X,pair.Item2.Y)},{"rootMaterial",root},{"soilOwners",soil.Length},{"rootBeforeSoil",before},{"passThrough",pass}});
   if(!root||!before||!pass)throw new Exception("Left actual rooted stem must remain before zero-height soil and pass through pointer");
  }
  var output=OS.GetEnvironment("LEFT_FRAME_OUTPUT");if(output.Length==0)output="res://left-frame-actual-overlap-discovery.json";File.WriteAllText(ProjectSettings.GlobalizePath(output),new JsonObject{{"scope","Real initial Torren appearances at fixed legal display poses only; no commands/AI/save. Two frozen real front controls; fixed old wood witnesses and owner-specific real holes. Two tested back/side poses have no overlap and are not rear controls."},{"scene",scenePath},{"stateUnchanged",digest==EcmaJson.Stringify(state)},{"issues",board.RenderingIssueCount},{"rows",rows},{"woodAndOwnerSpecificHoles",material},{"rootLayering",roots}}.ToJsonString());
  if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static annex invariant changed");GD.Print("LEFT_FRAME_FROZEN_CONTRACTS_OK");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
