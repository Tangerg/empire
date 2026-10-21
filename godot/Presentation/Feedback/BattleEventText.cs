using System.Text.Json;
using Empire.Localization;

namespace Empire.Presentation.Feedback;

/// <summary>The descriptive half of the source event presenters, with no animation or rule transitions.</summary>
public sealed class BattleEventText
{
    private readonly Dictionary<string, Func<BattleLogContext, JsonElement, string>> entries = new(StringComparer.Ordinal);
    private bool sealedRegistry;

    public BattleEventText Register(string type, Func<BattleLogContext, JsonElement, string> describe)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(describe);
        if (!entries.TryAdd(type, describe)) throw new InvalidOperationException($"battle event text already registered: \"{type}\"");
        return this;
    }

    public BattleEventText Replace(string type, Func<BattleLogContext, JsonElement, string> describe)
    {
        RequireMutable();
        ArgumentNullException.ThrowIfNull(describe);
        entries[type] = describe;
        return this;
    }

    public IReadOnlyList<string> Keys() => entries.Keys.ToArray();
    public BattleEventText Seal() { sealedRegistry = true; return this; }
    public BattleEventText Clone()
    {
        var result = new BattleEventText();
        foreach (var entry in entries) result.Register(entry.Key, entry.Value);
        return result;
    }

    public string Describe(BattleLogContext context, JsonElement value) =>
        entries.TryGetValue(FeedbackJson.Text(value.GetProperty("type")), out var describe) ? describe(context, value) : "";

    private void RequireMutable()
    {
        if (sealedRegistry) throw new InvalidOperationException("battle event text registry is sealed after composition");
    }

    private static double Number(JsonElement value, string key) => value.GetProperty(key).GetDouble();
    private static double Id(JsonElement value, string key) => Number(value, key);

    public static BattleEventText CreateDefault(GameText text, BattleText battleText)
    {
        text.ValidateKeys(new[] { "battle.event.attack", "battle.event.counter", "battle.event.support", "battle.event.parting",
            "battle.event.area", "battle.event.structure", "battle.event.area_structure", "battle.event.target_killed",
            "battle.event.ally_killed", "battle.event.structure_destroyed", "battle.event.heal", "battle.event.recruit",
            "battle.event.capture", "battle.event.capture_progress", "battle.event.turn", "battle.event.resource",
            "battle.event.rank", "battle.event.career", "battle.event.facing", "battle.event.defeat",
            "ui.rank.recruit", "ui.rank.veteran", "ui.rank.elite" });
        string Casualty(JsonElement value, string field = "killed", string key = "battle.event.target_killed") =>
            value.GetProperty(field).GetBoolean() ? text.Format(key) : "";
        string Rank(double value) => text.Format(value switch
        {
            0 => "ui.rank.recruit", 1 => "ui.rank.veteran", 2 => "ui.rank.elite",
            _ => throw new InvalidOperationException($"Unknown rank in committed event: {value}."),
        });
        return new BattleEventText()
            .Register("attack", (context, value) => text.Format("battle.event.attack", context.UnitName(Id(value, "attacker")), Number(value, "damage"), Casualty(value)))
            .Register("counter", (_, value) => text.Format("battle.event.counter", Number(value, "damage"), Casualty(value, key: "battle.event.ally_killed")))
            .Register("supportAttack", (context, value) => text.Format("battle.event.support", context.UnitName(Id(value, "attacker")), Number(value, "damage"), Casualty(value)))
            .Register("partingShot", (context, value) => text.Format("battle.event.parting", context.UnitName(Id(value, "attacker")), Number(value, "damage"), Casualty(value)))
            .Register("areaAttack", (context, value) => text.Format("battle.event.area", context.UnitName(Id(value, "defender")), Number(value, "damage"), Casualty(value)))
            .Register("attackStructure", (_, value) => text.Format("battle.event.structure", Number(value, "damage"), Casualty(value, "destroyed", "battle.event.structure_destroyed")))
            .Register("areaAttackStructure", (_, value) => text.Format("battle.event.area_structure", Number(value, "damage"), Casualty(value, "destroyed", "battle.event.structure_destroyed")))
            .Register("death", (_, _) => "")
            .Register("heal", (context, value) => text.Format("battle.event.heal", context.UnitName(Id(value, "source")), Number(value, "amount")))
            .Register("recruit", (context, value) => text.Format("battle.event.recruit", context.PlayerName(context.State.GetProperty("currentPlayer").GetDouble()), context.UnitName(Id(value, "unit"))))
            .Register("capture", (context, value) => value.GetProperty("captured").GetBoolean()
                ? text.Format("battle.event.capture", context.PlayerName(Id(value, "player")), Number(value.GetProperty("at"), "x"), Number(value.GetProperty("at"), "y"))
                : text.Format("battle.event.capture_progress"))
            .Register("turnStart", (context, value) => text.Format("battle.event.turn", Number(value, "turn"), context.PlayerName(Id(value, "player"))))
            .Register("resourceChanged", (context, value) => text.Format("battle.event.resource", context.HolderName(value.GetProperty("subject")),
                context.ResourceName(FeedbackJson.Text(value.GetProperty("resource"))), Number(value, "amount") >= 0 ? "+" : "", Number(value, "amount"), Number(value, "current")))
            .Register("rankChanged", (context, value) => text.Format("battle.event.rank", context.UnitName(Id(value, "unit")), Rank(Id(value, "to"))))
            .Register("careerChanged", (context, value) => text.Format("battle.event.career", context.UnitName(Id(value, "unit")), context.CareerName(FeedbackJson.Text(value.GetProperty("to")))))
            .Register("facingChanged", (context, value) => text.Format("battle.event.facing", context.UnitName(Id(value, "unit"))))
            .Register("defeat", (context, value) => text.Format("battle.event.defeat", context.PlayerName(Id(value, "player"))))
            .Register("gameOver", (context, value) => battleText.DescribeConclusion(context.State, value.GetProperty("conclusion")))
            .Seal();
    }
}
