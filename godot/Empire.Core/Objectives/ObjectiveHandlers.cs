using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.References;
using Empire.Core.Registries;
using Empire.Core.Scenario;
using Empire.Core.State;
using static Empire.Core.State.StateJson;
using static Empire.Core.Lifecycle.TurnOrderSystem;

namespace Empire.Core.Objectives;

public sealed record ObjectiveRules(ReadOnlyContent Content, ScenarioConditionHandlerRegistry ScenarioConditions);

public sealed class ObjectiveAssessment
{
    public string Outcome
    {
        get;
    }
    public JsonElement Facts
    {
        get;
    }

    public ObjectiveAssessment(string outcome, JsonObject facts)
    {
        if (outcome is not ("pending" or "success" or "failure"))
            throw new DomainInvariantException($"Unknown objective outcome '{outcome}'.");
        Outcome = outcome;
        Facts = EcmaJson.ToElement(facts);
    }
}

/// <summary>Detached immutable projection; only the handler computes progress facts.</summary>
public sealed class ObjectiveProgress
{
    public string Id
    {
        get;
    }
    public string Kind
    {
        get;
    }
    public string Status
    {
        get;
    }
    public string Role
    {
        get;
    }
    public JsonElement Definition
    {
        get;
    }
    public JsonElement Facts
    {
        get;
    }
    public IReadOnlyList<ObjectiveProgress> Children
    {
        get;
    }

    internal ObjectiveProgress(JsonObject objective, string status, string role, JsonElement facts, IEnumerable<ObjectiveProgress> children)
    {
        Id = Text(objective, "id");
        Kind = Text(objective, "type");
        Status = status;
        Role = role;
        Definition = EcmaJson.ToElement(objective);
        Facts = facts.Clone();
        Children = System.Array.AsReadOnly(children.ToArray());
    }
}

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

public sealed record ObjectiveHandler(string Kind, Func<ObjectiveEvaluationContext, JsonObject, ObjectiveAssessment> Assess)
{
    public string Role { get; init; } = "primary";
    public string Refresh { get; init; } = "self";
    public Func<JsonObject, IReadOnlyList<JsonObject>>? Children
    {
        get; init;
    }
    public Func<JsonObject, PayloadReferences>? References
    {
        get; init;
    }
}

public sealed class ObjectiveHandlerRegistry() : KeyedRegistry<ObjectiveHandler, ObjectiveHandlerRegistry>("objective handler"), IRuleTreeRegistry
{
    protected override string KeyOf(ObjectiveHandler handler) => handler.Kind;
    public ObjectiveHandlerRegistry Clone() => CopyInto(new ObjectiveHandlerRegistry());
    public string Role(JsonObject objective) => Get(Text(objective, "type")).Role;
    public string RefreshMode(JsonObject objective) => Get(Text(objective, "type")).Refresh;
    public IReadOnlyList<JsonObject> Children(JsonObject objective) => Get(Text(objective, "type")).Children?.Invoke(objective) ?? [];
    public PayloadReferences References(JsonObject objective)
    {
        var references = TryGet(Text(objective, "type"))?.References?.Invoke(objective) ?? new();
        return objective.ContainsKey("label") ? references.Fault("Objective labels belong to presentation metadata, not gameplay definitions.") : references;
    }
    public ObjectiveAssessment Assess(ObjectiveRules rules, JsonObject state, int owner, JsonObject objective) => Status(state, owner, objective) switch
    {
        "completed" => new("success", new()),
        "failed" => new("failure", new()),
        "inactive" or "cancelled" => new("pending", new()),
        "active" => Get(Text(objective, "type")).Assess(new(rules, state, owner, this), objective),
        var status => throw new DomainInvariantException($"Unknown objective status '{status}'."),
    };
    public string Evaluate(ObjectiveRules rules, JsonObject state, int owner, JsonObject objective) => Assess(rules, state, owner, objective).Outcome;
    public ObjectiveProgress Project(ObjectiveRules rules, JsonObject state, int owner, JsonObject objective) =>
        new(objective, Status(state, owner, objective), Role(objective), Assess(rules, state, owner, objective).Facts,
            Children(objective).Select(child => Project(rules, state, owner, child)));
    public static string Status(JsonObject state, int owner, JsonObject objective) => objective["id"] is { } id &&
        Object(Player(state, owner)["objectiveStates"], "objective states")[Text(id, "objective id")] is JsonObject runtime ? Text(runtime, "status") : "active";
}
