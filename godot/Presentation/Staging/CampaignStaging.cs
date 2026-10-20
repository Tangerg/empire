using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Empire.Core.Determinism;

namespace Empire.Presentation;

public sealed record CampaignStagingArt(string Source, string? ResourcePath);
public sealed record CampaignStagingUnit(string Id, string UnitType, string Name, string Status,
    string? ActorKey, CampaignStagingArt? Portrait);
public sealed record CampaignStagingResource(string Id, string Label, string Amount);
public sealed record CampaignStagingRelation(string Id, string Label, string Amount, double Fill);

/// <summary>Detached presentation of campaign facts and source adapter metadata, never a campaign transition.</summary>
public sealed class CampaignStagingView
{
    public string LevelId { get; }
    public string CampaignTitle { get; }
    public string ChapterHeading { get; }
    public string Progress { get; }
    public int CompletedBattles { get; }
    public int BattleTotal { get; }
    public string Eyebrow { get; }
    public string Title { get; }
    public string Description { get; }
    public string ActionNote { get; }
    public string StartLabel { get; }
    public CampaignStagingArt ChapterArt { get; }
    public CampaignStagingArt? BriefingArt { get; }
    public IReadOnlyList<CampaignStagingUnit> Roster { get; }
    public IReadOnlyList<CampaignStagingResource> Resources { get; }
    public IReadOnlyList<CampaignStagingRelation> Relations { get; }
    public const string RosterHeading = "出战名册";
    public const string StateHeading = "战役状态";
    public const string EmptyResources = "暂无战役资源";
    public const string EmptyRelations = "尚未形成明确阵营关系。";

    internal CampaignStagingView(string levelId, string campaignTitle, string chapterHeading, string progress,
        int completedBattles, int battleTotal, string eyebrow, string title, string description, string actionNote, string startLabel,
        CampaignStagingArt chapterArt, CampaignStagingArt? briefingArt, IEnumerable<CampaignStagingUnit> roster,
        IEnumerable<CampaignStagingResource> resources, IEnumerable<CampaignStagingRelation> relations)
    {
        LevelId = levelId; CampaignTitle = campaignTitle; ChapterHeading = chapterHeading; Progress = progress;
        CompletedBattles = completedBattles; BattleTotal = battleTotal;
        Eyebrow = eyebrow; Title = title; Description = description; ActionNote = actionNote; StartLabel = startLabel;
        ChapterArt = chapterArt; BriefingArt = briefingArt;
        Roster = Array.AsReadOnly(roster.ToArray()); Resources = Array.AsReadOnly(resources.ToArray());
        Relations = Array.AsReadOnly(relations.ToArray());
    }
}

public static class CampaignStaging
{
    public static CampaignStagingView Create(JsonElement state, JsonElement battleNode, JsonElement level,
        JsonElement metadata, Func<string, string> unitTypeName, bool hasPendingRequest)
    {
        if (metadata.GetProperty("schema").GetInt32() != 1) throw new InvalidOperationException("Unsupported campaign staging metadata schema.");
        var id = Text(level.GetProperty("id"));
        if (Text(battleNode.GetProperty("type")) != "battle" || Text(battleNode.GetProperty("level")) != id ||
            Text(battleNode.GetProperty("id")) != Text(state.GetProperty("currentNode")))
            throw new InvalidOperationException("Staging level and current battle node do not match.");
        var presentation = metadata.GetProperty("levels").GetProperty(id);
        var completed = state.GetProperty("battleHistory").GetArrayLength();
        var battleTotal = metadata.GetProperty("battleTotal").GetInt32();
        var chapter = presentation.GetProperty("chapter").GetInt32();
        var order = presentation.GetProperty("order").GetInt32();
        var bindings = battleNode.TryGetProperty("rosterBindings", out var declared)
            ? declared.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
        var joins = metadata.GetProperty("joinAfter");
        var portraits = metadata.GetProperty("portraits");
        var roster = new List<CampaignStagingUnit>();
        foreach (var entry in state.GetProperty("roster").EnumerateObject())
        {
            var unit = entry.Value;
            var unitId = Text(unit.GetProperty("id"));
            if (joins.TryGetProperty(unitId, out var after) && after.GetDouble() > completed) continue;
            var type = Text(unit.GetProperty("unitType"));
            var disposition = Text(unit.GetProperty("disposition"));
            var rank = unit.TryGetProperty("rank", out var rawRank) && rawRank.ValueKind != JsonValueKind.Null ? Text(rawRank) : "0";
            var status = disposition == "available"
                ? $"生命 {Number(Math.Floor(unit.GetProperty("hpRatio").GetDouble() * 100 + 0.5))}% · 军衔 {rank}" : disposition;
            var binding = bindings.FirstOrDefault(candidate => Text(candidate.GetProperty("campaignUnit")) == unitId);
            roster.Add(new CampaignStagingUnit(unitId, type, unitTypeName(type), status,
                binding.ValueKind == JsonValueKind.Undefined ? null : Text(binding.GetProperty("levelUnitKey")),
                portraits.TryGetProperty(unitId, out var portrait) ? Art(portrait) : null));
        }
        var resources = state.GetProperty("resources").EnumerateObject().Select(entry =>
            new CampaignStagingResource(entry.Name, Label(metadata.GetProperty("resourceLabels"), entry.Name), Text(entry.Value)));
        var relations = state.GetProperty("relations").EnumerateObject().Where(entry => entry.Value.GetDouble() != 0).Take(5).Select(entry =>
        {
            var amount = entry.Value.GetDouble();
            return new CampaignStagingRelation(entry.Name, Label(metadata.GetProperty("relationLabels"), entry.Name),
                (amount > 0 ? "+" : "") + Text(entry.Value), Math.Clamp((amount + 3) / 6, 0, 1));
        });
        return new CampaignStagingView(id, Text(metadata.GetProperty("title")),
            $"第 {Number(chapter)} 章 · {Text(presentation.GetProperty("chapterTitle"))}", $"{Number(completed)}/{Number(battleTotal)}",
            completed, battleTotal, $"作战准备 · {order.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0')}/{Number(battleTotal)}",
            Text(level.GetProperty("name")), level.TryGetProperty("description", out var description) && description.ValueKind != JsonValueKind.Null ? Text(description) : "",
            hasPendingRequest ? "离开战场后将从本关开局重新开始。" : "进入战斗后自动保留关前存档。",
            (hasPendingRequest ? "重新进入战斗" : "开始战斗") + " →",
            Art(presentation.GetProperty("chapterArt")), presentation.GetProperty("briefingArt").ValueKind == JsonValueKind.Null ? null : Art(presentation.GetProperty("briefingArt")),
            roster, resources, relations);
    }

    private static CampaignStagingArt Art(JsonElement value) => new(Text(value.GetProperty("source")),
        value.GetProperty("resourcePath").ValueKind == JsonValueKind.Null ? null : Text(value.GetProperty("resourcePath")));
    private static string Label(JsonElement labels, string id) => labels.TryGetProperty(id, out var value) ? Text(value) : id;
    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? EcmaJson.ReadString(EcmaJson.Parse(value.GetRawText())) : EcmaJson.Stringify(EcmaJson.Parse(value.GetRawText()));
    private static string Number(double value) => EcmaJson.Stringify(JsonValue.Create(value));
}
