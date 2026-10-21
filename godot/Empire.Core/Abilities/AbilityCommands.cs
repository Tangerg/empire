using System.Text.Json.Nodes;
using Empire.Core.Combat;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.State;
using Empire.Core.Tactics;

namespace Empire.Core.Abilities;

/// <summary>One command policy for menus and execution; Battle owns transaction commit and rollback.</summary>
public sealed class AbilityCommands(AbilityRules rules, TacticalCommands commands)
{
    public bool CanUse(AbilityDefinition ability, AbilityQuery query)
    {
        var weapon = ability.WeaponFor(rules, query);
        var tags = weapon is null ? ability.Tags : ability.Tags.Concat(
            StateJson.Array(weapon["tags"], "weapon tags").Select(value => StateJson.Text(value, "weapon tag")));
        return rules.Combat.Effects.BlockedAbilityStatus(query.Unit, tags) is null && ability.Usable(rules, query);
    }

    public IReadOnlyList<Coord> Targets(AbilityDefinition ability, AbilityQuery query)
    {
        var targets = ability.Targets(rules, query);
        return ability.Engagement is { } kind ? targets.Where(target => BattleCombat.HostileActionAllowed(
            query.State, StateJson.Integer(query.Unit, "owner"), query.At, target, kind)).ToArray() : targets;
    }

    public IReadOnlyList<CommandOption> Options(JsonObject state, JsonObject unit, Coord at)
    {
        var query = new AbilityQuery(state, unit, at, at != TacticalQueries.Position(unit));
        var options = new List<CommandOption>();
        foreach (var id in rules.Careers.UnitAbilityIds(unit))
        {
            var ability = rules.Abilities.Get(id);
            var choices = ability.WeaponChoices(rules, query);
            if (choices.Count == 0) Add(ability, query, null);
            else foreach (var weapon in choices)
                Add(ability, query with { WeaponId = StateJson.Text(weapon, "id") }, weapon);
        }
        return options.OrderBy(option => option, Comparer<CommandOption>.Create((left, right) =>
        {
            var difference = rules.Abilities.Get(left.Ability).Priority - rules.Abilities.Get(right.Ability).Priority;
            return difference < 0 ? -1 : difference > 0 ? 1 : StringComparer.Ordinal.Compare(left.Key, right.Key);
        })).ToArray();

        void Add(AbilityDefinition ability, AbilityQuery order, JsonObject? weapon)
        {
            if (!CanUse(ability, order)) return;
            var targets = Targets(ability, order);
            if (!ability.SelfTargeted && targets.Count == 0) return;
            var id = weapon is null ? null : StateJson.Text(weapon, "id");
            options.Add(new(id is null ? ability.Id : $"{ability.Id}:{id}", ability.Id, id,
                weapon is null ? ability.Name : $"{ability.Name} · {StateJson.Text(weapon, "name")}",
                weapon is null ? ability.Hint : $"{ability.Hint} 射程 {EcmaNumber.Format(StateJson.Number(weapon["minRange"], "minRange"))}-{EcmaNumber.Format(StateJson.Number(weapon["maxRange"], "maxRange"))}",
                ability.SelfTargeted, targets, ability.Engagement));
        }
    }

    public void Execute(JsonObject state, JsonObject action, Action<JsonObject> emit)
    {
        var movement = commands.ValidateMovement(state, action);
        var unit = TacticalQueries.RequireUnit(state, movement.UnitId);
        var command = StateJson.Object(action["command"], "command");
        var ability = rules.Abilities.Get(StateJson.Text(command, "ability"));
        var weapon = command["weapon"]?.GetValue<string>();
        var query = new AbilityQuery(state, unit, movement.Destination, movement.Moved, weapon);
        if (!rules.Careers.UnitAbilityIds(unit).Contains(ability.Id))
            throw new IllegalActionException(new ActionRefusal("ability.not_owned", new()
            { ["unitType"] = StateJson.Text(unit, "type"), ["abilityId"] = ability.Id }));
        if (!string.IsNullOrEmpty(weapon) && !ability.WeaponChoices(rules, query).Any(choice => StateJson.Text(choice, "id") == weapon))
            throw new IllegalActionException(new ActionRefusal("ability.weapon_unavailable", new() { ["abilityId"] = ability.Id, ["weaponId"] = weapon }));
        if (!CanUse(ability, query)) throw new IllegalActionException(new ActionRefusal("ability.unusable", new() { ["abilityId"] = ability.Id }));
        Coord? target = command["target"] is JsonObject location ? TacticalQueries.Position(location) : null;
        if (!ability.SelfTargeted)
        {
            if (target is null) throw new IllegalActionException(new ActionRefusal("ability.target_required", new() { ["abilityId"] = ability.Id }));
            if (!Targets(ability, query).Contains(target.Value)) throw new IllegalActionException(new ActionRefusal("ability.target_invalid", new() { ["abilityId"] = ability.Id }));
        }
        var surviving = commands.ApplyMovement(state, movement, emit, rules.Combat.ResolvePartingShots);
        if (surviving is null) return;
        if (target is { } at) commands.TurnToward(state, surviving, movement.Destination, at, emit);
        ability.Execute(rules, query, target, emit);
        TacticalCommands.FinishCommand(state, unit);
    }
}
