using System.Collections.Frozen;
using System.Text.Json;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record BattleUnitNameRules(GameText Messages, ContentText Content,
    IReadOnlyDictionary<string, string> IdentityNameKeys);

/// <summary>Projects authored instance identities; generic units retain their content-type labels.</summary>
public sealed class BattleUnitNames
{
    private readonly BattleUnitNameRules rules;
    private readonly FrozenDictionary<string, string> identities;

    public BattleUnitNames(BattleUnitNameRules rules, IReadOnlyDictionary<string, string> identities)
    {
        this.rules = rules;
        this.identities = identities.ToFrozenDictionary(StringComparer.Ordinal);
        foreach (var identity in this.identities.Values)
            _ = IdentityNames.Resolve(rules.Messages, rules.IdentityNameKeys, identity);
    }

    public string? Identity(string unitKey) => identities.GetValueOrDefault(unitKey);

    public string Name(JsonElement unit)
    {
        var identity = unit.TryGetProperty("key", out var key) ? Identity(FeedbackJson.Text(key)) : null;
        return identity is not null ? IdentityNames.Resolve(rules.Messages, rules.IdentityNameKeys, identity)
            : rules.Content.UnitName(FeedbackJson.Text(unit.GetProperty("type")));
    }
}
