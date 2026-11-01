using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Presentation.Tactical;

public partial class FenceSurfaceProbe:Node
{
 public override void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
 var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");
 var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);
 var kind=OS.GetEnvironment("C07_FORMAL_FENCE_CASE"); var scene=AuthoredScene.Load(kind.Length==0?"res://data/scenes/c01-07-fence-surface-preview.json":$"res://formal-fence-{kind}-scene.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
 var board=new BoardView{AnimationsEnabled=false};AddChild(board);
 board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
 BoardGroundView? ground=null;board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();board.RenderState(state);
 bool Covers(int x,int y)=>ground!.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("training-fence__surface_")).Any(e=>e.Visual!.Covers(board.ToGlobal(new(x,y))));
 var witnesses=new JsonArray();foreach(var (x,y,expected) in new[]{(972,203,false),(977,207,false),(954,194,false),(976,194,true),(950,208,true),(963,215,true),(987,228,true),(949,177,true),(965,185,true),(987,198,true),(1000,194,false)}){
 var actual=Covers(x,y);witnesses.Add(new JsonObject{{"source",new JsonArray(x,y)},{"expectedWood",expected},{"actualWood",actual}});}
 var resultTag=OS.GetEnvironment("C07_STATIC_RESULT_TAG");var actorKey=OS.GetEnvironment("C07_STATIC_ACTOR_KEY");if(actorKey.Length==0)actorKey="campaign-torren";var original=state["units"]!.AsArray().OfType<JsonObject>().Single(u=>u["key"]!.GetValue<string>()==actorKey);var poses=new JsonArray();
 foreach(var (x,y) in new[]{(60,11),(61,11),(60,12),(61,12),(62,12),(62,13),(63,13),(60,14),(61,14),(62,14),(61,15)}){
 if(level["terrain"]![y]!.GetValue<string>()[x]=='#')continue;
 var shown=state.DeepClone().AsObject();var actorState=original.DeepClone().AsObject();actorState["x"]=x;actorState["y"]=y;shown["units"]=new JsonArray(actorState);board.RenderState(shown);
 var id=actorState["id"]!.GetValue<int>();var actor=board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");var sprite=actor.GetNode<Sprite2D>("AuthoredSprite");int woodOverlap=0,woodHidden=0,holeOverlap=0,holeVisible=0,openBayOverlap=0,openBayVisible=0;var openBayWitnesses=new JsonArray();var woodWitnesses=new List<Vector2>();var points=new JsonArray();
 for(int py=175;py<=230;py++)for(int px=946;px<=992;px++){
 var p=new Vector2(px,py);if(!sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(p))))continue;
 var hit=board.Pick(p);bool visible=hit==TacticalHit.Unit(new(x,y),id);
 if(Covers(px,py)){woodOverlap++;if(!visible)woodHidden++;if(woodWitnesses.Count<5)woodWitnesses.Add(p);}
 else {
 var leftBay=new[]{new Vector2(953,188),new Vector2(960,192),new Vector2(960,209),new Vector2(953,205)};
 var rightBay=new[]{new Vector2(967,196),new Vector2(984,204),new Vector2(984,221),new Vector2(967,213)};
 if(Geometry2D.IsPointInPolygon(p,leftBay)||Geometry2D.IsPointInPolygon(p,rightBay)){
 openBayOverlap++;if(visible)openBayVisible++;if(openBayWitnesses.Count<8)openBayWitnesses.Add(new JsonArray(px,py));
 }
 }
 }
 foreach(var (px,py) in new[]{(972,203),(977,207),(954,194),(976,194)}){
 var p=new Vector2(px,py);bool alpha=sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(p)));var hit=board.Pick(p);bool visible=hit==TacticalHit.Unit(new(x,y),id);
 if(!Covers(px,py)&&alpha){holeOverlap++;if(visible)holeVisible++;}
 points.Add(new JsonObject{{"source",new JsonArray(px,py)},{"actorOpaque",alpha},{"actorPickable",visible},{"wood",Covers(px,py)}});}
 var holePoint=new Vector2(957,191);bool designedHoleAlpha=sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(holePoint)));bool designedHolePick=board.Pick(holePoint)==TacticalHit.Unit(new(x,y),id);
 int backgroundNegative=-1;if(x==60&&y==12&&woodWitnesses.Count>0){var nodes=ground!.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("training-fence__surface_")).Select(e=>(e.Node,e.Node.ZIndex)).ToArray();foreach(var (node,z) in nodes)node.ZIndex=-4096;backgroundNegative=woodWitnesses.Count(p=>board.Pick(p)==TacticalHit.Unit(new(x,y),id));foreach(var (node,z) in nodes)node.ZIndex=z;if(backgroundNegative!=woodWitnesses.Count)throw new Exception("Forced-background negative control failed");}
 poses.Add(new JsonObject{{"cell",new JsonArray(x,y)},{"woodOverlap",woodOverlap},{"woodHidden",woodHidden},{"newDesignedHoleWitness",new JsonObject{{"source",new JsonArray(957,191)},{"actorOpaque",designedHoleAlpha},{"wood",Covers(957,191)},{"actorPickable",designedHolePick}}},{"backgroundNegativeActorHits",backgroundNegative},{"openBayActorOverlap",openBayOverlap},{"openBayActorVisible",openBayVisible},{"openBayWitnesses",openBayWitnesses},{"frozenHoleActorOverlap",holeOverlap},{"frozenHoleVisible",holeVisible},{"points",points}});
 }
 var result=new JsonObject{{"actorKey",actorKey},{"scope","STATIC canonical-initial actor clones; no commands, carryover, real save or gameplay acceptance"},{"stateUnchanged",digest==EcmaJson.Stringify(state)},{"issues",board.RenderingIssueCount},{"witnesses",witnesses},{"poses",poses}};
 System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://formal-fence-surface-"+(resultTag.Length>0?resultTag:actorKey)+"-result.json"),result.ToJsonString());
 if(witnesses.Any(w=>w!["expectedWood"]!.GetValue<bool>()!=w["actualWood"]!.GetValue<bool>()))throw new Exception("Frozen material witness failed");
 if(actorKey=="campaign-torren"){
 JsonNode Pose(int x,int y)=>poses.Single(p=>p!["cell"]![0]!.GetValue<int>()==x&&p["cell"]![1]!.GetValue<int>()==y)!;
 var rear=Pose(60,12);var front=Pose(62,14);var h=rear["newDesignedHoleWitness"]!;
 if(rear["woodOverlap"]!.GetValue<int>()<=0||rear["woodOverlap"]!.GetValue<int>()!=rear["woodHidden"]!.GetValue<int>()||front["woodOverlap"]!.GetValue<int>()<=0||front["woodHidden"]!.GetValue<int>()!=0||!h["actorOpaque"]!.GetValue<bool>()||h["wood"]!.GetValue<bool>()||!h["actorPickable"]!.GetValue<bool>())throw new Exception("Actual back/front/new designed opening alpha contract failed");
 }
 if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static renderer/state contract failed");
 GD.Print("C07_FORMAL_FENCE_SURFACE_STATIC_COMPLETE");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
