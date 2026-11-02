using Godot;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Reflection;
using System.Text.Json.Nodes;

// Static diagnosis only: reuses the production bounded-floor API by reflection.
// No production patch, commands, AI, turns, or saves; no new raster or shadow size.
public partial class ContactShadowDepthProbe : Node2D
{
 public override async void _Ready(){try{
  var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v=>v["id"]!.GetValue<string>()=="c01-07");
  var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);var shown=state.DeepClone().AsObject();
  var torren=state["units"]!.AsArray().OfType<JsonObject>().Single(v=>v["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();torren["x"]=28;torren["y"]=30;torren["facing"]="east";shown["units"]=new JsonArray(torren);
  var scene=AuthoredScene.Load("res://data/scenes/c01-07-selected-assembly.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());var board=new BoardView{AnimationsEnabled=false};AddChild(board);BoardGroundView ground=null!;
  board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();board.RenderState(shown);board.Scale=Vector2.One*2.5f;
  void Layout()=>board.Position=GetViewportRect().Size/2-board.Projection.CellContact(new(30,27))*2.5f;GetViewport().SizeChanged+=Layout;Layout();
  var ids=new[]{"west-inner-house-main","west-inner-blue-house-main","west-inner-rear-house-main"};
  var targets=ground.DepthEntries.Where(e=>ids.Contains(e.Node.Name.ToString())).Select(e=>(Entry:e,Shadow:e.Node.GetNode<Sprite2D>("SourceStructureContact"))).ToArray();if(targets.Length!=3)throw new Exception("Three existing contact shadows required");
  var report=new JsonArray();foreach(var pair in targets){var point=pair.Entry.Node.Position;report.Add(new JsonObject{{"id",pair.Entry.Node.Name.ToString()},{"anchor",new JsonArray(point.X,point.Y)},{"parentZ",pair.Entry.Node.ZIndex},{"groundAtAnchor",board.Projection.Pick(point)?.DrawDepth},{"shadowLocalPosition",new JsonArray(pair.Shadow.Position.X,pair.Shadow.Position.Y)},{"textureSize",new JsonArray(pair.Shadow.Texture.GetWidth(),pair.Shadow.Texture.GetHeight())}});}
  var output=OS.GetEnvironment("CONTACT_SHADOW_OUTPUT");if(output.Length==0)throw new Exception("Output directory required");
  async System.Threading.Tasks.Task Capture(string name){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var tex=GetViewport().GetTexture();using var img=tex.GetImage();img.SavePng(output+"-"+name+".png");}
  await Capture("legacy");foreach(var pair in targets)pair.Shadow.Visible=false;await Capture("hidden");
  var type=typeof(BoardView).Assembly.GetType("Empire.Presentation.PaintedSurface")!;
  var create=type.GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(BoardProjection),typeof(Sprite2D),typeof(Vector2)},null)!;
  foreach(var pair in targets){var source=pair.Shadow;var node=pair.Entry.Node;var copy=new Sprite2D{Texture=source.Texture,Centered=source.Centered,Position=source.Position*node.Scale,Scale=source.Scale*node.Scale,TextureFilter=source.TextureFilter};var wrapper=new Node2D{Name="DiagnosticGroundContact_"+node.Name,Position=node.Position};ground.AddChild(wrapper);var floor=(Node2D)create.Invoke(null,new object[]{board.Projection,copy,node.Position})!;wrapper.AddChild(floor);copy.Free();}
  await Capture("bounded-floor");
  File.WriteAllText(output+".json",new JsonObject{{"scope","Three existing contact shadows: unchanged pixels/radii/opacity, legacy vs hidden vs existing bounded-floor API. Initial/display clone only."},{"entries",report},{"stateUnchanged",digest==EcmaJson.Stringify(state)},{"issues",board.RenderingIssueCount}}.ToJsonString());if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static state/render changed");GD.Print("CONTACT_SHADOW_DEPTH_DIAGNOSIS_COMPLETE");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
