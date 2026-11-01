using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;
public partial class SouthernSupplyOriginalProbe:Node {
 public override async void _Ready(){try{
 var e=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));var l=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var s=e.CreateState(l);var digest=EcmaJson.Stringify(s);var sc=AuthoredScene.Load("res://data/scenes/c01-07.json");var r=BoardSceneRendering.Load(sc,l,s["map"]!.AsObject());var b=new BoardView{AnimationsEnabled=false};AddChild(b);b.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),e.Grids.Get("square4"),e.Catalog,r.Plane),l,sc);BoardGroundView? ground=null;b.ConfigureGround((rules,scene)=>ground=r.Ground(rules,scene));b.ConfigureDynamicState();
 if(OS.GetEnvironment("C07_SUPPLY_SITE_ONLY")=="1"){var empty=s.DeepClone().AsObject();empty["units"]=new JsonArray();b.RenderState(empty);var siteResult=new JsonObject{{"scope","Original static site baseline, no actions"},{"site",b.Pick(b.Projection.CellContact(new(70,50)))?.ToString()},{"approach",b.Pick(b.Projection.CellContact(new(70,51)))?.ToString()},{"stateUnchanged",digest==EcmaJson.Stringify(s)}};File.WriteAllText(ProjectSettings.GlobalizePath("res://southern-supply-original-site.json"),siteResult.ToJsonString());GD.Print(siteResult.ToJsonString());GetTree().Quit();return;}
 var rows=new JsonArray();int poses=0;var templates=s["units"]!.AsArray().OfType<JsonObject>().Where(u=>u["key"]!.GetValue<string>() is "campaign-torren" or "engineer").ToArray();
 foreach(int y in new[]{40,41,42,47,48,49,50})foreach(int x in new[]{71,72,73,76,77,78,79,80,81,82,83}){
 if(l["terrain"]![y]!.GetValue<string>()[x]!='p')continue;
 foreach(var original in templates)foreach(var facing in new[]{"east","west"}){
 var u=original.DeepClone().AsObject();u["x"]=x;u["y"]=y;u["facing"]=facing;int id=u["id"]!.GetValue<int>();var shown=s.DeepClone().AsObject();shown["units"]=new JsonArray(u);b.RenderState(shown);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);poses++;var mainEntries=ground!.DepthEntries.Where(d=>d.Visual is not null&&d.Node.Name.ToString().StartsWith("southern-supply_")).ToArray();var actor=b.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");var sprite=actor.GetNode<Sprite2D>("AuthoredSprite");var witnesses=new JsonArray();int overlaps=0;
 for(int py=y*16-38;py<=y*16+16;py+=2)for(int px=x*16-18;px<=x*16+34;px+=2){
 var p=new Vector2(px+.5f,py+.5f);if(!sprite.IsPixelOpaque(sprite.ToLocal(b.ToGlobal(p))))continue;
 if(!mainEntries.Any(d=>d.Visual!.Covers(b.ToGlobal(p))))continue;overlaps++; if(witnesses.Count>=3)continue;var cover=ground!.DepthEntries.Where(d=>d.Visual is not null&&d.Visual.Covers(b.ToGlobal(p))).ToArray();
 if(witnesses.Count<3)witnesses.Add(new JsonObject{{"source",new JsonArray(p.X,p.Y)},{"actorZ",actor.ZIndex},{"surfaceDepth",b.Projection.Pick(p)?.DrawDepth},{"pick",b.Pick(p)?.ToString()},{"actorPickable",b.Pick(p)==TacticalHit.Unit(new(x,y),id)},{"cover",new JsonArray(cover.Select(d=>(JsonNode)new JsonObject{{"name",d.Node.Name.ToString()},{"z",d.Node.ZIndex}}).ToArray())}});
 }
 GD.Print($"SURVEY_POSE {poses} {x},{y} {facing} overlaps={overlaps}");if(overlaps>0)rows.Add(new JsonObject{{"key",original["key"]!.DeepClone()},{"type",original["type"]!.DeepClone()},{"cell",new JsonArray(x,y)},{"facing",facing},{"overlapSamples",overlaps},{"witnesses",witnesses}});
 }}
 var result=new JsonObject{{"scope","Read-only original main-body overlap survey using actual C07 appearance templates placed in renderer clones on canonical p cells; no actions/AI/save; overlap is not yet proof of wrong occlusion"},{"poses",poses},{"sampleStepPixels",2},{"stateUnchanged",digest==EcmaJson.Stringify(s)},{"issues",b.RenderingIssueCount},{"rows",rows}};File.WriteAllText(ProjectSettings.GlobalizePath("res://southern-supply-original-survey.json"),result.ToJsonString());if(digest!=EcmaJson.Stringify(s)||b.RenderingIssueCount!=0)throw new Exception("Static survey mutated state/render issues");GD.Print($"SOUTHERN_SUPPLY_ORIGINAL_SURVEY poses={poses} overlaps={rows.Count}");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
