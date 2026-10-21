using System.Text.Json.Nodes;
using Empire.Core.Determinism;
using static Empire.Core.State.StateJson;

namespace Empire.Core.Lifecycle;

/// <summary>The semantic outcome of a concluded battle, independent of presentation.</summary>
public sealed record BattleConclusion
{
    public string Kind { get; }
    public int? Team { get; }
    public int? Player { get; }
    public string? Objective { get; }

    private BattleConclusion(string kind, int? team, int? player = null, string? objective = null)
    {
        Kind = kind;
        Team = team;
        Player = player;
        Objective = objective;
        switch (kind)
        {
            case "allDefeated" or "turnLimitReached" or "noActors" when team is null && player is null && objective is null:
            case "lastTeamRemaining" when team is not null && player is null && objective is null:
            case "objectiveCompleted" when team is not null && player is not null && objective is not null && !EcmaText.IsBlank(objective):
                break;
            default:
                throw new DomainInvariantException("battle conclusion kind and details are inconsistent");
        }
    }

    public static BattleConclusion AllDefeated() => new("allDefeated", null);
    public static BattleConclusion LastTeamRemaining(int team) => new("lastTeamRemaining", team);
    public static BattleConclusion ObjectiveCompleted(int team, int player, string objective) => new("objectiveCompleted", team, player, objective);
    public static BattleConclusion TurnLimitReached() => new("turnLimitReached", null);
    public static BattleConclusion NoActors() => new("noActors", null);

    public static BattleConclusion Parse(JsonNode? raw)
    {
        var value = Object(raw, "battle conclusion");
        var kind = Text(value, "kind");
        var fields = kind == "objectiveCompleted"
            ? new[] { "kind", "team", "player", "objective" }
            : new[] { "kind", "team" };
        if (value.Count != fields.Length || fields.Any(field => !value.ContainsKey(field)))
            throw new DomainInvariantException("battle conclusion fields do not match its kind");
        int? team = value["team"] is { } winner ? Integer(winner, "conclusion team") : null;
        return kind == "objectiveCompleted"
            ? new(kind, team, Integer(value, "player"), Text(value, "objective"))
            : new(kind, team);
    }

    public JsonObject ToJson()
    {
        var result = new JsonObject { ["kind"] = Kind, ["team"] = Team };
        if (Kind == "objectiveCompleted")
        {
            result["player"] = Player;
            result["objective"] = Objective;
        }
        return result;
    }

    public static BattleConclusion? FromState(JsonObject state)
    {
        if (!state.ContainsKey("conclusion") || state.ContainsKey("winnerTeam") || state.ContainsKey("endReason"))
            throw new DomainInvariantException("battle state must contain only the current conclusion representation");
        var phase = Text(state, "phase");
        if (phase is not ("deployment" or "playing" or "over"))
            throw new DomainInvariantException("battle phase is unknown");
        var conclusion = state["conclusion"] is { } raw ? Parse(raw) : null;
        if ((phase == "over") != (conclusion is not null))
            throw new DomainInvariantException("battle phase and conclusion disagree");
        conclusion?.ValidateReferences(state);
        return conclusion;
    }

    public void ValidateReferences(JsonObject state)
    {
        if (Team is null) return;
        var players = Objects(Array(state["players"], "players"), "players").ToArray();
        if (!players.Any(player => Integer(player, "team") == Team))
            throw new DomainInvariantException("battle conclusion references an unknown team");
        if (Kind != "objectiveCompleted") return;
        var owners = players.Where(player => Integer(player, "id") == Player).ToArray();
        if (owners.Length != 1 || Integer(owners[0], "team") != Team)
            throw new DomainInvariantException("battle conclusion player is not a member of its winning team");
        var owner = owners[0];
        if (Objects(Array(owner["objectives"], "objectives"), "objectives").Count(objective => Text(objective, "id") == Objective) != 1)
            throw new DomainInvariantException("battle conclusion references an unknown or ambiguous player objective");
        var runtime = Object(Object(owner["objectiveStates"], "objective states")[Objective!], "concluded objective state");
        if (Text(runtime, "id") != Objective)
            throw new DomainInvariantException("battle conclusion objective runtime identity is inconsistent");
        if (Text(runtime, "status") != "completed")
            throw new DomainInvariantException("battle conclusion objective is not completed");
    }
}
