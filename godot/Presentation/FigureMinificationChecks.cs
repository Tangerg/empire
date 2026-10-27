#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

public partial class FigureMinificationChecks : Node
{
    public override void _Ready()
    {
        try
        {
            Check();
            GD.Print("Figure area minification contracts passed.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check()
    {
        var output = OS.GetCmdlineUserArgs();
        var directory = output.Length > 0 ? output[0] : "user://figure-minification";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        using var stripes = Image.CreateEmpty(90, 60, false, Image.Format.Rgba8);
        for (var y = 0; y < 60; y++)
            for (var x = 0; x < 90; x++)
                stripes.SetPixel(x, y, x % 3 == 1 ? Colors.White : Colors.Black);
        // 6x reduction: the old bilinear pair always misses the white column.
        var before = Compose(stripes, new Rect2(0, 0, 15, 10), false);
        var after = Compose(stripes, new Rect2(0, 0, 15, 10), true);
        using (before.Image)
        using (after.Image)
        {
            Require(before.Image.GetPixel(2, 2).R < .01f, "Control must reproduce skipped high-frequency source pixels.");
            for (var y = 0; y < 10; y++)
                for (var x = 0; x < 15; x++)
                    Near(after.Image.GetPixel(x, y), new Color(1f / 3, 1f / 3, 1f / 3, 1), "Full footprint preserves stripe energy independent of phase.");
            Save(stripes, directory + "/stripes-source.png");
            Save(before.Image, directory + "/stripes-bilinear.png");
            Save(after.Image, directory + "/stripes-area.png");
        }
        using var edge = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
        for (var y = 0; y < 12; y++)
            for (var x = 0; x < 12; x++)
                edge.SetPixel(x, y, x < 6 ? new Color(1, 0, 0, .5f) : new Color(0, 0, 1, 0));
        var transparent = Compose(edge, new Rect2(.25f, .25f, 1, 1), true);
        using (transparent.Image)
        {
            // Sum premultiplied output mass: half the image is 127/255-alpha red.
            var mass = new Color(0, 0, 0, 0);
            for (var y = 0; y < transparent.Image.GetHeight(); y++)
                for (var x = 0; x < transparent.Image.GetWidth(); x++)
                {
                    var pixel = transparent.Image.GetPixel(x, y);
                    Require(pixel.B * pixel.A == 0 && pixel.G * pixel.A == 0, "Transparent blue must not bleed into a red silhouette.");
                    mass += new Color(pixel.R * pixel.A, pixel.G * pixel.A, pixel.B * pixel.A, pixel.A);
                }
            Near(mass, new Color(127f / 510, 0, 0, 127f / 510), "Subpixel canvas edges preserve premultiplied color and opacity mass.", .012f);
            Save(edge, directory + "/alpha-edge-source.png");
            Save(transparent.Image, directory + "/alpha-edge-area.png");
        }
        using var cache = new ShadowCache();
        var sprite = new Sprite2D
        {
            Texture = ImageTexture.CreateFromImage(stripes),
            Centered = false,
            Scale = Vector2.One / 6,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        try
        {
            var legacy = cache.Figure(sprite, Colors.White);
            sprite.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            var filtered = cache.Figure(sprite, Colors.White);
            Require(legacy.Texture != filtered.Texture, "Figure cache identity must include source filtering.");
            Require(filtered == cache.Figure(sprite, Colors.White), "Repeated figures reuse the area-composited texture.");
            Require(SpriteShadows.Drops(ShadowKind.Figure).Count == 0, "Area filtering must not reintroduce figure drop halos.");
            using var legacyImage = legacy.Texture.GetImage();
            using var filteredImage = filtered.Texture.GetImage();
            Require(!legacyImage.GetData().AsSpan().SequenceEqual(filteredImage.GetData()),
                "Explicit source Linear must change the CPU figure raster, not only its GPU filter or cache key.");
            Save(legacyImage, directory + "/figure-cache-bilinear.png");
            Save(filteredImage, directory + "/figure-cache-area.png");
        }
        finally { sprite.Texture.Dispose(); sprite.Free(); }
    }

    private static LayerFilterComposition.Raster Compose(Image source, Rect2 bounds, bool area) =>
        LayerFilterComposition.Compose(new[] { new LayerFilterComposition.Layer(source, bounds, AreaMinification: area) }, 1);
    private static void Save(Image image, string path)
    {
        if (image.SavePng(path) != Error.Ok)
            throw new InvalidOperationException("Cannot save check evidence: " + path);
    }
    private static void Near(Color actual, Color expected, string message, float tolerance = .005f) =>
        Require(Mathf.Abs(actual.R - expected.R) < tolerance && Mathf.Abs(actual.G - expected.G) < tolerance
            && Mathf.Abs(actual.B - expected.B) < tolerance && Mathf.Abs(actual.A - expected.A) < tolerance, message + $" Actual={actual}, expected={expected}");
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
