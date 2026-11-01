using Godot;
using System.Text.Json.Nodes;
using Empire.Presentation;
using Empire.Core.Geometry;

// Static fixture for one unchanged RGBA sprite with separately owned architectural surfaces.
public partial class SectionedSpriteGround : BoardGroundView
{
    readonly BoardGroundView ground;
    readonly List<TerrainDepthEntry> entries = new();
    readonly List<(Node2D Node, Vector2 Contact, StandingVisual Visual)> slices = new();
    readonly IReadOnlyList<GroundSite> sites;
    readonly Coord selection;
    public override IReadOnlyList<string> Diagnostics => ground.Diagnostics;
    public override IReadOnlyList<GroundSite> Sites => sites;
    public override IReadOnlyList<TerrainDepthEntry> DepthEntries => entries;
    public SectionedSpriteGround(BoardGroundView ground, JsonObject geometry)
    {
        this.ground = ground; AddChild(ground);
        Vector2 Pair(JsonNode p) => new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());
        var texture = GD.Load<Texture2D>(geometry["textureResourcePath"]!.GetValue<string>());
        var sampler = new Sprite2D { Texture = texture, Centered = false, Visible = false }; AddChild(sampler);
        float scale = geometry["scale"]!.GetValue<float>(); var translation = Pair(geometry["translation"]!);
        var target = geometry["selectionCell"]!; selection = new(target[0]!.GetValue<int>(),target[1]!.GetValue<int>());
        sites = ground.Sites.Append(new GroundSite("southeast-gate",selection,true)).ToArray();
        var pointer = geometry["pointerSourcePolygon"]!.AsArray().Select(p=>Pair(p!)).ToArray();
        foreach(var section in geometry["sections"]!.AsArray().OfType<JsonObject>())
        {
            var polygon=section["polygonPixels"]!.AsArray().Select(p=>Pair(p!)).ToArray();
            var support=section["supportLinePixels"]!.AsArray().Select(p=>Pair(p!)).ToArray();
            float Foot(float x)
            {
                for(int i=0;i<support.Length-1;i++)if(x<=support[i+1].X)
                    return Mathf.Lerp(support[i].Y,support[i+1].Y,(x-support[i].X)/(support[i+1].X-support[i].X));
                throw new Exception("Section support does not span its owned pixels");
            }
            int min=(int)MathF.Floor(polygon.Min(p=>p.X)), max=(int)MathF.Ceiling(polygon.Max(p=>p.X));
            for(int x=min;x<max;x+=8)
            {
                int right=Math.Min(x+8,max); var contact=new Vector2((x+right)/2f,Foot((x+right)/2f))*scale+translation;
                var node=new Node2D{Name=$"SEGate_{section["id"]!.GetValue<string>()}_{x}"};var shapes=new List<Vector2[]>();
                foreach(var clip in Geometry2D.IntersectPolygons(polygon,new[]{new Vector2(x,-1),new Vector2(right,-1),new Vector2(right,texture.GetHeight()+1),new Vector2(x,texture.GetHeight()+1)}))
                {
                    if(clip.Length<3)continue;
                    var local=clip.Select(p=>p*scale+translation-contact).ToArray();shapes.Add(local);
                    node.AddChild(new Polygon2D{Polygon=local,UV=clip,Texture=texture,TextureFilter=CanvasItem.TextureFilterEnum.Linear});
                }
                if(shapes.Count==0){node.Free();continue;}
                AddChild(node);slices.Add((node,contact,new AlphaSectionVisual(node,shapes,sampler,scale,translation,contact,pointer)));
            }
        }
    }
    public override void ConfigureAuthoredGround(Node2D n)=>ground.ConfigureAuthoredGround(n);
    public override Node2D DetachAuthoredGround()=>ground.DetachAuthoredGround();
    public override void Render(BoardProjection projection,string[] tiles,int[] elevation,Vector2I size)
    {
        ground.Render(projection,tiles,elevation,size);entries.Clear();entries.AddRange(ground.DepthEntries);
        foreach(var (node,contact,visual) in slices)
        {
            var logical=contact/16*BoardProjection.TileSize;node.Position=projection.Project(logical);
            entries.Add(new(node,logical,2,visual,selection));
        }
    }
    sealed class AlphaSectionVisual(Node2D node,List<Vector2[]> polygons,Sprite2D sampler,float scale,Vector2 translation,Vector2 contact,Vector2[] pointer):StandingVisual(node)
    {
        Vector2 Source(Vector2 global)=>Node.ToLocal(global)+contact;
        public override bool Covers(Vector2 global)
        {
            var local=Node.ToLocal(global);
            return polygons.Any(p=>Geometry2D.IsPointInPolygon(local,p))&&sampler.IsPixelOpaque((Source(global)-translation)/scale);
        }
        public override bool ContainsPointer(Vector2 global)=>Covers(global)&&Geometry2D.IsPointInPolygon(Source(global),pointer);
        public override bool IntersectsPointer(Vector2[] globalPolygon)=>Geometry2D.IntersectPolygons(globalPolygon.Select(Source).ToArray(),pointer).Count>0;
        public override Rect2 GlobalBounds{get{var p=polygons.SelectMany(p=>p).Select(Node.ToGlobal).ToArray();var r=new Rect2(p[0],Vector2.Zero);foreach(var v in p)r=r.Expand(v);return r;}}
        protected override Vector2 ParentFrontOffset=>Vector2.Zero;
    }
}
