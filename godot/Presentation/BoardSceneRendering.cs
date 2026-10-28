#nullable enable
using System.Text.Json.Nodes;

namespace Empire.Presentation;

/// <summary>Resolves the authored ground renderer and its matching coordinate registration together.</summary>
public sealed record BoardSceneRendering(BoardPlaneFactory Plane, BoardGroundFactory Ground)
{
    public static BoardSceneRendering Load(AuthoredScene scene, JsonObject registeredLevel, JsonObject registeredMap)
    {
        if (scene.AssembledGroundPath is { } assembledPath)
        {
            if (scene.PaintedGroundPath is not null)
                throw new System.InvalidOperationException("A scene must select exactly one ground renderer.");
            var assembled = PresentationJson.Load<AssembledGroundDocument>(assembledPath);
            assembled.ValidateLevel(registeredLevel);
            return new(assembled.CreatePlane, assembled.Factory(registeredMap));
        }
        if (scene.PaintedGroundPath is not { } path)
            return new(BoardPlane.Dimetric, TerrainView.Factory(WorldMaterialsManifest.Load()));
        var art = PresentationJson.Load<PaintedGroundDocument>(path);
        art.ValidateLevel(registeredLevel);
        return new(art.CreatePlane, art.Factory(registeredMap));
    }
}
