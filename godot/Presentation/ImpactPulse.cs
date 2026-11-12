#nullable enable
using Godot;

namespace Empire.Presentation;

/// <summary>Open impact silhouette sampled by the existing damage clock, never a second timeline.</summary>
public partial class ImpactPulse : Node2D
{
    public void Sample(double progress)
    {
        var t = (float)progress;
        Scale = Vector2.One * (.5f + t);
        Modulate = new Color(1, 1, 1, 1 - t);
    }

    public override void _Draw()
    {
        var ink = new Color("#3b2830", .8f);
        var light = new Color("#fff0cf", 1);
        for (var index = 0; index < 6; index++)
        {
            var angle = index * Mathf.Tau / 6 + Mathf.Pi / 12;
            DrawArc(Vector2.Zero, 20, angle, angle + .55f, 8, ink, 3, true);
            DrawArc(Vector2.Zero, 20, angle, angle + .55f, 8, light, 1.8f, true);
            var ray = Vector2.FromAngle(angle + .25f);
            DrawLine(ray * 26, ray * 34, ink, 3, true);
            DrawLine(ray * 26, ray * 34, light, 1.8f, true);
        }
    }
}
