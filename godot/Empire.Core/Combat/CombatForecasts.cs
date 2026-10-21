using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed record CombatModifier(string Id, string Label, string Source, string Stage, string Operation, double Value);
public sealed record DamageBreakdown(string Weapon, string DamageType, double Base, double Mitigation,
    IReadOnlyList<CombatModifier> Modifiers, double Damage)
{
    public double FactorOf(string id, double absent = 1) => Modifiers.FirstOrDefault(value => value.Id == id)?.Value ?? absent;
    public DamageBreakdown And(CombatModifier modifier) => this with
    {
        Modifiers = Modifiers.Append(modifier).ToArray(),
        Damage = Math.Max(1, StateJson.Round(Damage * modifier.Value)),
    };
}
public sealed record CombatReaction(int Unit, string Stance, int? ProtectedUnit = null);
public sealed record CombatForecast(int Attacker, int Defender, DamageBreakdown Strike, int DamageRecipient,
    int? Interceptor, CombatReaction? Reaction, double RecipientHpAfter, bool RecipientDies,
    double DefenderHpAfter, bool DefenderDies, DamageBreakdown? Counter, double AttackerHpAfter, bool AttackerDies);
public sealed record StructureCombatForecast(int Attacker, string Structure, string Weapon, double Base,
    double CommanderAttackMultiplier, double TargetBonusMultiplier, double RawDamage, double Damage, double HpAfter, bool Destroyed);
public sealed record ReactiveStrike(JsonObject Weapon, DamageBreakdown Damage);

public sealed class CombatForecasts
{
    private readonly StateContent content;
    private readonly GridRegistry grids;
    private readonly BattleResources resources;
    private readonly Dictionary<(string Damage, string Armor), double> matchups = new();
    public ReactionRegistry Reactions { get; }
    private readonly CombatModifierRules modifierRules;
    public CombatModifierPipeline Modifiers { get; }
    public CombatForecasts(ReadOnlyContent catalog, GridRegistry grids, BattleResources resources,
        CombatModifierPipeline modifiers, ReactionRegistry reactions)
    {
        var packs = catalog.CopyPacks();
        content = catalog.Source;
        this.grids = grids;
        this.resources = resources;
        Modifiers = modifiers;
        Reactions = reactions;
        modifierRules = new(catalog, grids, Effectiveness);
        foreach (var pack in StateJson.Objects(packs, "packs"))
            foreach (var entry in StateJson.Objects(pack["damageMatchups"], "damageMatchups"))
            {
                var key = (Damage: StateJson.Text(entry, "damageType"), Armor: StateJson.Text(entry, "armorClass"));
                double multiplier;
                try { multiplier = StateJson.Number(entry, "multiplier", 1); }
                catch (DomainInvariantException)
                { throw new DomainInvariantException($"damage matchup \"{key.Damage}\" -> \"{key.Armor}\" must be > 0"); }
                if (multiplier <= 0)
                    throw new DomainInvariantException($"damage matchup \"{key.Damage}\" -> \"{key.Armor}\" must be > 0");
                if (!matchups.TryAdd(key, multiplier))
                    throw new DomainInvariantException($"damage matchup already registered: \"{key.Damage}\" -> \"{key.Armor}\"");
            }
    }
    public ReactionBehavior Reaction(string id) => Reactions.Get(id);
    public IReadOnlyList<string> ReactionIds => Reactions.Keys();
    public double Effectiveness(string damageType, string armorClass) =>
        matchups.GetValueOrDefault((damageType, armorClass), 1);
    public ITacticalGrid Grid(JsonObject state) => grids.Get(StateJson.Text(state["rules"]!.AsObject(), "grid"));
    public IReadOnlyList<JsonObject> UnitWeapons(JsonObject unit) => content.Get("units", StateJson.Text(unit, "type"))["weapons"]!
        .AsArray().Select(id => content.Get("weapons", id!.GetValue<string>())).ToArray();
    public JsonObject PrimaryWeapon(JsonObject unit) => UnitWeapons(unit).FirstOrDefault()
        ?? throw new DomainInvariantException($"{StateJson.Text(unit, "type")} has no weapon");
    public JsonObject? ReadyWeapon(JsonObject state, JsonObject unit, string id) =>
        UnitWeapons(unit).FirstOrDefault(weapon => StateJson.Text(weapon, "id") == id && IsWeaponReady(state, unit, weapon));
    public JsonObject RequireReadyWeapon(JsonObject state, JsonObject unit, string id) => ReadyWeapon(state, unit, id)
        ?? throw new DomainInvariantException($"{StateJson.Text(content.Get("units", StateJson.Text(unit, "type")), "name")} cannot fire weapon \"{id}\" right now");
    public bool IsWeaponReady(JsonObject state, JsonObject unit, JsonObject weapon)
    {
        var id = StateJson.Text(weapon, "id");
        if (!UnitWeapons(unit).Any(value => StateJson.Text(value, "id") == id)) return false;
        if (unit["weaponState"]?[id] is not JsonObject runtime || StateJson.Number(runtime, "cooldownRemaining", 0) > 0) return false;
        var context = ResourceContext(state, unit, id);
        return resources.CanAffordTransactions(context, Transactions(weapon["resourceRequirements"]!.AsArray()))
            && resources.CanAffordTransactions(context, Transactions(weapon["resourceCosts"]!.AsArray()));
    }
    private static ResourceTransactionContext ResourceContext(JsonObject state, JsonObject unit, string weapon) =>
        new(unit, weapon, TacticalQueries.Player(state, StateJson.Integer(unit, "owner")));

    private static IEnumerable<ResourceTransaction> Transactions(JsonArray transactions) =>
        StateJson.Objects(transactions, "transactions").Select(value => new ResourceTransaction(
            StateJson.Text(value, "subject"), StateJson.Text(value, "resource"), StateJson.Number(value, "amount", 0)));

    public void ConsumeWeapon(JsonObject state, JsonObject unit, string id, Action<JsonObject> emit)
    {
        var weapon = RequireReadyWeapon(state, unit, id);
        var context = ResourceContext(state, unit, id);
        foreach (var cost in Transactions(weapon["resourceCosts"]!.AsArray()))
        {
            var subject = resources.SubjectFor(context, cost);
            var amount = resources.Spend(subject, cost.Resource, cost.Amount);
            resources.Announce(subject, cost.Resource, -amount, emit);
        }
        var runtime = unit["weaponState"]![id]!.AsObject();
        runtime["cooldownRemaining"] = Math.Max(StateJson.Number(runtime, "cooldownRemaining", 0), StateJson.Number(weapon, "cooldown", 0));
    }
    public bool CanReachWithWeapon(JsonObject state, JsonObject weapon, Coord from, Coord target)
    {
        var distance = Grid(state).Distance(from, target);
        return distance >= StateJson.Number(weapon, "minRange", 0) && distance <= StateJson.Number(weapon, "maxRange", 0);
    }
    public static bool CanReact(JsonObject state, JsonObject unit) => StateJson.Number(unit, "reactionUsedRound", -1) != StateJson.Number(state, "turn", 0);
    public static void ConsumeReaction(JsonObject state, JsonObject unit) => unit["reactionUsedRound"] = state["turn"]!.DeepClone();

    public DamageBreakdown ComputeDamage(JsonObject state, JsonObject attacker, JsonObject defender,
        Coord? attackerAt = null, Coord? defenderAt = null, string? weapon = null)
    {
        var from = attackerAt ?? TacticalQueries.Position(attacker);
        var to = defenderAt ?? TacticalQueries.Position(defender);
        var selected = RequireReadyWeapon(state, attacker, weapon ?? StateJson.Text(PrimaryWeapon(attacker), "id"));
        var battlefield = new TacticalBattlefield(state, content);
        var context = new UnitDamageContext(modifierRules, state, attacker, from, defender, to, selected,
            battlefield, new TacticalBoard(state["map"]!.AsObject(), Grid(state)));
        var result = Modifiers.Evaluate(StateJson.Number(selected, "power", 0), context);
        return new(StateJson.Text(selected, "id"), StateJson.Text(selected, "damageType"),
            StateJson.Number(selected, "power", 0), result.Mitigation, result.Modifiers, result.Damage);
    }

    public StructureCombatForecast ForecastStructure(JsonObject state, JsonObject attacker, JsonObject structure, string? weapon = null)
    {
        var selected = RequireReadyWeapon(state, attacker, weapon ?? StateJson.Text(PrimaryWeapon(attacker), "id"));
        var definition = content.Get("structures", StateJson.Text(structure, "type"));
        var commander = TacticalModifiers.CommanderAura(Grid(state), state, attacker).AttackMultiplier;
        var bonus = StateJson.Objects(selected["bonuses"], "bonuses").Where(value => definition["tags"]!.AsArray()
            .Any(tag => tag!.GetValue<string>() == StateJson.Text(value, "targetTag"))).Aggregate(1.0, (result, value) => result * StateJson.Number(value, "multiplier", 1));
        var power = StateJson.Number(selected, "power", 0);
        var raw = Math.Max(1, StateJson.Round(power * DefaultCombatModifiers.AttackerStrength(content, attacker) * CombatEffects.CalculateModifiers(content, attacker).AttackMultiplier * commander * bonus));
        var hp = StateJson.Number(structure, "hp", 0);
        var damage = Math.Min(hp, Math.Max(1, StateJson.Round(raw * (1 - StateJson.Number(definition, "defense", 0)))));
        var after = Math.Max(0, hp - damage);
        return new(StateJson.Integer(attacker, "id"), StateJson.Text(structure, "id"), StateJson.Text(selected, "id"), power, commander, bonus, raw, damage, after, after <= 0);
    }
    public ReactiveStrike? BestReactiveStrike(JsonObject state, JsonObject reactor, Coord reactorAt, JsonObject target, Coord targetAt, ReactionBehavior stance) =>
        UnitWeapons(reactor).Where(weapon => !(stance.ConservesResources && (weapon["resourceCosts"]!.AsArray().Count > 0 || StateJson.Number(weapon, "cooldown", 0) > 0))
            && StateJson.IsTrue(weapon["canCounter"]) && IsWeaponReady(state, reactor, weapon) && CanReachWithWeapon(state, weapon, reactorAt, targetAt))
        .Select(weapon => new ReactiveStrike(weapon, ComputeDamage(state, reactor, target, reactorAt, targetAt, StateJson.Text(weapon, "id"))))
        .OrderByDescending(value => value.Damage.Damage).ThenBy(value => StateJson.Text(value.Weapon, "id"), StringComparer.Ordinal).FirstOrDefault();

    public CombatForecast Forecast(JsonObject state, JsonObject attacker, JsonObject defender, Coord? from = null, string? weapon = null)
    {
        var origin = from ?? TacticalQueries.Position(attacker);
        var defenderAt = TacticalQueries.Position(defender);
        var interceptor = StateJson.Objects(state["units"], "units").Where(candidate =>
            StateJson.Integer(candidate, "id") != StateJson.Integer(defender, "id")
            && TacticalQueries.AreAllies(state, StateJson.Integer(candidate, "owner"), StateJson.Integer(defender, "owner"))
            && Reaction(StateJson.Text(candidate, "reaction")).Intercepts && CanReact(state, candidate)
            && Grid(state).Distance(TacticalQueries.Position(candidate), defenderAt) == 1)
            .OrderByDescending(candidate => StateJson.Number(candidate, "hp", 0)).ThenBy(candidate => StateJson.Integer(candidate, "id")).FirstOrDefault();
        var recipient = interceptor ?? defender;
        var strike = ComputeDamage(state, attacker, recipient, origin, TacticalQueries.Position(recipient), weapon);
        var stance = Reaction(StateJson.Text(defender, "reaction"));
        CombatReaction? reaction = null;
        if (interceptor is not null) reaction = new(StateJson.Integer(interceptor, "id"), StateJson.Text(interceptor, "reaction"), StateJson.Integer(defender, "id"));
        else if (stance.IncomingMultiplier != 1 && CanReact(state, defender))
        {
            strike = strike.And(new("reaction." + stance.Id, stance.Name, "reaction", "final", "multiply", stance.IncomingMultiplier));
            reaction = new(StateJson.Integer(defender, "id"), stance.Id);
        }
        var recipientHp = Math.Max(0, StateJson.Number(recipient, "hp", 0) - strike.Damage);
        var defenderHp = ReferenceEquals(recipient, defender) ? recipientHp : StateJson.Number(defender, "hp", 0);
        DamageBreakdown? counter = null;
        var attackerHp = StateJson.Number(attacker, "hp", 0);
        if (defenderHp > 0 && stance.Retaliates && StateJson.IsTrue(state["rules"]!["counterAttack"]))
        {
            var source = (JsonObject)defender.DeepClone(); source["hp"] = defenderHp;
            var target = (JsonObject)attacker.DeepClone(); target["facing"] = Grid(state).Toward(origin, defenderAt);
            counter = BestReactiveStrike(state, source, defenderAt, target, origin, stance)?.Damage;
            if (counter is not null) attackerHp = Math.Max(0, attackerHp - counter.Damage);
        }
        return new(StateJson.Integer(attacker, "id"), StateJson.Integer(defender, "id"), strike,
            StateJson.Integer(recipient, "id"), interceptor is null ? null : StateJson.Integer(interceptor, "id"), reaction,
            recipientHp, recipientHp <= 0, defenderHp, defenderHp <= 0, counter, attackerHp, attackerHp <= 0);
    }
}
