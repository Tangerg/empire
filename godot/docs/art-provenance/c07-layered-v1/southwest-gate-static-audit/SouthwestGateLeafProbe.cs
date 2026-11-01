using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;
public partial class SouthwestGateLeafProbe:Node{
public override void _Ready(){try{
var e=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));var l=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var s=e.CreateState(l);var digest=EcmaJson.Stringify(s);bool candidate=true;bool behind=OS.GetEnvironment("C07_GATE_BACK_DEPTH")=="1";bool oldMaterial=OS.GetEnvironment("C07_GATE_V1_MATERIAL")=="1";var sc=AuthoredScene.Load(candidate?"res://data/scenes/c01-07-council-preview.json":"res://data/scenes/c01-07.json");var r=BoardSceneRendering.Load(sc,l,s["map"]!.AsObject());var b=new BoardView{AnimationsEnabled=false};AddChild(b);b.Configure(new(AssetCatalog.Load(),EnvironmentCatalog.Load(),e.Grids.Get("square4"),e.Catalog,r.Plane),l,sc);BoardGroundView? ground=null;var geometry=PresentationJson.Load<JsonObject>("res://southwest-gate-v1-sections.json");
if(oldMaterial)geometry["textureResourcePath"]="res://assets/native/c07-layered-v1/southeast-gate-v1-candidate.png";
if(behind)foreach(var section in geometry["sections"]!.AsArray())foreach(var foot in section!["supportLinePixels"]!.AsArray())foot![1]=(884-geometry["translation"]![1]!.GetValue<float>())/geometry["scale"]!.GetValue<float>();
var leafGeometry=PresentationJson.Load<JsonObject>("res://southwest-door-leaf-geometry.json");var leafNegative=OS.GetEnvironment("C07_LEAF_NEGATIVE");
if(leafNegative.Length>0)foreach(var foot in leafGeometry["supportLine"]!.AsArray())foot![1]=leafNegative=="background"?860f:940f;
b.ConfigureGround((rules,scene)=>ground=new TexturedPropGround(new SectionedSpriteGround(r.Ground(rules,scene),geometry),leafGeometry));b.ConfigureDynamicState();
var empty=s.DeepClone().AsObject();empty["units"]=new JsonArray();b.RenderState(empty);
var gateEntries=ground!.DepthEntries.Where(d=>d.Node.Name.ToString().StartsWith("SWGate_")).Select(d=>(d.Visual!,d.Visual!.GlobalBounds)).ToArray();
bool GateCovers(Vector2 p){var global=b.ToGlobal(p);return gateEntries.Any(d=>d.Item2.HasPoint(global)&&d.Item1.Covers(global));}
bool InsideInclusive(Vector2 p,Vector2[] poly){
for(int i=0;i<poly.Length;i++){var a=poly[i];var d=poly[(i+1)%poly.Length]-a;var q=p-a;if(MathF.Abs(d.Cross(q))<0.001f&&q.Dot(d)>=0&&q.Dot(d)<=d.LengthSquared())return true;}
return Geometry2D.IsPointInPolygon(p,poly);}
bool LeafCovers(Vector2 p)=>ground!.DepthEntries.Any(d=>d.Node.Name.ToString().StartsWith("SouthwestGateWoodLeaf")&&d.Visual!.Covers(b.ToGlobal(p)));
var leafMaterial=new JsonArray();foreach(var spec in new[]{(new Vector2(198,875),true),(new Vector2(199,891),true),(new Vector2(205,889),true),(new Vector2(191,899),true),(new Vector2(198,896),true),(new Vector2(205,893),true),(new Vector2(215,888),false),(new Vector2(198,903),false),(new Vector2(185,892),false),(new Vector2(198,844),false)}){
var covered=LeafCovers(spec.Item1);leafMaterial.Add(new JsonObject{{"source",new JsonArray(spec.Item1.X,spec.Item1.Y)},{"expectedWood",spec.Item2},{"covered",covered}});
if(covered!=spec.Item2)throw new Exception("Original leaf wood versus independent ground/stone material contract failed");}
var union=new HashSet<Vector2>();var regions=new JsonArray();foreach(var region in PresentationJson.Load<JsonObject>("res://southwest-gate-original-road-regions.json")["regions"]!.AsArray()){
var poly=region!["polygon"]!.AsArray().Select(p=>new Vector2(p![0]!.GetValue<float>(),p[1]!.GetValue<float>())).ToArray();int count=0,covered=0;var bad=new JsonArray();
for(int y=(int)poly.Min(p=>p.Y);y<=poly.Max(p=>p.Y);y++)for(int x=(int)poly.Min(p=>p.X);x<=poly.Max(p=>p.X);x++)if(InsideInclusive(new(x,y),poly)){count++;union.Add(new(x,y));if(GateCovers(new(x,y))||LeafCovers(new(x,y))){covered++;if(bad.Count<8)bad.Add(new JsonArray(x,y));}}
if(count!=region["integerLatticeSampleCount"]!.GetValue<int>())throw new Exception("Original continuous region enumeration mismatch");
regions.Add(new JsonObject{{"id",region["id"]!.DeepClone()},{"count",count},{"covered",covered},{"firstCovered",bad}});}
if(union.Count!=1405)throw new Exception("Original continuous road union mismatch");
var texture=GD.Load<Texture2D>(geometry["textureResourcePath"]!.GetValue<string>());var sampler=new Sprite2D{Texture=texture,Centered=false,Visible=false};AddChild(sampler);int ownershipSamples=0,ownershipMismatch=0;
var scale=geometry["scale"]!.GetValue<float>();var translation=new Vector2(geometry["translation"]![0]!.GetValue<float>(),geometry["translation"]![1]!.GetValue<float>());
for(float y=.41f;y<texture.GetHeight();y+=17)for(float x=.37f;x<texture.GetWidth();x+=17){var pixel=new Vector2(x,y);ownershipSamples++;if(sampler.IsPixelOpaque(pixel)!=GateCovers(pixel*scale+translation))ownershipMismatch++;}
var target=b.Pick(new(280,895));var approach=b.Pick(new(264,824));
var shown=s.DeepClone().AsObject();var u=s["units"]!.AsArray().OfType<JsonObject>().Single(v=>v["key"]!.GetValue<string>()=="campaign-torren").DeepClone().AsObject();bool side=OS.GetEnvironment("C07_GATE_FRONT")=="1"; int px=12, py=side?57:55; u["x"]=px;u["y"]=py;u["facing"]="east";shown["units"]=new JsonArray(u);if(l["terrain"]![py]!.GetValue<string>()[px]!='p')throw new Exception("Fixed diagnostic pose terrain changed");b.RenderState(shown);int id=u["id"]!.GetValue<int>();var actor=b.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}");var sprite=actor.GetNode<Sprite2D>("AuthoredSprite");var rows=new JsonArray();
var leafEntries=ground!.DepthEntries.Where(d=>d.Node.Name.ToString().StartsWith("SouthwestGateWoodLeaf")).ToArray();
foreach(var p in (side?new[]{new Vector2(199.25f,891.25f),new Vector2(200.25f,891.25f)}:new[]{new Vector2(198.25f,875.25f),new Vector2(199.25f,875.25f)})){
var global=b.ToGlobal(p);var local=sprite.ToLocal(global);var leaves=leafEntries.Where(d=>d.Visual!.Covers(global)).ToArray();var hit=b.Pick(p);
rows.Add(new JsonObject{{"source",new JsonArray(p.X,p.Y)},{"actorOpaque",sprite.IsPixelOpaque(local)},{"leafCovers",leaves.Length>0},{"actorPickable",hit==TacticalHit.Unit(new(px,py),id)},{"actorZ",actor.ZIndex},{"leafZ",leaves.Length>0?leaves[0].Node.ZIndex:0},{"pick",hit?.ToString()},{"allCoveringParts",new JsonArray(ground.DepthEntries.Where(d=>d.Visual is not null&&d.Visual.Covers(global)).Select(d=>(JsonNode)new JsonObject{{"name",d.Node.Name.ToString()},{"z",d.Node.ZIndex}}).ToArray())}});
}
var result=new JsonObject{{"scope",candidate?"Independent southwest left wood leaf with upward-right bottom plus stone v1; actual Torren static front/back, no commands/save/gameplay":"Original southeast gate missing left-arch-leg back coverage and front protective control, actual Torren, p poses, no commands/save/gameplay"},{"independentCandidate",candidate},{"leafNegative",leafNegative},{"mainBehindNegative",behind},{"appearanceType","campaign-torren / c01.swordsman"},{"cell",new JsonArray(px,py)},{"facing","east"},{"spriteTexture",sprite.Texture.ResourcePath},{"spriteScale",new JsonArray(sprite.Scale.X,sprite.Scale.Y)},{"spriteOffset",new JsonArray(sprite.Offset.X,sprite.Offset.Y)},{"flipH",sprite.FlipH},{"stateUnchanged",digest==EcmaJson.Stringify(s)},{"issues",b.RenderingIssueCount},{"witnesses",rows},{"roadRegions",regions},{"leafMaterial",leafMaterial},{"roadUnionCount",union.Count},{"ownershipSamples",ownershipSamples},{"ownershipMismatch",ownershipMismatch},{"oldMaterialNegative",oldMaterial},{"gateTarget",target?.ToString()},{"approach",approach?.ToString()}};System.IO.File.WriteAllText(ProjectSettings.GlobalizePath($"res://southwest-gate-leaf-final-{leafNegative}-{(side?"front":"back")}-native-result.json"),result.ToJsonString());
if(rows.Any(v=>!v!["actorOpaque"]!.GetValue<bool>()||!v["leafCovers"]!.GetValue<bool>()||v["actorPickable"]!.GetValue<bool>()!=side))throw new Exception("Independent wooden leaf actual front/back depth contract failed");
if(regions.Any(v=>v!["covered"]!.GetValue<int>()>0)||target!=TacticalHit.Object(new(15,51))||approach!=TacticalHit.Ground(new(16,51))||ownershipMismatch!=0||digest!=EcmaJson.Stringify(s)||b.RenderingIssueCount!=0)throw new Exception("Gate leaf integration opening or ownership contract failed");
GD.Print("SOUTHWEST_GATE_LEAF_STATIC_COMPLETE");GetTree().Quit();
}catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(1);}}
}
