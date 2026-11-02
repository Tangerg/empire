using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;

// Offline deterministic transfer of the frozen textured-prop study. Runtime consumes only its explicit surface output.
public partial class TexturedSurfaceExport : Node
{
 public override void _Ready(){try{
 var input=OS.GetEnvironment("C07_TEXTURED_SOURCE");if(input.Length==0)input="res://atomic-canopy-geometry.json";
 var output=OS.GetEnvironment("C07_TEXTURED_OUTPUT");if(output.Length==0)output="res://data/scenes/c01-07-canopy-surface.json";
 var source=PresentationJson.Load<JsonObject>(input);Vector2 Pair(JsonNode p)=>new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());JsonArray Point(Vector2 p)=>new(p.X,p.Y);JsonArray Points(IEnumerable<Vector2> p)=>new(p.Select(v=>(JsonNode)Point(v)).ToArray());
 var members=source["members"]!.AsArray().OfType<JsonObject>().ToArray();var vertices=members.SelectMany(m=>m["polygon"]!.AsArray().Select(p=>Pair(p!))).ToArray();
 int minX=(int)MathF.Floor(vertices.Min(p=>p.X)),maxX=(int)MathF.Ceiling(vertices.Max(p=>p.X));float minY=vertices.Min(p=>p.Y)-1,maxY=vertices.Max(p=>p.Y)+1;var origin=new Vector2(minX,MathF.Floor(vertices.Min(p=>p.Y)));
 var supportMode=source["supportMode"]?.GetValue<string>();if(supportMode is not (null or "horizontal-plane"))throw new Exception("Unknown offline support mode");bool horizontal=supportMode=="horizontal-plane";
 var support=source["supportLine"]!.AsArray().Select(p=>Pair(p!)).ToArray();float Front(float x){for(int i=0;i<support.Length-1;i++){var a=support[i];var b=support[i+1];if(x<=b.X)return a.Y+(b.Y-a.Y)*(x-a.X)/(b.X-a.X);}throw new Exception("Outside declared support extent");}
 var exportedMembers=new JsonArray();var exportedSupports=new JsonArray();
 for(int x=horizontal?(int)MathF.Floor(minY+1):minX;x<(horizontal?(int)MathF.Ceiling(maxY-1):maxX);x++){
  var batch=new JsonArray();foreach(var member in members){var polygon=member["polygon"]!.AsArray().Select(p=>Pair(p!)).ToArray();var uv=member["uv"]!.AsArray().Select(p=>Pair(p!)).ToArray();var triangles=Geometry2D.TriangulatePolygon(polygon);
   for(int ti=0;ti<triangles.Length;ti+=3){int ia=triangles[ti],ib=triangles[ti+1],ic=triangles[ti+2];var a=polygon[ia];var b=polygon[ib];var c=polygon[ic];int clipIndex=0;
    foreach(var clip in Geometry2D.IntersectPolygons(new[]{a,b,c},horizontal?new[]{new Vector2(minX,x),new Vector2(maxX,x),new Vector2(maxX,x+1),new Vector2(minX,x+1)}:new[]{new Vector2(x,minY),new Vector2(x+1,minY),new Vector2(x+1,maxY),new Vector2(x,maxY)})){
     if(clip.Length<3)continue;
     Vector2 ToUv(Vector2 p)=>ClippedTriangleUv.Map(p,a,b,c,uv[ia],uv[ib],uv[ic]);
     batch.Add(new JsonObject{{"id",member["id"]!.GetValue<string>()+"-"+x+"-"+ti+"-"+clipIndex++},{"supportId","column-"+x},{"polygonPixels",Points(clip.Select(p=>p-origin))},{"uvPixels",Points(clip.Select(ToUv))},{"tint",member["tint"]!.GetValue<string>()},{"coverage","opaque"}});
    }
   }
  }
  if(batch.Count==0)continue;var ownedX=batch.OfType<JsonObject>().SelectMany(m=>m["polygonPixels"]!.AsArray().Select(p=>p![0]!.GetValue<float>()+origin.X)).ToArray();var contactX=(ownedX.Min()+ownedX.Max())/2;exportedSupports.Add(new JsonObject{{"id","column-"+x},{"frontPixels",Point(new Vector2(contactX,horizontal?x+.5f:Front(contactX))-origin)},{"kind","structural"}});foreach(var m in batch)exportedMembers.Add(m!.DeepClone());
 }
 var result=new JsonObject{{"schema",1},{"supports",exportedSupports},{"members",exportedMembers}};File.WriteAllText(ProjectSettings.GlobalizePath(output),result.ToJsonString(new(){WriteIndented=true}));GD.Print($"TEXTURED_SURFACE_EXPORT_COMPLETE origin={origin} supports={exportedSupports.Count} members={exportedMembers.Count}");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
