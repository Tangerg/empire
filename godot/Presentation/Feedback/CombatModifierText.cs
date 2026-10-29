using System.Text.Json;
using Empire.Core.Combat;
using Empire.Core.Registries;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record CombatModifierTextRules(GameText Messages, ContentText Content);

public sealed record CombatModifierFormatter(string Kind, IReadOnlyList<string> MessageKeys,
    Func<CombatModifierTextRules, JsonElement, string> Describe);

/// <summary>Formats provider-owned facts without inspecting battle state or repeating combat rules.</summary>
public sealed class CombatModifierText : KeyedRegistry<CombatModifierFormatter, CombatModifierText>
{
    private readonly GameText text;
    private readonly ContentText content;

    public CombatModifierText(CombatModifierTextRules rules) : base("combat modifier presentation")
    {
        text = rules.Messages;
        content = rules.Content;
        text.ValidateKeys(new[] { "combat.modifier.row", "combat.modifier.label", "combat.modifier.multiply",
            "combat.modifier.add", "combat.modifier.mitigation" });
    }
    protected override string KeyOf(CombatModifierFormatter entry) => entry.Kind;
    protected override void ValidateEntry(CombatModifierFormatter entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Kind))
            throw new LocalizationException("A combat formatter kind must not be empty.");
        ArgumentNullException.ThrowIfNull(entry.Describe);
        text.ValidateKeys(entry.MessageKeys);
    }
    public CombatModifierText Clone() => CopyInto(new CombatModifierText(new(text, content)));
    public void ValidateCoverage(IEnumerable<string> kinds)
    {
        foreach (var kind in kinds)
            if (!Has(kind))
                throw new LocalizationException($"No combat presentation is registered for '{kind}'.");
    }
    public string Describe(CombatModifier modifier)
    {
        var formatter = TryGet(modifier.Kind) ?? throw new LocalizationException($"No combat presentation is registered for '{modifier.Kind}'.");
        var amount = modifier.Stage == "mitigation" ? text.Format("combat.modifier.mitigation", modifier.Value)
            : modifier.Operation == "multiply" ? text.Format("combat.modifier.multiply", modifier.Value)
            : text.Format("combat.modifier.add", modifier.Value);
        return text.Format("combat.modifier.row", formatter.Describe(new(text, content), modifier.Facts), amount);
    }

    public static CombatModifierText CreateDefault(CombatModifierTextRules rules)
    {
        var result = new CombatModifierText(rules);
        void Static(string kind, string source, string label) => result.Register(new(kind, new[] { source, label },
            (rules, _) => Label(rules.Messages, source, rules.Messages.Format(label))));
        Static("core.matchup", "ui.hud.modifier_source.matchup", "combat.modifier.matchup");
        Static("core.attacker-strength", "ui.hud.modifier_source.unit", "combat.modifier.hp_strength");
        Static("core.status", "ui.hud.modifier_source.status", "combat.modifier.attacker_status");
        Static("core.commander", "ui.hud.modifier_source.commander", "combat.modifier.attacker_command");
        result.Register(new("core.weapon-target-tags", ["ui.hud.modifier_source.weapon"], (rules, facts) =>
            Label(rules.Messages, "ui.hud.modifier_source.weapon", rules.Content.WeaponBonus(S(facts, "weaponId"), S(facts, "bonusId")))));
        result.Register(new("core.rank", ["ui.hud.modifier_source.unit", "combat.modifier.rank", "ui.rank.recruit", "ui.rank.veteran", "ui.rank.elite"],
            (rules, facts) => Label(rules.Messages, "ui.hud.modifier_source.unit", rules.Messages.Format("combat.modifier.rank",
                rules.Messages.Format(facts.GetProperty("rank").GetInt32() switch
                {
                    0 => "ui.rank.recruit",
                    1 => "ui.rank.veteran",
                    2 => "ui.rank.elite",
                    _ => throw new LocalizationException("Invalid modifier rank fact.")
                })))));
        result.Register(new("core.formation-attack", ["ui.hud.modifier_source.formation"], (rules, facts) =>
            Label(rules.Messages, "ui.hud.modifier_source.formation", rules.Content.Name(ContentTextKind.Formation, S(facts, "formationId")))));
        result.Register(new("core.elevation", ["ui.hud.modifier_source.elevation", "combat.modifier.elevation"], (rules, facts) =>
            Label(rules.Messages, "ui.hud.modifier_source.elevation", rules.Messages.Format("combat.modifier.elevation", facts.GetProperty("delta").GetDouble()))));
        result.Register(new("core.position", ["ui.hud.modifier_source.position", "combat.modifier.back", "combat.modifier.side", "combat.modifier.flank"],
            (rules, facts) => Label(rules.Messages, "ui.hud.modifier_source.position", rules.Messages.Format(S(facts, "position") switch
            {
                "back" => "combat.modifier.back",
                "side" => "combat.modifier.side",
                "flank" => "combat.modifier.flank",
                _ => throw new LocalizationException("Invalid modifier position fact.")
            }))));
        result.Register(new("core.cover", ["ui.hud.modifier_source.cover", "combat.modifier.full_cover", "combat.modifier.half_cover"],
            (rules, facts) => Label(rules.Messages, "ui.hud.modifier_source.cover", rules.Messages.Format(S(facts, "cover") switch
            {
                "full" => "combat.modifier.full_cover",
                "half" => "combat.modifier.half_cover",
                _ => throw new LocalizationException("Invalid modifier cover fact.")
            }))));
        result.Register(new("core.defense", ["ui.hud.modifier_source.terrain", "ui.hud.modifier_source.unit"],
            (rules, facts) => S(facts, "component") switch
            {
                "terrain" => rules.Messages.Format("ui.hud.modifier_source.terrain"),
                "unit" => rules.Messages.Format("ui.hud.modifier_source.unit"),
                _ => throw new LocalizationException("Invalid modifier defense fact."),
            }));
        result.Register(new("reaction.support", ["ui.hud.modifier_source.reaction", "combat.modifier.support"], (rules, facts) =>
            Label(rules.Messages, "ui.hud.modifier_source.reaction", rules.Messages.Format("combat.modifier.support", rules.Content.ReactionName(S(facts, "reactionId"))))));
        return result.Register(new("reaction.incoming", ["ui.hud.modifier_source.reaction"], (rules, facts) =>
            Label(rules.Messages, "ui.hud.modifier_source.reaction", rules.Content.ReactionName(S(facts, "reactionId")))));
    }
    private static string Label(GameText text, string sourceKey, string name) => text.Format("combat.modifier.label", text.Format(sourceKey), name);
    private static string S(JsonElement value, string property) => FeedbackJson.Text(value.GetProperty(property));
}
