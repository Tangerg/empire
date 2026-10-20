#nullable enable
using System.Collections.Generic;
using System.Text.Json;

namespace Empire.Presentation.Tactical;

public enum TacticalHudIntentKind { Command, Tactic, Recruit, DeployPick, Disembark, Back, Action }
public sealed record TacticalHudIntent(TacticalStamp Stamp, TacticalHudIntentKind Kind, string? Key = null,
    int? Unit = null, JsonElement? Action = null);
public sealed record TacticalHudRow(string Key, string Label, string Hint = "", bool Enabled = true,
    bool Selected = false, TacticalHudIntent? Intent = null, string? Icon = null, TacticalHudPortrait? UnitIcon = null, TacticalHudGauge? Gauge = null);
public sealed record TacticalHudSection(string Key, string Title, IReadOnlyList<TacticalHudRow> Rows);
/// <summary>Detached immutable presentation facts. No widget may originate a battle transition.</summary>
public sealed record TacticalHudGauge(double Value, double Maximum);
public sealed record TacticalHudPortrait(string UnitType, string? ActorKey, string TeamColor);
public sealed record TacticalHudProjection(TacticalStamp Stamp, string Hint, IReadOnlyList<TacticalHudSection> Sections, TacticalHudPortrait? Portrait = null, bool Busy = false);
