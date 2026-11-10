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
    private bool recovering;

    public void Configure(ActorMotion actionMotion, Vector2 aim)
    {
        motion = actionMotion;
        direction = aim.IsZeroApprox() ? 0 : aim.Angle();
    }

    public void Sample(double elapsed, double release, double length)
    {
        recovering = elapsed >= release;
        var phase = (float)(!recovering ? elapsed / release : release == length ? 1 : (elapsed - release) / (length - release));
        strength = recovering ? 1 - phase : phase * 0.65f;
        progress = recovering ? phase : 1 - phase;
        Rotation = motion == ActorMotion.Lunge
            ? direction + (recovering ? phase * .65f : (phase - 1) * .65f)
            : 0;
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
            var radius = 16 + progress * 5;
            if (recovering)
            {
                var trail = new Color(light, strength * .35f);
                DrawRibbon(radius - 3, -1.7f, 2.05f, 1.6f, trail);
                DrawRibbon(radius - 6, -1.9f, 1.8f, .8f, trail);
            }
            DrawRibbon(radius, -1.35f, 2.2f, 4.15f, light);
            DrawArc(Vector2.Zero, radius + 1, -1.35f, .85f, segments + 1, ink, 1, true);
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

    private void DrawRibbon(float radius, float start, float span, float width, Color color)
    {
        const int segments = 18;
        var points = new Vector2[(segments + 1) * 2];
        for (var i = 0; i <= segments; i++)
        {
            var t = (float)i / segments;
            var edge = Vector2.FromAngle(start + t * span);
            points[i] = edge * radius;
            points[points.Length - 1 - i] = edge * (radius - .35f - MathF.Sin(t * MathF.PI) * width);
        }
        DrawColoredPolygon(points, color);
    }
}
