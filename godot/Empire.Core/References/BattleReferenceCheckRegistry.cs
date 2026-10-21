using System.Text.Json.Nodes;
using Empire.Core.Registries;
using Empire.Core.State;

namespace Empire.Core.References;

public readonly record struct RuleReference(string By, string Name);

public sealed record RuleReferenceCheck(string Id, string Subject,
    Func<RuleReferenceRules, IEnumerable<string>> Known)
{
    public Func<ReadOnlyContent, IEnumerable<RuleReference>>? InContent { get; init; }
    public Func<JsonObject, RuleReferenceRules, IEnumerable<RuleReference>>? InLevel { get; init; }
    public Func<JsonObject, RuleReferenceRules, IEnumerable<RuleReference>>? InState { get; init; }
}

/// <summary>Ordered, replaceable checks shared by catalog, level, and saved-state admission.</summary>
public sealed class BattleReferenceCheckRegistry : KeyedRegistry<RuleReferenceCheck, BattleReferenceCheckRegistry>
{
    public BattleReferenceCheckRegistry() : base("rule reference check") { }
    protected override string KeyOf(RuleReferenceCheck entry) => entry.Id;
    public BattleReferenceCheckRegistry Clone() => CopyInto(new BattleReferenceCheckRegistry());
    public IReadOnlyList<string> ContentIssues(RuleReferenceRules rules) => Issues(rules, check => check.InContent?.Invoke(rules.Content));
    public IReadOnlyList<string> LevelIssues(RuleReferenceRules rules, JsonObject level) => Issues(rules, check => check.InLevel?.Invoke(level, rules));
    public IReadOnlyList<string> StateIssues(RuleReferenceRules rules, JsonObject state) => Issues(rules, check => check.InState?.Invoke(state, rules));

    private IReadOnlyList<string> Issues(RuleReferenceRules rules, Func<RuleReferenceCheck, IEnumerable<RuleReference>?> referencesOf)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var check in All())
        {
            var references = referencesOf(check);
            if (references is null) continue;
            var known = check.Known(rules).ToHashSet(StringComparer.Ordinal);
            foreach (var reference in references)
            {
                if (known.Contains(reference.Name)) continue;
                var issue = $"{reference.By} 引用了未注册的{check.Subject}「{reference.Name}」";
                if (seen.Add(issue)) found.Add(issue);
            }
        }
        return found;
    }

    public static BattleReferenceCheckRegistry CreateDefault() => DefaultRuleReferenceChecks.Create();
}
