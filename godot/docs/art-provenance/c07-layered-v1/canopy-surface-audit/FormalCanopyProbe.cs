using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
public partial class FormalCanopyProbe:Node{
public override void _Ready(){try{
var e=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));var l=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var s=e.CreateState(l);var digest=EcmaJson.Stringify(s);var kind=OS.GetEnvironment("C07_FORMAL_CANOPY_CASE"); var sc=AuthoredScene.Load(kind.Length==0?"res://data/scenes/c01-07-canopy-surface-preview.json":$"res://formal-canopy-{kind}-scene.json");var r=BoardSceneRendering.Load(sc,l,s["map"]!.AsObject());var b=new BoardView{AnimationsEnabled=false};AddChild(b);b.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),e.Grids.Get("square4"),e.Catalog,r.Plane),l,sc);
var geometry=PresentationJson.Load<JsonObject>("res://atomic-canopy-geometry.json");bool mutant=kind.Length>0;
BoardGroundView? whole=null;b.ConfigureGround((rules,scene)=>whole=r.Ground(rules,scene));b.ConfigureDynamicState();b.RenderState(s);
IEnumerable<TerrainDepthEntry> Canopy()=>whole!.DepthEntries.Where(x=>x.Node.Name.ToString().StartsWith("barracks-front-canopy__surface_"));
bool Covers(Vector2 p)=>Canopy().Any(x=>x.Visual!.Covers(b.ToGlobal(p)));
var material=new JsonArray();foreach(var key in new[]{"canopyMaterialNegatives","materialPositives"})foreach(var p in geometry[key]!.AsArray()){var v=new Vector2(p![0]!.GetValue<float>(),p[1]!.GetValue<float>());material.Add(new JsonObject{{"source",p.DeepClone()},{"expected",key=="materialPositives"},{"actual",Covers(v)}});}
var original=s["units"]!.AsArray().OfType<JsonObject>().Single(u=>u["key"]!.GetValue<string>()=="campaign-torren");var rows=new JsonArray();
foreach(var(x,y)in new[]{(60,12),(60,11),(59,11),(61,12),(63,6)}){
if(l["terrain"]![y]!.GetValue<string>()[x] is '#' or '~' or '_')continue;
var shown=s.DeepClone().AsObject();var u=original.DeepClone().AsObject();u["x"]=x;u["y"]=y;shown["units"]=new JsonArray(u);b.RenderState(shown);int id=u["id"]!.GetValue<int>();var actor=b.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");var body=actor.GetNode<Sprite2D>("AuthoredSprite");int overlap=0,visible=0,headAlpha=0,headVisible=0;var hidden=new List<Vector2>();
for(int py=120;py<=205;py++)for(int px=940;px<=1000;px++){var p=new Vector2(px,py);if(!body.IsPixelOpaque(body.ToLocal(b.ToGlobal(p))))continue;bool hit=b.Pick(p)==TacticalHit.Unit(new(x,y),id);if(x==60&&y==12&&px<=990&&py>=145&&py<=180){headAlpha++;if(hit)headVisible++;}if(Covers(p)){overlap++;if(hit)visible++;else if(hidden.Count<5)hidden.Add(p);}}
int negative=-1;if(x==60&&(y==11||y==12)&&hidden.Count>0){var old=Canopy().Select(d=>(d.Node,d.Node.ZIndex)).ToArray();foreach(var(n,z)in old)n.ZIndex=-4096;negative=hidden.Count(p=>b.Pick(p)==TacticalHit.Unit(new(x,y),id));foreach(var(n,z)in old)n.ZIndex=z;}
rows.Add(new JsonObject{{"cell",new JsonArray(x,y)},{"canopyOverlap",overlap},{"canopyOverlapPickingActor",visible},{"headAlpha",headAlpha},{"headPickingActor",headVisible},{"forcedBackgroundActorHits",negative},{"backgroundWitnessCount",hidden.Count},{"hiddenPoints",new JsonArray(hidden.Select(p=>(JsonNode)new JsonArray(p.X,p.Y)).ToArray())},{"higherTerrainOwners",new JsonArray(hidden.Select(p=>(JsonNode)new JsonObject{{"point",new JsonArray(p.X,p.Y)},{"owners",new JsonArray(whole!.DepthEntries.Where(d=>d.Node.ZIndex>=actor.ZIndex&&d.Visual is not null&&d.Visual.Covers(b.ToGlobal(p))).Select(d=>(JsonNode)JsonValue.Create(d.Node.Name.ToString())!).ToArray())}}).ToArray())}});
}
var result=new JsonObject{{"scope","STATIC core+canopy+cargo+fence+blue, not full C07 and no gameplay"},{"filledMutant",mutant},{"stateUnchanged",digest==EcmaJson.Stringify(s)},{"issues",b.RenderingIssueCount},{"materials",material},{"rows",rows}};System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://formal-canopy-"+(kind.Length>0?kind:"positive")+"-result.json"),result.ToJsonString());
if(material.Any(p=>p!["expected"]!.GetValue<bool>()!=p["actual"]!.GetValue<bool>()))throw new Exception("Frozen canopy material contract failed");
var front=rows.Single(p=>p!["cell"]![0]!.GetValue<int>()==60&&p["cell"]![1]!.GetValue<int>()==12)!;if(front["headAlpha"]!.GetValue<int>()!=270||front["headPickingActor"]!.GetValue<int>()!=270||front["canopyOverlap"]!.GetValue<int>()==0||front["forcedBackgroundActorHits"]!.GetValue<int>()>0)throw new Exception("Original head/front material regression");
var back=rows.Single(p=>p!["cell"]![0]!.GetValue<int>()==60&&p["cell"]![1]!.GetValue<int>()==11)!;if(back["canopyOverlap"]!.GetValue<int>()<=back["canopyOverlapPickingActor"]!.GetValue<int>()||back["forcedBackgroundActorHits"]!.GetValue<int>()<=0)throw new Exception("No proven true back occlusion/negative control");
if(digest!=EcmaJson.Stringify(s)||b.RenderingIssueCount!=0)throw new Exception("State/rendering contract failed");GD.Print("C07_FORMAL_CANOPY_STATIC_COMPLETE");GetTree().Quit();
}catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
