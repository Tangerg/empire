#nullable enable
using System;

namespace Empire.Presentation;

public readonly record struct CameraPoint(double X, double Y);
public readonly record struct CameraInsets(double Left, double Top, double Right, double Bottom);

/// <summary>
/// Presentation-only scroll geometry. The screen applies FieldOrigin and Zoom to its
/// single world transform; the board and canonical battle state remain unchanged.
/// </summary>
public sealed class BattlefieldCamera
{
    public const double InitialZoom = 1.25;
    public const double MinimumZoom = 0.5;
    public const double MaximumZoom = 2.2;

    private readonly CameraPoint fieldSize;
    private readonly CameraInsets sceneInsets;
    private readonly CameraPoint initialFocus;
    private CameraPoint viewport;
    private CameraInsets padding;
    private double pendingZoom;

    public BattlefieldCamera(CameraPoint fieldSize, CameraInsets sceneInsets, CameraPoint initialFocus)
    {
        RequirePoint(fieldSize, nameof(fieldSize));
        if (fieldSize.X <= 0 || fieldSize.Y <= 0) throw new ArgumentOutOfRangeException(nameof(fieldSize));
        RequireInsets(sceneInsets, nameof(sceneInsets));
        RequirePoint(initialFocus, nameof(initialFocus));
        this.fieldSize = fieldSize;
        this.sceneInsets = sceneInsets;
        this.initialFocus = initialFocus;
    }

    public double Zoom { get; private set; } = InitialZoom;
    public CameraPoint Scroll { get; private set; }
    public bool HasViewport { get; private set; }
    public CameraPoint FieldOrigin => new(
        SceneOrigin.X + sceneInsets.Left * Zoom - Scroll.X,
        SceneOrigin.Y + sceneInsets.Top * Zoom - Scroll.Y);
    public CameraPoint Position => ToField(UsefulCenter);

    private CameraPoint SceneSize => new(
        (fieldSize.X + sceneInsets.Left + sceneInsets.Right) * Zoom,
        (fieldSize.Y + sceneInsets.Top + sceneInsets.Bottom) * Zoom);
    private CameraPoint SceneOrigin => new(
        padding.Left + Math.Max(0, (viewport.X - padding.Left - padding.Right - SceneSize.X) / 2),
        padding.Top + Math.Max(0, (viewport.Y - padding.Top - padding.Bottom - SceneSize.Y) / 2));
    private CameraPoint UsefulCenter => new(
        (padding.Left + viewport.X - padding.Right) / 2,
        (padding.Top + viewport.Y - padding.Bottom) / 2);

    /// <summary>Only the first usable layout chooses focus. Later layouts retain scroll pixels.</summary>
    public void Resize(CameraPoint viewportSize, CameraInsets viewportPadding = default)
    {
        RequirePoint(viewportSize, nameof(viewportSize));
        RequireInsets(viewportPadding, nameof(viewportPadding));
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;
        viewport = viewportSize;
        padding = viewportPadding;
        if (HasViewport) ScrollTo(Scroll);
        else
        {
            HasViewport = true;
            CenterOn(initialFocus);
        }
    }

    public void CenterOn(CameraPoint fieldPoint)
    {
        RequirePoint(fieldPoint, nameof(fieldPoint));
        if (!HasViewport) return;
        ScrollTo(new CameraPoint(
            SceneOrigin.X + (fieldPoint.X + sceneInsets.Left) * Zoom - UsefulCenter.X,
            SceneOrigin.Y + (fieldPoint.Y + sceneInsets.Top) * Zoom - UsefulCenter.Y));
    }

    public void Pan(CameraPoint screenDelta)
    {
        RequirePoint(screenDelta, nameof(screenDelta));
        if (HasViewport) ScrollTo(new CameraPoint(Scroll.X - screenDelta.X, Scroll.Y - screenDelta.Y));
    }

    public void Reveal(CameraPoint fieldPoint, double screenMargin)
    {
        RequirePoint(fieldPoint, nameof(fieldPoint));
        if (!double.IsFinite(screenMargin) || screenMargin < 0)
            throw new ArgumentOutOfRangeException(nameof(screenMargin));
        if (!HasViewport) return;
        var x = FieldOrigin.X + fieldPoint.X * Zoom;
        var y = FieldOrigin.Y + fieldPoint.Y * Zoom;
        var right = viewport.X - padding.Right;
        var bottom = viewport.Y - padding.Bottom;
        var marginX = Math.Min(screenMargin, Math.Max(0, (right - padding.Left) / 2));
        var marginY = Math.Min(screenMargin, Math.Max(0, (bottom - padding.Top) / 2));
        var dx = x - Math.Max(padding.Left + marginX, Math.Min(x, right - marginX));
        var dy = y - Math.Max(padding.Top + marginY, Math.Min(y, bottom - marginY));
        if (dx != 0 || dy != 0) ScrollTo(new CameraPoint(Scroll.X + dx, Scroll.Y + dy));
    }

    public void QueueZoom(double delta)
    {
        if (!double.IsFinite(delta) || !double.IsFinite(pendingZoom + delta))
            throw new ArgumentOutOfRangeException(nameof(delta));
        pendingZoom += delta;
    }

    /// <summary>Call once per presentation frame, not once per wheel event.</summary>
    public bool ApplyPendingZoom()
    {
        var step = pendingZoom;
        pendingZoom = 0;
        var next = Math.Clamp(Zoom + step, MinimumZoom, MaximumZoom);
        if (next == Zoom) return false;
        var center = Position;
        Zoom = next;
        if (HasViewport) CenterOn(center);
        return true;
    }

    /// <summary>Inverse world transform only; playable-cell validity belongs to the injected board/session.</summary>
    public CameraPoint ToField(CameraPoint viewportPoint)
    {
        RequirePoint(viewportPoint, nameof(viewportPoint));
        return new CameraPoint((viewportPoint.X - FieldOrigin.X) / Zoom, (viewportPoint.Y - FieldOrigin.Y) / Zoom);
    }

    private void ScrollTo(CameraPoint point) => Scroll = new CameraPoint(
        Math.Clamp(point.X, 0, Math.Max(0, SceneSize.X + padding.Left + padding.Right - viewport.X)),
        Math.Clamp(point.Y, 0, Math.Max(0, SceneSize.Y + padding.Top + padding.Bottom - viewport.Y)));

    private static void RequirePoint(CameraPoint point, string name)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(name);
    }

    private static void RequireInsets(CameraInsets value, string name)
    {
        if (!double.IsFinite(value.Left) || !double.IsFinite(value.Top) || !double.IsFinite(value.Right)
            || !double.IsFinite(value.Bottom) || value.Left < 0 || value.Top < 0 || value.Right < 0 || value.Bottom < 0)
            throw new ArgumentOutOfRangeException(name);
    }
}
