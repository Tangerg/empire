using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Resources;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public sealed partial class BattleCombat
{
    private void ChangeMomentum(JsonObject unit, double requested, Action<JsonObject> emit)
    {
        var subject = new BattleResources.Subject("unit", unit);
        if (!resources.HasAccount(subject, "momentum") || requested == 0) return;
        var balance = resources.Balance(subject, "momentum");
        var amount = requested > 0 ? resources.Credit(subject, "momentum", requested)
            : -resources.Spend(subject, "momentum", Math.Min(-requested, balance ?? 0));
        resources.Announce(subject, "momentum", amount, emit);
    }
    private DamageOutcome Land(JsonObject state, JsonObject striker, int target, double amount,
        IReadOnlyList<JsonObject> effects, Func<DamageOutcome, JsonObject> report, Action<JsonObject> emit)
    {
        var outcome = Damage.Resolve(state, target, amount, false, report, emit);
        if (!outcome.Landed) return outcome;
        Effects.AwardCombatProgress(striker, outcome.Amount, outcome.Killed, emit);
        if (outcome.LeftField) return outcome;
        if (effects.Count > 0) ApplyHitEffects(state, striker, TacticalQueries.RequireUnit(state, target), effects, emit);
        var survivor = TacticalQueries.UnitById(state, target);
        if (survivor is not null) ChangeMomentum(survivor, 3, emit);
        return outcome;
    }
    private static JsonObject StrikeEvent(string type, int attacker, int defender, string weapon, DamageOutcome outcome, int? protectedUnit = null)
    {
        var value = new JsonObject { ["type"] = type, ["attacker"] = attacker, ["defender"] = defender,
            ["weapon"] = weapon, ["damage"] = outcome.Amount, ["killed"] = outcome.Killed };
        if (protectedUnit is not null) value["protectedUnit"] = protectedUnit.Value;
        return value;
    }
    public void ExecutePlan(JsonObject state, CombatPlan plan, Action<JsonObject> emit)
    {
        var attacker = TacticalQueries.RequireUnit(state, plan.Attacker);
        Forecasts.ConsumeWeapon(state, attacker, plan.Weapon, emit);
        if (plan.PrimaryUnit?.Reaction is { } reaction)
        {
            var reactor = TacticalQueries.UnitById(state, reaction.Unit);
            if (reactor is not null)
            {
                CombatForecasts.ConsumeReaction(state, reactor);
                if (reaction.Stance == "support") Effects.AwardRankProgress(reactor, 20, emit);
            }
            var value = new JsonObject { ["type"] = "reactionTriggered", ["unit"] = reaction.Unit, ["stance"] = reaction.Stance };
            if (reaction.ProtectedUnit is not null) value["protectedUnit"] = reaction.ProtectedUnit.Value;
            emit(value);
        }
        var unitKilled = false;
        foreach (var hit in plan.UnitHits)
        {
            var outcome = Land(state, attacker, hit.Target, hit.Damage.Damage, hit.Effects,
                blow => StrikeEvent(hit.Primary ? "attack" : "areaAttack", plan.Attacker, hit.Target, hit.Damage.Weapon, blow, hit.ProtectedUnit), emit);
            unitKilled |= outcome.Killed;
        }
        foreach (var hit in plan.StructureHits)
        {
            emit(new JsonObject { ["type"] = hit.Primary ? "attackStructure" : "areaAttackStructure",
                ["attacker"] = plan.Attacker, ["structure"] = hit.Target, ["weapon"] = hit.Forecast.Weapon,
                ["damage"] = hit.Forecast.Damage, ["destroyed"] = hit.Forecast.Destroyed });
            var amount = DamageStructure(state, hit.Target, hit.Forecast.RawDamage, emit);
            Effects.AwardCombatProgress(attacker, amount, hit.Forecast.Destroyed, emit);
        }
        ChangeMomentum(attacker, unitKilled || plan.StructureHits.Any(hit => hit.Forecast.Destroyed) ? 10 : 5, emit);
        if (plan.PrimaryUnit?.Counter is { } counter && TacticalQueries.UnitById(state, plan.PrimaryUnit.Defender) is { } defender)
        {
            Forecasts.ConsumeWeapon(state, defender, counter.Weapon, emit);
            var outcome = Land(state, defender, plan.Attacker, counter.Damage,
                StateJson.Objects(content.Get("weapons", counter.Weapon)["hitEffects"], "hitEffects").ToArray(),
                blow => StrikeEvent("counter", StateJson.Integer(defender, "id"), plan.Attacker, counter.Weapon, blow), emit);
            ChangeMomentum(defender, outcome.Killed ? 10 : 5, emit);
        }
        if (plan.SupportAttack is not { } support) return;
        var supporter = TacticalQueries.UnitById(state, support.Attacker);
        var target = TacticalQueries.UnitById(state, support.Target);
        if (supporter is null || target is null) return;
        CombatForecasts.ConsumeReaction(state, supporter);
        Forecasts.ConsumeWeapon(state, supporter, support.Weapon, emit);
        var supportOutcome = Land(state, supporter, support.Target, support.Damage.Damage, support.Effects,
            blow => StrikeEvent("supportAttack", support.Attacker, support.Target, support.Weapon, blow), emit);
        ChangeMomentum(supporter, supportOutcome.Killed ? 10 : 5, emit);
    }
    public double DamageStructure(JsonObject state, string id, double raw, Action<JsonObject> emit)
    {
        var structure = StateJson.Objects(state["structures"], "structures").FirstOrDefault(value => StateJson.Text(value, "id") == id)
            ?? throw new DomainInvariantException($"unknown structure \"{id}\"");
        var hp = StateJson.Number(structure, "hp", 0);
        if (hp <= 0) return 0;
        if (!double.IsFinite(raw) || raw < 0) throw new DomainInvariantException($"structure damage must be non-negative, got {raw}");
        var definition = content.Get("structures", StateJson.Text(structure, "type"));
        var amount = Math.Min(hp, Math.Max(1, StateJson.Round(raw * (1 - StateJson.Number(definition, "defense", 0)))));
        structure["hp"] = hp - amount;
        if (hp - amount <= 0) structure["disabled"] = true;
        emit(new JsonObject { ["type"] = "structureDamaged", ["structure"] = id, ["amount"] = amount, ["hpAfter"] = hp - amount });
        if (hp - amount <= 0) emit(new JsonObject { ["type"] = "structureDestroyed", ["structure"] = id,
            ["at"] = TacticalQueries.CoordinateJson(TacticalQueries.Position(structure)) });
        return amount;
    }
    public double RepairStructure(JsonObject state, string id, double requested, Action<JsonObject> emit)
    {
        var structure = StateJson.Objects(state["structures"], "structures").FirstOrDefault(value => StateJson.Text(value, "id") == id)
            ?? throw new DomainInvariantException($"unknown structure \"{id}\"");
        var definition = content.Get("structures", StateJson.Text(structure, "type"));
        if (!StateJson.IsTrue(definition["repairable"]) || requested <= 0) return 0;
        var hp = StateJson.Number(structure["hp"], "hp");
        var amount = Math.Min(StateJson.Round(requested), Math.Max(0, StateJson.Number(definition["maxHp"], "maxHp") - hp));
        structure["hp"] = hp + amount;
        if (hp + amount > 0) structure["disabled"] = false;
        if (amount > 0) emit(new JsonObject { ["type"] = "structureRepaired", ["structure"] = id, ["amount"] = amount, ["hpAfter"] = hp + amount });
        return amount;
    }
    public void ResolvePartingShots(JsonObject state, JsonObject unit, Coord from, Coord to, Action<JsonObject> emit)
    {
        if (!StateJson.IsTrue(state["rules"]!["zoneOfControl"])) return;
        var grid = Forecasts.Grid(state);
        var controllers = StateJson.Objects(state["units"], "units").Where(other =>
            TacticalQueries.AreEnemies(state, StateJson.Integer(other, "owner"), StateJson.Integer(unit, "owner"))
            && Forecasts.Reaction(StateJson.Text(other, "reaction")).Retaliates && CombatForecasts.CanReact(state, other)
            && MovementRules.ControlRadius(content, state, other) > 0
            && grid.Distance(TacticalQueries.Position(other), from) <= MovementRules.ControlRadius(content, state, other)
            && grid.Distance(TacticalQueries.Position(other), to) > MovementRules.ControlRadius(content, state, other)).ToArray();
        foreach (var controller in controllers)
        {
            if (TacticalQueries.UnitById(state, StateJson.Integer(unit, "id")) is null) return;
            var stance = Forecasts.Reaction(StateJson.Text(controller, "reaction"));
            var strike = Forecasts.BestReactiveStrike(state, controller, TacticalQueries.Position(controller), unit, from, stance);
            if (strike is null) continue;
            CombatForecasts.ConsumeReaction(state, controller);
            var weapon = StateJson.Text(strike.Weapon, "id");
            Forecasts.ConsumeWeapon(state, controller, weapon, emit);
            Damage.Resolve(state, StateJson.Integer(unit, "id"), Math.Max(1, StateJson.Round(strike.Damage.Damage * 0.7)), false,
                blow => new JsonObject { ["type"] = "partingShot", ["attacker"] = controller["id"]!.DeepClone(),
                    ["defender"] = unit["id"]!.DeepClone(), ["weapon"] = weapon, ["at"] = TacticalQueries.CoordinateJson(from),
                    ["damage"] = blow.Amount, ["killed"] = blow.Killed }, emit);
        }
    }
    public void ApplyHitEffects(JsonObject state, JsonObject attacker, JsonObject target,
        IReadOnlyList<JsonObject> effects, Action<JsonObject> emit) =>
        HitEffects.Apply(hitEffectRules, state, attacker, target, effects, emit);
    public ForcedMovementResult ForceMove(JsonObject state, int unitId, Coord source, string mode,
        double distance, double collisionDamage, Action<JsonObject> emit)
    {
        if (mode is not ("push" or "pull")) throw new DomainInvariantException($"unknown forced movement mode \"{mode}\"");
        var unit = TacticalQueries.RequireUnit(state, unitId);
        var from = TacticalQueries.Position(unit);
        var count = Math.Max(0, StateJson.Round(distance));
        var board = Board(state);
        var facing = mode == "push" ? board.Grid.Toward(source, from) : board.Grid.Toward(from, source);
        var path = new List<Coord> { from };
        var battlefield = new TacticalBattlefield(state, content);
        var movementClass = StateJson.Text(content.Get("units", StateJson.Text(unit, "type")), "movementClass");
        var collided = from == source && count > 0;
        for (var step = 0; step < count && !collided; step++)
        {
            var next = board.Grid.Step(path[^1], facing);
            if (!battlefield.Contains(next) || !battlefield.Cell(next).CanReceive(movementClass)
                || battlefield.TraversalCost(path[^1], next, movementClass) is null) { collided = true; break; }
            path.Add(next);
        }
        var to = path[^1];
        if (from != to)
        {
            state["map"]!["captureProgress"]!.AsArray()[board.IndexOf(from)] = 0;
            unit["x"] = to.X; unit["y"] = to.Y; unit["capture"] = 0; unit["facing"] = facing;
        }
        emit(new JsonObject { ["type"] = "forcedMove", ["unit"] = unitId, ["mode"] = mode,
            ["from"] = TacticalQueries.CoordinateJson(from), ["to"] = TacticalQueries.CoordinateJson(to),
            ["path"] = TacticalQueries.PathJson(path), ["collided"] = collided });
        var collision = Math.Max(0, StateJson.Round(collisionDamage));
        var impact = collided && collision > 0 ? Damage.Resolve(state, unitId, collision, false,
            blow => new JsonObject { ["type"] = "collisionDamage", ["unit"] = unitId,
                ["amount"] = blow.Amount, ["hpAfter"] = blow.HpAfter, ["killed"] = blow.Killed }, emit) : null;
        return new(from, to, path.AsReadOnly(), impact?.Killed ?? false);
    }
}
