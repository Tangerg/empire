#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace Empire.Presentation;

/// <summary>Source filter rasterization. Input images, texture files and battle state are never modified.</summary>
internal static class CandidateShadowRaster
{
    internal static (Image Image, int Padding) Filter(Image source, IReadOnlyList<CandidateSpriteShadows.Drop> drops, float density)
    {
        var result = Shadows(source, drops, density);
        // The filter result includes the source. Keeping two overlapping draws would
        // apply group opacity twice and change translucent art and its silhouette.
        result.Image.BlendRect(source, new Rect2I(0, 0, source.GetWidth(), source.GetHeight()), new Vector2I(result.Padding, result.Padding));
        return result;
    }

    internal static (Image Image, int Padding) Shadows(Image source, IReadOnlyList<CandidateSpriteShadows.Drop> drops, float density)
    {
        var spill = 0f;
        foreach (var drop in drops) spill += Math.Max(Math.Abs(drop.Offset.X), Math.Abs(drop.Offset.Y)) + 4 * drop.Sigma;
        var padding = checked((int)Math.Ceiling(spill * density) + 1);
        var sourceWidth = source.GetWidth(); var sourceHeight = source.GetHeight();
        var width = checked(sourceWidth + padding * 2);
        var height = checked(sourceHeight + padding * 2);
        var original = new float[checked(width * height)];
        for (var y = 0; y < sourceHeight; y++) for (var x = 0; x < sourceWidth; x++)
            original[(y + padding) * width + x + padding] = source.GetPixel(x, y).A;
        var alpha = (float[])original.Clone();
        var red = new float[alpha.Length]; var green = new float[alpha.Length]; var blue = new float[alpha.Length]; var shadowAlpha = new float[alpha.Length];
        // Every pass overwrites its scratch buffers; they never outlive this bake.
        var blurred = new float[alpha.Length];
        float[]? horizontal = null;
        foreach (var drop in drops)
        {
            var sigma = drop.Sigma * density;
            if (sigma == 0) Array.Copy(alpha, blurred, alpha.Length);
            else Blur(alpha, width, height, sigma, horizontal ??= new float[alpha.Length], blurred);
            var offsetX = drop.Offset.X * density;
            var offsetY = drop.Offset.Y * density;
            var integralOffset = offsetX == MathF.Floor(offsetX) && offsetY == MathF.Floor(offsetY);
            var integerX = (int)offsetX; var integerY = (int)offsetY;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var sampleX = x - integerX; var sampleY = y - integerY;
                var sample = integralOffset
                    ? (sampleX < 0 || sampleY < 0 || sampleX >= width || sampleY >= height ? 0 : blurred[sampleY * width + sampleX])
                    : Sample(blurred, width, height, x - offsetX, y - offsetY);
                var added = sample * drop.Color.A * (1 - shadowAlpha[index]);
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
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var index = y * width + x;
            var a = shadowAlpha[index];
            image.SetPixel(x, y, a == 0 ? Colors.Transparent : new Color(red[index] / a, green[index] / a, blue[index] / a, a));
        }
        return (image, padding);
    }

    internal static (Image Image, Vector2 Centre) Ellipse(Vector2 radii, Color color, float opacity, float sigma, float density)
    {
        var padding = (int)Math.Ceiling(4 * sigma * density) + 1;
        var width = checked((int)Math.Ceiling(radii.X * 2 * density) + padding * 2);
        var height = checked((int)Math.Ceiling(radii.Y * 2 * density) + padding * 2);
        var centre = new Vector2(width / 2f, height / 2f);
        var alpha = new float[checked(width * height)];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            // Subpixel coverage, followed by the source CSS contact blur.
            var hits = 0;
            for (var sy = 0; sy < 4; sy++) for (var sx = 0; sx < 4; sx++)
            {
                var dx = (x + (sx + .5f) / 4 - centre.X) / (radii.X * density);
                var dy = (y + (sy + .5f) / 4 - centre.Y) / (radii.Y * density);
                if (dx * dx + dy * dy <= 1) hits++;
            }
            alpha[y * width + x] = hits / 16f * opacity;
        }
        alpha = Blur(alpha, width, height, sigma * density);
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            image.SetPixel(x, y, new Color(color, alpha[y * width + x]));
        return (image, centre);
    }

    internal static float[] Blur(float[] source, int width, int height, float sigma)
    {
        if (sigma == 0) return (float[])source.Clone();
        if (!float.IsFinite(sigma) || sigma < 0) throw new ArgumentOutOfRangeException(nameof(sigma));
        var horizontal = new float[source.Length];
        var output = new float[source.Length];
        Blur(source, width, height, sigma, horizontal, output);
        return output;
    }

    private static void Blur(float[] source, int width, int height, float sigma, float[] horizontal, float[] output)
    {
        if (!float.IsFinite(sigma) || sigma <= 0) throw new ArgumentOutOfRangeException(nameof(sigma));
        // Four-sigma support bounds truncation; this is a Gaussian rasterization,
        // not a claim of pixel-identical browser-specific blur implementations.
        var radius = checked((int)Math.Ceiling(sigma * 4));
        var kernel = new float[radius * 2 + 1];
        var total = 0f;
        for (var offset = -radius; offset <= radius; offset++)
            total += kernel[offset + radius] = MathF.Exp(-offset * offset / (2 * sigma * sigma));
        for (var index = 0; index < kernel.Length; index++) kernel[index] /= total;
        // Clip omitted zero-padding taps without changing their accumulation order.
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var first = Math.Max(-radius, -x);
            var last = Math.Min(radius, width - x - 1);
            var index = y * width + x;
            var sum = 0f;
            for (var offset = first; offset <= last; offset++)
                sum += source[index + offset] * kernel[offset + radius];
            horizontal[index] = sum;
        }
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var first = Math.Max(-radius, -y);
            var last = Math.Min(radius, height - y - 1);
            var index = y * width + x;
            var sum = 0f;
            for (var offset = first; offset <= last; offset++)
                sum += horizontal[index + offset * width] * kernel[offset + radius];
            output[index] = sum;
        }
    }

    private static float Sample(float[] data, int width, int height, float x, float y)
    {
        var left = (int)MathF.Floor(x); var top = (int)MathF.Floor(y);
        float Pixel(int px, int py) => px < 0 || py < 0 || px >= width || py >= height ? 0 : data[py * width + px];
        return Mathf.Lerp(Mathf.Lerp(Pixel(left, top), Pixel(left + 1, top), x - left),
            Mathf.Lerp(Pixel(left, top + 1), Pixel(left + 1, top + 1), x - left), y - top);
    }
}
