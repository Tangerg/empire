using System.Text.Json.Nodes;
using Empire.Core.Engine;
using static Empire.Core.State.StateJson;
using static Empire.Core.Tactics.TacticalQueries;

namespace Empire.Core.Ai;

/// <summary>Original one-ply policy, using the execution engine's legality and forecasts.</summary>
public sealed class AiPlanner
{
    private readonly GrayFlagEngine engine;
    public AiIntentRegistry Intents { get; }
    public AbilityAiEvaluatorRegistry Abilities { get; }
    public AiObjectiveAdvisorRegistry Objectives { get; }

    public AiPlanner(GrayFlagEngine engine, AiIntentRegistry intents,
        AbilityAiEvaluatorRegistry abilities, AiObjectiveAdvisorRegistry objectives)
    { this.engine = engine; Intents = intents; Abilities = abilities; Objectives = objectives; }

    public JsonObject ChooseAction(JsonObject state, double? aggression = null)
    {
        if (Text(state, "phase") == "deployment") return new JsonObject { ["kind"] = "finishDeployment" };
        var side = Integer(state, "currentPlayer");
        var policy = aggression ?? Number(Player(state, side)["ai"]!["aggression"], "aggression");
        return Intents.Choose(new AiTurnContext(engine, state, side, policy, Abilities, Objectives)) ??
            new JsonObject { ["kind"] = "endTurn" };
    }
}
