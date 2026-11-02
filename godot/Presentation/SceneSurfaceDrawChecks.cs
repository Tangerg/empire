#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Empire.Presentation;

/// <summary>Actual GPU alpha/order check; no battle state, commands or campaign fixtures.</summary>
public partial class SceneSurfaceDrawChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var output = OS.GetEnvironment("SURFACE_DRAW_CHECK_OUTPUT");
            if (output.Length == 0 || !Directory.Exists(output))
                throw new InvalidOperationException("Explicit existing GPU evidence directory required.");
            using var pixels = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            pixels.Fill(new Color(1, 1, 1, .5f));
            using var texture = ImageTexture.CreateFromImage(pixels);
            var document = new SceneSurfaceDocument
            {
                Schema = 1,
                Supports = [new() { Id = "a", FrontPixels = [2, 5], Kind = "structural" },
                    new() { Id = "b", FrontPixels = [3, 6], Kind = "structural" },
                    new() { Id = "c", FrontPixels = [4, 7], Kind = "structural" }],
                Members = Enumerable.Range(0, 3).Select(index => new SceneSurfaceMember
                {
                    Id = "part" + index, SupportId = new[] { "a", "b", "c" }[index],
                    Coverage = "native-alpha", Tint = new[] { "#ff0000", "#0000ff", "#00ff00" }[index],
                    PolygonPixels = [[index, index % 2], [index + 4, index % 2], [index, index % 2 + 4]],
                    UvPixels = [[1, 1], [5, 1], [1, 5]]
                }).ToArray()
            };
            var viewport = new SubViewport { Size = new(256, 192), TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(viewport);
            async System.Threading.Tasks.Task<byte[]> Capture(string name)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = viewport.GetTexture().GetImage();
                image.SavePng(Path.Combine(output, name + ".png"));
                return image.GetData();
            }
            (Node2D Owner, CompiledSceneSurface.Instance Instance) Create(bool reverse, bool barrier)
            {
                var owner = new Node2D { Position = new(32, 32), Scale = Vector2.One * 24 };
                viewport.AddChild(owner);
                var sampler = new Sprite2D { Texture = texture, Centered = false, Visible = false };
                owner.AddChild(sampler);
                var source = new SceneSurfaceDocument { Schema = 1,
                    Supports = reverse ? document.Supports.Reverse().ToArray() : document.Supports,
                    Members = document.Members };
                var instance = CompiledSceneSurface.Compile(source, texture, "gpu-order")
                    .Instantiate(owner, "piece", 1, Vector2.Zero, true, null, sampler);
                foreach (var piece in instance.Pieces)
                {
                    piece.Node.ZAsRelative = false;
                    piece.Node.ZIndex = 10;
                }
                if (barrier)
                {
                    var intervening = new Node2D { ZAsRelative = false, ZIndex = 10 };
                    intervening.AddChild(new Polygon2D { Polygon = [new(1, 0), new(5, 0), new(5, 4), new(1, 4)],
                        Color = new Color(1, 1, 0, .5f) });
                    owner.AddChild(intervening);
                    owner.MoveChild(intervening, instance.Pieces[1].Node.GetIndex());
                }
                return (owner, instance);
            }
            var normal = Create(false, false);
            var before = await Capture("surface-order-before");
            normal.Instance.ApplyDrawOrder();
            var after = await Capture("surface-order-after");
            if (!before.SequenceEqual(after))
                throw new InvalidOperationException("Same-depth translucent draw grouping must retain exact GPU RGBA.");
            normal.Instance.ApplyDrawOrder();
            if (!after.SequenceEqual(await Capture("surface-order-repeat")))
                throw new InvalidOperationException("Repeated grouping must not accumulate alpha or geometry.");
            normal.Owner.Free();
            var reversed = Create(true, false);
            reversed.Instance.ApplyDrawOrder();
            var wrong = await Capture("surface-order-reversed-negative");
            if (before.SequenceEqual(wrong))
                throw new InvalidOperationException("Reverse-order negative must expose real overlapping-alpha order.");
            reversed.Owner.Free();
            var separated = Create(false, true);
            var separateBefore = await Capture("surface-barrier-before");
            separated.Instance.ApplyDrawOrder();
            if (!separateBefore.SequenceEqual(await Capture("surface-barrier-after")))
                throw new InvalidOperationException("Same-Z intervening geometry must preserve its actual tree draw order.");
            separated.Owner.Free();
            File.WriteAllText(Path.Combine(output, "surface-draw-result.json"), JsonSerializer.Serialize(new
            {
                scope = "Actual GPU static synthetic half-alpha material; no battle commands/AI/turns/saves",
                exactRgba = true, repeatedExact = true, realSiblingBarrierExact = true,
                reversedNegativeDifferentBytes = before.Zip(wrong).Count(pair => pair.First != pair.Second)
            }));
            GD.Print("SCENE_SURFACE_DRAW_CHECKS_OK");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
