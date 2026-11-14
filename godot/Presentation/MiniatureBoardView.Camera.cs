#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Empire.Presentation;

public partial class MiniatureBoardView
{
    /// <summary>Reveal real 3D contacts inside the owner's HUD-safe rectangle. Only the existing
    /// camera owns pan/zoom; ray projection converts the required screen correction back to it.</summary>
    public bool RevealContacts(BattlefieldCamera owner, IReadOnlyList<Vector3> contacts, Vector2 size,
        bool center = false, float margin = 40)
    {
        if (contacts.Count == 0 || !owner.HasViewport || size.X <= 0 || size.Y <= 0)
            return false;
        var minimum = new Vector2((float)owner.SafeMinimum.X, (float)owner.SafeMinimum.Y) + Vector2.One * margin;
        var maximum = new Vector2((float)owner.SafeMaximum.X, (float)owner.SafeMaximum.Y) - Vector2.One * margin;
        if (maximum.X <= minimum.X || maximum.Y <= minimum.Y)
            return false;
        // Orthographic projection is affine. Re-evaluate after clamping to the existing camera limits.
        for (var iteration = 0; iteration < 3; iteration++)
        {
            SyncCamera(owner, size);
            var points = contacts.Select(_camera.UnprojectPosition).ToArray();
            var low = new Vector2(points.Min(p => p.X), points.Min(p => p.Y));
            var high = new Vector2(points.Max(p => p.X), points.Max(p => p.Y));
            var extent = high - low;
            var available = maximum - minimum;
            var ratio = Math.Min(1, Math.Min(available.X / Math.Max(.001f, extent.X), available.Y / Math.Max(.001f, extent.Y)));
            if (ratio < .999 && owner.Zoom > BattlefieldCamera.MinimumZoom)
            {
                owner.QueueZoom(owner.Zoom * (ratio - 1));
                owner.ApplyPendingZoom();
                continue;
            }
            float Shift(float lo, float hi, float min, float max) => hi - lo > max - min || center
                ? (lo + hi - min - max) / 2 : Math.Clamp(0, hi - max, lo - min);
            var correction = new Vector2(Shift(low.X, high.X, minimum.X, maximum.X), Shift(low.Y, high.Y, minimum.Y, maximum.Y));
            if (correction.LengthSquared() < .01f)
                return true;
            var screenCenter = size / 2;
            Vector3 PlanePoint(Vector2 point)
            {
                var origin = _camera.ProjectRayOrigin(point);
                var direction = _camera.ProjectRayNormal(point);
                return origin + direction * ((.6f - origin.Y) / direction.Y);
            }
            var delta = PlanePoint(screenCenter + correction) - PlanePoint(screenCenter);
            var logical = Geometry.Projection.LogicalGroundPoint(new((float)owner.Position.X, (float)owner.Position.Y));
            logical += new Vector2(delta.X, delta.Z) * BoardProjection.TileSize;
            var projected = Geometry.Projection.Project(logical);
            projected.Y += Geometry.Projection.SurfaceHeight(logical, projected);
            owner.CenterOn(new(projected.X, projected.Y));
            center = false;
        }
        SyncCamera(owner, size);
        return contacts.All(contact =>
        {
            var point = _camera.UnprojectPosition(contact);
            return point.X >= minimum.X - .5f && point.X <= maximum.X + .5f
                && point.Y >= minimum.Y - .5f && point.Y <= maximum.Y + .5f;
        });
    }
}
