using System.Text.Json;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

public sealed record BattleResultMetric(string Label, int Value);
public sealed record CampaignSummaryText(GameText Messages, BattleText Battle, Func<string, string> UnitTypeName);

/// <summary>Detached result-screen projection, never a victory decision or a campaign transition.</summary>
public sealed class CampaignBattleSummary
{
    public string Title { get; }
    public int Chapter { get; }
    public string Outcome { get; }
    public int Turns { get; }
    public int AlliesRemaining { get; }
    public int EnemiesRemaining { get; }
    public IReadOnlyList<string> Fallen { get; }
    public IReadOnlyList<string> Signals { get; }
    public int StrikeCount { get; }
    public string Heading { get; }
    public string FallenLabel { get; }
    public string SignalsLabel { get; }
    public string ContinueLabel { get; }
    public IReadOnlyList<BattleResultMetric> Metrics { get; }
    public string FallenText { get; }
    public string SignalsText { get; }

    public CampaignBattleSummary(GameText text, string title, int chapter, string outcome, int turns,
        int alliesRemaining, int enemiesRemaining, IEnumerable<string> fallen, IEnumerable<string> signals, int strikeCount)
    {
        Title = title; Chapter = chapter; Outcome = outcome; Turns = turns;
        AlliesRemaining = alliesRemaining; EnemiesRemaining = enemiesRemaining; StrikeCount = strikeCount;
        Fallen = Array.AsReadOnly(fallen.ToArray()); Signals = Array.AsReadOnly(signals.ToArray());
        Heading = text.Format("ui.result.summary_heading");
        FallenLabel = text.Format("ui.result.fallen_heading");
        SignalsLabel = text.Format("ui.result.signals_heading");
        ContinueLabel = text.Format("ui.result.continue");
        Metrics = Array.AsReadOnly(new[]
        {
            new BattleResultMetric(text.Format("ui.result.turns"), Turns),
            new BattleResultMetric(text.Format("ui.result.allies_remaining"), AlliesRemaining),
            new BattleResultMetric(text.Format("ui.result.enemies_remaining"), EnemiesRemaining),
            new BattleResultMetric(text.Format("ui.result.strikes"), StrikeCount),
        });
        FallenText = Fallen.Count > 0 ? string.Join(text.Format("ui.result.fallen_separator"), Fallen) : text.Format("ui.result.none_fallen");
        SignalsText = Signals.Count > 0 ? string.Join(text.Format("ui.result.signal_separator"), Signals) : text.Format("ui.result.no_signals");
    }

    /// <param name="signals">Ordered signal identities from the canonical CampaignBattleBridge result.</param>
    /// <param name="strikeCount">Count from the canonical combat event facts owner.</param>
    public static CampaignBattleSummary Create(CampaignSummaryText presentation, int chapter, int perspectivePlayer,
        JsonElement state, IEnumerable<string> signals, int strikeCount)
    {
        var levelId = FeedbackJson.Text(state.GetProperty("levelId"));
        var players = state.GetProperty("players").EnumerateArray().ToArray();
        var ours = players.First(player => player.GetProperty("id").GetInt32() == perspectivePlayer);
        var team = ours.GetProperty("team").GetInt32();
        var units = state.GetProperty("units").EnumerateArray().ToArray();
        var fallen = state.GetProperty("markers").EnumerateArray()
            .Where(marker => marker.TryGetProperty("fallenUnit", out var unit) && unit.ValueKind == JsonValueKind.Object &&
                unit.GetProperty("owner").GetInt32() == perspectivePlayer)
            .Select(marker => presentation.UnitTypeName(FeedbackJson.Text(marker.GetProperty("fallenUnit").GetProperty("type"))))
            .ToArray();
        var enemies = units.Count(unit => !players.Any(player =>
            player.GetProperty("id").GetInt32() == unit.GetProperty("owner").GetInt32() && player.GetProperty("team").GetInt32() == team));
        return new CampaignBattleSummary(presentation.Messages, presentation.Battle.LevelTitle(levelId), chapter,
            presentation.Battle.DescribeConclusion(state), state.GetProperty("turn").GetInt32(),
            units.Count(unit => unit.GetProperty("owner").GetInt32() == perspectivePlayer), enemies, fallen,
            signals.Select(id => presentation.Battle.SignalName(levelId, id)), strikeCount);
    }
}
