using System.Text.Json.Nodes;
using Empire.Core.References;
using Empire.Core.Registries;
using Empire.Core.Scenario;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Objectives;

public sealed record ObjectiveRules(ReadOnlyContent Content, ScenarioConditionHandlerRegistry ScenarioConditions);

public sealed class ObjectiveEvaluationContext(ObjectiveRules rules, JsonObject state, int owner, ObjectiveHandlerRegistry handlers)
{
    public ObjectiveRules Rules { get; } = rules;
    public JsonObject State { get; } = state;
    public int Owner { get; } = owner;
    public ObjectiveHandlerRegistry Handlers { get; } = handlers;
    public ReadOnlyContent Content => Rules.Content;
    public ScenarioConditionHandlerRegistry ScenarioConditions => Rules.ScenarioConditions;
    public string Status(JsonObject objective) => ObjectiveHandlerRegistry.Status(State, Owner, objective);
    public string Outcome(JsonObject objective) => Handlers.Evaluate(Rules, State, Owner, objective);
}

public sealed record ObjectiveHandler(string Kind, string Label,
    Func<ObjectiveEvaluationContext, JsonObject, string> Outcome,
    Func<ObjectiveEvaluationContext, JsonObject, string> Progress)
{
    public string Role { get; init; } = "primary";
    public string Refresh { get; init; } = "self";
    public Func<JsonObject, IReadOnlyList<JsonObject>>? Children { get; init; }
    public Func<JsonObject, PayloadReferences>? References { get; init; }
    public Func<JsonObject, ObjectiveHandlerRegistry, string>? Describe { get; init; }
}

public sealed class ObjectiveHandlerRegistry() : KeyedRegistry<ObjectiveHandler, ObjectiveHandlerRegistry>("objective handler"), IRuleTreeRegistry
{
    protected override string KeyOf(ObjectiveHandler handler) => handler.Kind;
    public ObjectiveHandlerRegistry Clone() => CopyInto(new ObjectiveHandlerRegistry());
    public string Role(JsonObject objective) => Get(Text(objective, "type")).Role;
    public string RefreshMode(JsonObject objective) => Get(Text(objective, "type")).Refresh;
    public IReadOnlyList<JsonObject> Children(JsonObject objective) => Get(Text(objective, "type")).Children?.Invoke(objective) ?? [];
    public PayloadReferences References(JsonObject objective) => TryGet(Text(objective, "type"))?.References?.Invoke(objective) ?? new();
    public string Describe(JsonObject objective)
    {
        if (objective["label"] is JsonValue label && label.TryGetValue<string>(out var text) && text.Length > 0) return text;
        var handler = Get(Text(objective, "type"));
        return handler.Describe?.Invoke(objective, this) ?? handler.Label;
    }
    public IReadOnlyList<(string Type, string Label)> Kinds() => All().Select(handler => (handler.Kind, handler.Label)).ToArray();
    public string Evaluate(ObjectiveRules rules, JsonObject state, int owner, JsonObject objective) => Status(state, owner, objective) switch
    {
        "completed" => "success",
        "failed" => "failure",
        "inactive" or "cancelled" => "pending",
        _ => Get(Text(objective, "type")).Outcome(new(rules, state, owner, this), objective),
    };
    public string Progress(ObjectiveRules rules, JsonObject state, int owner, JsonObject objective) => Status(state, owner, objective) switch
    {
        "completed" => "已完成",
        "failed" => "已失败",
        "inactive" => "未激活",
        "cancelled" => "已取消",
        _ => Get(Text(objective, "type")).Progress(new(rules, state, owner, this), objective),
    };
    public static string Status(JsonObject state, int owner, JsonObject objective) => objective["id"] is { } id &&
        Object(Player(state, owner)["objectiveStates"], "objective states")[Text(id, "objective id")] is JsonObject runtime ? Text(runtime, "status") : "active";
}
