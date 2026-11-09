#nullable enable
using System;
using Godot;

namespace Empire.Presentation;

/// <summary>Local silhouette accents sampled only by the committed action's clip clock.</summary>
public partial class ActorActionAccent : Node2D
{
    private ActorMotion motion;
    private float direction;
    private float strength;
    private float progress;

    public void Configure(ActorMotion actionMotion, Vector2 aim)
    {
        motion = actionMotion;
        direction = aim.IsZeroApprox() ? 0 : aim.Angle();
    }

    public void Sample(double elapsed, double release, double length)
    {
        var charging = elapsed < release;
        progress = (float)(charging ? elapsed / release : release == length ? 1 : (elapsed - release) / (length - release));
        strength = charging ? progress * 0.65f : 1 - progress;
        if (charging)
            progress = 1 - progress;
        Visible = elapsed < length && strength > 0;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var ink = new Color("#243644", strength * 0.8f);
        var light = new Color("#fff0ce", strength);
        if (motion == ActorMotion.Lunge)
        {
            // Motion determines the gesture, not the weapon's material or damage type.
            const int segments = 18;
            var points = new Vector2[(segments + 1) * 2];
            var radius = 16 + progress * 5;
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                var angle = direction - 1.35f + t * 2.2f;
                var edge = Vector2.FromAngle(angle);
                points[i] = edge * radius;
                points[points.Length - 1 - i] = edge * (radius - .35f - MathF.Sin(t * MathF.PI) * 4.15f);
            }
            DrawColoredPolygon(points, light);
            DrawArc(Vector2.Zero, radius + 1, direction - 1.35f, direction + 0.85f, segments + 1, ink, 1, true);
        }
        else
        {
            var radius = 8 + progress * 7;
            var points = new[] { new Vector2(0, -radius), new Vector2(radius * .65f, 0),
                new Vector2(0, radius), new Vector2(-radius * .65f, 0), new Vector2(0, -radius) };
            DrawPolyline(points, ink, 3, true);
            DrawPolyline(points, light, 1.5f, true);
            for (var side = -1; side <= 1; side += 2)
                DrawLine(new Vector2(side * (radius + 3), 3), new Vector2(side * (radius + 3), -5 - progress * 5), light, 2, true);
        }
    }
}
