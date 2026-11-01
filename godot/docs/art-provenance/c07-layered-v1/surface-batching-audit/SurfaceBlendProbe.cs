using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;

// Code-native synthetic material diagnostic, not artwork or a campaign progression fixture.
public partial class SurfaceBlendProbe : Node2D
{
 public override async void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var state=engine.CreateState(level);var original=EcmaJson.Stringify(state);
 var scene=AuthoredScene.Load("res://data/scenes/c01-07-fence-surface-preview.json");var document=PresentationJson.Load<AssembledGroundDocument>(scene.AssembledGroundPath!);BoardPlaneFactory plane=document.CreatePlane;
 using var image=Image.CreateEmpty(64,64,false,Image.Format.Rgba8);image.Fill(new Color(1,1,1,.5f));image.FillRect(new Rect2I(0,0,16,64),Colors.Transparent);image.FillRect(new Rect2I(48,0,16,64),Colors.White);using var texture=ImageTexture.CreateFromImage(image);
 var reverse=OS.GetEnvironment("C07_BLEND_REVERSE")=="1";
 var catalog=new SceneObjectCatalog{Schema=1,Objects=new(){["training-fence"]=new(){ResourcePath="code-native-alpha-fixture",PixelSize=[64,64],AnchorPixels=[0,0],ContactShadowRadiiPixels=null,SurfaceGeometryPath=reverse?"res://surface-blend-reversed.json":"res://surface-blend-geometry.json"}}};
 var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,plane),level,scene);
 board.ConfigureGround((rules,s)=>AssembledGroundView.Create(document,GD.Load<Texture2D>(document.ResourcePath),catalog,new Dictionary<string,Texture2D>{{"training-fence",texture}},state["map"]!.AsObject(),engine.Catalog,rules.Shadows));board.ConfigureDynamicState();board.RenderState(state);
 var zoom=float.Parse(OS.GetEnvironment("C07_BLEND_ZOOM"),System.Globalization.CultureInfo.InvariantCulture);var shift=float.Parse(OS.GetEnvironment("C07_BLEND_SHIFT"),System.Globalization.CultureInfo.InvariantCulture);board.Scale=Vector2.One*zoom;board.Position=GetViewportRect().Size/2-board.Projection.CellContact(new(60,13))*zoom+Vector2.One*shift;
 var layer=new CanvasLayer();AddChild(layer);layer.AddChild(new Label{Text="SYNTHETIC NATIVE-ALPHA / TINT / ORDER FIXTURE | no actions or saves\nNot game artwork. zoom="+zoom+" shift="+shift+" reversed="+reverse,Position=new(12,12)});
 await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
 using var shot=GetViewport().GetTexture().GetImage();shot.SavePng(OS.GetEnvironment("C07_BLEND_OUTPUT"));if(original!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static synthetic material changed state or introduced rendering issue");
 board.Free();GD.Print("SURFACE_BLEND_STATIC_COMPLETE stateUnchanged=true");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
