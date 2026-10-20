using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Lifecycle;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed partial class BattleCombat : ILifecycleEffects
{
    private readonly StateContent content;
    private readonly BattleResources resources;
    private readonly MovementRules movement;
    public CombatForecasts Forecasts { get; }
    public BattleDamage Damage { get; }
    public CombatEffects Effects { get; }
    public BattleCombat(JsonArray packs, GridRegistry grids, BattleResources resources, MovementRules movement)
    {
        content = new StateContent(packs);
        this.resources = resources;
        this.movement = movement;
        Forecasts = new CombatForecasts(packs, grids, resources);
        Damage = new BattleDamage(packs, grids);
        Effects = new CombatEffects(packs);
    }
    public bool CanUseAttack(JsonObject state, JsonObject unit, Coord at, bool moved, string? weapon = null)
    {
        var id = weapon ?? Forecasts.UnitWeapons(unit).FirstOrDefault()?["id"]?.GetValue<string>();
        if (id is null || Forecasts.ReadyWeapon(state, unit, id) is not { } selected) return false;
        return (!moved || StateJson.IsTrue(selected["moveAndAttack"]))
            && Effects.BlockedAbilityStatus(unit, selected["tags"]!.AsArray().Select(value => value!.GetValue<string>()).Prepend("attack")) is null;
    }
    public IReadOnlyList<Coord> AttackTargets(JsonObject state, JsonObject unit, Coord at, string? weapon = null)
    {
        var id = weapon ?? Forecasts.UnitWeapons(unit).FirstOrDefault()?["id"]?.GetValue<string>();
        if (id is null || Forecasts.ReadyWeapon(state, unit, id) is not { } selected) return Array.Empty<Coord>();
        return movement.AttackTargetCoords(state, unit, at, selected)
            .Where(target => HostileActionAllowed(state, StateJson.Integer(unit, "owner"), at, target)).ToArray();
    }
    public static bool HostileActionAllowed(JsonObject state, int actor, Coord from, Coord target)
    {
        var scenario = state["scenario"]!.AsObject();
        foreach (var rule in StateJson.Objects(scenario["engagementRules"], "engagementRules"))
        {
            if (rule["players"] is JsonArray players && players.Count > 0 && !players.Any(value => StateJson.Integer(value, "player") == actor)) continue;
            var cells = scenario["zones"]?[StateJson.Text(rule, "zone")];
            if (StateJson.Objects(cells, "zone").Any(cell => TacticalQueries.Position(cell) == from || TacticalQueries.Position(cell) == target)) return false;
        }
        return true;
    }
    private JsonObject? HostileStructure(JsonObject state, JsonObject attacker, Coord at)
    {
        var structure = TacticalQueries.StructureAt(state, at);
        if (structure is null || !StateJson.IsTrue(content.Get("structures", StateJson.Text(structure, "type"))["targetable"])) return null;
        var owner = StateJson.Integer(structure, "owner");
        return owner == 0 || TacticalQueries.AreEnemies(state, owner, StateJson.Integer(attacker, "owner")) ? structure : null;
    }
    public IReadOnlyList<Coord> Coverage(JsonObject state, Coord from, Coord aimedAt, string area)
    {
        var board = movement.Board(state);
        IEnumerable<Coord> cells = area switch
        {
            "single" => new[] { aimedAt },
            "cross1" => board.Ring(aimedAt, 0, 1),
            "ring1" => Enumerable.Range(-1, 3).SelectMany(y => Enumerable.Range(-1, 3).Select(x => new Coord(aimedAt.X + x, aimedAt.Y + y))),
            "line" => board.Grid.Line(from, aimedAt).Skip(1),
            _ => throw new DomainInvariantException($"unknown weapon area shape \"{area}\""),
        };
        return cells.Where(board.Contains).Distinct().ToArray();
    }
    public CombatPlan Forecast(JsonObject state, JsonObject attacker, Coord target, Coord? from = null, string? weapon = null)
    {
        var origin = from ?? TacticalQueries.Position(attacker);
        if (!HostileActionAllowed(state, StateJson.Integer(attacker, "owner"), origin, target))
            throw new DomainInvariantException("combat plan target is protected by an engagement rule");
        var id = weapon ?? StateJson.Text(Forecasts.PrimaryWeapon(attacker), "id");
        var selected = content.Get("weapons", id);
        var area = StateJson.Text(selected, "area");
        var primaryTarget = TacticalQueries.UnitAt(state, target);
        var structureTarget = HostileStructure(state, attacker, target);
        if (primaryTarget is null && structureTarget is null && area == "single")
            throw new DomainInvariantException("combat plan requires a hostile primary target");
        if (primaryTarget is not null && !TacticalQueries.AreEnemies(state, StateJson.Integer(primaryTarget, "owner"), StateJson.Integer(attacker, "owner")))
            throw new DomainInvariantException("combat plan cannot target an allied unit");
        var primary = primaryTarget is null ? null : Forecasts.Forecast(state, attacker, primaryTarget, origin, id);
        var structure = structureTarget is null ? null : Forecasts.ForecastStructure(state, attacker, structureTarget, id);
        var cells = Coverage(state, origin, target, area);
        var hits = new List<PlannedUnitHit>();
        var structures = new List<PlannedStructureHit>();
        var excluded = new HashSet<int>();
        var effects = StateJson.Objects(selected["hitEffects"], "hitEffects").ToArray();
        if (primary is not null && primaryTarget is not null)
        {
            var recipient = TacticalQueries.RequireUnit(state, primary.DamageRecipient);
            excluded.Add(StateJson.Integer(primaryTarget, "id")); excluded.Add(primary.DamageRecipient);
            hits.Add(new(primary.DamageRecipient, TacticalQueries.Position(recipient), true,
                primary.Interceptor is not null ? StateJson.Integer(primaryTarget, "id") : null,
                primary.Strike, StateJson.Number(recipient, "hp", 0), primary.RecipientHpAfter,
                primary.RecipientDies, primary.RecipientDies ? Array.Empty<JsonObject>() : effects));
        }
        if (structure is not null && structureTarget is not null)
            structures.Add(new(StateJson.Text(structureTarget, "id"), TacticalQueries.Position(structureTarget), true, structure));
        foreach (var cell in cells)
        {
            if (cell == target) continue;
            var unit = TacticalQueries.UnitAt(state, cell);
            if (unit is not null && TacticalQueries.AreEnemies(state, StateJson.Integer(unit, "owner"), StateJson.Integer(attacker, "owner")) && excluded.Add(StateJson.Integer(unit, "id")))
            {
                var damage = Forecasts.ComputeDamage(state, attacker, unit, origin, cell, id);
                var hp = StateJson.Number(unit, "hp", 0);
                hits.Add(new(StateJson.Integer(unit, "id"), cell, false, null, damage, hp, Math.Max(0, hp - damage.Damage), damage.Damage >= hp,
                    damage.Damage >= hp ? Array.Empty<JsonObject>() : effects));
            }
            var otherStructure = HostileStructure(state, attacker, cell);
            if (otherStructure is not null && StateJson.Text(otherStructure, "id") != structure?.Structure)
                structures.Add(new(StateJson.Text(otherStructure, "id"), cell, false, Forecasts.ForecastStructure(state, attacker, otherStructure, id)));
        }
        var support = primary is not null && primaryTarget is not null && !primary.DefenderDies
            ? PlanSupport(state, attacker, origin, primaryTarget, primary.DefenderHpAfter) : null;
        return new(StateJson.Integer(attacker, "id"), id, origin, target, area, cells, primary, structure, hits.AsReadOnly(), structures.AsReadOnly(), support);
    }
    private PlannedSupportAttack? PlanSupport(JsonObject state, JsonObject attacker, Coord at, JsonObject defender, double hp)
    {
        var candidates = StateJson.Objects(state["units"], "units").Where(candidate => StateJson.Integer(candidate, "id") != StateJson.Integer(attacker, "id")
            && TacticalQueries.AreAllies(state, StateJson.Integer(candidate, "owner"), StateJson.Integer(attacker, "owner"))
            && StateJson.Text(candidate, "reaction") == "support" && CombatForecasts.CanReact(state, candidate)
            && movement.Grid(state).Distance(TacticalQueries.Position(candidate), at) == 1)
            .SelectMany(supporter => Forecasts.UnitWeapons(supporter).Where(weapon => StateJson.IsTrue(weapon["canCounter"])
                && Forecasts.IsWeaponReady(state, supporter, weapon)
                && Forecasts.CanReachWithWeapon(state, weapon, TacticalQueries.Position(supporter), TacticalQueries.Position(defender)))
            .Select(weapon => new { Supporter = supporter, Weapon = weapon, Damage = Forecasts.ComputeDamage(state, supporter, defender,
                TacticalQueries.Position(supporter), TacticalQueries.Position(defender), StateJson.Text(weapon, "id"))
                .And(new("reaction.support-attack", "援护攻击", "reaction", "final", "multiply", 0.6)) }))
            .OrderByDescending(value => value.Damage.Damage).ThenBy(value => StateJson.Integer(value.Supporter, "id"))
            .ThenBy(value => StateJson.Text(value.Weapon, "id"), StringComparer.Ordinal).FirstOrDefault();
        if (candidates is null) return null;
        var after = Math.Max(0, hp - candidates.Damage.Damage);
        return new(StateJson.Integer(candidates.Supporter, "id"), StateJson.Integer(defender, "id"), StateJson.Text(candidates.Weapon, "id"),
            candidates.Damage, hp, after, after <= 0, after <= 0 ? Array.Empty<JsonObject>() : StateJson.Objects(candidates.Weapon["hitEffects"], "hitEffects").ToArray());
    }
    public void ExecuteAttack(JsonObject state, JsonObject unit, Coord at, Coord target, string? weapon, Action<JsonObject> emit)
    {
        var selected = Forecasts.RequireReadyWeapon(state, unit, weapon ?? StateJson.Text(Forecasts.PrimaryWeapon(unit), "id"));
        if (StateJson.Number(selected, "castTurns", 0) > 0) { BeginCast(state, unit, selected, target, at, emit); return; }
        ExecutePlan(state, Forecast(state, unit, target, at, StateJson.Text(selected, "id")), emit);
    }
    public void DamageStatus(JsonObject state, JsonObject unit, JsonObject status, double requested, bool nonlethal, Action<JsonObject> emit) =>
        Damage.DamageStatus(state, unit, status, requested, nonlethal, emit);
}
