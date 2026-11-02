using Godot;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using System.Reflection;
using System.Text.Json.Nodes;
public partial class ContactShadowLifecycleProbe : Node
{
 public override void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
 var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v=>v["id"]!.GetValue<string>()=="c01-01");
 var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);var map=state["map"]!.AsObject();var scene=AuthoredScene.Load("res://data/scenes/c01-01.json");var rendering=BoardSceneRendering.Load(scene,level,map);var rows=new JsonArray();
 foreach(bool fallback in new[]{false,true}){
 var board=new BoardView{AnimationsEnabled=false};AddChild(board);BoardGroundView ground=null!;
 board.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);
 board.ConfigureGround((rules,s)=>ground=rendering.Ground(fallback?rules with{Shadows=null!}:rules,s));board.ConfigureDynamicState();board.RenderState(state);
 var contacts=ground.GetChildren().OfType<Node2D>().Where(n=>n.Name.ToString().EndsWith("__ground_contact")).ToArray();if(contacts.Length==0)throw new Exception("No contacts");
 var old=contacts.Select(n=>n.GetNode<Node2D>("RegisteredSurface")).ToArray();var sources=contacts.Select(n=>n.GetNode<Sprite2D>("SourceStructureContact")).ToArray();var textures=sources.Select(s=>s.Texture).ToArray();var projection=board.Projection;
 var playable=(IReadOnlySet<Coord>)typeof(BoardProjection).GetField("_playable",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(projection)!;
 var replacement=new BoardProjection(engine.Grids.Get("square4"),rendering.Plane,map,playable);
 var tiles=map["tiles"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray();var heights=map["elevation"]!.AsArray().Select(n=>n!.GetValue<int>()).ToArray();var size=new Vector2I(map["width"]!.GetValue<int>(),map["height"]!.GetValue<int>());
 ground.Render(replacement,tiles,heights,size);
 if(old.Any(GodotObject.IsInstanceValid)||contacts.Any(n=>n.GetChildCount()!=2)||textures.Any(t=>!GodotObject.IsInstanceValid(t)))throw new Exception("Projection replacement ownership");
 var current=contacts.Select(n=>n.GetNode<Node2D>("RegisteredSurface")).ToArray();ground.Render(replacement,tiles,heights,size);
 if(!current.SequenceEqual(contacts.Select(n=>n.GetNode<Node2D>("RegisteredSurface"))))throw new Exception("Repeated projection replaced floors");
 var eventField=typeof(BoardProjection).GetField("DepthPartitionsChanged",BindingFlags.Instance|BindingFlags.NonPublic)!;
 int Subscriptions(BoardProjection p)=>((Delegate?)eventField.GetValue(p))?.GetInvocationList().Length??0;
 var activeSubscriptions=Subscriptions(replacement);RemoveChild(board);board.Free();
 if(current.Any(GodotObject.IsInstanceValid)||sources.Any(GodotObject.IsInstanceValid)||Subscriptions(replacement)!=0)throw new Exception("Unload retained nodes/events");
 if(fallback&&textures.Any(GodotObject.IsInstanceValid))throw new Exception("Fallback texture not released");
 rows.Add(new JsonObject{{"fallback",fallback},{"contacts",contacts.Length},{"activeProjectionSubscriptions",activeSubscriptions},{"releasedProjectionSubscriptions",Subscriptions(replacement)},{"unloadVerified",true}});
 }
 if(digest!=EcmaJson.Stringify(state))throw new Exception("Initial state changed");File.WriteAllText(OS.GetEnvironment("CONTACT_LIFECYCLE_OUTPUT"),rows.ToJsonString());GD.Print("CONTACT_SHADOW_LIFECYCLE_COMPLETE");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
