using System.Text.Json;

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

    private static string Number(JsonElement value, string key) => FeedbackJson.Text(value.GetProperty(key));
    private static double Id(JsonElement value, string key) => value.GetProperty(key).GetDouble();
    private static string Casualty(JsonElement value, string key = "killed", string phrasing = "，目标阵亡") =>
        value.GetProperty(key).GetBoolean() ? phrasing : "";

    public static BattleEventText CreateDefault() => new BattleEventText()
        .Register("attack", (context, value) =>
            $"{context.UnitName(Id(value, "attacker"))} 造成 {Number(value, "damage")} 点伤害{Casualty(value)}")
        .Register("counter", (_, value) =>
            $"反击造成 {Number(value, "damage")} 点伤害{Casualty(value, phrasing: "，我方阵亡")}")
        .Register("supportAttack", (context, value) =>
            $"{context.UnitName(Id(value, "attacker"))} 援护攻击造成 {Number(value, "damage")} 点伤害{Casualty(value)}")
        .Register("partingShot", (context, value) =>
            $"{context.UnitName(Id(value, "attacker"))} 借机攻击造成 {Number(value, "damage")} 点伤害{Casualty(value)}")
        .Register("areaAttack", (context, value) =>
            $"范围攻击对 {context.UnitName(Id(value, "defender"))} 造成 {Number(value, "damage")} 点伤害{Casualty(value)}")
        .Register("attackStructure", (_, value) =>
            $"对结构造成 {Number(value, "damage")} 点伤害{Casualty(value, "destroyed", "，结构被摧毁")}")
        .Register("areaAttackStructure", (_, value) =>
            $"范围攻击对结构造成 {Number(value, "damage")} 点伤害{Casualty(value, "destroyed", "，结构被摧毁")}")
        .Register("death", (_, _) => "")
        .Register("heal", (context, value) =>
            $"{context.UnitName(Id(value, "source"))} 治疗了 {Number(value, "amount")} 点生命")
        .Register("recruit", (context, value) =>
            $"{context.PlayerName(context.State.GetProperty("currentPlayer").GetDouble())} 征募了 {context.UnitName(Id(value, "unit"))}")
        .Register("capture", (context, value) => value.GetProperty("captured").GetBoolean()
            ? $"{context.PlayerName(Id(value, "player"))} 占领了 ({Number(value.GetProperty("at"), "x")}, {Number(value.GetProperty("at"), "y")})" : "占领进度提升")
        .Register("turnStart", (context, value) => $"第 {Number(value, "turn")} 回合 · {context.PlayerName(Id(value, "player"))}")
        .Register("resourceChanged", (context, value) =>
            $"{context.HolderName(value.GetProperty("subject"))} {context.ResourceName(FeedbackJson.Text(value.GetProperty("resource")))} {(value.GetProperty("amount").GetDouble() >= 0 ? "+" : "")}{Number(value, "amount")}（{Number(value, "current")}）")
        .Register("rankChanged", (context, value) =>
            $"{context.UnitName(Id(value, "unit"))} 晋升为 {RankName(Id(value, "to"))}")
        .Register("careerChanged", (context, value) =>
            $"{context.UnitName(Id(value, "unit"))} 转职为 {context.CareerName(FeedbackJson.Text(value.GetProperty("to")))}")
        .Register("facingChanged", (context, value) => $"{context.UnitName(Id(value, "unit"))} 调整了朝向")
        .Register("defeat", (context, value) => $"{context.PlayerName(Id(value, "player"))} 已被击败")
        .Register("gameOver", (_, value) => FeedbackJson.Text(value.GetProperty("reason")))
        .Seal();

    private static string RankName(double rank) => rank switch { 0 => "新兵", 1 => "老兵", 2 => "精英", _ => "undefined" };
}
