using Godot;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Reflection;
using System.Text.Json.Nodes;

// Static diagnosis only: reuses the production bounded-floor API by reflection.
// No production patch, commands, AI, turns, or saves; no new raster or shadow size.
public partial class ContactShadowFloorProbe : Node2D
{
 public override async void _Ready(){try{
  var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v=>v["id"]!.GetValue<string>()=="c01-07");
  var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);var shown=state.DeepClone().AsObject();
  var torren=state["units"]!.AsArray().OfType<JsonObject>().Single(v=>v["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();torren["x"]=28;torren["y"]=30;torren["facing"]="east";shown["units"]=new JsonArray(torren);
  var scene=AuthoredScene.Load("res://data/scenes/c01-07-selected-assembly.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());var board=new BoardView{AnimationsEnabled=false};AddChild(board);BoardGroundView ground=null!;
  board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();board.RenderState(shown);board.Scale=Vector2.One*2.5f;
  void Layout()=>board.Position=GetViewportRect().Size/2-board.Projection.CellContact(new(30,27))*2.5f;GetViewport().SizeChanged+=Layout;Layout();
  var contacts=ground.GetChildren().OfType<Node2D>().Where(n=>n.Name.ToString().EndsWith("__ground_contact")).ToArray();
  if(contacts.Length==0)throw new Exception("No independent floor contacts");
  var ids=contacts.Select(n=>n.GetNode<Node2D>("RegisteredSurface").GetInstanceId()).ToArray();
  var textureIds=contacts.Select(n=>n.GetNode<Sprite2D>("SourceStructureContact").Texture.GetInstanceId()).ToArray();
  int Count(Node n)=>1+n.GetChildren().Sum(c=>Count(c));
  var count=Count(ground);
  var picks=Enumerable.Range(0,80).SelectMany(x=>Enumerable.Range(0,64).Select(y=>new Vector2(x*16+8,y*16+8))).Select(p=>(p,hit:board.Pick(p))).ToArray();
  for(int repeat=0;repeat<3;repeat++)board.RenderState(shown);
  if(Count(ground)!=count||!ids.SequenceEqual(contacts.Select(n=>n.GetNode<Node2D>("RegisteredSurface").GetInstanceId()))||!textureIds.SequenceEqual(contacts.Select(n=>n.GetNode<Sprite2D>("SourceStructureContact").Texture.GetInstanceId())))throw new Exception("Repeated render accumulated/replaced contact nodes or textures");
  foreach(var c in contacts){if(c.GetChildCount()!=2||c.GetNode<Sprite2D>("SourceStructureContact").Visible)throw new Exception("Contact source/floor ownership");if(ground.DepthEntries.Any(e=>e.Node==c||e.Node==c.GetNode<Node2D>("RegisteredSurface")))throw new Exception("Decorative floor registered for Pick");}
  var output=OS.GetEnvironment("CONTACT_SHADOW_OUTPUT");if(output.Length==0)throw new Exception("Output directory required");
  async System.Threading.Tasks.Task Capture(string name){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var tex=GetViewport().GetTexture();using var img=tex.GetImage();img.SavePng(output+"-"+name+".png");}
  await Capture("formal");
  foreach(var c in contacts)c.Visible=false;
  if(picks.Any(pair=>board.Pick(pair.p)!=pair.hit))throw new Exception("Decorative contact changed Pick");
  await Capture("hidden");foreach(var c in contacts)c.Visible=true;
  File.WriteAllText(output+".json",new JsonObject{{"scope","Formal bounded floor contacts; initial/display clone only, no commands."},{"contactCount",contacts.Length},{"stableNodeCount",count},{"repeatedRenders",3},{"unchangedPickPoints",picks.Length},{"stateUnchanged",digest==EcmaJson.Stringify(state)},{"issues",board.RenderingIssueCount}}.ToJsonString());if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static state/render changed");GD.Print("CONTACT_SHADOW_FLOOR_COMPLETE");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
