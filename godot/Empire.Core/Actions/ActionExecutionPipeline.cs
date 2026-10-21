using System.Text.Json.Nodes;
using Empire.Core.State;
using Empire.Core.Transactions;

namespace Empire.Core.Actions;

public sealed class ActionExecutionPipeline(ActionRules rules)
{
    public void Execute(BattleActionRegistration? registration, JsonObject state, JsonObject action, Action<JsonObject> output)
    {
        var context = new ActionExecutionContext(state, rules, output);
        var phase = StateJson.Text(state, "phase");
        if (phase == "over") throw new IllegalActionException(new ActionRefusal("action.over"));
        var deployment = registration?.DuringDeployment == true;
        if (phase == "deployment" && !deployment) throw new IllegalActionException(new ActionRefusal("action.deployment_required"));
        if (phase == "playing" && deployment) throw new IllegalActionException(new ActionRefusal("action.deployment_finished"));
        if (registration is null) throw new IllegalActionException(new ActionRefusal("action.unsupported", new() { ["kind"] = StateJson.Text(action, "kind") }));
        registration.Handler(context, action);
        if (StateJson.Text(state, "phase") == "playing" && !deployment) context.Lifecycle.AfterAction();
    }
}
