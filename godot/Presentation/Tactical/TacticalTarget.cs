#nullable enable
namespace Empire.Presentation.Tactical;

public enum TacticalTargetKind
{
    Cell, Unit, Object
}

/// <summary>Canonical command subject, distinct from the renderer's pointer visibility decision.</summary>
public sealed record TacticalTarget
{
    public BoardCell Cell
    {
        get;
    }
    public TacticalTargetKind Kind
    {
        get;
    }
    public int? UnitId
    {
        get;
    }

    private TacticalTarget(BoardCell cell, TacticalTargetKind kind, int? unitId = null)
    {
        Cell = cell;
        Kind = kind;
        UnitId = unitId;
    }

    public static TacticalTarget AtCell(BoardCell cell) => new(cell, TacticalTargetKind.Cell);
    public static TacticalTarget Unit(BoardCell cell, int unitId) => new(cell, TacticalTargetKind.Unit, unitId);
    public static TacticalTarget Object(BoardCell cell) => new(cell, TacticalTargetKind.Object);

    public bool Accepts(TacticalHit hit) => Cell == hit.Cell && (Kind switch
    {
        TacticalTargetKind.Cell => true,
        TacticalTargetKind.Unit => hit.Kind == TacticalHitKind.Unit && UnitId == hit.UnitId,
        TacticalTargetKind.Object => hit.Kind == TacticalHitKind.Object,
        _ => throw new System.InvalidOperationException("Unknown tactical target kind."),
    });
}
