using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Registries;
using Empire.Core.State;
using Empire.Core.Statuses;
using Empire.Core.Tactics;

namespace Empire.Core.Combat;

public delegate ForcedMovementResult ForceWeaponTarget(JsonObject state, int unitId, Coord source,
    string mode, double distance, double collisionDamage, Action<JsonObject> emit);

/// <summary>Named canonical mutation owners available to weapon effects. No parallel status, HP, or movement writer.</summary>
public sealed record WeaponHitEffectRules(ReadOnlyContent Content, GridRegistry Grids, CombatEffects Effects,
    BattleDamage Damage, ForceWeaponTarget ForceMove);

public sealed record WeaponHitEffectContext(WeaponHitEffectRules Rules, JsonObject State,
    JsonObject Attacker, JsonObject Target, Action<JsonObject> Emit)
{
    public ReadOnlyContent Content => Rules.Content;
}

public sealed record WeaponHitEffectHandler(string Kind, Action<WeaponHitEffectContext, JsonObject> Apply)
{
    public Action<ReadOnlyContent, JsonObject, string> Validate { get; init; } = (_, _, _) => { };
}

public sealed class WeaponHitEffectHandlerRegistry : KeyedRegistry<WeaponHitEffectHandler, WeaponHitEffectHandlerRegistry>
{
    public WeaponHitEffectHandlerRegistry() : base("weapon hit effect") { }
    protected override string KeyOf(WeaponHitEffectHandler entry) => entry.Kind;
    public WeaponHitEffectHandlerRegistry Clone() => CopyInto(new WeaponHitEffectHandlerRegistry());
    public void Validate(ReadOnlyContent content, JsonObject effect, string owner) =>
        Get(StateJson.Text(effect, "type")).Validate(content, effect, owner);

    public void Apply(WeaponHitEffectRules rules, JsonObject state, JsonObject attacker, JsonObject target,
        IEnumerable<JsonObject> effects, Action<JsonObject> emit)
    {
        var context = new WeaponHitEffectContext(rules, state, attacker, target, emit);
        foreach (var effect in effects)
        {
            Validate(rules.Content, effect, "weapon hit effect");
            Get(StateJson.Text(effect, "type")).Apply(context, effect);
        }
    }
}

public static class DefaultWeaponHitEffects
{
    private static readonly WeaponHitEffectHandlerRegistry defaults = new WeaponHitEffectHandlerRegistry()
        .Register(new("addStatus", (context, effect) => context.Rules.Effects.AddStatus(context.Target,
            StateJson.Text(effect, "status"), StateJson.Number(effect, "duration", 0), StateJson.Integer(context.Attacker, "id"), context.Emit))
        {
            Validate = (content, effect, owner) =>
            {
                RequireStatus(content, effect, owner);
                StatusTransitions.RequireDuration(Number(effect["duration"]));
            },
        })
        .Register(new("removeStatus", (context, effect) => context.Rules.Effects.RemoveStatus(context.Target,
            StateJson.Text(effect, "status"), context.Emit))
        {
            Validate = RequireStatus,
        })
        .Register(new("forcedMove", (context, effect) => context.Rules.ForceMove(context.State,
            StateJson.Integer(context.Target, "id"), TacticalQueries.Position(context.Attacker), StateJson.Text(effect, "mode"),
            StateJson.Number(effect, "distance", 0), StateJson.Number(effect, "collisionDamage", 0), context.Emit))
        {
            Validate = (_, effect, owner) =>
            {
                var distance = Number(effect["distance"]);
                if (!double.IsFinite(distance) || distance < 0 || distance != Math.Truncate(distance) ||
                    effect.ContainsKey("collisionDamage") && (!double.IsFinite(Number(effect["collisionDamage"])) || Number(effect["collisionDamage"]) < 0) ||
                    StateJson.Text(effect, "mode") is not ("push" or "pull"))
                    throw new DomainInvariantException($"{owner} has an invalid forced-movement effect");
            },
        })
        .Seal();
    private static double Number(JsonNode? raw)
    {
        if (raw is not JsonValue value) return double.NaN;
        if (value.TryGetValue<double>(out var number)) return number;
        if (value.TryGetValue<float>(out var single)) return single;
        return value.GetValueKind() == JsonValueKind.Number && double.TryParse(value.ToJsonString(),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number)
            ? number : double.NaN;
    }

    private static void RequireStatus(ReadOnlyContent content, JsonObject effect, string owner)
    {
        var id = StateJson.Text(effect, "status");
        if (content.Definition("statuses", id) is null)
            throw new DomainInvariantException($"{owner} references missing content id \"{id}\"");
    }

    public static WeaponHitEffectHandlerRegistry Create() => defaults.Clone();
}
