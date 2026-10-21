using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using Empire.Core.Lifecycle;
using Empire.Core.Objectives;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

/// <summary>Renders battle identities and semantic conclusions using explicitly composed presentation metadata.</summary>
public sealed class BattleText
{
    private readonly GameText text;
    private readonly BattleTextDocument document;
    private readonly ObjectiveText objectiveText;

    public BattleText(GameText text, BattleTextDocument document, ObjectiveText objectiveText)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(objectiveText);
        this.text = text;
        this.document = document;
        this.objectiveText = objectiveText.Clone().Seal();
        this.objectiveText.ValidateCoverage(document.ObjectiveKinds);
        document.ValidateKeys(text);
        text.ValidateKeys(new[]
        {
            "battle.conclusion.all_defeated", "battle.conclusion.last_team_remaining",
            "battle.conclusion.objective_completed", "battle.conclusion.turn_limit_reached", "battle.conclusion.no_actors",
        });
    }

    public string LevelTitle(string levelId) => document.LevelTitle(text, levelId);
    public string LevelDescription(string levelId) => document.LevelDescription(text, levelId);
    public string SignalName(string levelId, string signalId) => document.SignalName(text, levelId, signalId);

    public string PlayerName(string levelId, int playerId) => document.PlayerName(text, levelId, playerId);

    public string ObjectiveDescription(string levelId, int playerId, ObjectiveProgress objective)
    {
        ArgumentNullException.ThrowIfNull(objective);
        return document.ObjectiveDescription(text, levelId, playerId, objective.Id)
            ?? objectiveText.Describe(objective, child => ObjectiveDescription(levelId, playerId, child));
    }

    public string ObjectiveProgress(ObjectiveProgress objective) => objectiveText.Progress(objective);

    public string DescribeConclusion(JsonElement state)
    {
        var semanticState = State(state);
        var conclusion = BattleConclusion.FromState(semanticState);
        return conclusion is null ? "" : DescribeConclusion(state, conclusion);
    }

    public string DescribeConclusion(JsonElement state, JsonElement conclusion)
    {
        var semanticConclusion = BattleConclusion.Parse(EcmaJson.Parse(conclusion.GetRawText()));
        semanticConclusion.ValidateReferences(State(state));
        return DescribeConclusion(state, semanticConclusion);
    }

    private string DescribeConclusion(JsonElement state, BattleConclusion conclusion) => conclusion.Kind switch
    {
        "allDefeated" => text.Format("battle.conclusion.all_defeated"),
        "lastTeamRemaining" => text.Format("battle.conclusion.last_team_remaining"),
        "objectiveCompleted" => DescribeObjectiveConclusion(state, conclusion),
        "turnLimitReached" => text.Format("battle.conclusion.turn_limit_reached"),
        "noActors" => text.Format("battle.conclusion.no_actors"),
        _ => throw new LocalizationException($"Unknown battle conclusion kind '{conclusion.Kind}'."),
    };

    private string DescribeObjectiveConclusion(JsonElement state, BattleConclusion conclusion)
    {
        var levelId = FeedbackJson.Text(state.GetProperty("levelId"));
        var playerId = conclusion.Player!.Value;
        var owner = state.GetProperty("players").EnumerateArray().Single(player => player.GetProperty("id").GetInt32() == playerId);
        var objective = owner.GetProperty("objectives").EnumerateArray()
            .Single(candidate => FeedbackJson.Text(candidate.GetProperty("id")) == conclusion.Objective);
        return text.Format("battle.conclusion.objective_completed", PlayerName(levelId, playerId),
            DescribeDefinition(levelId, playerId, objective));
    }

    private string DescribeDefinition(string levelId, int playerId, JsonElement objective)
    {
        var authored = document.ObjectiveDescription(text, levelId, playerId, FeedbackJson.Text(objective.GetProperty("id")));
        if (authored is not null) return authored;
        var children = document.Children(objective);
        return objectiveText.Describe(objective, index => DescribeDefinition(levelId, playerId, children[index]));
    }

    private static JsonObject State(JsonElement state) => EcmaJson.Parse(state.GetRawText()) as JsonObject
        ?? throw new LocalizationException("Battle state must be an object.");
}
