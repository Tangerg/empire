#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Empire.Presentation;

/// <summary>Finite view transitions only. Cancellation resolves every waiter.</summary>
internal sealed class PresentationTimeline
{
    private sealed record Tween(double Started, double Duration, Action<double> Step, TaskCompletionSource<bool> Completion);
    private readonly List<Tween> waiting = new();
    private double time;
    private bool cancelled;

    public bool MotionEnabled { get; set; } = true;
    internal int PendingCount => waiting.Count;

    public Task<bool> TweenAsync(double milliseconds, Action<double> step)
    {
        if (cancelled) return Task.FromResult(false);
        if (!MotionEnabled || milliseconds <= 0)
        {
            step(1);
            return Task.FromResult(true);
        }
        var completion = new TaskCompletionSource<bool>();
        waiting.Add(new Tween(time, milliseconds / 1000, step, completion));
        return completion.Task;
    }

    public Task<bool> WaitAsync(double milliseconds) => TweenAsync(milliseconds, _ => { });

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        time += seconds;
        foreach (var tween in waiting.ToArray())
        {
            if (!waiting.Contains(tween)) continue;
            var progress = Math.Min(1, (time - tween.Started) / tween.Duration);
            try { tween.Step(progress); }
            catch (Exception error)
            {
                waiting.Remove(tween);
                tween.Completion.TrySetException(error);
                continue;
            }
            if (progress < 1) continue;
            waiting.Remove(tween);
            tween.Completion.TrySetResult(true);
        }
    }

    public void Cancel()
    {
        if (cancelled) return;
        cancelled = true;
        var pending = waiting.ToArray();
        waiting.Clear();
        foreach (var tween in pending) tween.Completion.TrySetResult(false);
    }
}
