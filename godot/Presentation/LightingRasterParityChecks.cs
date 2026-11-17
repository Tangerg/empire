#nullable enable
using System;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Exact native conversion and pre-optimization raster/coverage references, not visual acceptance.</summary>
public partial class LightingRasterParityChecks : Node
{
    private int _checks;
    public override void _Ready()
    {
        try
        {
            GD.Print($"SIMD hardware={System.Numerics.Vector.IsHardwareAccelerated}, lanes={System.Numerics.Vector<float>.Count}");
            PixelConversions();
            RasterComparisons();
            Compositions();
            GD.Print($"Lighting raster parity checks passed: {_checks} assertions.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private void PixelConversions()
    {
        var values = new System.Collections.Generic.List<float> { -1, 0, 1, 2, float.NegativeInfinity, float.PositiveInfinity };
        for (var value = 0; value <= 255; value++)
        {
            var boundary = value / 255f;
            values.Add(boundary);
            values.Add(MathF.BitIncrement(boundary));
            values.Add(MathF.BitDecrement(boundary));
        }
        var random = new Random(6391);
        for (var index = 0; index < 4096; index++)
            values.Add((float)random.NextDouble() * 1.2f - .1f);
        using var output = Image.CreateEmpty(values.Count, 1, false, Image.Format.Rgba8);
        var bytes = new byte[values.Count * 4];
        for (var index = 0; index < values.Count; index++)
        {
            var color = new Color(values[index], values[(index + 7) % values.Count], values[(index + 33) % values.Count], values[(index + 19) % values.Count]);
            output.SetPixel(index, 0, color);
            RasterPixels.Write(bytes, index, color);
        }
        Require(bytes.SequenceEqual(output.GetData()), "Bulk RGBA8 quantization differs from native SetPixel.");
        var rampBytes = new byte[256 * 4];
        for (var value = 0; value <= 255; value++)
        {
            rampBytes[value * 4] = (byte)value;
            rampBytes[value * 4 + 1] = (byte)(255 - value);
            rampBytes[value * 4 + 2] = (byte)(value * 17 % 256);
            rampBytes[value * 4 + 3] = (byte)value;
        }
        using var ramp = Image.CreateFromData(256, 1, false, Image.Format.Rgba8, rampBytes);
        var rampReader = new RasterPixels(ramp);
        for (var value = 0; value <= 255; value++)
        {
            Require(Bits(ramp.GetPixel(value, 0)).SequenceEqual(Bits(rampReader.Pixel(value, 0))), "RGBA8 byte decoding changed.");
            Require(BitConverter.SingleToInt32Bits(ramp.GetPixel(value, 0).A) == BitConverter.SingleToInt32Bits(rampReader.Alpha(value, 0)), "RGBA8 alpha decoding changed.");
        }
        foreach (var format in new[] { Image.Format.Rgba8, Image.Format.Rgb8, Image.Format.La8, Image.Format.Rgbaf, Image.Format.Rgbah, Image.Format.Rgba4444, Image.Format.R8, Image.Format.Rg8 })
            foreach (var width in new[] { 1, 3, 7, 17, 31 })
            {
                using var source = Pattern(width, 5, format);
                var bulk = new RasterPixels(source);
                for (var y = 0; y < source.GetHeight(); y++)
                    for (var x = 0; x < width; x++)
                    {
                        var expected = source.GetPixel(x, y);
                        var actual = bulk.Pixel(x, y);
                        Require(Bits(expected).SequenceEqual(Bits(actual)), $"Bulk/fallback pixel bits changed: {format} {x},{y}.");
                        Require(BitConverter.SingleToInt32Bits(expected.A) == BitConverter.SingleToInt32Bits(bulk.Alpha(x, y)), "Bulk alpha bits changed.");
                    }
            }
    }
    private void RasterComparisons()
    {
        foreach (var format in new[] { Image.Format.Rgba8, Image.Format.Rgb8, Image.Format.La8, Image.Format.Rgbaf, Image.Format.Rgbah })
            foreach (var width in new[] { 1, 7, 17, 33 })
                foreach (var density in new[] { .5f, 1f, 1.5f, 2f })
                {
                    using var source = Pattern(width, 5, format);
                    var before = source.GetData();
                    var drops = new[] {
                new SpriteShadows.Drop(new(-1.25f, .75f), .65f, new Color(.2f, .7f, .4f, .45f)),
                new SpriteShadows.Drop(new(0, 2), 1.2f, new Color(.1f, .3f, .8f, .75f)),
                new SpriteShadows.Drop(new(.25f, -1.5f), 0, new Color(.9f, .1f, .8f, .7f)),
            };
                    var expected = format == Image.Format.Rgba8 ? LightingRasterReference.Filter(source, drops, density) : LightingRasterReference.Shadows(source, drops, density);
                    var actual = format == Image.Format.Rgba8 ? ShadowRaster.Filter(source, drops, density) : ShadowRaster.Shadows(source, drops, density);
                    using (expected.Image)
                    using (actual.Image)
                        Require(expected.Padding == actual.Padding && expected.Image.GetData().SequenceEqual(actual.Image.GetData()), $"Full raster bytes changed: {format}, {width}, {density}.");
                    Require(source.GetData().SequenceEqual(before), "Bulk raster mutated source image.");
                }
    }
    private void Compositions()
    {
        foreach (var format in new[] { Image.Format.Rgba8, Image.Format.Rgb8, Image.Format.La8, Image.Format.Rgbaf, Image.Format.Rgbah })
            foreach (var density in new[] { .5f, 1f, 1.5f, 2f })
            {
                using var source = Pattern(17, 9, format);
                using var other = Pattern(7, 3, Image.Format.Rgba8);
                var bounds = new Rect2(-3.5f, -.25f, 11.75f, 8.1f);
                var secondBounds = new Rect2(.125f, 3, 9.5f, 4.25f);
                var cutouts = new[] { new Rect2(-1.2f, 1, 3.75f, 2.125f), new Rect2(4, 2.25f, 2, 9) };
                var actual = LayerFilterComposition.Compose(new[] { new LayerFilterComposition.Layer(source, bounds, cutouts), new LayerFilterComposition.Layer(other, secondBounds) }, density);
                var expected = LightingCompositionReference.Compose(new[] { new LightingCompositionReference.Layer(source, bounds, cutouts), new LightingCompositionReference.Layer(other, secondBounds) }, density);
                using (actual.Image)
                using (expected.Image)
                    Require(actual.Origin == expected.Origin && actual.Density == expected.Density && actual.Image.GetData().SequenceEqual(expected.Image.GetData()), $"Composition bytes changed: {format}, {density}.");
            }
    }
    private static Image Pattern(int width, int height, Image.Format format)
    {
        var image = Image.CreateEmpty(width, height, false, format);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image.SetPixel(x, y, new Color((x * 13 % 31) / 30f, (y * 7 % 19) / 18f, ((x + y) % 7) / 6f, (x + y) % 3 == 0 ? 0 : ((x * 3 + y * 7) % 17) / 16f));
        return image;
    }
    private static int[] Bits(Color color) => new[] { BitConverter.SingleToInt32Bits(color.R), BitConverter.SingleToInt32Bits(color.G), BitConverter.SingleToInt32Bits(color.B), BitConverter.SingleToInt32Bits(color.A) };
    private void Require(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
