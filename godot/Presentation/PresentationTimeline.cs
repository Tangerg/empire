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
        if (cancelled)
            return Task.FromResult(false);
        if (!double.IsFinite(milliseconds))
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        if (!MotionEnabled || milliseconds <= 0)
        {
            step(1);
            return Task.FromResult(true);
        }
        var duration = milliseconds / 1000;
        if (!double.IsFinite(time + duration) || time + duration <= time)
            throw new InvalidOperationException("Tween duration cannot advance the current presentation time.");
        var completion = new TaskCompletionSource<bool>();
        waiting.Add(new Tween(time, duration, step, completion));
        return completion.Task;
    }

    public Task<bool> WaitAsync(double milliseconds) => TweenAsync(milliseconds, _ => { });

    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        var target = time + seconds;
        if (!double.IsFinite(target))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        // A completed waiter can enqueue the next phase. Its origin is the exact
        // completion boundary, not the end of a coarse frame that crossed it.
        while (waiting.Count > 0)
        {
            var boundary = double.PositiveInfinity;
            foreach (var tween in waiting)
                boundary = Math.Min(boundary, tween.Started + tween.Duration);
            if (boundary > target)
                break;
            time = boundary;
            Sample();
        }
        time = target;
        Sample();
    }

    private void Sample()
    {
        foreach (var tween in waiting.ToArray())
        {
            if (!waiting.Contains(tween))
                continue;
            var progress = time >= tween.Started + tween.Duration ? 1 : (time - tween.Started) / tween.Duration;
            try
            {
                tween.Step(progress);
            }
            catch (Exception error)
            {
                waiting.Remove(tween);
                tween.Completion.TrySetException(error);
                continue;
            }
            if (progress < 1)
                continue;
            waiting.Remove(tween);
            tween.Completion.TrySetResult(true);
        }
    }

    public void Cancel()
    {
        if (cancelled)
            return;
        cancelled = true;
        var pending = waiting.ToArray();
        waiting.Clear();
        foreach (var tween in pending)
            tween.Completion.TrySetResult(false);
    }
}
