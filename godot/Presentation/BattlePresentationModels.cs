#nullable enable
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace Empire.Presentation;

public readonly record struct BoardCell(int X, int Y);

/// <summary>Detached committed facts, not an instruction to recompute or apply an action.</summary>
public sealed record BattlePresentationTransition
{
    public long Revision { get; }
    public JsonElement Before { get; }
    public JsonElement Action { get; }
    public IReadOnlyList<JsonElement> Events { get; }
    public JsonElement After { get; }
    public bool IsComputerAction { get; init; }
    public IReadOnlySet<int>? BeforeVisibleUnitIds { get; }
    public IReadOnlyList<string> EventDescriptions { get; }

    public BattlePresentationTransition(long Revision, JsonElement Before, JsonElement Action,
        IReadOnlyList<JsonElement> Events, JsonElement After, bool IsComputerAction,
        IReadOnlySet<int>? BeforeVisibleUnitIds = null, IReadOnlyList<string>? EventDescriptions = null)
    {
        if (EventDescriptions is not null && EventDescriptions.Count != Events.Count)
            throw new System.ArgumentException("Event descriptions must correspond to the ordered receipt.", nameof(EventDescriptions));
        this.Revision = Revision;
        this.Before = Before.Clone();
        this.Action = Action.Clone();
        this.Events = new ReadOnlyCollection<JsonElement>(Events.Select(value => value.Clone()).ToArray());
        this.After = After.Clone();
        this.IsComputerAction = IsComputerAction;
        this.BeforeVisibleUnitIds = BeforeVisibleUnitIds?.ToFrozenSet();
        this.EventDescriptions = System.Array.AsReadOnly(EventDescriptions?.ToArray() ?? Enumerable.Repeat("", Events.Count).ToArray());
    }
}

/// <summary>State must be a detached JsonElement.Clone(); its JSON is immutable to the view.</summary>
public sealed record BattleSnapshot(long Revision, JsonElement State, IReadOnlySet<int>? VisibleUnitIds = null,
    IReadOnlySet<BoardCell>? VisibleCells = null, IReadOnlyDictionary<BoardCell, double>? Incoming = null, string? ObjectiveSummary = null,
    BattlePresentationTransition? Transition = null, string? InitialLogMessage = null);

public sealed record BattleDispatchResult(bool Accepted, string Message);
