using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Persistence;

internal sealed partial class BattleSaveInspection
{
    private void CheckRuleReferences(JsonObject save)
    {
        var issues = environment.ReferenceChecks.StateIssues(environment.Rules, State(save));
        if (issues.Count > 0) StoredSchema.Reject(string.Join("；", issues));
        var state = State(save);
        var grid = environment.Rules.Grids.Get(StateJson.Text(state["rules"]!["grid"], "grid"));
        foreach (var (unit, owner) in AllUnits(state))
            if (!UnitDisposition.AdmitsFacing(grid, StateJson.Text(unit, "facing")))
                StoredSchema.Reject($"{owner} has a facing not admitted by grid \"{grid.Id}\"");
    }
}
