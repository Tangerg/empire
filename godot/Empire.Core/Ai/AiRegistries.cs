using System.Text.Json.Nodes;
using Empire.Core.Abilities;
using Empire.Core.Geometry;
using Empire.Core.Registries;

namespace Empire.Core.Ai;

public sealed record AiIntent(string Id, double Priority, Func<AiTurnContext, JsonObject?> Propose) : IPrioritizedEntry;
public sealed class AiIntentRegistry : PriorityRegistry<AiIntent, AiIntentRegistry>
{
    public AiIntentRegistry() : base("AI intent") { }
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
public sealed class AbilityAiEvaluatorRegistry : KeyedRegistry<AbilityAiEvaluator, AbilityAiEvaluatorRegistry>
{
    public AbilityAiEvaluatorRegistry() : base("AI ability evaluator") { }
    protected override string KeyOf(AbilityAiEvaluator entry) => entry.Ability;
    public AbilityAiEvaluatorRegistry Clone() => CopyInto(new AbilityAiEvaluatorRegistry());
}

public sealed record AiObjectiveAdvisor(string Kind, Action<AiObjectiveAdviceContext, JsonObject> Advise);
public sealed class AiObjectiveAdvisorRegistry : KeyedRegistry<AiObjectiveAdvisor, AiObjectiveAdvisorRegistry>
{
    public AiObjectiveAdvisorRegistry() : base("AI objective advisor") { }
    protected override string KeyOf(AiObjectiveAdvisor entry) => entry.Kind;
    public AiObjectiveAdvisorRegistry Clone() => CopyInto(new AiObjectiveAdvisorRegistry());
}
