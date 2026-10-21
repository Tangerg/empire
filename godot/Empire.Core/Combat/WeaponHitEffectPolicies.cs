using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Geometry;
using Empire.Core.Registries;
using Empire.Core.State;
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

public sealed record WeaponHitEffectHandler(string Kind, Action<WeaponHitEffectContext, JsonObject> Apply,
    Func<JsonObject, string> Describe);

public sealed class WeaponHitEffectHandlerRegistry : KeyedRegistry<WeaponHitEffectHandler, WeaponHitEffectHandlerRegistry>
{
    public WeaponHitEffectHandlerRegistry() : base("weapon hit effect") { }
    protected override string KeyOf(WeaponHitEffectHandler entry) => entry.Kind;
    public WeaponHitEffectHandlerRegistry Clone() => CopyInto(new WeaponHitEffectHandlerRegistry());
    public void Apply(WeaponHitEffectRules rules, JsonObject state, JsonObject attacker, JsonObject target,
        IEnumerable<JsonObject> effects, Action<JsonObject> emit)
    {
        var context = new WeaponHitEffectContext(rules, state, attacker, target, emit);
        foreach (var effect in effects) Get(StateJson.Text(effect, "type")).Apply(context, effect);
    }
    public string Describe(JsonObject effect) => Get(StateJson.Text(effect, "type")).Describe(effect);
}

public static class DefaultWeaponHitEffects
{
    private static readonly WeaponHitEffectHandlerRegistry defaults = new WeaponHitEffectHandlerRegistry()
        .Register(new("addStatus", (context, effect) => context.Rules.Effects.AddStatus(context.Target,
            StateJson.Text(effect, "status"), StateJson.Number(effect, "duration", 0), StateJson.Integer(context.Attacker, "id"), context.Emit),
            effect => $"施加 {StateJson.Text(effect, "status")}（{EcmaNumber.Format(StateJson.Number(effect, "duration", 0))} 回合）"))
        .Register(new("removeStatus", (context, effect) => context.Rules.Effects.RemoveStatus(context.Target,
            StateJson.Text(effect, "status"), context.Emit), effect => $"移除 {StateJson.Text(effect, "status")}"))
        .Register(new("forcedMove", (context, effect) => context.Rules.ForceMove(context.State,
            StateJson.Integer(context.Target, "id"), TacticalQueries.Position(context.Attacker), StateJson.Text(effect, "mode"),
            StateJson.Number(effect, "distance", 0), StateJson.Number(effect, "collisionDamage", 0), context.Emit),
            effect => $"{(StateJson.Text(effect, "mode") == "push" ? "击退" : "拉拽")} {EcmaNumber.Format(StateJson.Number(effect, "distance", 0))} 格"))
        .Seal();
    public static WeaponHitEffectHandlerRegistry Create() => defaults.Clone();
}
