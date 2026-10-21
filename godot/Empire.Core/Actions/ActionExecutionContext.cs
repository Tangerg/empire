using System.Text.Json.Nodes;
using Empire.Core.Lifecycle;
using Empire.Core.State;

namespace Empire.Core.Actions;

public sealed class ActionExecutionContext
{
    private readonly ActionRules? rules;
    public JsonObject State
    {
        get;
    }
    public Action<JsonObject> Emit
    {
        get;
    }
    public ActionRules Rules => rules ?? throw new DomainInvariantException("Battle action requires composed rule services");
    private readonly BattleLifecycle? lifecycle;
    public BattleLifecycle Lifecycle => lifecycle ?? throw new DomainInvariantException("Battle action requires composed lifecycle services");

    public ActionExecutionContext(ActionRules rules, JsonObject state, Action<JsonObject> output)
    {
        State = state;
        this.rules = rules;
        Emit = value =>
        {
            output(value);
            var counts = State["scenario"]!["eventCounts"]!.AsObject();
            var type = StateJson.Text(value, "type");
            counts[type] = StateJson.Number(counts, type, 0) + 1;
        };
        lifecycle = rules.CreateLifecycle(state, Emit);
    }

    private ActionExecutionContext(JsonObject state, Action<JsonObject> output)
    {
        State = state;
        Emit = output;
    }

    internal static ActionExecutionContext Bare(JsonObject state, Action<JsonObject> output) => new(state, output);
}
