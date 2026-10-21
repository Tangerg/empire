#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

// Borrowed image, per-composition byte snapshot; no image or cache lifetime ownership.
// Conversion matches Godot 4.6.3 core/io/image.cpp _get/_set_color_at_ofs:
// https://github.com/godotengine/godot/blob/4.6.3-stable/core/io/image.cpp
internal sealed class RasterPixels
{
    private readonly Image _source;
    private readonly byte[]? _rgba;
    internal int Width
    {
        get;
    }
    internal int Height
    {
        get;
    }
    internal RasterPixels(Image source)
    {
        _source = source;
        Width = source.GetWidth();
        Height = source.GetHeight();
        if (source.GetFormat() == Image.Format.Rgba8)
            _rgba = source.GetData();
    }
    internal Color Pixel(int x, int y)
    {
        if (_rgba is null)
            return _source.GetPixel(x, y);
        var at = (y * Width + x) * 4;
        return new((float)(_rgba[at] / 255.0), (float)(_rgba[at + 1] / 255.0),
            (float)(_rgba[at + 2] / 255.0), (float)(_rgba[at + 3] / 255.0));
    }
    internal float Alpha(int x, int y) => _rgba is null ? _source.GetPixel(x, y).A : (float)(_rgba[(y * Width + x) * 4 + 3] / 255.0);
    internal static void Write(byte[] rgba, int pixel, Color color)
    {
        var at = pixel * 4;
        rgba[at] = Quantize(color.R);
        rgba[at + 1] = Quantize(color.G);
        rgba[at + 2] = Quantize(color.B);
        rgba[at + 3] = Quantize(color.A);
    }
    // Godot multiplies by double 255.0 before truncation; float multiplication changes bytes.
    private static byte Quantize(float value) => (byte)Math.Clamp((double)value * 255.0, 0, 255);
}
