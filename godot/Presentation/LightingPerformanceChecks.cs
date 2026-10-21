#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Empire.Core.Determinism;
using Godot;

namespace Empire.Presentation;

/// <summary>Deterministic raster parity and measured cold/warm resource behavior, not a frame-rate gate.</summary>
public partial class LightingPerformanceChecks : Node
{
    private int _checks;
    public override async void _Ready()
    {
        try
        {
            GD.Print($"SIMD hardware={System.Numerics.Vector.IsHardwareAccelerated}, lanes={System.Numerics.Vector<float>.Count}");
            BlurParity();
            RasterParity();
            await Board();
            Frames();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"Lighting performance checks passed: {_checks} assertions. Timings are diagnostic, not desktop acceptance.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void BlurParity()
    {
        var random = new Random(6301);
        foreach (var size in new[] { new Vector2I(1, 1), new(2, 3), new(7, 5), new(9, 7), new(17, 9), new(33, 17), new(31, 48), new(128, 96), new(129, 97) })
            foreach (var sigma in new[] { 0f, .01f, .55f, .65f, 1f, 1.1f, 1.2f, 2f, 7f, 14f })
            {
                var input = Enumerable.Range(0, size.X * size.Y).Select(_ => (float)random.NextDouble()).ToArray();
                var before = (float[])input.Clone();
                var expected = ReferenceBlur(input, size.X, size.Y, sigma);
                var actual = ShadowRaster.Blur(input, size.X, size.Y, sigma);
                Require(actual.Select(BitConverter.SingleToInt32Bits).SequenceEqual(expected.Select(BitConverter.SingleToInt32Bits)), $"Gaussian float bits changed: {size}, sigma {sigma}.");
                Require(input.SequenceEqual(before), "Gaussian changed its input.");
            }
    }

    // Literal pre-optimization convolution oracle: retains the original tap order and edge omission.
    private static float[] ReferenceBlur(float[] source, int width, int height, float sigma)
    {
        if (sigma == 0)
            return (float[])source.Clone();
        var radius = checked((int)Math.Ceiling(sigma * 4));
        var kernel = new float[radius * 2 + 1];
        var total = 0f;
        for (var offset = -radius; offset <= radius; offset++)
            total += kernel[offset + radius] = MathF.Exp(-offset * offset / (2 * sigma * sigma));
        for (var index = 0; index < kernel.Length; index++)
            kernel[index] /= total;
        var horizontal = new float[source.Length];
        var output = new float[source.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                for (var offset = -radius; offset <= radius; offset++)
                    if (x + offset >= 0 && x + offset < width)
                        horizontal[y * width + x] += source[y * width + x + offset] * kernel[offset + radius];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                for (var offset = -radius; offset <= radius; offset++)
                    if (y + offset >= 0 && y + offset < height)
                        output[y * width + x] += horizontal[(y + offset) * width + x] * kernel[offset + radius];
        return output;
    }

    private void RasterParity()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var size in new[] { new Vector2I(1, 1), new(7, 3), new(32, 48) })
        {
            using var image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
            for (var y = 0; y < size.Y; y++)
                for (var x = 0; x < size.X; x++)
                    image.SetPixel(x, y, new Color((x * 13 % 31) / 30f, (y * 7 % 19) / 18f, ((x + y) % 7) / 6f, (x + y) % 3 == 0 ? 0 : ((x * 3 + y * 7) % 17) / 16f));
            var before = image.GetData();
            foreach (var density in new[] { .5f, 1f, 2f })
                foreach (var kind in new[] { ShadowKind.Topic, ShadowKind.Environment, ShadowKind.Standing, ShadowKind.Foreground, ShadowKind.FrameEdge, ShadowKind.Figure, ShadowKind.BattleProp, ShadowKind.OwnerBanner, ShadowKind.Route, ShadowKind.BoardRelief })
                {
                    var raster = ShadowRaster.Filter(image, SpriteShadows.Drops(kind), density);
                    using (raster.Image)
                        hash.AppendData(raster.Image.GetData());
                    Require(before.SequenceEqual(image.GetData()), "Raster changed immutable source art.");
                }
            var fractional = ShadowRaster.Filter(image, new[] {
                new SpriteShadows.Drop(new(-1.25f, .75f), .65f, new Color(.2f, .7f, .4f, .45f)),
                new SpriteShadows.Drop(new(.25f, -1.5f), 0, new Color(.9f, .1f, .8f, .7f)),
            }, 1.5f);
            using (fractional.Image)
                hash.AppendData(fractional.Image.GetData());
            using var crop = image.GetRegion(new Rect2I(0, 0, Math.Max(1, size.X / 2), Math.Max(1, size.Y / 2)));
            var cropped = ShadowRaster.Filter(crop, SpriteShadows.Drops(ShadowKind.Figure), 2);
            using (cropped.Image)
                hash.AppendData(cropped.Image.GetData());
        }
        foreach (var density in new[] { .5f, 1f, 2f })
        {
            var ellipse = ShadowRaster.Ellipse(new(12.5f, 4.2f), new Color(.2f, .4f, .8f, .6f), .62f, .55f, density);
            using (ellipse.Image)
                hash.AppendData(ellipse.Image.GetData());
        }
        var digest = Convert.ToHexString(hash.GetHashAndReset());
        Require(digest == "2C438BB68D4BBEACF3C7C02972C5CD3CADEB20830FEE0E38F5826AC37C8DF566", "Pre-optimization RGBA raster bytes changed: " + digest);
        GD.Print($"Raster corpus SHA256: {digest}");
    }

    private void Frames()
    {
        var assets = AssetCatalog.Load();
        var asset = assets.ResolveUnit("c01.swordsman", "campaign-torren");
        var sprite = assets.CreateSprite(asset);
        using var cache = new ShadowCache();
        SpriteShadows? effect = null;
        try
        {
            foreach (var role in new[] { "idle", "walk-a", "walk-b", "attack", "idle", "walk-a", "attack" })
            {
                sprite.Texture = assets.CreateFrameTexture(asset, AssetCatalog.FrameIndex(asset, role));
                var before = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                if (effect is null)
                {
                    effect = SpriteShadows.CreateFigure(sprite, Colors.Blue, cache);
                    sprite.AddChild(effect);
                }
                else
                    effect.Synchronize(sprite, ShadowKind.Figure, asset.PixelDensity);
                GD.Print($"Figure {role}: {timer.Elapsed.TotalMilliseconds:F3}ms, {GC.GetAllocatedBytesForCurrentThread() - before} managed bytes, {cache.TextureCount} textures.");
            }
            Require(cache.TextureCount == 4, "Repeated idle/walk/attack frames grew the cache.");
            var texture = effect!.Texture;
            sprite.Free();
            cache.Dispose();
            cache.Dispose();
            Require(cache.IsDisposed && cache.TextureCount == 0 && !GodotObject.IsInstanceValid(texture), "Figure cache did not release resources.");
        }
        finally { if (GodotObject.IsInstanceValid(sprite)) sprite.Free(); }
    }

    private async Task Board()
    {
        var fixture = PresentationJson.Load<JsonObject>("res://tests/oracle/grayflag-initial-raw.json");
        var state = fixture["state"]!.AsObject();
        var assets = AssetCatalog.Load();
        var scene = AuthoredScene.Load();
        var board = new BoardView { AdvanceAnimationsAutomatically = false };
        AddChild(board);
        var cache = board.LightingResources;
        try
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            board.Configure(new(assets, EnvironmentCatalog.Load(), facing => facing == "west", null), fixture["level"]!.AsObject(), scene);
            board.ConfigureTerrain(WorldMaterialsManifest.Load());
            board.ConfigureDynamicState(new Empire.Core.State.ReadOnlyContent(PresentationJson.Load<JsonArray>("res://data/content-packs.json")));
            board.RenderState(state);
            cache = board.LightingResources;
            GD.Print($"GrayFlag first board: {timer.Elapsed.TotalMilliseconds:F3}ms, {GC.GetAllocatedBytesForCurrentThread() - before} managed bytes, {cache.TextureCount} textures.");
            var count = cache.TextureCount;
            before = GC.GetAllocatedBytesForCurrentThread();
            timer.Restart();
            for (var index = 0; index < 8; index++)
                board.RenderState(state);
            GD.Print($"GrayFlag cached projection mean: {timer.Elapsed.TotalMilliseconds / 8:F3}ms, {(GC.GetAllocatedBytesForCurrentThread() - before) / 8} managed bytes.");
            Require(cache.TextureCount == count, "Repeat board projection grew its cache.");
        }
        finally { board.QueueFree(); }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(cache.IsDisposed && cache.TextureCount == 0, "Queued board deletion did not release its cache.");
    }
    private void Require(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
