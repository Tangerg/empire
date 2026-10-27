using System;
using Godot;

namespace Empire.Presentation;

/// <summary>Frames character artwork without transparent animation padding or aspect-ratio cropping.</summary>
public static class CharacterPortrait
{
    public static TextureRect Create(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        using var pixels = texture.GetImage()
            ?? throw new InvalidOperationException("Character portrait texture has no readable image.");
        var visible = pixels.GetUsedRect();
        if (visible.Size.X <= 0 || visible.Size.Y <= 0)
            throw new InvalidOperationException("Character portrait texture contains no visible pixels.");
        Texture2D framed = visible == new Rect2I(Vector2I.Zero, pixels.GetSize())
            ? texture
            : new AtlasTexture { Atlas = texture, Region = visible, FilterClip = true };
        return new TextureRect
        {
            Texture = framed,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }
}
