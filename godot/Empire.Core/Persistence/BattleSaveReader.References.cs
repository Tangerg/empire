using System.Text.Json.Nodes;

namespace Empire.Core.Persistence;

internal sealed partial class BattleSaveInspection
{
    private void CheckRuleReferences(JsonObject save)
    {
        var issues = environment.ReferenceChecks.StateIssues(environment.Rules, State(save));
        if (issues.Count > 0) StoredSchema.Reject(string.Join("；", issues));
    }
}
