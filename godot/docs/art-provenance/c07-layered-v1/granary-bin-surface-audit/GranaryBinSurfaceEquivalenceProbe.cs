using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;

public partial class GranaryBinSurfaceEquivalenceProbe : Node
{
 public override void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
 var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var state=engine.CreateState(level);
 (BoardView Board,BoardGroundView Ground) Make(bool previous){
  var scene=AuthoredScene.Load(previous?"res://data/scenes/c01-07-granary-canopy-surface-preview.json":"res://data/scenes/c01-07-granary-bin-surface-preview.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
  var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
  BoardGroundView? ground=null;board.ConfigureGround((rules,s)=>{ground=rendering.Ground(rules,s);if(previous)ground=new TexturedPropGround(ground,PresentationJson.Load<JsonObject>("res://granary-bin-geometry.json"));return ground;});board.ConfigureDynamicState();board.RenderState(state);return(board,ground!);
 }
 var old=Make(true);var current=Make(false);if(!old.Ground.Sites.SequenceEqual(current.Ground.Sites))throw new Exception("Site ownership changed");
 var a=old.Ground.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("GranaryBinColumn")).ToArray();
 var b=current.Ground.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("granary-low-bin__surface_")).ToArray();
 if(a.Length!=b.Length)throw new Exception("Support slice count changed");
 List<(Vector2 Point,Vector2 UV,Color Tint,Vector2 Source)> Vertices(TerrainDepthEntry entry){var result=new List<(Vector2,Vector2,Color,Vector2)>();foreach(var poly in entry.Node.GetChildren().OfType<Polygon2D>()){var v=poly.Polygon;var u=poly.UV;var ids=poly.Polygons.Count>0?poly.Polygons.SelectMany(p=>p.AsInt32Array()).ToArray():Geometry2D.TriangulatePolygon(v);for(int offset=0;offset<ids.Length;offset+=3){var triangle=ids.Skip(offset).Take(3).Select(i=>(Point:poly.ToGlobal(v[i]),UV:u[i],Tint:poly.VertexColors.Length==v.Length?poly.VertexColors[i]:poly.Color,Source:poly.HasMeta("originalSourcePolygon")?poly.GetMeta("originalSourcePolygon").AsVector2Array()[i]:poly.ToGlobal(v[i]))).OrderBy(p=>p.Point.X).ThenBy(p=>p.Point.Y).ToArray();result.AddRange(triangle);}}return result;}
 float maxFormalSourcePoint=0,maxPreviousSourcePoint=0;float maxPoint=0,maxUv=0,maxDepth=0,maxTint=0;int vertices=0;int maxPointUlps=0;var changedVertices=new JsonArray();
 for(int i=0;i<a.Length;i++){
  var av=Vertices(a[i]);var bv=Vertices(b[i]);if(av.Count!=bv.Count)throw new Exception("Triangle topology changed");vertices+=av.Count;
  for(int j=0;j<av.Count;j++){maxFormalSourcePoint=Math.Max(maxFormalSourcePoint,av[j].Source.DistanceTo(bv[j].Point));maxPreviousSourcePoint=Math.Max(maxPreviousSourcePoint,av[j].Source.DistanceTo(av[j].Point));maxPoint=Math.Max(maxPoint,av[j].Point.DistanceTo(bv[j].Point));for(int axis=0;axis<2;axis++){int ulps=Math.Abs(BitConverter.SingleToInt32Bits(av[j].Point[axis])-BitConverter.SingleToInt32Bits(bv[j].Point[axis]));maxPointUlps=Math.Max(maxPointUlps,ulps);if(ulps>0)changedVertices.Add(new JsonObject{{"supportIndex",i},{"vertexIndex",j},{"axis",axis},{"previous",av[j].Point[axis]},{"formal",bv[j].Point[axis]},{"ulps",ulps}});}maxUv=Math.Max(maxUv,av[j].UV.DistanceTo(bv[j].UV));for(int channel=0;channel<4;channel++)maxTint=Math.Max(maxTint,Math.Abs(av[j].Tint[channel]-bv[j].Tint[channel]));}
  var da=old.Board.Projection.Depth(a[i].LogicalContact)+a[i].Visual!.FrontOffset(a[i].Node).Y;
  var db=current.Board.Projection.Depth(b[i].LogicalContact)+b[i].Visual!.FrontOffset(b[i].Node).Y;maxDepth=Math.Max(maxDepth,Math.Abs(da-db));
 }
 int sampled=0,coverageMismatch=0;for(int y=218;y<=252;y++)for(int x=410;x<=443;x++){
  var p=new Vector2(x,y);bool ac=a.Any(e=>e.Visual!.Covers(old.Board.ToGlobal(p))),bc=b.Any(e=>e.Visual!.Covers(current.Board.ToGlobal(p)));sampled++;if(ac!=bc)coverageMismatch++;
 }
 var report=new JsonObject{{"maximumFormalSourcePointError",maxFormalSourcePoint},{"maximumPreviousSourcePointError",maxPreviousSourcePoint},{"maximumPointUlps",maxPointUlps},{"changedCoordinates",changedVertices},{"supports",a.Length},{"triangleVertices",vertices},{"maximumPointError",maxPoint},{"maximumUvError",maxUv},{"maximumDepthError",maxDepth},{"maximumTintError",maxTint},{"sampledMaterialPixels",sampled},{"coverageMismatch",coverageMismatch},{"productionPolygonNodes",b.Sum(e=>e.Node.GetChildren().OfType<Polygon2D>().Count())},{"previousPolygonNodes",a.Sum(e=>e.Node.GetChildren().OfType<Polygon2D>().Count())}};
 System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://formal-granary-bin-equivalence-result.json"),report.ToJsonString());
 if(maxFormalSourcePoint!=0||maxPointUlps>1||maxUv!=0||maxDepth!=0||maxTint!=0||coverageMismatch!=0)throw new Exception("Renderer migration exceeds one coordinate ULP or changes exact UV/depth/tint/coverage");
 GD.Print("FORMAL_GRANARY_BIN_EQUIVALENCE_COMPLETE "+report.ToJsonString());GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
