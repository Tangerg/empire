using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;

public partial class SoutheastSurfaceEquivalenceProbe : Node
{
 public override void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
 var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var state=engine.CreateState(level);
 (BoardView Board,BoardGroundView Ground) Make(bool previous){
  var scene=AuthoredScene.Load(previous?"res://selected-southeast-before-scene.json":"res://data/scenes/c01-07-selected-assembly.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
  var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
  BoardGroundView? ground=null;board.ConfigureGround((rules,s)=>{ground=rendering.Ground(rules,s);if(previous)ground=new SectionedSpriteGround(ground,PresentationJson.Load<JsonObject>("res://southeast-gate-v2-sections.json"));return ground;});board.ConfigureDynamicState();board.RenderState(state);return(board,ground!);
 }
 var old=Make(true);var current=Make(false);
 var a=old.Ground.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("SEGate_")).ToArray();
 var b=current.Ground.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("southeast-gate-stone__surface_")).ToArray();
 if(a.Length!=b.Length)throw new Exception("Support slice count changed");
 List<(Vector2 Point,Vector2 UV,Color Tint)> Vertices(TerrainDepthEntry entry,bool source=false){var result=new List<(Vector2,Vector2,Color)>();foreach(var poly in entry.Node.GetChildren().OfType<Polygon2D>()){var v=poly.Polygon;var u=poly.UV;var ids=poly.Polygons.Count>0?poly.Polygons.SelectMany(p=>p.AsInt32Array()).ToArray():Geometry2D.TriangulatePolygon(v);for(int offset=0;offset<ids.Length;offset+=3){var triangle=ids.Skip(offset).Take(3).Select(i=>(Point:source?poly.GetMeta("originalSourcePolygon").AsVector2Array()[i]:poly.ToGlobal(v[i]),UV:u[i],Tint:poly.VertexColors.Length==v.Length?poly.VertexColors[i]:poly.Color)).OrderBy(p=>p.Point.X).ThenBy(p=>p.Point.Y).ToArray();result.AddRange(triangle);}}return result;}
 float maxPoint=0,maxSourcePoint=0,maxUv=0,maxDepth=0,maxTint=0;int vertices=0;
 for(int i=0;i<a.Length;i++){
  var av=Vertices(a[i]);var source=Vertices(a[i],true);var bv=Vertices(b[i]);if(av.Count!=bv.Count)throw new Exception("Triangle topology changed");vertices+=av.Count;
  for(int j=0;j<av.Count;j++){maxPoint=Math.Max(maxPoint,av[j].Point.DistanceTo(bv[j].Point));maxSourcePoint=Math.Max(maxSourcePoint,source[j].Point.DistanceTo(bv[j].Point));maxUv=Math.Max(maxUv,av[j].UV.DistanceTo(bv[j].UV));for(int channel=0;channel<4;channel++)maxTint=Math.Max(maxTint,Math.Abs(av[j].Tint[channel]-bv[j].Tint[channel]));}
  var da=old.Board.Projection.Depth(a[i].LogicalContact)+a[i].Visual!.FrontOffset(a[i].Node).Y;
  var db=current.Board.Projection.Depth(b[i].LogicalContact)+b[i].Visual!.FrontOffset(b[i].Node).Y;maxDepth=Math.Max(maxDepth,Math.Abs(da-db));
 }
 int sampled=0,coverageMismatch=0;for(int y=723;y<=954;y+=3)for(int x=1194;x<=1451;x+=3){
  var p=new Vector2(x,y);bool ac=a.Any(e=>e.Visual!.Covers(old.Board.ToGlobal(p))),bc=b.Any(e=>e.Visual!.Covers(current.Board.ToGlobal(p)));sampled++;if(ac!=bc)coverageMismatch++;
 }
 var report=new JsonObject{{"supports",a.Length},{"triangleVertices",vertices},{"maximumPointError",maxPoint},{"maximumSourcePointError",maxSourcePoint},{"maximumUvError",maxUv},{"maximumDepthError",maxDepth},{"maximumTintError",maxTint},{"sampledMaterialPixels",sampled},{"coverageMismatch",coverageMismatch},{"productionPolygonNodes",b.Sum(e=>e.Node.GetChildren().OfType<Polygon2D>().Count())},{"previousPolygonNodes",a.Sum(e=>e.Node.GetChildren().OfType<Polygon2D>().Count())}};
 System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://formal-southeast-equivalence-result.json"),report.ToJsonString());
 if(maxSourcePoint!=0||maxPoint>MathF.BitIncrement(1451f)-1451f||maxUv!=0||maxDepth!=0||maxTint!=0||coverageMismatch!=0)throw new Exception("Exact renderer migration equivalence failed");
 GD.Print("FORMAL_SOUTHEAST_EQUIVALENCE_COMPLETE "+report.ToJsonString());GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
