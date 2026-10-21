using System.Text.Json.Nodes;
using Empire.Core.Engine;

namespace Empire.Core.Validation;

public sealed record BattleDefinitionValidationRules(BattleContentValidator Content, BattleLevelValidator Levels);

/// <summary>Explicit catalog and authored-level admission boundary for a composed native engine.</summary>
public sealed class BattleDefinitionValidator
{
    private readonly BattleContentValidator content;
    private readonly BattleLevelValidator levels;
    public BattleDefinitionValidator(BattleDefinitionValidationRules rules)
    {
        content = rules.Content;
        levels = rules.Levels;
    }
    public void ValidateContent() => content.Validate();
    public void ValidateLevel(JsonObject level)
    {
        var issues = levels.Issues(level);
        if (issues.Count > 0)
            throw new BattleLevelException(level["id"]?.GetValue<string>() ?? "", issues);
    }
}
