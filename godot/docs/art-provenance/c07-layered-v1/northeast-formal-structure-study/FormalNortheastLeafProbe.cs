using Godot;
using Empire.Presentation;
using Empire.Presentation.Tactical;
using Empire.Core.Engine;
using Empire.Core.Determinism;
using System.Text.Json.Nodes;

// Static rendering witnesses only. No engine commands, AI, turns or save writes.
public partial class FormalNortheastLeafProbe : Node
{
    public override void _Ready()
    {
        try
        {
            var engine = new BattleEngine(PresentationJson.Load<JsonArray>("res://data/content-packs.json"));
            var level = PresentationJson.Load<JsonArray>("res://data/grayflag-campaign-levels.json").OfType<JsonObject>().Single(v => v["id"]!.GetValue<string>() == "c01-07");
            var state = engine.CreateState(level);
            var digest = EcmaJson.Stringify(state);
            var negative = OS.GetEnvironment("NE_LEAF_NEGATIVE");
            if (negative is not ("" or "late-depth" or "early-depth" or "road-invasion" or "flat-foot")) throw new Exception("Unknown leaf negative.");
            var selected = OS.GetEnvironment("NE_SELECTED") == "1";
            var scene = AuthoredScene.Load(selected ? "res://data/scenes/c01-07-selected-assembly.json" : negative.Length == 0 ? "res://northeast-leaf-scene.json" : $"res://northeast-leaf-{negative}-scene.json");
            var rendering = BoardSceneRendering.Load(scene, level, state["map"]!.AsObject());
            var board = new BoardView { AnimationsEnabled = false };
            AddChild(board);
            board.Configure(new(AssetCatalog.Load(), EnvironmentCatalog.Load(), engine.Grids.Get("square4"), engine.Catalog, rendering.Plane), level, scene);
            BoardGroundView? ground = null;
            board.ConfigureGround((rules, authored) => ground = rendering.Ground(rules, authored));
            board.ConfigureDynamicState();
            var shown = state.DeepClone().AsObject(); shown["units"] = new JsonArray(); board.RenderState(shown);
            var leafSource=PresentationJson.Load<JsonObject>("res://northeast-leaf-source.json");
            var feet=leafSource["supportLine"]!.AsArray().Select(v=>Point(v!)).ToArray();
            var sourceVertices=leafSource["members"]!.AsArray().SelectMany(v=>v!["polygon"]!.AsArray().Select(p=>Point(p!))).ToArray();
            var origin=new Vector2(MathF.Floor(sourceVertices.Min(v=>v.X)),MathF.Floor(sourceVertices.Min(v=>v.Y)));
            var surface=PresentationJson.Load<JsonObject>(negative.Length==0?"res://northeast-leaf-surface.json":$"res://northeast-leaf-{negative}-surface.json");
            // Inspect the ordinary export separately from deliberately wrong-depth Pick negatives.
            if (negative is "" or "flat-foot")
                foreach(var support in surface["supports"]!.AsArray())
                {
                    var front=Point(support!["frontPixels"]!)+origin;
                    var segment=Enumerable.Range(0,feet.Length-1).First(i=>front.X>=feet[i].X&&front.X<=feet[i+1].X);
                    var a=feet[segment];var b=feet[segment+1];var expected=a.Y+(b.Y-a.Y)*(front.X-a.X)/(b.X-a.X);
                    if(MathF.Abs(front.Y-expected)>0.00002f)throw new Exception("Exported leaf support deviates from its observed sloping foot.");
                }
            var leaf = ground!.DepthEntries.Where(v => v.Node.Name.ToString().Contains(selected ? "northeast-gate-leaf" : "northeast-leaf-study")).ToArray();
            if (leaf.Length == 0) throw new Exception("Missing independent leaf.");
            bool Covers(Vector2 point) => leaf.Any(v => v.Visual!.Covers(board.ToGlobal(point)));
            var union = new HashSet<Vector2>();
            foreach (var region in PresentationJson.Load<JsonObject>("res://northeast-gate-original-road-regions.json")["regions"]!.AsArray())
            {
                var polygon = region!["polygon"]!.AsArray().Select(v => Point(v!)).ToArray();
                for (int y=(int)polygon.Min(v=>v.Y); y<=polygon.Max(v=>v.Y); y++)
                for (int x=(int)polygon.Min(v=>v.X); x<=polygon.Max(v=>v.X); x++)
                {
                    var point = new Vector2(x,y); if (!InsideInclusive(point,polygon)) continue;
                    union.Add(point); if (Covers(point)) throw new Exception($"Leaf enters original road: {point}");
                }
            }
            if (union.Count != 1103) throw new Exception("Original road set changed.");
            foreach (var p in new[]{new Vector2(1362,116),new Vector2(1366,126),new Vector2(1371,129)})
                if (!Covers(p)) throw new Exception($"Missing observed leaf material: {p}");
            foreach (var p in new[]{new Vector2(1358,141),new Vector2(1366,137),new Vector2(1373,134)})
                if (Covers(p)) throw new Exception($"Leaf invades ground below its own foot: {p}");
            if(board.Pick(new(1417,130)) != TacticalHit.Object(new(83,11)) || board.Pick(new(1320,184)) != TacticalHit.Ground(new(82,11)))
                throw new Exception("Independent leaf stole canonical gate or approach target.");
            var entryCount=ground.DepthEntries.Count;
            board.RenderState(shown);board.RenderState(shown);
            if(ground.DepthEntries.Count!=entryCount)throw new Exception("Repeated leaf rendering accumulates entries.");
            var rows = new JsonArray();
            foreach (var cell in new[]{new Vector2I(85,7),new Vector2I(85,9)})
            {
                if (level["terrain"]![cell.Y]!.GetValue<string>()[cell.X]!='p') throw new Exception("Witness pose not legal road.");
                var unit = state["units"]!.AsArray().OfType<JsonObject>().Single(v=>v["key"]!.GetValue<string>()=="raider-1").DeepClone().AsObject();
                unit["x"]=cell.X; unit["y"]=cell.Y; unit["facing"]="east"; shown["units"]=new JsonArray(unit); board.RenderState(shown);
                int id=unit["id"]!.GetValue<int>(); var point=new Vector2(1365,115);
                var sprite=board.GetNode<CanvasGroup>($"CanonicalProjection/Unit_{id}").GetNode<Sprite2D>("AuthoredSprite");
                bool opaque=sprite.IsPixelOpaque(sprite.ToLocal(board.ToGlobal(point)));
                bool picked=board.Pick(point)==TacticalHit.Unit(new(cell.X,cell.Y),id);
                foreach(var entry in leaf) entry.Node.Visible=false;
                bool withoutLeaf=board.Pick(point)==TacticalHit.Unit(new(cell.X,cell.Y),id);
                foreach(var entry in leaf) entry.Node.Visible=true;
                if (!opaque || !withoutLeaf || picked != (cell.Y == 9)) throw new Exception("Independent leaf front/back Pick contract failed.");
                rows.Add(new JsonObject{["cell"]=new JsonArray(cell.X,cell.Y),["point"]=new JsonArray(point.X,point.Y),["actorOpaque"]=opaque,["withLeafActorPick"]=picked,["withoutLeafActorPick"]=withoutLeaf});
            }
            if(digest!=EcmaJson.Stringify(state)||board.RenderingIssueCount!=0)throw new Exception("Static state/renderer changed.");
            var result=new JsonObject{["scope"]="Independent NE left leaf; static detached legal poses, no gameplay",["roadSamples"]=union.Count,["leafDepthEntries"]=leaf.Length,["witnesses"]=rows,["stateUnchanged"]=true};
            File.WriteAllText(ProjectSettings.GlobalizePath(selected ? "res://selected-leaf-witnesses.json" : "res://leaf-initial-witnesses.json"),result.ToJsonString(new(){WriteIndented=true}));
            GD.Print("NE_LEAF_INITIAL_STATIC_MEASUREMENT_COMPLETE");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
    private static Vector2 Point(JsonNode p)=>new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());
    private static bool InsideInclusive(Vector2 p,Vector2[] polygon)
    {
        for(int i=0;i<polygon.Length;i++) {var a=polygon[i];var e=polygon[(i+1)%polygon.Length]-a;var d=p-a;if(MathF.Abs(e.Cross(d))<.001f&&d.Dot(e)>=0&&d.Dot(e)<=e.LengthSquared())return true;}
        return Geometry2D.IsPointInPolygon(p,polygon);
    }
}
