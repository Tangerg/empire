using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Engine;
using Empire.Core.Persistence;
using Empire.Core.References;

namespace Empire.Core.Validation;

/// <summary>Explicit catalog and authored-level admission boundary for a composed native engine.</summary>
public sealed class BattleDefinitionValidator
{
    private readonly BattleContentValidator content;
    private readonly BattleLevelValidator levels;
    public BattleDefinitionValidator(JsonArray packs, RuleReferenceRules rules, BattleReferenceCheckRegistry referenceChecks, GridRegistry grids)
    {
        content = new BattleContentValidator(packs, rules, referenceChecks);
        levels = new BattleLevelValidator(rules, referenceChecks, grids);
    }
    public void ValidateContent() => content.Validate();
    public void ValidateLevel(JsonObject level)
    {
        var issues = levels.Issues(level);
        if (issues.Count > 0)
            throw new BattleLevelException(level["id"]?.GetValue<string>() ?? "", issues);
    }
}
