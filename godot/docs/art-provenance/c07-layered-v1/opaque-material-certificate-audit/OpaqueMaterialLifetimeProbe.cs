using Godot;
using Empire.Presentation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
public partial class OpaqueMaterialLifetimeProbe : Node
{
 [MethodImpl(MethodImplOptions.NoInlining)]
 static (object Compiled, WeakReference Material, WeakReference Prefix, Texture2D Texture) CompileAndRelease()
 {
  var type=typeof(BoardView).Assembly.GetType("Empire.Presentation.CompiledSceneSurface")!;
  var materialsType=type.GetNestedType("Materials",BindingFlags.NonPublic)!;
  using var image=Image.CreateEmpty(8,8,false,Image.Format.Rgba8);image.Fill(Colors.White);image.SetPixel(7,7,Colors.Transparent);
  var texture=ImageTexture.CreateFromImage(image);
  using var materials=(IDisposable)Activator.CreateInstance(materialsType,true)!;
  var material=materialsType.GetMethod("Get",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(materials,new object[]{texture})!;
  var materialType=material.GetType();materialType.GetMethod("ContainsOnlyOpaqueTexels",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(material,new object[]{new Rect2(0,0,2,2)});
  var prefix=materialType.GetField("_nonOpaquePrefix",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(material)!;
  var document = new SceneSurfaceDocument
  {
   Schema = 1,
   Supports = [new() { Id = "foot", FrontPixels = [1, 2], Kind = "structural" }],
   Members = [new() { Id = "triangle", SupportId = "foot", Coverage = "opaque", Tint = "#ffffff",
    PolygonPixels = [[0, 0], [2, 0], [0, 2]], UvPixels = [[0, 0], [2, 0], [0, 2]] }]
  };
  var compile=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="Compile"&&m.GetParameters()[1].ParameterType==materialType);
  var compiled=compile.Invoke(null,new[]{document,material,(object)"lifetime"})!;
  return(compiled,new WeakReference(material),new WeakReference(prefix),texture);
 }
 public override void _Ready(){try{
  var result=CompileAndRelease();GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
  var report=new{scope="Diagnostic-only forced collection, compiled geometry kept alive; no gameplay or runtime GC changes",materialAlive=result.Material.IsAlive,prefixAlive=result.Prefix.IsAlive};
  File.WriteAllText(ProjectSettings.GlobalizePath("res://opaque-material-lifetime-result.json"),JsonSerializer.Serialize(report));
  if(result.Material.IsAlive||result.Prefix.IsAlive)throw new Exception("Compiled geometry retained disposed temporary material/prefix");GC.KeepAlive(result.Compiled);result.Texture.Dispose();GD.Print("OPAQUE_MATERIAL_LIFETIME_OK");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
