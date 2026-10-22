#nullable enable
using System.Text.Json;
using Empire.Presentation.Tactical;

namespace Empire.Presentation;

/// <summary>Native tactical presentation port; all answers and commits share one session.</summary>
public interface ITacticalBattleSession
{
    /// <summary>Live session freshness stamp without constructing a battlefield projection.</summary>
    long Revision
    {
        get;
    }
    BattleSnapshot GetSnapshot();
    BattleDispatchResult Dispatch(JsonElement action, long expectedRevision);
    ITacticalSelectionQueries GetTacticalQueries(long binding);
    TacticalHudProjection ProjectHud(TacticalStamp stamp, TacticalSelection selection, BoardCell? cursor, int? inspectId, bool busy);
    BattleDispatchResult Undo(long expectedRevision);
}
