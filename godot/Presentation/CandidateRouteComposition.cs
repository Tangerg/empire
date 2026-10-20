#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

/// <summary>
/// Each source connector cell is masked, filtered, then placed. Filtering an
/// already merged map would change overlaps at cell seams and is not equivalent.
/// </summary>
internal static class CandidateRouteComposition
{
    internal static CandidateFilterComposition.Raster Compose(Image mask, Rect2I sourceCell,
        Image material, Vector2 logicalPeriod, Vector2 worldOrigin)
    {
        if (sourceCell.Size != new Vector2I(64, 64) || logicalPeriod.X <= 0 || logicalPeriod.Y <= 0)
            throw new ArgumentException("Route masks require 64-pixel cells and positive world material period.");
        using var source = Image.CreateEmpty(68, 68, false, Image.Format.Rgba8);
        for (var y = 0; y < 68; y++) for (var x = 0; x < 68; x++)
        {
            var local = new Vector2(-1, -1) + new Vector2(x + .5f, y + .5f) / 2;
            if (local.X < -.5f || local.Y < -.5f || local.X >= 32.5f || local.Y >= 32.5f) continue;
            var world = worldOrigin + local;
            var sample = new Vector2(world.X / logicalPeriod.X * material.GetWidth() - .5f,
                world.Y / logicalPeriod.Y * material.GetHeight() - .5f);
            var color = RepeatLinear(material, sample);
            var maskPoint = (local + Vector2.One * .5f) * (64f / 33f) - Vector2.One * .5f;
            color.A *= MaskLinear(mask, sourceCell, maskPoint);
            source.SetPixel(x, y, color);
        }
        var filtered = CandidateShadowRaster.Filter(source, CandidateSpriteShadows.Drops(CandidateShadowKind.Route), 2);
        return new(filtered.Image, -Vector2.One * (1 + filtered.Padding / 2f), 2);
    }

    private static float MaskLinear(Image image, Rect2I cell, Vector2 point)
    {
        var x = (int)MathF.Floor(point.X); var y = (int)MathF.Floor(point.Y);
        float Pixel(int px, int py) => image.GetPixel(cell.Position.X + Math.Clamp(px, 0, 63), cell.Position.Y + Math.Clamp(py, 0, 63)).A;
        var u = point.X - x; var v = point.Y - y;
        return Mathf.Lerp(Mathf.Lerp(Pixel(x, y), Pixel(x + 1, y), u), Mathf.Lerp(Pixel(x, y + 1), Pixel(x + 1, y + 1), u), v);
    }

    internal static Color RepeatLinear(Image image, Vector2 point)
    {
        var x = (int)MathF.Floor(point.X); var y = (int)MathF.Floor(point.Y);
        Color Pixel(int px, int py)
        {
            var width = image.GetWidth(); var height = image.GetHeight();
            return image.GetPixel((px % width + width) % width, (py % height + height) % height);
        }
        return Pixel(x, y).Lerp(Pixel(x + 1, y), point.X - x)
            .Lerp(Pixel(x, y + 1).Lerp(Pixel(x + 1, y + 1), point.X - x), point.Y - y);
    }

    internal static Sprite2D Create(Image mask, Rect2I sourceCell, Image material,
        Vector2 logicalPeriod, Vector2 worldOrigin, object identity, CandidateShadowCache? cache)
    {
        Sprite2D sprite;
        Vector2 origin;
        if (cache is null)
        {
            var raster = Compose(mask, sourceCell, material, logicalPeriod, worldOrigin);
            using (raster.Image) sprite = OwnedLightingSprite.Create(raster.Image);
            origin = raster.Origin;
        }
        else
        {
            var frame = cache.Get(identity, () => Compose(mask, sourceCell, material, logicalPeriod, worldOrigin));
            sprite = new Sprite2D { Texture = frame.Texture };
            origin = frame.Origin;
        }
        sprite.Name = "SourceRouteCell";
        sprite.Centered = false;
        sprite.Offset = origin * 2;
        sprite.Position = worldOrigin;
        sprite.Scale = Vector2.One / 2;
        sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        sprite.SetMeta("shadow_kind", CandidateShadowKind.Route.ToString());
        sprite.SetMeta("route_filter_scope", "individual-cell");
        return sprite;
    }
}
