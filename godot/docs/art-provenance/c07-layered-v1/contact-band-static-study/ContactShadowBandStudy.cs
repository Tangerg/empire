using Godot;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Reflection;
using System.Text.Json.Nodes;

// Static diagnosis only: reuses the production bounded-floor API by reflection.
// No production patch, commands, AI, turns, or saves; no new raster or shadow size.
public partial class ContactShadowBandStudy : Node2D
{
 public override async void _Ready(){try{
  var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
  var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v=>v["id"]!.GetValue<string>()=="c01-07");
  var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);var shown=state.DeepClone().AsObject();
  var torren=state["units"]!.AsArray().OfType<JsonObject>().Single(v=>v["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();torren["x"]=28;torren["y"]=30;torren["facing"]="east";shown["units"]=new JsonArray(torren);
  var scene=AuthoredScene.Load("res://data/scenes/c01-07-selected-assembly.json");var rendering=BoardSceneRendering.Load(scene,level,state["map"]!.AsObject());var board=new BoardView{AnimationsEnabled=false};AddChild(board);BoardGroundView ground=null!;
  board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();board.RenderState(shown);float zoom=2.5f;board.Scale=Vector2.One*zoom;
  void Layout()=>board.Position=GetViewportRect().Size/2-board.Projection.CellContact(new(30,27))*zoom;GetViewport().SizeChanged+=Layout;Layout();

  var output=OS.GetEnvironment("CONTACT_BAND_OUTPUT");if(output.Length==0)throw new Exception("Output required");
  async System.Threading.Tasks.Task Capture(string name){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var tex=GetViewport().GetTexture();using var img=tex.GetImage();img.SavePng(output+"-"+name+".png");}
  await Capture("existing-near");
  var document=PresentationJson.Load<JsonObject>("res://data/scenes/c01-07-selected-assembly-ground.json");
  var catalog=PresentationJson.Load<JsonObject>("res://data/scenes/c01-07-selected-assembly-objects.json")["objects"]!.AsObject();
  var floorType=typeof(BoardView).Assembly.GetType("Empire.Presentation.PaintedSurface")!;
  var create=floorType.GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(BoardProjection),typeof(Sprite2D),typeof(Vector2)},null)!;
  var textures=new List<ImageTexture>();var rows=new JsonArray();
  string F(float n)=>n.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
  foreach(var reference in PresentationJson.Load<JsonObject>("res://contact-band-references-study.json")["objects"]!.AsArray().OfType<JsonObject>()){
   var id=reference["instance"]!.GetValue<string>();
   var placement=document["objects"]!.AsArray().OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()==id);var asset=catalog[placement["assetId"]!.GetValue<string>()]!.AsObject();
   Vector2 V(JsonNode n)=>new(n[0]!.GetValue<float>(),n[1]!.GetValue<float>());
   var scale=placement["scale"]!.GetValue<float>();var anchor=V(asset["anchorPixels"]!);var contact=V(placement["contact"]!)*16;
   Vector2[][] chains;
   if(reference["stripChains"] is JsonArray strips)chains=strips.Select(chain=>chain!.AsArray().Select(index=>(V(asset["depthStrips"]![index!.GetValue<int>()]!["groundFrontPixels"]!)-anchor)*scale).ToArray()).ToArray();
   else {var supports=PresentationJson.Load<JsonObject>(asset["surfaceGeometryPath"]!.GetValue<string>())["supports"]!.AsArray().OfType<JsonObject>().ToDictionary(v=>v["id"]!.GetValue<string>());
    chains=reference["supportChains"]!.AsArray().Select(chain=>chain!.AsArray().Select(key=>(V(supports[key!.GetValue<string>()]["frontPixels"]!)-anchor)*scale).ToArray()).ToArray();}
   var points=chains.SelectMany(v=>v).ToArray();if(points.Length==0)throw new Exception("Empty contact references");
   var min=(new Vector2(points.Min(p=>p.X),points.Min(p=>p.Y))-Vector2.One*2).Floor();var max=(new Vector2(points.Max(p=>p.X),points.Max(p=>p.Y))+Vector2.One*2).Ceil();var size=max-min;
   var path=string.Join(" ",chains.Select(chain=>"M "+string.Join(" L ",(chain.Length==1?new[]{chain[0],chain[0]}:chain).Select(p=>F(p.X-min.X)+","+F(p.Y-min.Y)))));
   var svg=$"<svg xmlns='http://www.w3.org/2000/svg' width='{F(size.X)}' height='{F(size.Y)}' viewBox='0 0 {F(size.X)} {F(size.Y)}'>";
   foreach(var (width,opacity) in new[]{(2f,.04f),(1.2f,.07f),(.6f,.12f)})svg+=$"<path d='{path}' fill='none' stroke='#403627' stroke-width='{F(width)}' stroke-opacity='{F(opacity)}' stroke-linecap='round' stroke-linejoin='round'/>";
   svg+="</svg>";using var pixels=new Image();if(pixels.LoadSvgFromString(svg,2)!=Error.Ok)throw new Exception("Native vector shadow raster failed");var texture=ImageTexture.CreateFromImage(pixels);textures.Add(texture);
   var source=new Sprite2D{Texture=texture,Centered=false,Position=min,Scale=Vector2.One/2,TextureFilter=CanvasItem.TextureFilterEnum.Linear};
   var wrapper=new Node2D{Name=id+"__contact_band_study",Position=board.Projection.Project(contact)};ground.AddChild(wrapper);var floor=(Node2D)create.Invoke(null,new object[]{board.Projection,source,wrapper.Position})!;wrapper.AddChild(floor);source.Free();
   if(ground.GetNodeOrNull<Node2D>(id+"__ground_contact") is {} oldContact)oldContact.Visible=false;
   rows.Add(new JsonObject{{"id",id},{"references",reference.DeepClone()},{"pointCount",points.Length},{"chainCount",chains.Length},{"worldWidths",new JsonArray(2,1.2,.6)}});
  }
  await Capture("band-near");zoom=1.25f;board.Scale=Vector2.One*zoom;Layout();await Capture("band-default");
  if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static state changed");
  File.WriteAllText(output+".json",new JsonObject{{"scope","UNSELECTED vector contact-band appearance study. Source XY derived exclusively from explicit existing real-foot strip references; no new footprint or engine actions."},{"rows",rows},{"stateUnchanged",true},{"issues",board.RenderingIssueCount}}.ToJsonString());
  RemoveChild(board);board.Free();foreach(var texture in textures)texture.Dispose();GD.Print("CONTACT_BAND_STATIC_STUDY_COMPLETE");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
