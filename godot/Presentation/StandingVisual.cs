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
    // Projection preparation may run beneath a hidden screen; only visibility owned inside the board belongs to its geometry.
    internal static bool IsVisibleWithin(CanvasItem node, CanvasItem root)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
        {
            if (current is CanvasItem item && !item.Visible)
                return false;
            if (current == root)
                return true;
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

public sealed class SpriteStandingVisual(Sprite2D sprite) : StandingVisual(sprite)
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
    public override bool ContainsPointer(Vector2 globalPoint) => Covers(globalPoint);
    public override bool IntersectsPointer(Vector2[] globalPolygon)
    {
        // Placement may conservatively avoid transparent sprite canvas; actual pointer hits still use opaque pixels.
        var rect = sprite.GetRect();
        return Geometry2D.IntersectPolygons(globalPolygon.Select(sprite.ToLocal).ToArray(),
            new[] { rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y) }).Count > 0;
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
