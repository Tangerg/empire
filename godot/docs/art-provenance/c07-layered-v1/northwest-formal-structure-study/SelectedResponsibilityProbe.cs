using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Engine;
using Empire.Core.Geometry;
using Empire.Core.Determinism;
public partial class SelectedResponsibilityProbe:Node
{
 public override void _Ready(){try{
 var engine=new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));var level=PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(x=>x["id"]!.GetValue<string>()=="c01-07");var state=engine.CreateState(level);var digest=EcmaJson.Stringify(state);var map=state["map"]!.AsObject();var assets=AssetCatalog.Load();
 var scene=AuthoredScene.Load("res://data/scenes/c01-07-selected-assembly.json");var rendering=BoardSceneRendering.Load(scene,level,map);var board=new BoardView{AnimationsEnabled=false};AddChild(board);board.Configure(new(assets,EnvironmentCatalog.Load(),engine.Grids.Get("square4"),engine.Catalog,rendering.Plane),level,scene);BoardGroundView? ground=null;board.ConfigureGround((rules,s)=>ground=rendering.Ground(rules,s));board.ConfigureDynamicState();board.RenderState(state);
 var rows=new JsonArray();
 foreach(var site in PresentationJson.Load<JsonObject>("res://luojia-geometry-source.json")["sites"]!.AsArray()){
  int x=site!["cell"]![0]!.GetValue<int>(),y=site["cell"]![1]!.GetValue<int>();int index=y*map["width"]!.GetValue<int>()+x;var terrain=map["tiles"]![index]!.GetValue<string>();bool owns=ground!.OwnsMapStructureBody(new Coord(x,y));var fallback=board.GetNodeOrNull<Node2D>($"CanonicalProjection/MapStructure_{index}");
  rows.Add(new JsonObject{{"id",site["id"]!.DeepClone()},{"cell",site["cell"]!.DeepClone()},{"terrainId",terrain},{"selectedOwnsBody",owns},{"fallbackBindingExists",assets.HasMapStructureBinding(terrain)},{"fallbackNodeExists",fallback is not null},{"fallbackVisible",fallback?.Visible},{"bodyStatus",owns?"selected local candidate":fallback is not null?"generic fallback, not original restoration":"NO selected or fallback body"},{"siteCentrePick",board.Pick(board.Projection.CellContact(new Coord(x,y)))?.ToString()}});
 }
 var report=new JsonObject{{"scope","Initial-state query/render only; no commands/AI/save or completed foundation claims"},{"stateUnchanged",digest==EcmaJson.Stringify(state)},{"issues",board.RenderingIssueCount},{"sites",rows}};
 File.WriteAllText(ProjectSettings.GlobalizePath("res://selected-responsibility-native-current.json"),report.ToJsonString());
 if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Responsibility inventory changed state/render issues");
 GD.Print("SELECTED_RESPONSIBILITY_NATIVE_COMPLETE "+report.ToJsonString());GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
