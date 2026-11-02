using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;

// Initial-state render only. No commands, AI, campaign saves or UI actions.
public partial class FullSceneFrameProbe : Node2D
{
 readonly List<object> samples=new();double elapsed;bool ready;string variant="";BoardView board=null!;BoardGroundView ground=null!;double buildMs;string initial="";JsonObject state=null!;
 static IEnumerable<Node> Desc(Node n){foreach(var c in n.GetChildren()){yield return c;foreach(var d in Desc(c))yield return d;}}
 public override void _Ready(){try{
  variant=OS.GetEnvironment("C07_SURFACE_PERF_VARIANT");if(variant is not ("selected" or "left-candidate"))throw new Exception("Explicit performance variant required");
  var sw=System.Diagnostics.Stopwatch.StartNew();var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");state=engine.CreateState(level);initial=EcmaJson.Stringify(state);
  var shown=state.DeepClone().AsObject();
  var scene=AuthoredScene.Load(variant=="left-candidate"?"res://left-garden-stone-study.json":"res://data/scenes/c01-07-selected-assembly.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
  board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
  board.ConfigureGround((rules,s)=>{ground=rendering.Ground(rules,s);return ground;});board.ConfigureDynamicState();board.RenderState(shown);
  int width=state["map"]!["width"]!.GetValue<int>(),height=state["map"]!["height"]!.GetValue<int>();float zoom=MathF.Min((GetViewportRect().Size.X-32)/(width*16),(GetViewportRect().Size.Y-64)/(height*16));board.Scale=Vector2.One*zoom;board.Position=GetViewportRect().Size/2-board.Projection.CellContact(new(width/2,height/2))*zoom;
  var layer=new CanvasLayer();AddChild(layer);layer.AddChild(new Label{Text="C07 FULL SELECTED ASSEMBLY STATIC COST | "+variant+" | no actions, saves or screenshots during sample",Position=new(12,12)});
  buildMs=sw.Elapsed.TotalMilliseconds;ready=true;
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
 public override void _Process(double delta)
 {
  if(!ready)return;elapsed+=delta;if(elapsed<10)return;
  if(elapsed<30){samples.Add(new{elapsed,delta,processMs=Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000,drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),nodes=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)});return;}
  ready=false;using(var image=GetViewport().GetTexture().GetImage())image.SavePng(OS.GetEnvironment("C07_SURFACE_PERF_OUTPUT")+".png");bool same=initial==EcmaJson.Stringify(state);if(!same){GD.PushError("Static performance changed canonical state");GetTree().Quit(1);return;}
  File.WriteAllText(OS.GetEnvironment("C07_SURFACE_PERF_OUTPUT"),JsonSerializer.Serialize(new{variant,scope="Entire current selected assembly / explicitly unselected left stone variant, complete initial-state roster without actions; fixed fit-map camera;10sec warmup+20sec sample;llvmpipe and shared cloud CPU, NOT user hardware FPS or whole-scene acceptance",stateUnchanged=same,buildMs,viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},entries=ground.DepthEntries.Count,sceneScale=board.Scale.X,renderIssues=board.RenderingIssueCount,initialUnits=state["units"]!.AsArray().Count,spriteNodes=Desc(board).OfType<Sprite2D>().Count(),totalBoardNodes=Desc(board).Count(),polygonNodes=Desc(ground).OfType<Polygon2D>().Count(),triangles=board.Projection.Triangles.Count,patches=board.Projection.DepthPatches.Count,partitions=board.Projection.DepthPartitions.Count,samples},new JsonSerializerOptions{WriteIndented=true}));GD.Print("FULL_SCENE_FRAME_SAMPLE_COMPLETE "+variant);GetTree().Quit();
 }
}
