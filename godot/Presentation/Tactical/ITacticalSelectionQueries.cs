#nullable enable
using System.Collections.Generic;

namespace Empire.Presentation.Tactical;

public readonly record struct TacticalStamp(long Binding, long Revision);
public sealed record TacticalUnit(int Id, BoardCell At, bool CanAct, bool Visible);
public sealed record TacticalCommand(string Key, string Ability, string? Weapon,
    bool SelfTargeted, bool Hostile, IReadOnlyList<BoardCell> Targets);
public sealed record TacticalAttack(double StrikeDamage, double SplashDamage, double CounterDamage,
    double TerrainDefense, bool HasPrimaryUnit, IReadOnlyList<BoardCell> AffectedCells);
public sealed record TacticalTactic(string Commander, string Id, IReadOnlyList<BoardCell> Targets);
public sealed record TacticalPassenger(int Id, IReadOnlyList<BoardCell> Spots);

/// <summary>
/// Revision-bound, detached canonical answers. Ordered collections preserve canonical order.
/// The adapter must reject a changed revision rather than mixing facts from separate snapshots.
/// No method commits, computes presentation choices, or exposes mutable battle objects.
/// </summary>
public interface ITacticalSelectionQueries
{
    TacticalStamp Stamp
    {
        get;
    }
    string Phase
    {
        get;
    }
    bool HumanInput
    {
        get;
    }
    bool HumanTurn
    {
        get;
    }
    bool CanUndo
    {
        get;
    }
    /// <summary>Immutable in-bounds cells whose canonical terrain is not tagged outside.</summary>
    IReadOnlySet<BoardCell> InteractiveCells
    {
        get;
    }
    TacticalUnit? Unit(int id);
    TacticalUnit? UnitAt(BoardCell at);
    bool AreEnemies(int firstUnit, int secondUnit);
    bool IsEnemyOfViewer(int unit);
    bool CanOpenRecruitment(BoardCell at);
    IReadOnlyList<int> Actors();
    IReadOnlyList<BoardCell> MoveStops(int unit);
    IReadOnlyList<BoardCell>? PathTo(int unit, BoardCell at);
    IReadOnlyList<BoardCell> Threat(int unit);
    IReadOnlyList<BoardCell> ControlZone(int unit);
    IReadOnlyList<TacticalCommand> CommandsAt(int unit, BoardCell at);
    TacticalAttack Attack(int unit, BoardCell target, BoardCell from, string weapon);
    IReadOnlyList<int> DeploymentUnits();
    IReadOnlyList<BoardCell> DeploymentZone();
    IReadOnlyList<BoardCell> DeploymentSpots(int unit);
    IReadOnlyList<TacticalTactic> Tactics();
    IReadOnlyList<TacticalPassenger> Passengers(int carrier);
    /// <summary>In-bounds steps in configured grid direction order, not sorted coordinates.</summary>
    IReadOnlyList<BoardCell> DirectionSteps(BoardCell from);
}
