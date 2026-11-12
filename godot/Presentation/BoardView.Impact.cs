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
            HealingAura? aura = null;
            if (healing)
            {
                aura = new HealingAura { Name = "Aura" };
                effect.AddChild(aura);
            }
            var number = NumberLabel((healing ? "+" : "-") + amount.ToString(CultureInfo.InvariantCulture),
                healing ? new Color("#eaffee") : Colors.White, healing ? new Color("#1c5c33") : new Color("#7a1208"), healing ? -34 : -36, size: 18);
            var origin = number.Position;
            effect.AddChild(number);
            ImpactPulse? pulse = null;
            Polygon2D? burst = null;
            if (!healing)
            {
                pulse = new ImpactPulse { Name = "Pulse", Position = new Vector2(0, -12) };
                effect.AddChild(pulse);
                effect.MoveChild(pulse, 0);
                burst = new Polygon2D
                {
                    Name = "Burst",
                    Position = new Vector2(0, -12),
                    Color = new Color("#fff0ce"),
                    Polygon = Enumerable.Range(0, 8).Select(index =>
                        Vector2.FromAngle(index * Mathf.Tau / 8 - Mathf.Pi / 4) * (index % 2 == 0 ? 15 : 3)).ToArray()
                };
                effect.AddChild(burst);
                effect.MoveChild(burst, sprite is null ? 1 : 2);
            }
            var duration = ImpactDuration(healing);
            return new ImpactVisual(effect, t =>
            {
                if (sprite is not null)
                    SetFrame(sprite, art!, StripFrame(art!, t * duration));
                aura?.Sample(t);
                pulse?.Sample(t);
                number.Position = origin + new Vector2(0, (float)(-(healing ? 16 : 14) * t));
                var fade = Math.Max(0, (t - .5) * 2);
                number.Modulate = new Color(1, 1, 1, (float)(1 - fade * fade));
                if (burst is not null)
                {
                    var strike = Math.Min(1, t / .28);
                    burst.Scale = Vector2.One * (float)(0.7 + strike * .65);
                    burst.Modulate = new Color(1, 1, 1, (float)(0.9 * (1 - strike)));
                }
            });
        }
        catch { RemoveEffect(effect); throw; }
    }

    private async Task<bool> AnimateImpact(Playback playback, Vector2 at, double amount, bool healing, bool fatal, string? topic,
        Node2D? recipient = null)
    {
        var reaction = !healing && amount > 0 && recipient is not null ? PrepareActorPose(recipient, "hit") : null;
        var impact = CreateImpactVisual(playback, at, amount, healing, topic);
        var impactDuration = ImpactDuration(healing);
        var duration = Math.Max(impactDuration, reaction?.Clip.Length ?? 0);
        var reactionComplete = false;
        void Sample(double elapsed)
        {
            if (elapsed < impactDuration)
                impact.Sample(elapsed / impactDuration);
            else
                RemoveEffect(impact.Node);
            if (reaction is null || reactionComplete)
                return;
            if (elapsed < reaction.Clip.Length)
                reaction.Sample(elapsed);
            else
            {
                ResumeIdle(recipient!);
                reactionComplete = true;
            }
        }
        try
        {
            Sample(0);
            if (!await playback.Clock.TweenAsync(duration, t => Sample(t * duration)))
                return false;
        }
        finally { RemoveEffect(impact.Node); }
        return !fatal || await playback.Clock.WaitAsync(40);
    }
}
