#nullable enable
using Godot;

namespace Empire.Presentation;

/// <summary>Recipient-only recovery geometry; the impact clock owns its entire lifetime.</summary>
public partial class HealingAura : Node2D
{
    public void Sample(double progress)
    {
        var t = (float)progress;
        Position = new Vector2(0, -8 * t);
        Scale = Vector2.One * (1 + .4f * t);
        Modulate = new Color(1, 1, 1, Mathf.Sin(Mathf.Pi * t));
    }

    public override void _Draw()
    {
        var outline = new Color("#173f36", .65f);
        var glow = new Color("#82dba8", .6f);
        var light = new Color("#d6ffe4", .9f);
        Ring(19, 7, outline, 3);
        Ring(19, 7, glow, 1.5f);
        Ring(13, 4.5f, glow, 1);
        for (var index = -2; index <= 2; index++)
        {
            var center = new Vector2(index * 8, -12 - (2 - Mathf.Abs(index)) * 5);
            var vertical = new Vector2(0, 3);
            var horizontal = new Vector2(1.8f, 0);
            DrawLine(center - vertical, center + vertical, outline, 3, true);
            DrawLine(center - vertical, center + vertical, light, 1, true);
            DrawLine(center - horizontal, center + horizontal, light, 1, true);
        }
    }

    private void Ring(float width, float height, Color color, float thickness)
    {
        const int segments = 32;
        var points = new Vector2[segments + 1];
        for (var index = 0; index <= segments; index++)
            points[index] = Vector2.FromAngle(index * Mathf.Tau / segments) * new Vector2(width, height);
        DrawPolyline(points, color, thickness, true);
    }
}
