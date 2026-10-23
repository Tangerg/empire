#nullable enable
namespace Empire.Presentation.Tactical;

public enum TacticalHitKind
{
    Ground, Unit, Object
}

/// <summary>The subject resolved by presentation coverage, never inferred from cell occupancy.</summary>
public sealed record TacticalHit
{
    public BoardCell Cell
    {
        get;
    }
    public TacticalHitKind Kind
    {
        get;
    }
    public int? UnitId
    {
        get;
    }

    private TacticalHit(BoardCell cell, TacticalHitKind kind, int? unitId = null)
    {
        Cell = cell;
        Kind = kind;
        UnitId = unitId;
    }

    public static TacticalHit Ground(BoardCell cell) => new(cell, TacticalHitKind.Ground);
    public static TacticalHit Unit(BoardCell cell, int unitId) => new(cell, TacticalHitKind.Unit, unitId);
    public static TacticalHit Object(BoardCell cell) => new(cell, TacticalHitKind.Object);
}
