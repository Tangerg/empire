using System.Text.Json.Nodes;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

public sealed record AiPlannerRules(AiPlanningDependencies Planning, AiIntentRegistry Intents);

/// <summary>One-ply policy using shared mission position value and the execution engine's legality and forecasts.</summary>
public sealed class AiPlanner
{
    public AiPlanningDependencies Planning
    {
        get;
    }
    public AiIntentRegistry Intents
    {
        get;
    }
    public AbilityAiEvaluatorRegistry Abilities => Planning.AbilityEvaluators;
    public AiObjectiveAdvisorRegistry Objectives => Planning.ObjectiveAdvisors;

    public AiPlanner(AiPlannerRules rules)
    {
        Planning = rules.Planning;
        Intents = rules.Intents;
    }

    public JsonObject ChooseAction(JsonObject state, double? aggression = null)
    {
        if (Text(state, "phase") == "deployment")
            return new JsonObject { ["kind"] = "finishDeployment" };
        var side = Integer(state, "currentPlayer");
        var policy = aggression ?? Number(Player(state, side)["ai"]!["aggression"], "aggression");
        return Intents.Choose(new AiTurnContext(Planning, state, side, policy)) ??
            new JsonObject { ["kind"] = "endTurn" };
    }
}
