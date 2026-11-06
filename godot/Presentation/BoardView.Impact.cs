#nullable enable
using System;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using Godot;

namespace Empire.Presentation;

public partial class BoardView
{
    private sealed record ImpactVisual(Node2D Node, Action<double> Sample);
    private static double ImpactDuration(bool healing) => healing ? 500.0 : 420.0;

    private ImpactVisual CreateImpactVisual(Playback playback, Vector2 at, double amount, bool healing, string? topic)
    {
        var effect = new Node2D { Name = healing ? "Heal" : "Hit", Position = at };
        try
        {
            playback.Effects.AddChild(effect);
            RuntimeAsset? art = topic is null ? null : _assets!.ResolveEffect(topic);
            Sprite2D? sprite = art is null ? null : _assets!.CreateSpriteAtFrame(art, 0);
            if (sprite is not null)
            {
                sprite.Name = "Strip";
                sprite.SetMeta("frame", 0);
                effect.AddChild(sprite);
            }
            var number = NumberLabel((healing ? "+" : "-") + amount.ToString(CultureInfo.InvariantCulture),
                healing ? new Color("#eaffee") : Colors.White, healing ? new Color("#1c5c33") : new Color("#7a1208"), healing ? -6 : -8);
            var origin = number.Position;
            effect.AddChild(number);
            Polygon2D? burst = null;
            if (!healing)
            {
                burst = new Polygon2D
                {
                    Name = "Burst",
                    Color = new Color(1, 1, 1, 0.65f),
                    Polygon = Enumerable.Range(0, 48).Select(index => Vector2.FromAngle(index * Mathf.Tau / 48) * 12).ToArray()
                };
                effect.AddChild(burst);
                effect.MoveChild(burst, sprite is null ? 0 : 1);
            }
            var duration = ImpactDuration(healing);
            return new ImpactVisual(effect, t =>
            {
                if (sprite is not null)
                    SetFrame(sprite, art!, StripFrame(art!, t * duration));
                number.Position = origin + new Vector2(0, (float)(-(healing ? 16 : 14) * t));
                number.Modulate = new Color(1, 1, 1, (float)(1 - t * t));
                if (burst is not null)
                {
                    burst.Scale = Vector2.One * (float)(1 + t * 0.83);
                    burst.Modulate = new Color(1, 1, 1, (float)(0.8 * (1 - t)));
                }
            });
        }
        catch { RemoveEffect(effect); throw; }
    }

    private async Task<bool> AnimateImpact(Playback playback, Vector2 at, double amount, bool healing, bool fatal, string? topic)
    {
        var impact = CreateImpactVisual(playback, at, amount, healing, topic);
        try
        {
            if (!await playback.Clock.TweenAsync(ImpactDuration(healing), impact.Sample))
                return false;
        }
        finally { RemoveEffect(impact.Node); }
        return !fatal || await playback.Clock.WaitAsync(40);
    }
}
