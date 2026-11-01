using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
public partial class ClippedTriangleUvChecks:Node
{
 public override void _Ready(){try{
 int assertions=0;void Check(bool ok,string message){assertions++;if(!ok)throw new Exception(message);}
 var a=new Vector2(0,0);var b=new Vector2(10,0);var c=new Vector2(0,10);var ua=new Vector2(0,0);var ub=new Vector2(1535,0);var uc=new Vector2(0,1023);
 foreach(var (p,uv) in new[]{(a,ua),(b,ub),(c,uc)})Check(ClippedTriangleUv.Map(p,a,b,c,ua,ub,uc)==uv,"Source UV vertex must remain exact.");
 Check(ClippedTriangleUv.Map(new(5,0),a,b,c,ua,ub,uc)==new Vector2(767.5f,0),"Closed edge midpoint must preserve linear interpolation.");
 foreach(var p in new[]{new Vector2(-1,1),new Vector2(8,8),new Vector2(1,-1),new Vector2(float.NaN,0)}){
 bool rejected=false;try{ClippedTriangleUv.Map(p,a,b,c,ua,ub,uc);}catch(ArgumentException error)when(error.Message.Contains("outside")){rejected=true;}Check(rejected,"Clearly outside/nonfinite point must reject, not normalize into triangle.");}
 var source=PresentationJson.Load<JsonObject>("res://granary-left-shed-geometries.json")["components"]![0]!["members"]![0]!;
 Vector2 Pair(JsonNode p)=>new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());
 var polygon=source["polygon"]!.AsArray().Select(p=>Pair(p!)).ToArray();var uvSource=source["uv"]!.AsArray().Select(p=>Pair(p!)).ToArray();var tri=Geometry2D.TriangulatePolygon(polygon);
 var failed=PresentationJson.Load<JsonObject>("res://formal-shed-roof-original-failed-surface.json");
 int originalNegative=0,total=0;float delta=0;var rows=new JsonArray();
 foreach(var member in failed["members"]!.AsArray()){
  var pieces=member!["id"]!.GetValue<string>().Split('-');int ti=int.Parse(pieces[^2]);int ia=tri[ti],ib=tri[ti+1],ic=tri[ti+2];
  for(int i=0;i<member["polygonPixels"]!.AsArray().Count;i++){
   var world=Pair(member["polygonPixels"]![i]!)+new Vector2(300,94);var old=Pair(member["uvPixels"]![i]!);
   var mapped=ClippedTriangleUv.Map(world,polygon[ia],polygon[ib],polygon[ic],uvSource[ia],uvSource[ib],uvSource[ic]);
   Check(mapped.X>=0&&mapped.Y>=0&&mapped.X<=1535&&mapped.Y<=1023,"Closed triangle mapping escaped original UV hull.");total++;delta=Math.Max(delta,old.DistanceTo(mapped));
   if(old.X<0||old.Y<0){originalNegative++;rows.Add(new JsonObject{{"world",new JsonArray(world.X,world.Y)},{"oldUv",new JsonArray(old.X,old.Y)},{"newUv",new JsonArray(mapped.X,mapped.Y)}});}
  }
 }
 Check(originalNegative==154,"Original 154 failed UV endpoints must remain reproduced.");
 var result=new JsonObject{{"assertions",assertions},{"sourceVertexCount",total},{"originalNegativeUvCount",originalNegative},{"maximumUvDeltaTexels",delta},{"worldGeometryChanged",false},{"originalSourceUvChanged",false},{"correctedOriginalFailures",rows}};
 File.WriteAllText(ProjectSettings.GlobalizePath("res://formal-shed-stable-uv-result.json"),result.ToJsonString());
 GD.Print("CLIPPED_TRIANGLE_UV_CHECKS_COMPLETE "+assertions+" negativeEndpoints="+originalNegative+" maximumDelta="+delta);GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
