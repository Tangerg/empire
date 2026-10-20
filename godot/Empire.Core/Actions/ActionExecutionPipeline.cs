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
        if (phase == "over") throw new IllegalActionException("对局已结束");
        var deployment = registration?.DuringDeployment == true;
        if (phase == "deployment" && !deployment) throw new IllegalActionException("请先完成战前部署");
        if (phase == "playing" && deployment) throw new IllegalActionException("战斗开始后不能重新部署");
        if (registration is null) throw new IllegalActionException($"没有处理行动类型「{StateJson.Text(action, "kind")}」的规则策略");
        registration.Handler(context, action);
        if (StateJson.Text(state, "phase") == "playing" && !deployment) context.Lifecycle.AfterAction();
    }
}
