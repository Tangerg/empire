using System.Text.Json.Nodes;
using Empire.Core.Geometry;
using Empire.Core.Persistence;

namespace Empire.Core.Validation;

/// <summary>Explicit catalog and authored-level admission boundary for a composed native engine.</summary>
public sealed class BattleDefinitionValidator
{
    private readonly BattleContentValidator content;
    private readonly BattleLevelValidator levels;
    private readonly BattleLevelCapabilities capabilities;
    public BattleDefinitionValidator(JsonArray packs, BattleSaveFeatures features, GridRegistry grids)
    {
        content = new BattleContentValidator(packs, features);
        levels = new BattleLevelValidator(packs, features, grids);
        capabilities = new BattleLevelCapabilities(features);
    }
    public void ValidateContent() => content.Validate();
    public void ValidateLevel(JsonObject level)
    {
        var issues = levels.Issues(level);
        if (issues.Count == 0) issues = capabilities.Issues(level);
        if (issues.Count > 0)
            throw new StoredDocumentException($"invalid battle level \"{level["id"]?.GetValue<string>()}\":\n" +
                string.Join("\n", issues.Select(issue => "- " + issue)));
    }
}
