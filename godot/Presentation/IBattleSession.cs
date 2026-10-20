#nullable enable
using System.Collections.Generic;
using System.Text.Json;

namespace Empire.Presentation;

public readonly record struct BoardCell(int X, int Y);

/// <summary>State must be a detached JsonElement.Clone(); its JSON is immutable to the view.</summary>
public sealed record BattleSnapshot(long Revision, JsonElement State, IReadOnlySet<int>? VisibleUnitIds = null,
    IReadOnlySet<BoardCell>? VisibleCells = null, IReadOnlyDictionary<BoardCell, double>? Incoming = null, string? ObjectiveSummary = null);

/// <summary>
/// The adapter supplies complete canonical actions, including any move-and-wait or
/// move-and-attack commitment. Target is a visual destination, never a legality claim by the UI.
/// </summary>
public sealed record BattleOrderChoice(
    string Label,
    string PresentationRole,
    BoardCell? Target,
    JsonElement Action);

public sealed record BattleDispatchResult(bool Accepted, string Message);

/// <summary>Rules and action revalidation belong exclusively to the injected session.</summary>
public interface IBattleSession
{
    BattleSnapshot GetSnapshot();
    IReadOnlyList<BattleOrderChoice> GetLegalOrders(int? selectedUnitId);
    BattleDispatchResult Dispatch(JsonElement action, long expectedRevision);
}
