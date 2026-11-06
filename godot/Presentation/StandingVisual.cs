#nullable enable
using System;
using System.Linq;
using Godot;

namespace Empire.Presentation;

/// <summary>Visible geometry and authored front contact consumed by the board's single depth/pick owner.</summary>
public abstract class StandingVisual(Node2D node)
{
    public Node2D Node { get; } = node;
    /// <summary>Material coverage decides which rendered body is visible, never which canonical cell it targets.</summary>
    public abstract bool Covers(Vector2 globalPoint);
    public abstract Rect2 GlobalBounds
    {
        get;
    }
    // Projection preparation may run while its owner is hidden. Only descendants'
    // authored visibility changes the stored geometry, not the owner's screen state.
    internal static bool IsVisibleWithin(CanvasItem node, CanvasItem root)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
        {
            if (current == root)
                return true;
            if (current is CanvasItem item && !item.Visible)
                return false;
        }
        throw new InvalidOperationException("Projected visual is outside its visibility owner.");
    }

    internal static Rect2 BoundsOf(Vector2[] points)
    {
        var bounds = new Rect2(points[0], Vector2.Zero);
        foreach (var point in points.Skip(1))
            bounds = bounds.Expand(point);
        return bounds;
    }
    internal static void ValidatePolygon(Vector2[] points, string identity)
    {
        if (points.Distinct().Count() != points.Length || Geometry2D.TriangulatePolygon(points).Length == 0)
            throw new InvalidOperationException($"Standing material polygon is degenerate: {identity}.");
        static double Cross(Vector2 a, Vector2 b, Vector2 c) =>
            ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
        static bool OnSegment(Vector2 point, Vector2 a, Vector2 b) => Cross(a, b, point) == 0
            && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X)
            && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);
        for (var vertex = 0; vertex < points.Length; vertex++)
        {
            var previous = points[(vertex + points.Length - 1) % points.Length];
            var next = points[(vertex + 1) % points.Length];
            if (OnSegment(previous, points[vertex], next) || OnSegment(next, previous, points[vertex]))
                throw new InvalidOperationException($"Standing material polygon doubles back: {identity}.");
        }
        for (var first = 0; first < points.Length; first++)
            for (var second = first + 1; second < points.Length; second++)
            {
                var firstEnd = (first + 1) % points.Length;
                var secondEnd = (second + 1) % points.Length;
                if (firstEnd == second || secondEnd == first)
                    continue;
                if (Geometry2D.SegmentIntersectsSegment(points[first], points[firstEnd], points[second], points[secondEnd]).VariantType != Variant.Type.Nil
                    || OnSegment(points[first], points[second], points[secondEnd])
                    || OnSegment(points[firstEnd], points[second], points[secondEnd])
                    || OnSegment(points[second], points[first], points[firstEnd])
                    || OnSegment(points[secondEnd], points[first], points[firstEnd]))
                    throw new InvalidOperationException($"Standing material polygon crosses itself: {identity}.");
            }
    }

    /// <summary>Pointer coverage is tested in render-depth order; covered identities stay hidden while explicit blockers remain effective.</summary>
    public abstract bool ContainsPointer(Vector2 globalPoint);
    /// <summary>Conservative visible-pointer overlap for placing an independent cue without covering this target.</summary>
    public abstract bool IntersectsPointer(Vector2[] globalPolygon);
    protected abstract Vector2 ParentFrontOffset
    {
        get;
    }

    public Vector2 FrontOffset(Node2D owner)
    {
        var offset = ParentFrontOffset;
        return Node == owner ? offset : owner.Transform * (Node.Position + offset) - owner.Position;
    }
}

public sealed class SpriteStandingVisual(Sprite2D sprite, bool receivesPointer = true, Vector2[]? localPointer = null) : StandingVisual(sprite)
{
    public override bool Covers(Vector2 globalPoint) => sprite.IsPixelOpaque(sprite.ToLocal(globalPoint));
    public override Rect2 GlobalBounds
    {
        get
        {
            var rect = sprite.GetRect();
            return BoundsOf(new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) }.Select(sprite.ToGlobal).ToArray());
        }
    }
    public override bool ContainsPointer(Vector2 globalPoint) => receivesPointer && Covers(globalPoint)
        && (localPointer is null || Geometry2D.IsPointInPolygon(sprite.ToLocal(globalPoint), localPointer));
    public override bool IntersectsPointer(Vector2[] globalPolygon)
    {
        if (!receivesPointer)
            return false;
        // Placement may conservatively avoid transparent sprite canvas; actual pointer hits still use opaque pixels.
        var rect = sprite.GetRect();
        return Geometry2D.IntersectPolygons(globalPolygon.Select(sprite.ToLocal).ToArray(),
            localPointer ?? new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) }).Count > 0;
    }
    protected override Vector2 ParentFrontOffset => SpriteGroundContact.Offset(sprite);
}

public sealed class PolygonStandingVisual(Polygon2D polygon, Vector2 localFront, Vector2[]? localPointer) : StandingVisual(polygon)
{
    public override Rect2 GlobalBounds => BoundsOf(polygon.Polygon.Select(polygon.ToGlobal).ToArray());
    public override bool Covers(Vector2 globalPoint) => polygon.SelfModulate.A > 0
        && Geometry2D.IsPointInPolygon(polygon.ToLocal(globalPoint), polygon.Polygon);
    public override bool ContainsPointer(Vector2 globalPoint) => localPointer is not null
        && Geometry2D.IsPointInPolygon(polygon.ToLocal(globalPoint), localPointer);
    public override bool IntersectsPointer(Vector2[] globalPolygon) => polygon.SelfModulate.A > 0 && localPointer is not null
        && Geometry2D.IntersectPolygons(localPointer, globalPolygon.Select(polygon.ToLocal).ToArray()).Count > 0;
    protected override Vector2 ParentFrontOffset => polygon.Transform * localFront - polygon.Position;
}
