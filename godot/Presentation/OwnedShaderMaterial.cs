#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

/// <summary>Owns an exclusive generated material and shader, never their borrowed texture inputs.</summary>
internal partial class OwnedShaderMaterial : Node
{
    private Sprite2D? _sprite;
    private Shader? _shader;
    private ShaderMaterial? _material;

    internal ShaderMaterial Value => _material ?? throw new ObjectDisposedException(nameof(OwnedShaderMaterial));

    internal static OwnedShaderMaterial Attach(Sprite2D sprite, string source)
    {
        if (sprite.Material is not null)
            throw new InvalidOperationException("Generated material ownership requires an unassigned sprite.");
        var owner = new OwnedShaderMaterial
        {
            Name = "GeneratedMaterialOwner",
            _sprite = sprite,
            _shader = new Shader { Code = source }
        };
        owner._material = new ShaderMaterial { Shader = owner._shader };
        sprite.AddChild(owner);
        sprite.Material = owner._material;
        return owner;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || _material is null) return;
        if (GodotObject.IsInstanceValid(_sprite) && ReferenceEquals(_sprite!.Material, _material))
            _sprite.Material = null;
        _material.Shader = null;
        _material.Dispose();
        _shader!.Dispose();
        _material = null;
        _shader = null;
        _sprite = null;
    }
}
