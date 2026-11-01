using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;
public partial class SouthernSupplyStockSurvey:Node {
 public override async void _Ready(){try{
 var e=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));var l=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var s=e.CreateState(l);var digest=EcmaJson.Stringify(s);var sc=AuthoredScene.Load("res://data/scenes/c01-07-selected-assembly.json");var r=BoardSceneRendering.Load(sc,l,s["map"]!.AsObject());var b=new BoardView{AnimationsEnabled=false};AddChild(b);b.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),e.Grids.Get("square4"),e.Catalog,r.Plane),l,sc);BoardGroundView? ground=null;b.ConfigureGround((rules,scene)=>ground=r.Ground(rules,scene));b.ConfigureDynamicState();var empty=s.DeepClone().AsObject();empty["units"]=new JsonArray();b.RenderState(empty);
 bool Cargo(TerrainDepthEntry d)=>d.Node.Name.ToString().StartsWith("southern-supply-stock-")||d.Node.Name.ToString().StartsWith("southern-supply-right-bales");var entries=ground!.DepthEntries.Where(Cargo).Select(d=>(Entry:d,Bounds:d.Visual!.GlobalBounds)).ToArray();var rows=new JsonArray();int poses=0;
 foreach(var coord in new[]{(70,49),(71,50),(72,50),(74,49),(75,49),(76,49),(77,50),(78,50),(79,50),(80,50),(81,49),(81,48),(70,44),(71,45)}){
 if(l["terrain"]![coord.Item2]!.GetValue<string>()[coord.Item1]!='p')continue;
 foreach(var key in new[]{"campaign-torren","engineer","raider-1"})foreach(var facing in new[]{"east","west"}){
 var u=s["units"]!.AsArray().OfType<JsonObject>().Single(v=>v["key"]!.GetValue<string>()==key).DeepClone().AsObject();u["x"]=coord.Item1;u["y"]=coord.Item2;u["facing"]=facing;int id=u["id"]!.GetValue<int>();var shown=s.DeepClone().AsObject();shown["units"]=new JsonArray(u);b.RenderState(shown);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);poses++;var actor=b.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");var sprite=actor.GetNode<Sprite2D>("AuthoredSprite");var witnesses=new JsonArray();var signatures=new HashSet<string>();int overlap=0;
 for(int py=coord.Item2*16-42;py<=coord.Item2*16+16;py+=2)for(int px=coord.Item1*16-30;px<=coord.Item1*16+40;px+=2){var p=new Vector2(px+.5f,py+.5f);if(!sprite.IsPixelOpaque(sprite.ToLocal(p)))continue;var covered=entries.Where(d=>d.Bounds.HasPoint(p)&&d.Entry.Visual!.Covers(p)).Select(d=>d.Entry).ToArray();if(covered.Length==0)continue;overlap++;bool front=covered.Any(d=>d.Node.ZIndex>actor.ZIndex);bool picked=b.Pick(p)==TacticalHit.Unit(new(coord.Item1,coord.Item2),id);string name=covered[0].Node.Name.ToString().Split("__")[0];string signature=name+front+picked;if(!signatures.Add(signature))continue;
 var other=ground.DepthEntries.Where(d=>!Cargo(d)&&d.Visual is not null&&d.Node.ZIndex>actor.ZIndex&&d.Visual.GlobalBounds.HasPoint(p)&&d.Visual.Covers(p)).Select(d=>d.Node.Name.ToString().Split("__")[0]).Distinct().ToArray();witnesses.Add(new JsonObject{{"source",new JsonArray(p.X,p.Y)},{"part",name},{"actorZ",actor.ZIndex},{"groundDepth",b.Projection.Pick(p)?.DrawDepth},{"cargoInFront",front},{"actorPickable",picked},{"otherForeground",new JsonArray(other.Select(n=>(JsonNode)JsonValue.Create(n)!).ToArray())}});
 }
 if(overlap>0)rows.Add(new JsonObject{{"key",key},{"cell",new JsonArray(coord.Item1,coord.Item2)},{"facing",facing},{"overlap",overlap},{"witnesses",witnesses}});GD.Print($"STOCK_SURVEY {poses} {coord} {key} {facing} overlap={overlap}");
 }}
 var result=new JsonObject{{"scope","Bounded actual C07 appearance survey in display clones; no action/AI/save, overlap alone not a correctness verdict."},{"poses",poses},{"stateUnchanged",digest==EcmaJson.Stringify(s)},{"issues",b.RenderingIssueCount},{"rows",rows}};File.WriteAllText(ProjectSettings.GlobalizePath("res://southern-supply-stock-role-survey.json"),result.ToJsonString());if(digest!=EcmaJson.Stringify(s)||b.RenderingIssueCount!=0)throw new Exception("Stock survey changed state/render issues");GD.Print("SOUTHERN_STOCK_ROLE_SURVEY_COMPLETE");GetTree().Quit();
 }catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
