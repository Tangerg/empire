#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

/// <summary>Source playable-shape alpha applies to every ground piece, not standing scenery.</summary>
internal static class GroundClipper
{
    private const string ShaderSource = """
        shader_type canvas_item;
        uniform sampler2D playable_shape : filter_linear, repeat_disable;
        uniform vec2 board_size;
        uniform vec2 basis_x;
        uniform vec2 basis_y;
        uniform vec2 board_origin;
        varying vec2 board_uv;
        varying vec4 authored_color;
        void vertex() {
            board_uv = (board_origin + basis_x * VERTEX.x + basis_y * VERTEX.y) / board_size;
            authored_color = COLOR;
        }
        void fragment() {
            float inside = step(0.0, board_uv.x) * step(0.0, board_uv.y)
                * step(board_uv.x, 1.0) * step(board_uv.y, 1.0);
            COLOR = texture(TEXTURE, UV) * authored_color;
            COLOR.a *= texture(playable_shape, board_uv).a * inside;
        }
        """;

    internal static void Remove(Node2D ground)
    {
        if (ground is Sprite2D sprite && sprite.HasMeta("ground_clipped"))
        {
            sprite.Material = null;
            sprite.RemoveMeta("ground_clipped");
        }
        foreach (var child in ground.GetChildren())
            if (child is Node2D child2D) Remove(child2D);
    }

    internal static void Apply(Node2D ground, Texture2D shape, Vector2 boardSize)
    {
        void Visit(Node2D node, Transform2D parent)
        {
            var transform = parent * node.Transform;
            if (node is Sprite2D sprite)
            {
                // Full-board material sprites already know their world UVs; preserve the RGB material shader.
                if (sprite.Material is ShaderMaterial material && sprite.HasMeta("world_material"))
                {
                    if (sprite.Name == "WorldField") return; // Its own alpha is the shape; don't square antialias coverage.
                    material.SetShaderParameter("has_playable_shape", true);
                    material.SetShaderParameter("playable_shape", shape);
                }
                else
                {
                    if (sprite.Material is not null && !sprite.HasMeta("ground_clipped"))
                        throw new InvalidOperationException($"Ground clip requires an explicit shader composition: {sprite.Name}");
                    var shader = sprite.Material as ShaderMaterial ?? new ShaderMaterial { Shader = new Shader { Code = ShaderSource } };
                    shader.SetShaderParameter("playable_shape", shape);
                    shader.SetShaderParameter("board_size", boardSize);
                    shader.SetShaderParameter("basis_x", transform.X);
                    shader.SetShaderParameter("basis_y", transform.Y);
                    shader.SetShaderParameter("board_origin", transform.Origin);
                    sprite.Material = shader;
                    sprite.SetMeta("ground_clipped", true);
                }
            }
            foreach (var child in node.GetChildren())
                if (child is Node2D child2D) Visit(child2D, transform);
        }
        Visit(ground, Transform2D.Identity);
    }
}
