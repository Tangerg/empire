using System.Text.Json;

namespace Empire.Presentation.Feedback;

public sealed record BattleResultMetric(string Label, int Value);

/// <summary>Result-screen projection, never a victory decision or a campaign transition.</summary>
public sealed record CampaignBattleSummary(
    string Title, int Chapter, string Outcome, int Turns, int AlliesRemaining, int EnemiesRemaining,
    IReadOnlyList<string> Fallen, IReadOnlyList<string> Signals, int StrikeCount)
{
    public const string Heading = "战斗胜利";
    public const string FallenLabel = "本关倒下";
    public const string SignalsLabel = "关键战场记录";
    public const string ContinueLabel = "查看战后剧情 →";

    public IReadOnlyList<BattleResultMetric> Metrics => new[]
    {
        new BattleResultMetric("回合", Turns), new BattleResultMetric("我方存续", AlliesRemaining),
        new BattleResultMetric("敌方存续", EnemiesRemaining), new BattleResultMetric("交战次数", StrikeCount),
    };
    public string FallenText => Fallen.Count > 0 ? string.Join("、", Fallen) : "无人倒下";
    public string SignalsText => Signals.Count > 0 ? string.Join(" · ", Signals) : "目标按计划完成";

    /// <param name="signals">Ordered signals from the canonical CampaignBattleBridge result.</param>
    /// <param name="strikeCount">Count from the canonical combat event facts owner.</param>
    public static CampaignBattleSummary Create(string title, int chapter, int perspectivePlayer,
        JsonElement state, Func<string, string> unitTypeName, IEnumerable<string> signals, int strikeCount)
    {
        var players = state.GetProperty("players").EnumerateArray().ToArray();
        var ours = players.First(player => player.GetProperty("id").GetInt32() == perspectivePlayer);
        var team = ours.GetProperty("team").GetInt32();
        var units = state.GetProperty("units").EnumerateArray().ToArray();
        var fallen = state.GetProperty("markers").EnumerateArray()
            .Where(marker => marker.TryGetProperty("fallenUnit", out var unit) && unit.ValueKind == JsonValueKind.Object &&
                unit.GetProperty("owner").GetInt32() == perspectivePlayer)
            .Select(marker => unitTypeName(FeedbackJson.Text(marker.GetProperty("fallenUnit").GetProperty("type"))))
            .ToArray();
        var enemies = units.Count(unit => !players.Any(player =>
            player.GetProperty("id").GetInt32() == unit.GetProperty("owner").GetInt32() && player.GetProperty("team").GetInt32() == team));
        return new CampaignBattleSummary(title, chapter, FeedbackJson.Text(state.GetProperty("endReason")),
            state.GetProperty("turn").GetInt32(), units.Count(unit => unit.GetProperty("owner").GetInt32() == perspectivePlayer),
            enemies, Array.AsReadOnly(fallen), Array.AsReadOnly(signals.ToArray()), strikeCount);
    }
}
