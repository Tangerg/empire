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
public sealed record ReactionBehavior(string Id, string Name, bool Intercepts, double IncomingMultiplier, bool Retaliates, bool ConservesResources);
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
    private readonly Dictionary<string, ReactionBehavior> reactions = new(StringComparer.Ordinal);
    public CombatForecasts(JsonArray packs, GridRegistry grids, BattleResources resources,
        IEnumerable<ReactionBehavior>? reactionDefinitions = null)
    {
        content = new StateContent(packs);
        this.grids = grids;
        this.resources = resources;
        foreach (var pack in StateJson.Objects(packs, "packs"))
            foreach (var entry in StateJson.Objects(pack["damageMatchups"], "damageMatchups"))
            {
                var key = (StateJson.Text(entry, "damageType"), StateJson.Text(entry, "armorClass"));
                var multiplier = StateJson.Number(entry, "multiplier", 1);
                if (multiplier <= 0 || !matchups.TryAdd(key, multiplier))
                    throw new DomainInvariantException($"invalid or duplicate damage matchup {key}");
            }
        foreach (var reaction in reactionDefinitions ?? DefaultReactions())
            if (!reactions.TryAdd(reaction.Id, reaction)) throw new DomainInvariantException($"duplicate reaction {reaction.Id}");
    }
    public static IReadOnlyList<ReactionBehavior> DefaultReactions() => new[]
    {
        new ReactionBehavior("counter", "反击", false, 1, true, false),
        new ReactionBehavior("guard", "防御", false, 0.7, false, false),
        new ReactionBehavior("support", "援护", true, 1, true, false),
        new ReactionBehavior("conserve", "节制", false, 1, true, true),
    };
    public ReactionBehavior Reaction(string id) => reactions.TryGetValue(id, out var value) ? value
        : throw new DomainInvariantException($"unknown reaction stance \"{id}\"");
    public IReadOnlyList<string> ReactionIds => reactions.Keys.ToArray();
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
        return Affordable(state, unit, id, weapon["resourceRequirements"]!.AsArray())
            && Affordable(state, unit, id, weapon["resourceCosts"]!.AsArray());
    }
    private BattleResources.Subject Subject(JsonObject state, JsonObject unit, string weapon, string kind) => kind switch
    {
        "player" => new(kind, TacticalQueries.Player(state, StateJson.Integer(unit, "owner"))),
        "unit" => new(kind, unit), "weapon" => new(kind, unit, weapon),
        _ => throw new DomainInvariantException($"unknown resource subject \"{kind}\""),
    };
    private bool Affordable(JsonObject state, JsonObject unit, string weapon, JsonArray transactions)
    {
        var totals = StateJson.Objects(transactions, "transactions")
            .GroupBy(value => (Kind: StateJson.Text(value, "subject"), Id: StateJson.Text(value, "resource")));
        return totals.All(group =>
        {
            var subject = Subject(state, unit, weapon, group.Key.Kind);
            return resources.HasAccount(subject, group.Key.Id)
                && resources.CanSpend(subject, group.Key.Id, group.Sum(value => StateJson.Number(value, "amount", 0)));
        });
    }
    public void ConsumeWeapon(JsonObject state, JsonObject unit, string id, Action<JsonObject> emit)
    {
        var weapon = RequireReadyWeapon(state, unit, id);
        foreach (var cost in StateJson.Objects(weapon["resourceCosts"], "resourceCosts"))
        {
            var subject = Subject(state, unit, id, StateJson.Text(cost, "subject"));
            var resource = StateJson.Text(cost, "resource");
            var amount = resources.Spend(subject, resource, StateJson.Number(cost, "amount", 0));
            resources.Announce(subject, resource, -amount, emit);
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
    private double Strength(JsonObject unit)
    {
        var maximum = StateJson.Number(content.Get("units", StateJson.Text(unit, "type")), "maxHp", 1);
        return 0.5 + 0.5 * (maximum > 0 ? Math.Clamp(StateJson.Number(unit, "hp", 0) / maximum, 0, 1) : 1);
    }

    public DamageBreakdown ComputeDamage(JsonObject state, JsonObject attacker, JsonObject defender,
        Coord? attackerAt = null, Coord? defenderAt = null, string? weapon = null)
    {
        var from = attackerAt ?? TacticalQueries.Position(attacker);
        var to = defenderAt ?? TacticalQueries.Position(defender);
        var selected = RequireReadyWeapon(state, attacker, weapon ?? StateJson.Text(PrimaryWeapon(attacker), "id"));
        var grid = Grid(state);
        var battlefield = new TacticalBattlefield(state, content);
        var rules = state["rules"]!.AsObject();
        var defenderDef = content.Get("units", StateJson.Text(defender, "type"));
        var modifiers = new List<CombatModifier>();
        void Add(string id, string label, string source, string stage, string operation, double value) =>
            modifiers.Add(new(id, label, source, stage, operation, value));
        Add("matchup.effectiveness", "伤害类型与护甲克制", "matchup", "power", "multiply",
            Effectiveness(StateJson.Text(selected, "damageType"), StateJson.Text(defenderDef, "armorClass")));
        foreach (var bonus in StateJson.Objects(selected["bonuses"], "bonuses"))
            if (defenderDef["tags"]!.AsArray().Any(tag => tag!.GetValue<string>() == StateJson.Text(bonus, "targetTag")))
                Add("weapon.target-tag." + StateJson.Text(bonus, "targetTag"), StateJson.Text(bonus, "reason"), "weapon", "power", "multiply", StateJson.Number(bonus, "multiplier", 1));
        Add("unit.hp-strength", "攻击者剩余生命", "unit", "power", "multiply", Strength(attacker));
        var rank = StateJson.Integer(attacker, "rank");
        Add("rank.attack", rank == 0 ? "新兵军衔" : rank == 1 ? "老兵军衔" : "精英军衔", "unit", "power", "multiply", 1 + rank * 0.04);
        Add("status.attack", "攻击方状态", "status", "power", "multiply", CombatEffects.CalculateModifiers(content, attacker).AttackMultiplier);
        Add("commander.attack", "攻击方指挥光环", "commander", "power", "multiply", TacticalModifiers.CommanderAura(grid, state, attacker).AttackMultiplier);
        var formation = TacticalModifiers.ActiveFormation(content, grid, state, attacker);
        if (formation is not null && StateJson.Number(formation, "attackMultiplier", 1) != 1)
            Add("formation.attack." + StateJson.Text(formation, "id"), StateJson.Text(formation, "name"), "formation", "power", "multiply", StateJson.Number(formation, "attackMultiplier", 1));
        var elevationDelta = battlefield.Cell(from).Elevation - battlefield.Cell(to).Elevation;
        if (elevationDelta >= StateJson.Number(rules, "highGroundThreshold", 0))
            Add("elevation.high-ground", $"高地优势（高 {elevationDelta} 级）", "elevation", "power", "multiply", StateJson.Number(rules, "highGroundDamageMultiplier", 1));
        var defenderPosition = TacticalQueries.Position(defender);
        if (grid.Distance(from, defenderPosition) == 1 && (selected["tags"]!.AsArray().Any(tag => tag!.GetValue<string>() == "melee") || StateJson.Number(selected, "maxRange", 1) <= 1))
        {
            var incoming = grid.Toward(defenderPosition, from);
            var facing = StateJson.Text(defender, "facing");
            if (incoming == grid.Opposite(facing)) Add("position.back-attack", "背刺", "position", "power", "multiply", StateJson.Number(rules, "backAttackMultiplier", 1));
            else if (incoming != facing) Add("position.side-attack", "侧击", "position", "power", "multiply", StateJson.Number(rules, "sideAttackMultiplier", 1));
            var behind = grid.Step(defenderPosition, grid.Opposite(incoming));
            var flanker = TacticalQueries.UnitAt(state, behind);
            if (flanker is not null && StateJson.Integer(flanker, "id") != StateJson.Integer(attacker, "id") && TacticalQueries.AreAllies(state, StateJson.Integer(flanker, "owner"), StateJson.Integer(attacker, "owner")))
                Add("position.flank", "夹击", "position", "power", "multiply", StateJson.Number(rules, "flankAttackMultiplier", 1));
        }
        if (grid.Distance(from, to) > 1 && StateJson.Text(selected, "lineOfSight") != "arc" && !selected["tags"]!.AsArray().Any(tag => tag!.GetValue<string>() == "ignores-cover"))
        {
            var cell = battlefield.Cell(to);
            static int Score(string value) => value == "full" ? 2 : value == "half" ? 1 : 0;
            var directional = cell.DirectionalCoverFrom(grid, from);
            var cover = Score(directional) > Score(cell.Cover) ? directional : cell.Cover;
            if (elevationDelta >= StateJson.Number(rules, "highGroundThreshold", 0)) cover = cover == "full" ? "half" : "none";
            var value = cover == "full" ? StateJson.Number(rules, "fullCoverDefense", 0) : cover == "half" ? StateJson.Number(rules, "halfCoverDefense", 0) : 0;
            if (value > 0) Add("cover." + cover, cover == "full" ? "全掩体" : "半掩体", "cover", "mitigation", "add", value);
        }
        var defenderStatus = CombatEffects.CalculateModifiers(content, defender);
        var defenderCommand = TacticalModifiers.CommanderAura(grid, state, defender);
        var defenderFormation = TacticalModifiers.ActiveFormation(content, grid, state, defender);
        var defense = Math.Max(0, StateJson.Number(defenderDef, "defense", 0) + StateJson.Number(defender, "rank", 0) * 0.02
            + defenderStatus.DefenseDelta + defenderCommand.DefenseDelta + (defenderFormation is null ? 0 : StateJson.Number(defenderFormation, "defenseDelta", 0)));
        Add("defense.terrain", "地形防御", "terrain", "mitigation", "add", battlefield.Cell(to).Defense);
        Add("defense.unit", "单位防御", "unit", "mitigation", "add", defense);
        var power = StateJson.Number(selected, "power", 0);
        double mitigation = 0, finalMultiplier = 1, finalOffset = 0;
        foreach (var modifier in modifiers)
        {
            if (modifier.Stage == "power") power = modifier.Operation == "multiply" ? power * modifier.Value : power + modifier.Value;
            else if (modifier.Stage == "mitigation") mitigation = modifier.Operation == "multiply" ? mitigation * modifier.Value : mitigation + modifier.Value;
            else if (modifier.Operation == "multiply") { finalMultiplier *= modifier.Value; finalOffset *= modifier.Value; }
            else finalOffset += modifier.Value;
        }
        mitigation = Math.Clamp(mitigation, 0, 0.6);
        var damage = Math.Max(1, StateJson.Round(power * (1 - mitigation) * finalMultiplier + finalOffset));
        return new(StateJson.Text(selected, "id"), StateJson.Text(selected, "damageType"), StateJson.Number(selected, "power", 0), mitigation, modifiers.AsReadOnly(), damage);
    }

    public StructureCombatForecast ForecastStructure(JsonObject state, JsonObject attacker, JsonObject structure, string? weapon = null)
    {
        var selected = RequireReadyWeapon(state, attacker, weapon ?? StateJson.Text(PrimaryWeapon(attacker), "id"));
        var definition = content.Get("structures", StateJson.Text(structure, "type"));
        var commander = TacticalModifiers.CommanderAura(Grid(state), state, attacker).AttackMultiplier;
        var bonus = StateJson.Objects(selected["bonuses"], "bonuses").Where(value => definition["tags"]!.AsArray()
            .Any(tag => tag!.GetValue<string>() == StateJson.Text(value, "targetTag"))).Aggregate(1.0, (result, value) => result * StateJson.Number(value, "multiplier", 1));
        var power = StateJson.Number(selected, "power", 0);
        var raw = Math.Max(1, StateJson.Round(power * Strength(attacker) * CombatEffects.CalculateModifiers(content, attacker).AttackMultiplier * commander * bonus));
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
