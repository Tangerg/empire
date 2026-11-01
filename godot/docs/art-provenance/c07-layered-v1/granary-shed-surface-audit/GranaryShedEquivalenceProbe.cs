using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;

public partial class GranaryShedEquivalenceProbe : Node
{
 public override void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
 var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var state=engine.CreateState(level);
 (BoardView Board,BoardGroundView Ground) Make(bool previous){
  var scene=AuthoredScene.Load(previous?"res://selected-shed-before-scene.json":"res://data/scenes/c01-07-selected-assembly.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());
  var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
  BoardGroundView? ground=null;board.ConfigureGround((rules,s)=>{ground=rendering.Ground(rules,s);if(previous)foreach(var component in PresentationJson.Load<JsonObject>("res://granary-left-shed-geometries.json")["components"]!.AsArray().OfType<JsonObject>())ground=new TexturedPropGround(ground,component);return ground;});board.ConfigureDynamicState();board.RenderState(state);return(board,ground!);
 }
 var old=Make(true);var current=Make(false);if(!old.Ground.Sites.SequenceEqual(current.Ground.Sites))throw new Exception("Site ownership changed");
 var a=old.Ground.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("GranaryShed")).ToArray();
 var b=current.Ground.DepthEntries.Where(e=>e.Node.Name.ToString().StartsWith("granary-shed-")).ToArray();
 if(a.Length!=b.Length)throw new Exception("Support slice count changed");
 List<(Vector2 Point,Vector2 UV,Color Tint,Vector2 Source)> Vertices(TerrainDepthEntry entry){var result=new List<(Vector2,Vector2,Color,Vector2)>();foreach(var poly in entry.Node.GetChildren().OfType<Polygon2D>()){var v=poly.Polygon;var u=poly.UV;var ids=poly.Polygons.Count>0?poly.Polygons.SelectMany(p=>p.AsInt32Array()).ToArray():Geometry2D.TriangulatePolygon(v);for(int offset=0;offset<ids.Length;offset+=3){var triangle=ids.Skip(offset).Take(3).Select(i=>(Point:poly.ToGlobal(v[i]),UV:u[i],Tint:poly.VertexColors.Length==v.Length?poly.VertexColors[i]:poly.Color,Source:poly.HasMeta("originalSourcePolygon")?poly.GetMeta("originalSourcePolygon").AsVector2Array()[i]:poly.ToGlobal(v[i]))).OrderBy(p=>p.Point.X).ThenBy(p=>p.Point.Y).ToArray();result.AddRange(triangle);}}return result;}
 float maxFormalSourcePoint=0,maxPreviousSourcePoint=0;float maxPoint=0,maxUv=0,maxDepth=0,maxTint=0;int vertices=0;int maxPointUlps=0;var changedVertices=new JsonArray();
 for(int i=0;i<a.Length;i++){
  var av=Vertices(a[i]);var bv=Vertices(b[i]);if(av.Count!=bv.Count)throw new Exception("Triangle topology changed");vertices+=av.Count;
  for(int j=0;j<av.Count;j++){maxFormalSourcePoint=Math.Max(maxFormalSourcePoint,av[j].Source.DistanceTo(bv[j].Point));maxPreviousSourcePoint=Math.Max(maxPreviousSourcePoint,av[j].Source.DistanceTo(av[j].Point));maxPoint=Math.Max(maxPoint,av[j].Point.DistanceTo(bv[j].Point));for(int axis=0;axis<2;axis++){int ulps=Math.Abs(BitConverter.SingleToInt32Bits(av[j].Point[axis])-BitConverter.SingleToInt32Bits(bv[j].Point[axis]));maxPointUlps=Math.Max(maxPointUlps,ulps);if(ulps>0)changedVertices.Add(new JsonObject{{"supportIndex",i},{"vertexIndex",j},{"axis",axis},{"previous",av[j].Point[axis]},{"formal",bv[j].Point[axis]},{"ulps",ulps}});}maxUv=Math.Max(maxUv,av[j].UV.DistanceTo(bv[j].UV));for(int channel=0;channel<4;channel++)maxTint=Math.Max(maxTint,Math.Abs(av[j].Tint[channel]-bv[j].Tint[channel]));}
  var da=old.Board.Projection.Depth(a[i].LogicalContact)+a[i].Visual!.FrontOffset(a[i].Node).Y;
  var db=current.Board.Projection.Depth(b[i].LogicalContact)+b[i].Visual!.FrontOffset(b[i].Node).Y;maxDepth=Math.Max(maxDepth,Math.Abs(da-db));
 }
 int sampled=0,coverageMismatch=0;for(int y=90;y<=215;y++)for(int x=296;x<=435;x++){
  var p=new Vector2(x,y);bool ac=a.Any(e=>e.Visual!.Covers(old.Board.ToGlobal(p))),bc=b.Any(e=>e.Visual!.Covers(current.Board.ToGlobal(p)));sampled++;if(ac!=bc)coverageMismatch++;
 }
 float maximumSourceUvError=0;int sourceUvVertices=0;
 foreach(var component in PresentationJson.Load<JsonObject>("res://granary-left-shed-geometries.json")["components"]!.AsArray().OfType<JsonObject>()){
  string name=component["namePrefix"]!.GetValue<string>().Replace("GranaryShed","").Replace("Column","").ToLowerInvariant();
  Vector2 Pair(JsonNode p)=>new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());
  var originalMembers=component["members"]!.AsArray().OfType<JsonObject>().ToArray();var allPoints=originalMembers.SelectMany(m=>m["polygon"]!.AsArray().Select(p=>Pair(p!))).ToArray();
  var origin=new Vector2(MathF.Floor(allPoints.Min(p=>p.X)),MathF.Floor(allPoints.Min(p=>p.Y)));
  foreach(var member in PresentationJson.Load<JsonObject>($"res://data/scenes/c01-07-granary-shed-{name}-surface.json")["members"]!.AsArray()){
   var pieces=member!["id"]!.GetValue<string>().Split('-');var original=originalMembers.Single(m=>m["id"]!.GetValue<string>()==string.Join("-",pieces.Take(pieces.Length-3)));int ti=int.Parse(pieces[^2]);
   var polygon=original["polygon"]!.AsArray().Select(p=>Pair(p!)).ToArray();var uv=original["uv"]!.AsArray().Select(p=>Pair(p!)).ToArray();var tri=Geometry2D.TriangulatePolygon(polygon);int ia=tri[ti],ib=tri[ti+1],ic=tri[ti+2];
   for(int j=0;j<member["polygonPixels"]!.AsArray().Count;j++){
    var world=Pair(member["polygonPixels"]![j]!)+origin;var expected=ClippedTriangleUv.Map(world,polygon[ia],polygon[ib],polygon[ic],uv[ia],uv[ib],uv[ic]);var actual=Pair(member["uvPixels"]![j]!);sourceUvVertices++;maximumSourceUvError=Math.Max(maximumSourceUvError,expected.DistanceTo(actual));
   }
  }
 }
 var report=new JsonObject{{"maximumExactSourceUvError",maximumSourceUvError},{"sourceUvVertices",sourceUvVertices},{"maximumFormalSourcePointError",maxFormalSourcePoint},{"maximumPreviousSourcePointError",maxPreviousSourcePoint},{"maximumPointUlps",maxPointUlps},{"changedCoordinates",changedVertices},{"supports",a.Length},{"triangleVertices",vertices},{"maximumPointError",maxPoint},{"maximumUvError",maxUv},{"maximumDepthError",maxDepth},{"maximumTintError",maxTint},{"sampledMaterialPixels",sampled},{"coverageMismatch",coverageMismatch},{"productionPolygonNodes",b.Sum(e=>e.Node.GetChildren().OfType<Polygon2D>().Count())},{"previousPolygonNodes",a.Sum(e=>e.Node.GetChildren().OfType<Polygon2D>().Count())}};
 System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://formal-shed-equivalence-result.json"),report.ToJsonString());
 if(maxFormalSourcePoint!=0||maxPointUlps>1||maximumSourceUvError!=0||maxDepth!=0||maxTint!=0||coverageMismatch!=0)throw new Exception("Renderer migration exceeds one coordinate ULP or changes exact UV/depth/tint/coverage");
 GD.Print("FORMAL_SHED_EQUIVALENCE_COMPLETE "+report.ToJsonString());GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
