using Empire.Presentation.Tactical;
namespace Empire.Presentation;

/// <summary>Native tactical presentation port; all answers and commits share one session.</summary>
public interface ITacticalBattleSession : IBattleSession
{
    ITacticalSelectionQueries GetTacticalQueries(long binding);
    TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, BoardCell? cursor, int? inspectId, bool busy);
    bool CanUndo { get; }
    BattleDispatchResult Undo(long expectedRevision);
}
