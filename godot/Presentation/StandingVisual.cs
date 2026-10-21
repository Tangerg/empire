#nullable enable
using Godot;

namespace Empire.Presentation;

/// <summary>Visible geometry and authored front contact consumed by the board's single depth/pick owner.</summary>
public abstract class StandingVisual(Node2D node)
{
    public Node2D Node { get; } = node;
    public abstract bool Contains(Vector2 globalPoint);
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
    public override bool Contains(Vector2 globalPoint) => sprite.IsPixelOpaque(sprite.ToLocal(globalPoint));
    protected override Vector2 ParentFrontOffset => SpriteGroundContact.Offset(sprite);
}

public sealed class PolygonStandingVisual(Polygon2D polygon, Vector2 localFront) : StandingVisual(polygon)
{
    public override bool Contains(Vector2 globalPoint) => Geometry2D.IsPointInPolygon(polygon.ToLocal(globalPoint), polygon.Polygon);
    protected override Vector2 ParentFrontOffset => polygon.Transform * localFront - polygon.Position;
}
