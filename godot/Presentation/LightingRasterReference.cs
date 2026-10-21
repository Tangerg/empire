#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

/// <summary>Frozen pre-optimization test oracle. Production rendering must not call this reference.</summary>
internal static class LightingRasterReference
{
    internal static (Image Image, int Padding) Filter(Image source, IReadOnlyList<SpriteShadows.Drop> drops, float density)
    {
        var result = Shadows(source, drops, density);
        // The filter result includes the source. Keeping two overlapping draws would
        // apply group opacity twice and change translucent art and its silhouette.
        result.Image.BlendRect(source, new Rect2I(0, 0, source.GetWidth(), source.GetHeight()), new Vector2I(result.Padding, result.Padding));
        return result;
    }

    internal static (Image Image, int Padding) Shadows(Image source, IReadOnlyList<SpriteShadows.Drop> drops, float density)
    {
        var spill = 0f;
        foreach (var drop in drops)
            spill += Math.Max(Math.Abs(drop.Offset.X), Math.Abs(drop.Offset.Y)) + 4 * drop.Sigma;
        var padding = checked((int)Math.Ceiling(spill * density) + 1);
        var width = checked(source.GetWidth() + padding * 2);
        var height = checked(source.GetHeight() + padding * 2);
        var original = new float[checked(width * height)];
        for (var y = 0; y < source.GetHeight(); y++)
            for (var x = 0; x < source.GetWidth(); x++)
                original[(y + padding) * width + x + padding] = source.GetPixel(x, y).A;
        var alpha = (float[])original.Clone();
        var red = new float[alpha.Length];
        var green = new float[alpha.Length];
        var blue = new float[alpha.Length];
        var shadowAlpha = new float[alpha.Length];
        foreach (var drop in drops)
        {
            var blurred = Blur(alpha, width, height, drop.Sigma * density);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var index = y * width + x;
                    var added = Sample(blurred, width, height, x - drop.Offset.X * density, y - drop.Offset.Y * density) * drop.Color.A * (1 - shadowAlpha[index]);
                    red[index] += drop.Color.R * added;
                    green[index] += drop.Color.G * added;
                    blue[index] += drop.Color.B * added;
                    shadowAlpha[index] += added;
                    // Each filter receives the previous filter's complete result,
                    // not just the original silhouette. Keep source art separate for drawing.
                    alpha[index] = original[index] + (1 - original[index]) * shadowAlpha[index];
                }
        }
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var a = shadowAlpha[index];
                image.SetPixel(x, y, a == 0 ? Colors.Transparent : new Color(red[index] / a, green[index] / a, blue[index] / a, a));
            }
        return (image, padding);
    }

    internal static float[] Blur(float[] source, int width, int height, float sigma)
    {
        if (sigma == 0)
            return (float[])source.Clone();
        if (!float.IsFinite(sigma) || sigma < 0)
            throw new ArgumentOutOfRangeException(nameof(sigma));
        // Four-sigma support bounds truncation; this is a Gaussian rasterization,
        // not a claim of pixel-identical browser-specific blur implementations.
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

    private static float Sample(float[] data, int width, int height, float x, float y)
    {
        var left = (int)MathF.Floor(x);
        var top = (int)MathF.Floor(y);
        float Pixel(int px, int py) => px < 0 || py < 0 || px >= width || py >= height ? 0 : data[py * width + px];
        return Mathf.Lerp(Mathf.Lerp(Pixel(left, top), Pixel(left + 1, top), x - left),
            Mathf.Lerp(Pixel(left, top + 1), Pixel(left + 1, top + 1), x - left), y - top);
    }
}
