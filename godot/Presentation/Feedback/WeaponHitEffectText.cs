using System.Text.Json;
using Empire.Core.Combat;
using Empire.Core.Registries;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record WeaponHitEffectTextRules(GameText Messages, ContentText Content);
public sealed record WeaponHitEffectDescription(string Compact, string Detail, bool Emphasis);
public sealed record WeaponHitEffectFormatter(string Kind, IReadOnlyList<string> MessageKeys,
    Func<WeaponHitEffectTextRules, JsonElement, WeaponHitEffectDescription> Describe);

/// <summary>Describes planned effect payloads, never predicts their battlefield outcomes.</summary>
public sealed class WeaponHitEffectText : KeyedRegistry<WeaponHitEffectFormatter, WeaponHitEffectText>
{
    private readonly WeaponHitEffectTextRules rules;

    public WeaponHitEffectText(WeaponHitEffectTextRules rules) : base("weapon hit effect presentation")
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(rules.Messages);
        ArgumentNullException.ThrowIfNull(rules.Content);
        this.rules = rules;
    }

    protected override string KeyOf(WeaponHitEffectFormatter entry) => entry.Kind;

    protected override void ValidateEntry(WeaponHitEffectFormatter entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Kind))
            throw new LocalizationException("A weapon hit effect formatter kind must not be empty.");
        ArgumentNullException.ThrowIfNull(entry.MessageKeys);
        ArgumentNullException.ThrowIfNull(entry.Describe);
        rules.Messages.ValidateKeys(entry.MessageKeys);
    }

    public WeaponHitEffectText Clone() => CopyInto(new WeaponHitEffectText(rules));

    public void ValidateCoverage(IEnumerable<string> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        foreach (var kind in kinds)
            if (!Has(kind))
                throw new LocalizationException($"No weapon hit effect presentation is registered for '{kind}'.");
    }

    public WeaponHitEffectDescription Describe(JsonElement effect)
    {
        var kind = String(effect, "type");
        var formatter = TryGet(kind)
            ?? throw new LocalizationException($"No weapon hit effect presentation is registered for '{kind}'.");
        var description = formatter.Describe(rules, effect);
        if (description is null || string.IsNullOrWhiteSpace(description.Compact) || string.IsNullOrWhiteSpace(description.Detail))
            throw new LocalizationException($"Weapon hit effect presentation for '{kind}' must describe both compact and detailed views.");
        return description;
    }

    public static WeaponHitEffectText CreateDefault(WeaponHitEffectTextRules rules)
    {
        var result = new WeaponHitEffectText(rules);
        result.Register(new("addStatus", ["combat.hit_effect.add_status.compact", "combat.hit_effect.add_status.detail"],
            (rules, effect) =>
            {
                var name = rules.Content.Name(ContentTextKind.Status, String(effect, "status"));
                var duration = Number(effect.GetProperty("duration"), "duration");
                return new(rules.Messages.Format("combat.hit_effect.add_status.compact", name, duration),
                    rules.Messages.Format("combat.hit_effect.add_status.detail", name, duration), false);
            }));
        result.Register(new("removeStatus", ["combat.hit_effect.remove_status.compact", "combat.hit_effect.remove_status.detail"],
            (rules, effect) =>
            {
                var name = rules.Content.Name(ContentTextKind.Status, String(effect, "status"));
                return new(rules.Messages.Format("combat.hit_effect.remove_status.compact", name),
                    rules.Messages.Format("combat.hit_effect.remove_status.detail", name), false);
            }));
        return result.Register(new("forcedMove", ["combat.hit_effect.forced_move.push", "combat.hit_effect.forced_move.pull",
            "combat.hit_effect.forced_move.compact", "combat.hit_effect.forced_move.detail",
            "combat.hit_effect.forced_move.collision_compact", "combat.hit_effect.forced_move.collision_detail"],
            (rules, effect) =>
            {
                var mode = rules.Messages.Format(String(effect, "mode") switch
                {
                    "push" => "combat.hit_effect.forced_move.push",
                    "pull" => "combat.hit_effect.forced_move.pull",
                    _ => throw new LocalizationException("Unknown forced-movement presentation mode."),
                });
                var distance = Number(effect.GetProperty("distance"), "distance");
                var collision = BattleCombat.CollisionDamageAmount(effect.TryGetProperty("collisionDamage", out var amount)
                    ? Number(amount, "collisionDamage") : 0);
                return collision > 0
                    ? new(rules.Messages.Format("combat.hit_effect.forced_move.collision_compact", mode, distance, collision),
                        rules.Messages.Format("combat.hit_effect.forced_move.collision_detail", mode, distance, collision), true)
                    : new(rules.Messages.Format("combat.hit_effect.forced_move.compact", mode, distance),
                        rules.Messages.Format("combat.hit_effect.forced_move.detail", mode, distance), false);
            }));
    }

    private static string String(JsonElement effect, string property)
    {
        var value = effect.GetProperty(property);
        if (value.ValueKind != JsonValueKind.String)
            throw new LocalizationException($"Weapon hit effect field '{property}' must be a string.");
        return FeedbackJson.Text(value);
    }

    private static double Number(JsonElement value, string property)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            throw new LocalizationException($"Weapon hit effect field '{property}' must be a finite number.");
        return number;
    }
}
