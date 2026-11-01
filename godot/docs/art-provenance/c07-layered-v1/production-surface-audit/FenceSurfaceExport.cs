using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;

// Deterministic migration of the frozen study, not a runtime geometry owner.
public partial class FenceSurfaceExport : Node
{
 public override void _Ready(){try{
 var source=PresentationJson.Load<JsonObject>("res://atomic-fence-geometry.json");
 var uv=source["woodUvRectangle"]!.AsArray().Select(n=>n!.GetValue<float>()).ToArray();
 Vector2 Pair(JsonNode n)=>new(n[0]!.GetValue<float>(),n[1]!.GetValue<float>());
 JsonArray Point(Vector2 p)=>new(p.X,p.Y);
 JsonArray Points(IEnumerable<Vector2> p)=>new(p.Select(v=>(JsonNode)Point(v)).ToArray());
 var origin=new Vector2(946,170);var supports=new JsonArray();var members=new JsonArray();
 for(int x=946;x<992;x++){
  var group=new JsonArray();var slab=new[]{new Vector2(x,170),new Vector2(x+1,170),new Vector2(x+1,232),new Vector2(x,232)};
  foreach(var m in source["members"]!.AsArray().OfType<JsonObject>()){
   var poly=m["polygon"]!.AsArray().Select(p=>Pair(p!)).ToArray();
   var start=Pair(m["materialAxis"]![0]!);var end=Pair(m["materialAxis"]![1]!);var along=(end-start).Normalized();var across=new Vector2(-along.Y,along.X);int part=0;
   foreach(var clip in Geometry2D.IntersectPolygons(poly,slab)){
    if(clip.Length<3)continue;
    var tex=clip.Select(p=>new Vector2(Mathf.Clamp((uv[0]+uv[2])/2+(p-start).Dot(across)*4,uv[0]+.5f,uv[2]-1.5f),Mathf.Clamp(uv[1]+10+(p-start).Dot(along)*10,uv[1]+.5f,uv[3]-1.5f)));
    group.Add(new JsonObject{{"id",m["id"]!.GetValue<string>()+"-"+x+"-"+part++},{"supportId","column-"+x},{"polygonPixels",Points(clip.Select(p=>p-origin))},{"uvPixels",Points(tex)},{"coverage","opaque"},{"tint",m["tint"]!.GetValue<string>()}});
   }
  }
  if(group.Count==0)continue;float sx=x+.5f;float sy=sx<=963?208+(sx-950)*7/13:215+(sx-963)*13/24;
  supports.Add(new JsonObject{{"id","column-"+x},{"frontPixels",Point(new Vector2(sx,sy)-origin)},{"kind","structural"}});
  foreach(var member in group)members.Add(member!.DeepClone());
 }
 var output=new JsonObject{{"schema",1},{"supports",supports},{"members",members}};
 System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://data/scenes/c01-07-fence-surface.json"),output.ToJsonString(new(){WriteIndented=true}));
 GD.Print($"FENCE_SURFACE_EXPORT_COMPLETE supports={supports.Count} members={members.Count}");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
