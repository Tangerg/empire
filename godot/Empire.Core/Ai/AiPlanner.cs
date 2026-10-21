using System.Text.Json.Nodes;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

/// <summary>Original one-ply policy, using the execution engine's legality and forecasts.</summary>
public sealed class AiPlanner
{
    public AiPlanningDependencies Planning { get; }
    public AiIntentRegistry Intents { get; }
    public AbilityAiEvaluatorRegistry Abilities => Planning.AbilityEvaluators;
    public AiObjectiveAdvisorRegistry Objectives => Planning.ObjectiveAdvisors;

    public AiPlanner(AiPlanningDependencies planning, AiIntentRegistry intents)
    { Planning = planning; Intents = intents; }

    public JsonObject ChooseAction(JsonObject state, double? aggression = null)
    {
        if (Text(state, "phase") == "deployment") return new JsonObject { ["kind"] = "finishDeployment" };
        var side = Integer(state, "currentPlayer");
        var policy = aggression ?? Number(Player(state, side)["ai"]!["aggression"], "aggression");
        return Intents.Choose(new AiTurnContext(Planning, state, side, policy)) ??
            new JsonObject { ["kind"] = "endTurn" };
    }
}
