using Godot;
using Empire.Presentation;
using System.Text.Json.Nodes;

// Read-only renderer fixture. Canonical game state and production scene remain untouched.
public partial class TexturedPropGround : BoardGroundView
{
    readonly BoardGroundView ground;
    readonly List<TerrainDepthEntry> entries = new();
    readonly List<(Node2D Node, Vector2 SourceContact, StandingVisual Visual)> slices = new();
    public override IReadOnlyList<string> Diagnostics => ground.Diagnostics;
    public override IReadOnlyList<GroundSite> Sites => ground.Sites;
    public override IReadOnlyList<TerrainDepthEntry> DepthEntries => entries;
    public TexturedPropGround(BoardGroundView ground, JsonObject geometry)
    {
        this.ground=ground; AddChild(ground);
        var texture=GD.Load<Texture2D>(geometry["textureResourcePath"]!.GetValue<string>());
        var prefix=geometry["namePrefix"]!.GetValue<string>();
        Vector2 Pair(JsonNode p)=>new(p[0]!.GetValue<float>(),p[1]!.GetValue<float>());
        using var texturePixels=texture.GetImage();
        var members=geometry["members"]!.AsArray().OfType<JsonObject>().ToArray();
        var support=geometry["supportLine"]!.AsArray().Select(p=>Pair(p!)).ToArray();
        float Front(float x){for(int i=0;i<support.Length-1;i++){var a=support[i];var b=support[i+1];if(x<=b.X)return a.Y+(b.Y-a.Y)*(x-a.X)/(b.X-a.X);}throw new Exception("Outside support extent");}
        var vertices=members.SelectMany(m=>m["polygon"]!.AsArray().Select(p=>Pair(p!))).ToArray();
        int minX=(int)MathF.Floor(vertices.Min(p=>p.X)),maxX=(int)MathF.Ceiling(vertices.Max(p=>p.X));
        float minY=vertices.Min(p=>p.Y)-1,maxY=vertices.Max(p=>p.Y)+1;
        for(int x=minX;x<maxX;x++)
        {
            var node=new Node2D{Name=$"{prefix}{x}"};var shapes=new List<Vector2[]>();var contact=new Vector2(x+.5f,Front(x+.5f));
            foreach(var member in members)
            {
                var polygon=member["polygon"]!.AsArray().Select(p=>Pair(p!)).ToArray();
                var uv=member["uv"]!.AsArray().Select(p=>Pair(p!)).ToArray();
                if(polygon.Length!=uv.Length)throw new Exception("UV geometry mismatch");
                // Triangulate once conceptually; clipped vertices retain the same affine texture coordinates.
                var triangles=Geometry2D.TriangulatePolygon(polygon);
                for(int ti=0;ti<triangles.Length;ti+=3)
                {
                    int ia=triangles[ti],ib=triangles[ti+1],ic=triangles[ti+2];var a=polygon[ia];var b=polygon[ib];var c=polygon[ic];
                    for(int u=0;u<=24;u++)for(int v=0;v<=24-u;v++){var sample=uv[ia]+(uv[ib]-uv[ia])*(u/24f)+(uv[ic]-uv[ia])*(v/24f);if(texturePixels.GetPixel((int)sample.X,(int)sample.Y).A<.96f)throw new Exception("Atomic material mapping includes a transparent texture hole");}
                    foreach(var clip in Geometry2D.IntersectPolygons(new[]{a,b,c},new[]{new Vector2(x,minY),new Vector2(x+1,minY),new Vector2(x+1,maxY),new Vector2(x,maxY)}))
                    {
                        if(clip.Length<3)continue;
                        Vector2 ToUv(Vector2 p){var v=b-a;var w=c-a;var q=p-a;float den=v.Cross(w);float bv=q.Cross(w)/den;float cv=v.Cross(q)/den;return uv[ia]+bv*(uv[ib]-uv[ia])+cv*(uv[ic]-uv[ia]);}
                        var local=clip.Select(p=>p-contact).ToArray();shapes.Add(local);
                        node.AddChild(new Polygon2D{Polygon=local,UV=clip.Select(ToUv).ToArray(),Texture=texture,Color=new Color(member["tint"]!.GetValue<string>()),TextureFilter=CanvasItem.TextureFilterEnum.Linear});
                    }
                }
            }
            if(shapes.Count==0){node.Free();continue;}AddChild(node);slices.Add((node,contact,new TexturedPolygonVisual(node,shapes)));
        }
    }
    public override void ConfigureAuthoredGround(Node2D n)=>ground.ConfigureAuthoredGround(n);
    public override Node2D DetachAuthoredGround()=>ground.DetachAuthoredGround();
    public override void Render(BoardProjection p,string[] t,int[] e,Vector2I size){ground.Render(p,t,e,size);entries.Clear();entries.AddRange(ground.DepthEntries);foreach(var(n,s,v) in slices){var logical=s/16*BoardProjection.TileSize;n.Position=p.Project(logical);entries.Add(new(n,logical,2,v));}}
    sealed class TexturedPolygonVisual(Node2D node,List<Vector2[]> polygons):StandingVisual(node)
    {
        public override bool Covers(Vector2 global)=>polygons.Any(p=>Geometry2D.IsPointInPolygon(Node.ToLocal(global),p));
        public override bool ContainsPointer(Vector2 global)=>false;
        public override bool IntersectsPointer(Vector2[] global)=>false;
        public override Rect2 GlobalBounds{get{var points=polygons.SelectMany(p=>p).Select(Node.ToGlobal).ToArray();var r=new Rect2(points[0],Vector2.Zero);foreach(var p in points)r=r.Expand(p);return r;}}
        protected override Vector2 ParentFrontOffset=>Vector2.Zero;
    }
}
