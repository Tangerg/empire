#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>
/// One owner for source-over inputs, geometric cutouts, and a filter applied to their
/// union. Colors are the original source pixels, not a replacement art treatment.
/// A single output draw applies group opacity after the entire filter chain.
/// </summary>
internal static class CandidateFilterComposition
{
    internal sealed record Layer(Image Image, Rect2 Bounds, IReadOnlyList<Rect2>? Cutouts = null);
    internal sealed record Raster(Image Image, Vector2 Origin, float Density);

    internal static Raster Compose(IReadOnlyList<Layer> layers, float density)
    {
        if (layers.Count == 0 || !float.IsFinite(density) || density <= 0)
            throw new ArgumentException("Composition requires explicit layers and a positive pixel density.");
        foreach (var layer in layers)
            if (!layer.Bounds.Position.IsFinite() || !layer.Bounds.Size.IsFinite()
                || layer.Bounds.Size.X <= 0 || layer.Bounds.Size.Y <= 0)
                throw new ArgumentException("Composition layer bounds must be finite and positive.");
        var minimum = new Vector2(layers.Min(layer => layer.Bounds.Position.X), layers.Min(layer => layer.Bounds.Position.Y));
        var maximum = new Vector2(layers.Max(layer => layer.Bounds.End.X), layers.Max(layer => layer.Bounds.End.Y));
        var origin = (minimum * density).Floor() / density;
        var size = (Vector2I)((maximum - origin) * density).Ceil();
        var pixels = new Color[checked(size.X * size.Y)]; // premultiplied source-over accumulator
        foreach (var layer in layers)
        {
            var input = new CandidateRasterPixels(layer.Image);
            var start = (Vector2I)((layer.Bounds.Position - origin) * density).Floor();
            var end = (Vector2I)((layer.Bounds.End - origin) * density).Ceil();
            for (var y = Math.Max(0, start.Y); y < Math.Min(size.Y, end.Y); y++)
            for (var x = Math.Max(0, start.X); x < Math.Min(size.X, end.X); x++)
            {
                var point = origin + new Vector2(x + .5f, y + .5f) / density;
                var coverage = Coverage(layer, origin + new Vector2(x, y) / density, 1 / density);
                if (coverage == 0) continue;
                var uv = (point - layer.Bounds.Position) / layer.Bounds.Size;
                var sample = Sample(input, uv * new Vector2(input.Width, input.Height) - Vector2.One / 2) * coverage;
                pixels[y * size.X + x] = sample + pixels[y * size.X + x] * (1 - sample.A);
            }
        }
        var rgba = new byte[checked(size.X * size.Y * 4)];
        for (var y = 0; y < size.Y; y++) for (var x = 0; x < size.X; x++)
        {
            var color = pixels[y * size.X + x];
            CandidateRasterPixels.Write(rgba, y * size.X + x, color.A == 0 ? Colors.Transparent : new Color(color.R / color.A, color.G / color.A, color.B / color.A, color.A));
        }
        return new(Image.CreateFromData(size.X, size.Y, false, Image.Format.Rgba8, rgba), origin, density);
    }

    internal static Raster Filter(Raster source, CandidateShadowKind kind)
    {
        var result = CandidateShadowRaster.Filter(source.Image, CandidateSpriteShadows.Drops(kind), source.Density);
        return new(result.Image, source.Origin - Vector2.One * result.Padding / source.Density, source.Density);
    }

    private static float Coverage(Layer layer, Vector2 topLeft, float size)
    {
        if (layer.Cutouts is null || layer.Cutouts.Count == 0)
        {
            // Use the original sample locations, not cell bounds: their float
            // rounding at an edge belongs to the coverage contract.
            var first = topLeft + Vector2.One * (.5f * (size / 4));
            var last = topLeft + Vector2.One * (3.5f * (size / 4));
            if (layer.Bounds.HasPoint(first) && layer.Bounds.HasPoint(last)) return 1;
        }
        var hits = 0;
        for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++)
        {
            var point = topLeft + new Vector2(x + .5f, y + .5f) * (size / 4);
            if (!layer.Bounds.HasPoint(point)) continue;
            var cutout = false;
            if (layer.Cutouts is not null)
                foreach (var cut in layer.Cutouts)
                    if (cut.HasPoint(point)) { cutout = true; break; }
            if (cutout) continue;
            hits++;
        }
        return hits / 16f;
    }

    private static Color Sample(CandidateRasterPixels image, Vector2 point)
    {
        var left = (int)MathF.Floor(point.X); var top = (int)MathF.Floor(point.Y);
        Color Pixel(int x, int y)
        {
            // Image edge clamps within its declared canvas; geometry coverage owns the boundary.
            var color = image.Pixel(Math.Clamp(x, 0, image.Width - 1), Math.Clamp(y, 0, image.Height - 1));
            return new Color(color.R * color.A, color.G * color.A, color.B * color.A, color.A);
        }
        return Pixel(left, top).Lerp(Pixel(left + 1, top), point.X - left)
            .Lerp(Pixel(left, top + 1).Lerp(Pixel(left + 1, top + 1), point.X - left), point.Y - top);
    }
}
