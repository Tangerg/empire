using Godot;
using Empire.Presentation;
using System.Diagnostics;
using System.Reflection;
public partial class GranaryShedCompileTiming:Node
{
 public override void _Ready(){try{
 var method=typeof(AssembledGroundView).Assembly.GetType("Empire.Presentation.CompiledSceneSurface")!.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="Compile"&&m.GetParameters()[1].ParameterType==typeof(Texture2D));
 foreach(var part in new[]{"roof","fascia","leftpost","nearpost","rightpost","rearpost"}){
  var document=PresentationJson.Load<SceneSurfaceDocument>($"res://data/scenes/c01-07-granary-shed-{part}-surface.json");
  var texture=GD.Load<Texture2D>("res://assets/native/c07-layered-v1/"+(part=="roof"?"orange-clay-tile-material-v1.png":"granary-main-v1-candidate.png"));
  GD.Print("SHED_MATERIAL_COMPILE_BEGIN "+part+" members="+document.Members.Length);
  var watch=Stopwatch.StartNew();method.Invoke(null,new object[]{document,texture,"shed-timing-"+part});watch.Stop();
  GD.Print("SHED_MATERIAL_COMPILE_END "+part+" elapsedMs="+watch.Elapsed.TotalMilliseconds);
 }
 GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
