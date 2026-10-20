using System.Text.Json.Nodes;
using Empire.Core.Engine;
using Empire.Core.Geometry;

namespace Empire.Core.Ai;

public abstract class AiRegistry<T> where T : class
{
    private readonly Dictionary<string, T> entries = new(StringComparer.Ordinal);
    private readonly string subject;
    protected AiRegistry(string subject) => this.subject = subject;
    protected abstract string KeyOf(T entry);
    public bool IsSealed { get; private set; }
    public AiRegistry<T> Register(T entry)
    {
        RequireMutable();
        if (!entries.TryAdd(KeyOf(entry), entry)) throw new DomainInvariantException($"{subject} already registered: \"{KeyOf(entry)}\"");
        return this;
    }
    public AiRegistry<T> Replace(T entry) { RequireMutable(); entries[KeyOf(entry)] = entry; return this; }
    public T Get(string key) => entries.GetValueOrDefault(key) ?? throw new DomainInvariantException($"unknown {subject} \"{key}\"");
    public T? TryGet(string key) => entries.GetValueOrDefault(key);
    public IReadOnlyList<string> Keys() => entries.Keys.ToArray();
    public IReadOnlyList<T> All() => entries.Values.ToArray();
    public AiRegistry<T> Seal() { IsSealed = true; return this; }
    protected R CopyInto<R>(R target) where R : AiRegistry<T> { foreach (var entry in entries.Values) target.Register(entry); return target; }
    private void RequireMutable() { if (IsSealed) throw new DomainInvariantException($"{subject} registry is sealed after composition"); }
}

public sealed record AiIntent(string Id, int Priority, Func<AiTurnContext, JsonObject?> Propose);
public sealed class AiIntentRegistry : AiRegistry<AiIntent>
{
    public AiIntentRegistry() : base("AI intent") { }
    protected override string KeyOf(AiIntent entry) => entry.Id;
    public IReadOnlyList<AiIntent> Ordered() => All().OrderBy(intent => intent.Priority).ThenBy(intent => intent.Id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> Ids() => Ordered().Select(intent => intent.Id).ToArray();
    public JsonObject? Choose(AiTurnContext context)
    {
        foreach (var intent in Ordered()) if (intent.Propose(context) is { } action) return action;
        return null;
    }
    public AiIntentRegistry Clone() => CopyInto(new AiIntentRegistry());
}

public sealed record AbilityAiEvaluationContext(AiTurnContext Turn, JsonObject Unit, Coord At,
    CommandOption Option, Coord? Target, double TileValue);
public sealed record AbilityAiEvaluator(string Ability, Func<AbilityAiEvaluationContext, double?> Score);
public sealed class AbilityAiEvaluatorRegistry : AiRegistry<AbilityAiEvaluator>
{
    public AbilityAiEvaluatorRegistry() : base("AI ability evaluator") { }
    protected override string KeyOf(AbilityAiEvaluator entry) => entry.Ability;
    public AbilityAiEvaluatorRegistry Clone() => CopyInto(new AbilityAiEvaluatorRegistry());
}

public sealed record AiObjectiveAdvisor(string Kind, Action<AiObjectiveAdviceContext, JsonObject> Advise);
public sealed class AiObjectiveAdvisorRegistry : AiRegistry<AiObjectiveAdvisor>
{
    public AiObjectiveAdvisorRegistry() : base("AI objective advisor") { }
    protected override string KeyOf(AiObjectiveAdvisor entry) => entry.Kind;
    public AiObjectiveAdvisorRegistry Clone() => CopyInto(new AiObjectiveAdvisorRegistry());
}
