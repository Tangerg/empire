using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;

// Offline transfer of the reviewed section ownership. The exported surface is the runtime fact.
public partial class SectionedSurfaceExport : Node
{
 public override void _Ready(){try{
 var source=PresentationJson.Load<JsonObject>(OS.GetEnvironment("C07_SECTION_SOURCE"));
 var output=OS.GetEnvironment("C07_SECTION_OUTPUT");
 var texture=GD.Load<Texture2D>(source["textureResourcePath"]!.GetValue<string>());
 Vector2 Pair(JsonNode p)=>new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());
 JsonArray Points(IEnumerable<Vector2> values)=>new(values.Select(p=>(JsonNode)new JsonArray(p.X,p.Y)).ToArray());
 var supports=new JsonArray();var members=new JsonArray();
 foreach(var section in source["sections"]!.AsArray().OfType<JsonObject>()){
  var polygon=section["polygonPixels"]!.AsArray().Select(p=>Pair(p!)).ToArray();
  var support=section["supportLinePixels"]!.AsArray().Select(p=>Pair(p!)).ToArray();
  float Foot(float x){for(int i=0;i<support.Length-1;i++)if(x<=support[i+1].X)return Mathf.Lerp(support[i].Y,support[i+1].Y,(x-support[i].X)/(support[i+1].X-support[i].X));throw new Exception("Section support does not span its owned pixels");}
  int min=(int)MathF.Floor(polygon.Min(p=>p.X)),max=(int)MathF.Ceiling(polygon.Max(p=>p.X));
  for(int x=min;x<max;x+=8){
   int right=Math.Min(x+8,max);string id=section["id"]!.GetValue<string>()+"-"+x;var pieces=new List<Vector2[]>();
   foreach(var clip in Geometry2D.IntersectPolygons(polygon,new[]{new Vector2(x,-1),new Vector2(right,-1),new Vector2(right,texture.GetHeight()+1),new Vector2(x,texture.GetHeight()+1)})){
    if(clip.Length<3)continue;var remainder=new List<Vector2[]>{clip};
    if(source["excludePolygons"] is JsonArray cuts)foreach(var cut in cuts){var points=cut!.AsArray().Select(p=>Pair(p!)).ToArray();remainder=remainder.SelectMany(p=>Geometry2D.ClipPolygons(p,points)).ToList();}
    foreach(var p in remainder){if(p.Length<3)continue;if(Geometry2D.IsPolygonClockwise(p)!=Geometry2D.IsPolygonClockwise(clip))throw new Exception("Unsupported inner ring");pieces.Add(p);}
   }
   if(pieces.Count==0)continue;var ownedXs=pieces.SelectMany(piece=>piece).Select(point=>point.X).ToArray();float sx=(ownedXs.Min()+ownedXs.Max())/2f;
   supports.Add(new JsonObject{{"id",id},{"frontPixels",new JsonArray(sx,Foot(sx))},{"kind","structural"}});
   for(int i=0;i<pieces.Count;i++)members.Add(new JsonObject{{"id",id+"-"+i},{"supportId",id},{"polygonPixels",Points(pieces[i])},{"uvPixels",Points(pieces[i])},{"coverage","native-alpha"},{"tint","#ffffff"}});
  }
 }
 File.WriteAllText(ProjectSettings.GlobalizePath(output),new JsonObject{{"schema",1},{"supports",supports},{"members",members}}.ToJsonString(new(){WriteIndented=true}));
 GD.Print($"SECTIONED_SURFACE_EXPORT_COMPLETE supports={supports.Count} members={members.Count}");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
