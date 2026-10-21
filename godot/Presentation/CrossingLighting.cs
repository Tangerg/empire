#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Source junction adapter: clip each registered axis, then filter their union once.</summary>
internal static class CrossingLighting
{
    internal static Sprite2D Compose(CrossingManifest manifest, Sprite2D horizontal, Sprite2D vertical, ShadowCache cache)
    {
        var records = new[] { manifest.Horizontal, manifest.Vertical };
        var windows = new[] { manifest.Junction.Horizontal.Cutouts, manifest.Junction.Vertical.Cutouts };
        // Geometry participates in cache identity; changing a declaration never reuses stale cutouts.
        var geometry = string.Join(";", records.SelectMany(record => record.SourceSize.Concat(record.SourceFoot).Append(record.LogicalPerSourcePixel))
            .Concat(windows.SelectMany(axis => axis.SelectMany(window => window)))
            .Select(value => value.ToString("R", CultureInfo.InvariantCulture)));
        var key = ("junction", horizontal.Texture.GetInstanceId(), vertical.Texture.GetInstanceId(), geometry);
        var frame = cache.Get(key, () =>
        {
            using var h = SpriteShadows.ReadFrame(horizontal.Texture);
            using var v = SpriteShadows.ReadFrame(vertical.Texture);
            LayerFilterComposition.Layer Layer(int index, Image image)
            {
                var source = records[index];
                var size = RasterResources.Pair(source.SourceSize, "junction canvas") * (float)source.LogicalPerSourcePixel;
                var anchor = RasterResources.Pair(source.SourceFoot, "junction anchor") * (float)source.LogicalPerSourcePixel;
                return new(image, new(-anchor, size), windows[index].Select(cut => new Rect2((float)cut[0], (float)cut[1], (float)cut[2], (float)cut[3])).ToArray());
            }
            var combined = LayerFilterComposition.Compose(new[] { Layer(0, h), Layer(1, v) }, 2);
            using (combined.Image)
                return LayerFilterComposition.Filter(combined, ShadowKind.Standing);
        });
        var result = new Sprite2D
        {
            Name = "SourceJunctionComposition",
            Texture = frame.Texture,
            Centered = false,
            Offset = frame.Origin * frame.Density,
            Scale = Vector2.One / frame.Density,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear
        };
        result.SetMeta("composition_inputs", 2);
        result.SetMeta("shadow_kind", ShadowKind.Standing.ToString());
        // Their shader contracts and immutable texture handles remain inspectable.
        // A shader can ignore SelfModulate, so suppress these original draws explicitly.
        horizontal.Visible = vertical.Visible = false;
        return result;
    }
}
