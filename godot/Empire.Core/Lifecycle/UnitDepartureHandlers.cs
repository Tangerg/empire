using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Geometry;
using Empire.Core.Registries;
using Empire.Core.State;

namespace Empire.Core.Lifecycle;

/// <summary>Explicit read services and the existing aftermath writer available to departure consequences.</summary>
public sealed record UnitDepartureRules(ReadOnlyContent Content, GridRegistry Grids,
    BattleDamage Damage, UnitDepartureHandlerRegistry UnitDepartures);

public sealed record UnitDeparture(UnitDepartureRules Rules, JsonObject State, JsonObject Unit, Action<JsonObject> Emit);
public sealed record UnitDepartureHandler(string Id, Action<UnitDeparture> Handle);

public sealed class UnitDepartureHandlerRegistry : KeyedRegistry<UnitDepartureHandler, UnitDepartureHandlerRegistry>
{
    public UnitDepartureHandlerRegistry() : base("departure handler") { }
    protected override string KeyOf(UnitDepartureHandler handler) => handler.Id;
    public UnitDepartureHandlerRegistry Clone() => CopyInto(new());

    public void Announce(UnitDeparture departure)
    {
        foreach (var handler in All()) handler.Handle(departure);
    }
}

public static class DefaultUnitDepartureHandlers
{
    public static UnitDepartureHandler CommanderDefeat { get; } = new("commander.defeat", departure =>
        departure.Rules.Damage.HandleCommanderDefeat(departure.State,
            StateJson.Integer(departure.Unit, "id"), departure.Emit));

    public static UnitDepartureHandler CastingCancellation { get; } = new("casting.cancel", departure =>
        departure.Rules.Damage.CancelCastOf(departure.State,
            StateJson.Integer(departure.Unit, "id"), departure.Emit));

    public static UnitDepartureHandlerRegistry Create() => new UnitDepartureHandlerRegistry()
        .Register(CommanderDefeat).Register(CastingCancellation);
}
