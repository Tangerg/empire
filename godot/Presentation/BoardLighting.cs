#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Unpositioned source scenery pieces. The caller owns board depth and lifetime.</summary>
public sealed record FieldLighting(Node2D Hearths, Sprite2D Sunlight);

/// <summary>Procedural presentation from candidate-01-map-scene.ts, without world-light simulation.</summary>
public static class BoardLighting
{
    internal sealed record Pool(Vector2 Centre, float Size, float Strength);

    public static FieldLighting CreateField(Vector2I mapSize, IReadOnlyList<string> tiles,
        WorldMaterialsManifest materials, Texture2D? playableShape = null)
    {
        var pools = Pools(mapSize, tiles, materials);
        var hearths = new Node2D { Name = "SourceHearthLights" };
        Sprite2D? sunlight = null;
        try
        {
            // Hearth pieces are not playable-shape clipped in the original source.
            foreach (var pool in pools)
            {
                var span = pool.Size * 32;
                var body = $"<defs><radialGradient id=\"hearth\" cx=\".5\" cy=\".5\" r=\".5\"><stop offset=\"0\" stop-color=\"#ffcf85\" stop-opacity=\"{F(pool.Strength)}\"/><stop offset=\".45\" stop-color=\"#ffb75f\" stop-opacity=\"{(pool.Strength * .42f).ToString("F3", CultureInfo.InvariantCulture)}\"/><stop offset=\"1\" stop-color=\"#ff9d3a\" stop-opacity=\"0\"/></radialGradient></defs><rect width=\"{F(span)}\" height=\"{F(span)}\" fill=\"url(#hearth)\"/>";
                var sprite = Svg(body, new Vector2(span, span), "Hearth");
                // Source toFixed(1) belongs to the authored placement, not to raster density.
                sprite.Position = new Vector2(RoundTenth(pool.Centre.X - span / 2), RoundTenth(pool.Centre.Y - span / 2));
                hearths.AddChild(sprite);
            }
            var size = new Vector2(mapSize.X * 32, mapSize.Y * 32);
            sunlight = Svg(SunlightMarkup(size), size, "SourceFieldSunlight");
            if (playableShape is not null)
                GroundClipper.Apply(sunlight, playableShape, size);
            return new(hearths, sunlight);
        }
        catch { hearths.Free(); sunlight?.Free(); throw; }
    }

    /// <summary>Position is relative to the sprite-local foot (16,31), not the board cell origin or image bounds.</summary>
    public static Sprite2D CreateUnitContact()
    {
        var contact = UnitContact();
        using (contact.Image)
        {
            var sprite = OwnedProceduralSprite.Create(contact.Image);
            sprite.Name = "SourceUnitContact";
            sprite.Centered = false;
            sprite.ShowBehindParent = true;
            sprite.Position = contact.Origin;
            sprite.Scale = Vector2.One / contact.Density;
            sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            return sprite;
        }
    }

    private static LayerFilterComposition.Raster UnitContact()
    {
        // Ground-only ambient contact: a figure silhouette shadow would read as a cutout.
        var contact = ShadowRaster.Ellipse(new Vector2(10.5f, 3.5f), new Color("#393426"), .42f, 1.1f, 2);
        return new(contact.Image, new Vector2(0, -1.8f) - contact.Centre / 2, 2);
    }

    internal static LayerFilterComposition.Raster UnitBody(Color team)
    {
        var contact = UnitContact();
        using var ring = new Image();
        // The source body is an unclipped group. Its ring extends below the 32px
        // tile, so the raster canvas must contain the stroke, not clip it at y=32.
        var error = ring.LoadSvgFromString($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"36\"><ellipse cx=\"16\" cy=\"29.5\" rx=\"11\" ry=\"2.25\" fill=\"none\" stroke=\"#{team.ToHtml(false)}\" stroke-width=\"1.45\" opacity=\".72\"/></svg>", 2);
        if (error != Error.Ok)
        {
            contact.Image.Dispose();
            throw new InvalidOperationException($"Source team ring SVG failed: {error}");
        }
        using (contact.Image)
            return LayerFilterComposition.Compose(new[] {
                new LayerFilterComposition.Layer(contact.Image, new(contact.Origin, new Vector2(contact.Image.GetWidth(), contact.Image.GetHeight()) / contact.Density)),
                new LayerFilterComposition.Layer(ring, new(new Vector2(-16, -31), new Vector2(32, 36))),
            }, 2);
    }

    public static Sprite2D CreateStructureContact(Vector2 frameSize, float presentationScale, ShadowCache? cache = null)
    {
        if (!frameSize.IsFinite() || frameSize.X <= 0 || frameSize.Y <= 0 || !float.IsFinite(presentationScale) || presentationScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameSize));
        LayerFilterComposition.Raster Raster()
        {
            var image = ShadowRaster.Ellipse(frameSize * presentationScale * new Vector2(.32f, .055f), new Color("#28231f"), .22f, 0, 2);
            return new(image.Image, -image.Centre / 2, 2);
        }
        Sprite2D sprite;
        if (cache is not null)
        {
            var frame = cache.Get(("structure-contact", frameSize, presentationScale), Raster);
            sprite = new Sprite2D { Texture = frame.Texture, Position = frame.Origin, Scale = Vector2.One / frame.Density };
        }
        else
        {
            var raster = Raster();
            using (raster.Image)
                sprite = OwnedProceduralSprite.Create(raster.Image);
            sprite.Position = raster.Origin;
            sprite.Scale = Vector2.One / raster.Density;
        }
        sprite.Name = "SourceStructureContact";
        sprite.Centered = false;
        sprite.ShowBehindParent = true;
        sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        return sprite;
    }

    internal static IReadOnlyList<Pool> Pools(Vector2I size, IReadOnlyList<string> tiles, WorldMaterialsManifest materials)
    {
        if (size.X <= 0 || size.Y <= 0 || tiles.Count != checked(size.X * size.Y))
            throw new ArgumentException("Lighting requires the complete canonical tile projection.");
        if (!materials.GroundTagFamilies.TryGetValue("settlement", out var settlement))
            throw new InvalidOperationException("Settlement tag family is absent from the material export.");
        var pools = new List<Pool>();
        for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                var id = tiles[y * size.X + x];
                if (!materials.TerrainTags.TryGetValue(id, out var tags))
                    throw new InvalidOperationException($"Terrain tags are absent for {id}.");
                var living = tags.Intersect(settlement, StringComparer.Ordinal).Any();
                var bridgeHead = id == "bridge" && (y == 0 || tiles[(y - 1) * size.X + x] != "bridge");
                if (living || bridgeHead)
                    pools.Add(new(new Vector2((x + .5f) * 32, (y + .62f) * 32), living ? 3 : 2, living ? .26f : .18f));
            }
        return pools;
    }

    internal static string SunlightMarkup(Vector2 size) =>
        $"<defs><linearGradient id=\"sun\" x1=\".12\" y1=\"0\" x2=\".72\" y2=\"1\"><stop offset=\"0\" stop-color=\"#ffeec2\" stop-opacity=\".125\"/><stop offset=\".42\" stop-color=\"#ffdea4\" stop-opacity=\".055\"/><stop offset=\"1\" stop-color=\"#9dc0dd\" stop-opacity=\".045\"/></linearGradient><radialGradient id=\"hot\" cx=\".3\" cy=\".08\" r=\".72\"><stop offset=\"0\" stop-color=\"#fff6da\" stop-opacity=\".16\"/><stop offset=\".55\" stop-color=\"#fff6da\" stop-opacity=\".045\"/><stop offset=\"1\" stop-color=\"#fff6da\" stop-opacity=\"0\"/></radialGradient></defs><rect width=\"{F(size.X)}\" height=\"{F(size.Y)}\" fill=\"url(#sun)\"/><rect width=\"{F(size.X)}\" height=\"{F(size.Y)}\" fill=\"url(#hot)\"/>";

    private static Sprite2D Svg(string body, Vector2 size, string name)
    {
        using var image = new Image();
        var error = image.LoadSvgFromString($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(size.X)}\" height=\"{F(size.Y)}\" viewBox=\"0 0 {F(size.X)} {F(size.Y)}\">{body}</svg>", 2);
        if (error != Error.Ok)
            throw new InvalidOperationException($"Source lighting SVG failed: {error}");
        var sprite = OwnedProceduralSprite.Create(image);
        sprite.Name = name;
        sprite.Centered = false;
        sprite.Scale = Vector2.One / 2;
        sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        return sprite;
    }

    private static float RoundTenth(float value) => (float)Math.Round(value, 1, MidpointRounding.AwayFromZero);
    private static string F(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
