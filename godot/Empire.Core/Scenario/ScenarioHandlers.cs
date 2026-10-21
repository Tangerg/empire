using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.References;
using Empire.Core.Registries;
using Empire.Core.State;

namespace Empire.Core.Scenario;

public record ScenarioConditionRules(ReadOnlyContent Content, ScenarioConditionHandlerRegistry ScenarioConditions);
public sealed record ScenarioRules(ReadOnlyContent Content, ScenarioConditionHandlerRegistry ScenarioConditions,
    ScenarioEffectHandlerRegistry ScenarioEffects, ScenarioCommands Commands) : ScenarioConditionRules(Content, ScenarioConditions);

public sealed class ScenarioConditionContext(JsonObject state, ScenarioConditionHandlerRegistry registry, ReadOnlyContent content)
{
    public JsonObject State { get; } = state;
    public ReadOnlyContent Content { get; } = content;
    public IBattleRandomSource Random => registry.Random;
    public bool Evaluate(JsonObject condition) => registry.Evaluate(State, condition, Content);
}

public sealed record ScenarioConditionHandler(string Kind, Func<ScenarioConditionContext, JsonObject, bool> Evaluate)
{
    public Func<JsonObject, IReadOnlyList<JsonObject>>? Children { get; init; }
    public Func<JsonObject, PayloadReferences>? References { get; init; }
}

public sealed class ScenarioConditionHandlerRegistry(IBattleRandomSource random)
    : KeyedRegistry<ScenarioConditionHandler, ScenarioConditionHandlerRegistry>("scenario condition handler"), IRuleTreeRegistry
{
    public IBattleRandomSource Random { get; } = random;
    protected override string KeyOf(ScenarioConditionHandler handler) => handler.Kind;
    public bool Evaluate(JsonObject state, JsonObject condition, ReadOnlyContent content) =>
        Get(StateJson.Text(condition, "type")).Evaluate(new(state, this, content), condition);
    public IReadOnlyList<JsonObject> Children(JsonObject condition) => Get(StateJson.Text(condition, "type")).Children?.Invoke(condition) ?? [];
    public PayloadReferences References(JsonObject condition) => TryGet(StateJson.Text(condition, "type"))?.References?.Invoke(condition) ?? new();
    public ScenarioConditionHandlerRegistry Clone(IBattleRandomSource? random = null) => CopyInto(new ScenarioConditionHandlerRegistry(random ?? Random));
}

public sealed class ScenarioEffectContext(ScenarioRules rules, JsonObject state, Action<JsonObject> emit)
{
    public ScenarioRules Rules { get; } = rules;
    public JsonObject State { get; } = state;
    public Action<JsonObject> Emit { get; } = emit;
    public ReadOnlyContent Content => Rules.Content;
    public IReadOnlyList<JsonObject> Select(JsonObject selector) => ScenarioSelection.SelectUnits(Content, State, selector);
    public IReadOnlyList<JsonObject> SelectMarkers(JsonObject selector) => ScenarioSelection.SelectMarkers(State, selector);
    public IReadOnlyList<JsonObject> Zone(string id) => ScenarioSelection.Zone(State, id).ToArray();
    public IEnumerable<JsonObject> Standing(JsonObject selector)
    {
        foreach (var unit in Select(selector))
            if (Tactics.TacticalQueries.UnitById(State, StateJson.Integer(unit, "id")) is not null) yield return unit;
    }
}

public sealed record ScenarioEffectHandler(string Kind, Action<ScenarioEffectContext, JsonObject> Apply)
{
    public Func<JsonObject, IReadOnlyList<string>> Signals { get; init; } = _ => Array.Empty<string>();
    public Func<JsonObject, PayloadReferences>? References { get; init; }
}

public sealed class ScenarioEffectHandlerRegistry() : KeyedRegistry<ScenarioEffectHandler, ScenarioEffectHandlerRegistry>("scenario effect handler"), IRulePayloadRegistry
{
    protected override string KeyOf(ScenarioEffectHandler handler) => handler.Kind;
    public void Apply(ScenarioRules rules, JsonObject state, JsonObject effect, Action<JsonObject> emit) =>
        Get(StateJson.Text(effect, "type")).Apply(new(rules, state, emit), effect);
    public IReadOnlyList<string> Signals(JsonObject effect)
    {
        var signals = Get(StateJson.Text(effect, "type")).Signals(effect).ToArray();
        if (signals.Any(string.IsNullOrWhiteSpace))
            throw new DomainInvariantException("Authored scenario signal identities must be nonempty.");
        return Array.AsReadOnly(signals);
    }
    public PayloadReferences References(JsonObject effect)
    {
        var handler = TryGet(StateJson.Text(effect, "type"));
        if (handler is null) return new();
        var references = handler.References?.Invoke(effect) ?? new();
        if (handler.Signals(effect).Any(string.IsNullOrWhiteSpace))
            references.Fault("Authored scenario signal identities must be nonempty.");
        return references;
    }
    public ScenarioEffectHandlerRegistry Clone() => CopyInto(new ScenarioEffectHandlerRegistry());
}
