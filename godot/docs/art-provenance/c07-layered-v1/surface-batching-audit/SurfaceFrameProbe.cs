using Godot;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;

// Initial-state render only. No commands, AI, campaign saves or UI actions.
public partial class SurfaceFrameProbe : Node2D
{
 readonly List<object> samples=new();double elapsed;bool ready;string variant="";BoardView board=null!;BoardGroundView ground=null!;double buildMs;string initial="";JsonObject state=null!;
 static IEnumerable<Node> Desc(Node n){foreach(var c in n.GetChildren()){yield return c;foreach(var d in Desc(c))yield return d;}}
 public override void _Ready(){try{
  variant=OS.GetEnvironment("C07_SURFACE_PERF_VARIANT");if(variant is not ("previous" or "formal"))throw new Exception("Explicit performance variant required");
  var sw=System.Diagnostics.Stopwatch.StartNew();var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");state=engine.CreateState(level);initial=EcmaJson.Stringify(state);
  var shown=state.DeepClone().AsObject();var actor=state["units"]!.AsArray().OfType<JsonObject>().Single(u=>u["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();actor["x"]=60;actor["y"]=12;shown["units"]=new JsonArray(actor);
  var scene=AuthoredScene.Load(variant=="previous"?"res://formal-fence-before-scene.json":"res://data/scenes/c01-07-fence-surface-preview.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
  board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
  board.ConfigureGround((rules,s)=>{ground=rendering.Ground(rules,s);if(variant=="previous")ground=new AtomicFenceGround(ground,PresentationJson.Load<JsonObject>("res://atomic-fence-geometry.json"));return ground;});board.ConfigureDynamicState();board.RenderState(shown);
  board.Scale=Vector2.One*4;board.Position=GetViewportRect().Size/2-board.Projection.CellContact(new(60,13))*4;
  var layer=new CanvasLayer();AddChild(layer);layer.AddChild(new Label{Text="C07 STATIC RENDER PERFORMANCE | "+variant+" | no actions, saves or screenshots during sample",Position=new(12,12)});
  buildMs=sw.Elapsed.TotalMilliseconds;ready=true;
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
 public override void _Process(double delta)
 {
  if(!ready)return;elapsed+=delta;if(elapsed<10)return;
  if(elapsed<30){samples.Add(new{elapsed,delta,processMs=Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000,drawCalls=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),nodes=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)});return;}
  ready=false;bool same=initial==EcmaJson.Stringify(state);if(!same){GD.PushError("Static performance changed canonical state");GetTree().Quit(1);return;}
  File.WriteAllText(OS.GetEnvironment("C07_SURFACE_PERF_OUTPUT"),JsonSerializer.Serialize(new{variant,scope="One-object initial-clone static rendering; 10sec warmup+20sec sampling; llvmpipe not user GPU, not full C07 acceptance",stateUnchanged=same,buildMs,viewport=new[]{GetViewportRect().Size.X,GetViewportRect().Size.Y},entries=ground.DepthEntries.Count,polygonNodes=Desc(ground).OfType<Polygon2D>().Count(),triangles=board.Projection.Triangles.Count,patches=board.Projection.DepthPatches.Count,partitions=board.Projection.DepthPartitions.Count,samples},new JsonSerializerOptions{WriteIndented=true}));GD.Print("SURFACE_FRAME_SAMPLE_COMPLETE "+variant);GetTree().Quit();
 }
}
