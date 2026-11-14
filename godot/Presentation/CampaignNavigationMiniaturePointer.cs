#nullable enable
using Empire.Presentation.Tactical;
using Godot;

namespace Empire.Presentation;

/// <summary>Test-only points on the mounted miniature, never the hidden 2D compatibility board.</summary>
internal static class CampaignNavigationMiniaturePointer
{
    internal static Vector2? Find(BattleScreen screen, TacticalHit expected)
    {
        var map = (Control)screen.FindChild("MapArea", true, false);
        var view = (MiniatureBoardView)screen.FindChild("MiniaturePresentation", true, false);
        var center = view.Camera.UnprojectPosition(view.Geometry.Contact(expected.Cell)
            + Vector3.Up * (expected.Kind == TacticalHitKind.Unit ? .55f : .025f));
        // Pick tests the actual visible sprite alpha and intervening world geometry.
        // Search locally, not across the board, so a point cannot silently name another cell.
        var extent = expected.Kind == TacticalHitKind.Unit ? 32 : 8;
        for (var radius = 0; radius <= extent; radius++)
            for (var y = -radius; y <= radius; y++)
                for (var x = -radius; x <= radius; x++)
                {
                    if (System.Math.Max(System.Math.Abs(x), System.Math.Abs(y)) != radius)
                        continue;
                    var local = center + new Vector2(x, y);
                    var global = map.GetGlobalTransformWithCanvas() * local;
                    if (map.GetGlobalRect().HasPoint(global) && view.Pick(local) == expected)
                        return global;
                }
        return null;
    }
}
