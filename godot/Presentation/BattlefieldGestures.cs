#nullable enable
using System;

namespace Empire.Presentation;

/// <summary>
/// Source letterbox pointer policy, independent of platform event capture. Button IDs
/// are DOM IDs (0 primary, 1 middle, 2 secondary). Boolean results mean prevent default.
/// </summary>
public sealed class BattlefieldGestures : IDisposable
{
    private sealed class Gesture(long id, int button, CameraPoint start)
    {
        public long Id { get; } = id;
        public int Button { get; } = button;
        public CameraPoint Start { get; } = start;
        public CameraPoint Last { get; set; } = start;
        public bool Dragging { get; set; }
    }

    private readonly Func<CameraPoint, CameraPoint?> project;
    private readonly Action<CameraPoint, int> press;
    private readonly Action<CameraPoint?> hover;
    private readonly Action leave;
    private readonly Action<CameraPoint> pan;
    private readonly Action<double> scaleNotches;
    private Gesture? gesture;
    private bool disposed;

    public BattlefieldGestures(Func<CameraPoint, CameraPoint?> project, Action<CameraPoint, int> press,
        Action<CameraPoint?> hover, Action leave, Action<CameraPoint> pan, Action<double> scaleNotches)
    {
        this.project = project ?? throw new ArgumentNullException(nameof(project));
        this.press = press ?? throw new ArgumentNullException(nameof(press));
        this.hover = hover ?? throw new ArgumentNullException(nameof(hover));
        this.leave = leave ?? throw new ArgumentNullException(nameof(leave));
        this.pan = pan ?? throw new ArgumentNullException(nameof(pan));
        this.scaleNotches = scaleNotches ?? throw new ArgumentNullException(nameof(scaleNotches));
    }

    public long? ActivePointerId => gesture?.Id;
    public bool IsDragging => gesture?.Dragging ?? false;

    public bool PointerDown(long id, int button, CameraPoint at, bool isPrimary = true)
    {
        if (disposed || gesture is not null || !isPrimary) return false;
        if (button is 0 or 1)
        {
            gesture = new Gesture(id, button, at);
            return true;
        }
        if (project(at) is { } point) press(point, button);
        return false;
    }

    public bool PointerMove(long id, CameraPoint at)
    {
        if (disposed) return false;
        if (gesture is not null) return MoveGesture(id, at);
        hover(project(at));
        return false;
    }

    public bool PointerUp(long id, int button, CameraPoint at)
    {
        if (disposed || gesture is null || gesture.Id != id || gesture.Button != button) return false;
        var prevented = MoveGesture(id, at);
        var click = gesture is { Dragging: false, Button: 0 };
        gesture = null;
        if (click && project(at) is { } point) press(point, 0);
        return prevented;
    }

    public void PointerLeave()
    {
        if (!disposed && gesture is null) leave();
    }

    /// <summary>Use matching ID for capture loss; omit it for screen cancellation/focus loss.</summary>
    public void Cancel(long? id = null)
    {
        if (id is null || gesture?.Id == id) gesture = null;
    }

    public bool Wheel(double deltaY, bool ctrl, bool meta)
    {
        if (disposed || (!ctrl && !meta)) return false;
        scaleNotches(-Math.Sign(deltaY));
        return true;
    }

    public void Dispose()
    {
        disposed = true;
        gesture = null;
    }

    private bool MoveGesture(long id, CameraPoint at)
    {
        var active = gesture;
        if (active is null || active.Id != id) return false;
        var dx = at.X - active.Last.X;
        var dy = at.Y - active.Last.Y;
        var fromStartX = at.X - active.Start.X;
        var fromStartY = at.Y - active.Start.Y;
        if (!active.Dragging && Math.Sqrt(fromStartX * fromStartX + fromStartY * fromStartY) >= 6)
        {
            active.Dragging = true;
            leave();
        }
        if (!active.Dragging) return false;
        pan(new CameraPoint(dx, dy));
        active.Last = at;
        return true;
    }
}
