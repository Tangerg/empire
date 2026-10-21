#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

/// <summary>
/// Owns one unshared procedural texture. Native Sprite2D references alone
/// do not define managed-wrapper lifetime: detach and dispose before node teardown.
/// Shared board-cache textures must never be assigned through this owner.
/// </summary>
internal partial class OwnedProceduralSprite : Sprite2D
{
    private ImageTexture? _ownedTexture;

    internal static OwnedProceduralSprite Create(Image source, Action<OwnedProceduralSprite>? configure = null)
    {
        var sprite = new OwnedProceduralSprite();
        try
        {
            sprite._ownedTexture = ImageTexture.CreateFromImage(source);
            sprite.Texture = sprite._ownedTexture;
            configure?.Invoke(sprite);
            return sprite;
        }
        catch
        {
            sprite.Free();
            throw;
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || _ownedTexture is null)
            return;
        Texture = null;
        _ownedTexture.Dispose();
        _ownedTexture = null;
    }
}
