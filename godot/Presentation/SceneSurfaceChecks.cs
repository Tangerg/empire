#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

public partial class SceneSurfaceChecks : Node
{
    private int _assertions;
    private void Require(bool value, string message)
    {
        _assertions++;
        if (!value)
            throw new InvalidOperationException(message);
    }
    public override void _Ready()
    {
        try
        {
            Check();
            GD.Print($"SCENE_SURFACE_CHECKS_OK {_assertions} assertions");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void Check()
    {
        using var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        image.SetPixel(3, 1, Colors.Transparent);
        using var texture = ImageTexture.CreateFromImage(image);
        SceneSurfaceMember Member(string id, string support, string coverage, double[][] uv) => new()
        {
            Id = id,
            SupportId = support,
            Coverage = coverage,
            Tint = "#ffffff",
            PolygonPixels = [[0, 0], [4, 0], [0, 4]],
            UvPixels = uv
        };
        SceneSurfaceDocument Document(params SceneSurfaceMember[] members) => new()
        {
            Schema = 1,
            Supports = [new() { Id = "foot", FrontPixels = [2, 4], Kind = "structural" }],
            Members = members
        };
        void Reject(SceneSurfaceDocument document, string expected)
        {
            try
            {
                _ = CompiledSceneSurface.Compile(document, texture, "fixture");
            }
            catch (InvalidOperationException error)
            {
                Require(error.Message.Contains(expected, StringComparison.Ordinal), "Wrong failure stage: " + error.Message);
                return;
            }
            throw new InvalidOperationException("Expected surface rejection: " + expected);
        }
        // Endpoints are opaque. Only the interior of this degenerate UV line crosses the transparent texel.
        Reject(Document(Member("line", "foot", "opaque", [[1, 1], [5, 1], [1, 1]])), "Opaque surface material includes transparency");
        Reject(Document(Member("narrow", "foot", "opaque", [[1, 1], [5, 1.1], [1, 1.1]])), "Opaque surface material includes transparency");
        var good = Member("good", "foot", "opaque", [[1, 3], [5, 3], [1, 3]]);
        var compiled = CompiledSceneSurface.Compile(Document(good), texture, "fixture");
        Require(compiled is not null, "Opaque degenerate UV line without a hole must remain valid.");
        Reject(Document(good, Member("copy", "foot", "opaque", [[1, 4], [5, 4], [1, 4]])), "Duplicate surface primitive");
        Reject(new SceneSurfaceDocument
        {
            Schema = 1,
            Supports = [new() { Id = "foot", FrontPixels = [2, 4], Kind = "structural" },
                new() { Id = "other", FrontPixels = [2, 2], Kind = "structural" }],
            Members = [good, Member("other", "other", "opaque", [[1, 4], [5, 4], [1, 4]])]
        }, "Duplicate surface primitive");
        Reject(new SceneSurfaceDocument
        {
            Schema = 1,
            Supports = [new() { Id = "foot", FrontPixels = [2, 40], Kind = "physical" }],
            Members = [good]
        }, "Physical surface contact lies outside its owned geometry");
        var native = CompiledSceneSurface.Compile(Document(Member("native", "foot", "native-alpha", [[1, 1], [5, 1], [1, 1]])), texture, "fixture");
        var sampler = new Sprite2D { Texture = texture, Centered = false, Visible = false };
        AddChild(sampler);
        foreach (var (node, visual) in native.Instantiate(this, "instance", 2, Vector2.Zero, true, null, sampler))
        {
            node.Position = new Vector2(20, 20);
            Require(!visual.Covers(node.ToGlobal(new Vector2(2.1f, .1f))), "Native-alpha mapped line must expose its real hole.");
            Require(visual.Covers(node.ToGlobal(new Vector2(.1f, .1f))), "Native-alpha mapped line must retain opaque endpoints.");
            Require(!visual.ContainsPointer(node.ToGlobal(new Vector2(2.1f, .1f))), "Pointer cannot fill native-alpha hole.");
            Require(visual.FrontOffset(node) == new Vector2(4, 8), "Support scales exactly once.");
            Require(visual.GlobalBounds == new Rect2(20, 20, 8, 8), "Bounds use the same transformed geometry.");
            node.Free();
        }
        var layered = CompiledSceneSurface.Compile(Document(good, new SceneSurfaceMember
        {
            Id = "overlap",
            SupportId = "foot",
            Coverage = "opaque",
            Tint = "#6633aa",
            PolygonPixels = [[1, 1], [5, 1], [1, 5]],
            UvPixels = [[1, 3], [5, 3], [1, 3]]
        }), texture, "batched-fixture");
        foreach (var (node, _) in layered.Instantiate(this, "batched", 1, Vector2.Zero, false, null, sampler))
        {
            Require(node.GetChildCount() == 1, "One shared support must not create one draw node per triangle.");
            var draw = node.GetChild<Polygon2D>(0);
            Require(draw.Polygons.Count == 2 && draw.Polygon.Length == 6 && draw.UV.Length == 6,
                "Batching must retain independent triangle vertices and UV seams.");
            Require(draw.VertexColors.Length == 6 && draw.VertexColors[0] == Colors.White
                && draw.VertexColors[3] == Color.FromHtml("#6633aa") && draw.Color == Colors.White,
                "Batch colors must preserve each primitive tint without multiplying it twice.");
            node.Free();
        }
        sampler.Free();
    }
}
