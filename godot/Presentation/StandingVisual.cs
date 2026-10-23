#nullable enable
using Godot;

namespace Empire.Presentation;

/// <summary>Visible geometry and authored front contact consumed by the board's single depth/pick owner.</summary>
public abstract class StandingVisual(Node2D node)
{
    public Node2D Node { get; } = node;
    /// <summary>Material coverage decides which rendered body is visible, never which canonical cell it targets.</summary>
    public abstract bool Covers(Vector2 globalPoint);
    /// <summary>Pointer coverage is tested in render-depth order; covered identities stay hidden while explicit blockers remain effective.</summary>
    public abstract bool ContainsPointer(Vector2 globalPoint);
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
    public override bool ContainsPointer(Vector2 globalPoint) => Covers(globalPoint);
    protected override Vector2 ParentFrontOffset => SpriteGroundContact.Offset(sprite);
}

public sealed class PolygonStandingVisual(Polygon2D polygon, Vector2 localFront, Vector2[]? localPointer) : StandingVisual(polygon)
{
    public override bool Covers(Vector2 globalPoint) => polygon.SelfModulate.A > 0
        && Geometry2D.IsPointInPolygon(polygon.ToLocal(globalPoint), polygon.Polygon);
    public override bool ContainsPointer(Vector2 globalPoint) => localPointer is not null
        && Geometry2D.IsPointInPolygon(polygon.ToLocal(globalPoint), localPointer);
    protected override Vector2 ParentFrontOffset => polygon.Transform * localFront - polygon.Position;
}
