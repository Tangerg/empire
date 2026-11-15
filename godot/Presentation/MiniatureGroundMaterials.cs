#nullable enable
using System.Text.Json.Nodes;
using Godot;

namespace Empire.Presentation;

internal static class MiniatureGroundMaterials
{
    private const string MarketZone = "central-village";
    private const string RetainingShader = "res://assets/c01-miniature-environment/miniature_retaining.gdshader";
    private const string SurfaceShader = "res://assets/c01-miniature-environment/miniature_ground.gdshader";

    internal static Vector3? MarketContact(MiniatureBoardGeometry geometry, JsonObject state)
    {
        if (state["scenario"]?["zones"]?[MarketZone] is not JsonArray { Count: 1 } zone)
            return null;
        var cell = zone[0]!.AsObject();
        return geometry.Contact(new(cell["x"]!.GetValue<int>(), cell["y"]!.GetValue<int>()));
    }

    internal static ShaderMaterial Surface(Color soil, Vector3? market)
    {
        var material = Create(SurfaceShader);
        material.SetShaderParameter("soil_color", soil);
        material.SetShaderParameter("pave_market", market.HasValue);
        if (market is { } position)
            material.SetShaderParameter("market", position);
        return material;
    }

    internal static ShaderMaterial RetainingWall() => Create(RetainingShader);

    private static ShaderMaterial Create(string path) => new()
    {
        Shader = GD.Load<Shader>(path) ?? throw new InvalidOperationException($"Missing miniature terrain shader: {path}"),
    };
}
