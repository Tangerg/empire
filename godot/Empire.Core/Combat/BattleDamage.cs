using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed record DamageOutcome(
    bool Landed, double Amount, double HpAfter, bool Killed, bool LeftField, Coord At);

/// <summary>
/// Owns a blow and its complete aftermath, including departures caused by morale.
/// Callers report the cause; this owner reports and settles every consequence.
/// </summary>
public sealed class BattleDamage
{
    private static readonly DamageOutcome NothingToHit = new(false, 0, 0, false, true, new Coord(-1, -1));
    private readonly StateContent content;
    private readonly GridRegistry grids;
    private readonly CombatEffects effects;
    private readonly UnitDepartureRules departureRules;

    public BattleDamage(ReadOnlyContent catalog, GridRegistry grids, CombatEffects effects, UnitDepartureHandlerRegistry unitDepartures)
    {
        content = catalog.Source;
        this.grids = grids;
        this.effects = effects;
        departureRules = new(catalog, grids, this, unitDepartures);
    }

    public DamageOutcome Resolve(
        JsonObject state, int unitId, double requested, bool nonlethal,
        Func<DamageOutcome, JsonObject> report, Action<JsonObject> emit)
    {
        var target = TacticalQueries.UnitById(state, unitId);
        if (target is null) return NothingToHit;

        var at = TacticalQueries.Position(target);
        var hp = StateJson.Number(target["hp"], "hp");
        var amount = Math.Max(0, StateJson.Round(requested));
        if (nonlethal) amount = Math.Min(amount, Math.Max(0, hp - 1));
        if (amount <= 0) return new DamageOutcome(true, 0, hp, false, false, at);
        if (!double.IsFinite(amount))
            throw new DomainInvariantException($"damage must be a finite non-negative number, got {amount}");

        amount = Math.Min(hp, amount);
        var hpAfter = Math.Max(0, hp - amount);
        target["hp"] = hpAfter;
        var killed = hpAfter <= 0;
        JsonObject? casualty = null;
        JsonObject? corpse = null;
        IReadOnlyList<JsonObject> passengerMarkers = Array.Empty<JsonObject>();
        if (killed)
        {
            casualty = (JsonObject)target.DeepClone();
            ClearCaptureAt(state, at);
            corpse = CreateMarker(state, target, "corpse", at);
            RemoveUnit(state, target);
            passengerMarkers = ExtractPassengerMarkers(state, unitId, at, "transport-loss");
        }

        var blow = new DamageOutcome(true, amount, hpAfter, killed, killed, at);
        emit(report(blow));
        if (casualty is not null && corpse is not null)
        {
            emit(new JsonObject { ["type"] = "death", ["unit"] = unitId, ["at"] = Coordinate(at) });
            AnnounceMarker(corpse, emit);
            foreach (var marker in passengerMarkers) AnnounceMarker(marker, emit);
            if (passengerMarkers.Count > 0)
            {
                var passengers = new JsonArray();
                foreach (var marker in passengerMarkers)
                    passengers.Add(StateJson.Integer(StateJson.Object(marker["fallenUnit"], "fallenUnit"), "id"));
                emit(new JsonObject
                {
                    ["type"] = "transportLost", ["carrier"] = unitId,
                    ["passengers"] = passengers, ["at"] = Coordinate(at),
                });
            }
            AnnounceDeparture(state, casualty, emit);
            MournFallen(state, target, at, emit);
        }
        else
        {
            SufferDamageShock(state, target, amount, emit);
        }
        return blow with { LeftField = TacticalQueries.UnitById(state, unitId) is null };
    }

    public double DamageStatus(
        JsonObject state, JsonObject unit, JsonObject status, double requested, bool nonlethal,
        Action<JsonObject> emit)
    {
        var id = StateJson.Integer(unit, "id");
        return Resolve(state, id, requested, nonlethal, blow => new JsonObject
        {
            ["type"] = "statusTick", ["unit"] = id, ["status"] = StateJson.Text(status, "id"),
            ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter,
        }, emit).Amount;
    }

    public double ChangeMorale(
        JsonObject state, int unitId, double requested, string reason, Action<JsonObject> emit)
    {
        var unit = TacticalQueries.RequireUnit(state, unitId);
        var morale = StateJson.Object(unit["morale"], "morale");
        var adjusted = StateJson.Round(requested < 0
            ? requested * (1 - StateJson.Number(morale["resilience"], "resilience"))
            : requested);
        var before = StateJson.Number(morale["current"], "current morale");
        var after = Math.Max(0, Math.Min(StateJson.Number(morale["maximum"], "maximum morale"), before + adjusted));
        morale["current"] = after;
        var applied = after - before;
        if (applied != 0)
            emit(new JsonObject
            {
                ["type"] = "moraleChanged", ["unit"] = unitId,
                ["amount"] = applied, ["current"] = after, ["reason"] = reason,
            });
        if (MoraleEnabled(state) && after <= 0 && TacticalQueries.UnitById(state, unitId) is not null)
            Route(state, unit, emit);
        return applied;
    }

    public JsonObject Surrender(JsonObject state, int unitId, int? to, Action<JsonObject> emit)
    {
        var unit = TacticalQueries.RequireUnit(state, unitId);
        var at = TacticalQueries.Position(unit);
        var marker = CreateMarker(state, unit, "surrendered", at);
        RemoveUnit(state, unit);
        WithdrawPassengers(state, unitId, at, "surrendered", emit);
        AnnounceMarker(marker, emit);
        var surrendered = new JsonObject
        {
            ["type"] = "unitSurrendered", ["unit"] = unitId,
            ["marker"] = StateJson.Integer(marker, "id"), ["at"] = Coordinate(at),
        };
        if (to is not null) surrendered["to"] = to.Value;
        emit(surrendered);
        AnnounceDeparture(state, unit, emit);
        return marker;
    }

    public JsonObject Withdraw(JsonObject state, int unitId, bool leaveCorpse, Action<JsonObject> emit)
        => Withdraw(state, TacticalQueries.RequireUnit(state, unitId), leaveCorpse, emit);

    public JsonObject Withdraw(JsonObject state, JsonObject unit, bool leaveCorpse, Action<JsonObject> emit)
    {
        var unitId = StateJson.Integer(unit, "id");
        var at = TacticalQueries.Position(unit);
        var marker = CreateMarker(state, unit, leaveCorpse ? "corpse" : "withdrawn", at);
        AnnounceMarker(marker, emit);
        RemoveUnit(state, unit);
        WithdrawPassengers(state, unitId, at, "withdrawn", emit);
        AnnounceDeparture(state, unit, emit);
        emit(new JsonObject { ["type"] = "unitWithdrawn", ["unit"] = unitId, ["at"] = Coordinate(at) });
        return marker;
    }

    /// <summary>
    /// Settles a departure already performed by a caller. Default handlers run in
    /// source registration order, so commander-induced routs precede cast cancellation.
    /// </summary>
    public void AnnounceDeparture(JsonObject state, JsonObject unit, Action<JsonObject> emit) =>
        departureRules.UnitDepartures.Announce(new(departureRules, state, unit, emit));

    internal void CancelCastOf(JsonObject state, int unitId, Action<JsonObject> emit)
    {
        var casts = StateJson.Array(state["pendingCasts"], "pendingCasts");
        var cast = casts.OfType<JsonObject>().FirstOrDefault(candidate => StateJson.Integer(candidate, "caster") == unitId);
        if (cast is null) return;
        casts.Remove(cast);
        emit(new JsonObject
        {
            ["type"] = "castCancelled", ["unit"] = StateJson.Integer(cast, "caster"),
            ["weapon"] = StateJson.Text(cast, "weapon"), ["at"] = cast["target"]!.DeepClone(),
            ["reason"] = "casterLost",
        });
    }

    private void SufferDamageShock(JsonObject state, JsonObject target, double damage, Action<JsonObject> emit)
    {
        if (!MoraleEnabled(state)) return;
        var id = StateJson.Integer(target, "id");
        if (TacticalQueries.UnitById(state, id) is null) return;
        var maximumHp = StateJson.Number(content.Get("units", StateJson.Text(target, "type"))["maxHp"], "maxHp");
        var morale = StateJson.Object(target["morale"], "morale");
        var rules = StateJson.Object(state["rules"], "rules");
        var loss = Math.Max(1, StateJson.Round(damage / maximumHp *
            StateJson.Number(morale["maximum"], "maximum morale") *
            StateJson.Number(rules["moraleDamageFactor"], "moraleDamageFactor")));
        ChangeMorale(state, id, -loss, "damage", emit);
    }

    private void MournFallen(JsonObject state, JsonObject fallen, Coord at, Action<JsonObject> emit)
    {
        if (!MoraleEnabled(state)) return;
        var rules = StateJson.Object(state["rules"], "rules");
        var grid = grids.Get(StateJson.Text(rules, "grid"));
        var radius = StateJson.Number(rules["moraleDefeatShockRadius"], "moraleDefeatShockRadius");
        var owner = StateJson.Integer(fallen, "owner");
        var mourners = StateJson.Objects(state["units"], "units").Where(unit =>
            TacticalQueries.AreAllies(state, StateJson.Integer(unit, "owner"), owner) &&
            grid.Distance(TacticalQueries.Position(unit), at) <= radius).ToArray();
        foreach (var mourner in mourners)
            ChangeMorale(state, StateJson.Integer(mourner, "id"),
                -StateJson.Number(rules["moraleAllyDefeatLoss"], "moraleAllyDefeatLoss"), "ally-defeated", emit);
    }

    internal void HandleCommanderDefeat(JsonObject state, int unitId, Action<JsonObject> emit)
    {
        var commander = StateJson.Objects(state["commanders"], "commanders")
            .FirstOrDefault(candidate => StateJson.Integer(candidate, "unitId") == unitId);
        if (commander is null) return;
        var commanderId = StateJson.Text(commander, "id");
        var owner = StateJson.Integer(commander, "owner");
        emit(new JsonObject { ["type"] = "commanderDefeated", ["commander"] = commanderId, ["unit"] = unitId });
        var followers = StateJson.Objects(state["units"], "units").Where(candidate =>
            candidate["commanderId"]?.GetValue<string>() == commanderId &&
            StateJson.Integer(candidate, "owner") == owner).ToArray();
        foreach (var follower in followers)
        {
            if (content.TryGet("statuses", "shaken") is not null)
                effects.AddStatus(follower, "shaken", 2, unitId, emit);
            var followerId = StateJson.Integer(follower, "id");
            if (MoraleEnabled(state) && TacticalQueries.UnitById(state, followerId) is not null)
                ChangeMorale(state, followerId,
                    -StateJson.Number(StateJson.Object(state["rules"], "rules")["moraleCommanderDefeatLoss"],
                        "moraleCommanderDefeatLoss"), "commander-defeated", emit);
        }
    }

    private void Route(JsonObject state, JsonObject unit, Action<JsonObject> emit)
    {
        var id = StateJson.Integer(unit, "id");
        var at = TacticalQueries.Position(unit);
        var marker = CreateMarker(state, unit, "routed", at);
        RemoveUnit(state, unit);
        WithdrawPassengers(state, id, at, "routed", emit);
        AnnounceMarker(marker, emit);
        emit(new JsonObject
        {
            ["type"] = "unitRouted", ["unit"] = id,
            ["marker"] = StateJson.Integer(marker, "id"), ["at"] = Coordinate(at),
        });
        AnnounceDeparture(state, unit, emit);
    }

    private static bool MoraleEnabled(JsonObject state) =>
        StateJson.IsTrue(StateJson.Object(state["rules"], "rules")["moraleEnabled"]);

    private static JsonObject Coordinate(Coord at) => TacticalQueries.CoordinateJson(at);

    private static JsonObject CreateMarker(JsonObject state, JsonObject unit, string kind, Coord at)
    {
        var id = StateJson.Integer(state, "nextMarkerId");
        state["nextMarkerId"] = id + 1;
        var marker = new JsonObject
        {
            ["id"] = id, ["kind"] = kind, ["at"] = Coordinate(at),
            ["owner"] = StateJson.Integer(unit, "owner"), ["fallenUnit"] = unit.DeepClone(),
        };
        StateJson.Array(state["markers"], "markers").Add(marker);
        return marker;
    }

    private static void RemoveUnit(JsonObject state, JsonObject unit)
    {
        var standing = TacticalQueries.UnitById(state, StateJson.Integer(unit, "id"));
        if (standing is null) return;
        ClearCaptureAt(state, TacticalQueries.Position(standing));
        StateJson.Array(state["units"], "units").Remove(standing);
    }

    private static void ClearCaptureAt(JsonObject state, Coord at)
    {
        var map = StateJson.Object(state["map"], "map");
        var extent = new GridExtent(StateJson.Integer(map, "width"), StateJson.Integer(map, "height"));
        if (!GridStorage.InBounds(extent, at.X, at.Y))
            throw new DomainInvariantException($"cell ({at.X}, {at.Y}) is outside the map");
        StateJson.Array(map["captureProgress"], "captureProgress")[GridStorage.Index(extent, at.X, at.Y)] = 0;
    }

    private static IReadOnlyList<JsonObject> ExtractPassengerMarkers(
        JsonObject state, int carrier, Coord at, string kind)
    {
        var embarked = StateJson.Array(state["embarkedUnits"], "embarkedUnits");
        var entries = embarked.OfType<JsonObject>()
            .Where(entry => StateJson.Integer(entry, "carrier") == carrier).ToArray();
        var markers = new List<JsonObject>();
        foreach (var entry in entries)
        {
            var unit = StateJson.Object(entry["unit"], "embarked unit");
            markers.Add(CreateMarker(state, unit, kind, at));
        }
        foreach (var entry in entries) embarked.Remove(entry);
        return markers;
    }

    private static void WithdrawPassengers(
        JsonObject state, int carrier, Coord at, string kind, Action<JsonObject> emit)
    {
        var embarked = StateJson.Array(state["embarkedUnits"], "embarkedUnits");
        var entries = embarked.OfType<JsonObject>()
            .Where(entry => StateJson.Integer(entry, "carrier") == carrier).ToArray();
        foreach (var entry in entries)
        {
            var unit = StateJson.Object(entry["unit"], "embarked unit");
            AnnounceMarker(CreateMarker(state, unit, kind, at), emit);
        }
        foreach (var entry in entries) embarked.Remove(entry);
    }

    private static void AnnounceMarker(JsonObject marker, Action<JsonObject> emit) => emit(new JsonObject
    {
        ["type"] = "markerAdded", ["marker"] = StateJson.Integer(marker, "id"),
        ["kind"] = StateJson.Text(marker, "kind"), ["at"] = marker["at"]!.DeepClone(),
    });
}
