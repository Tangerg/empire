using System.Text.Json.Nodes;
using Empire.Core.State;

namespace Empire.Core.Validation;

/// <summary>Unit seed content invariants, independent of battlefield placement or availability.</summary>
internal static class UnitContentValidation
{
    internal static string? TypeIssue(ReadOnlyContent content, string type) =>
        content.Source.TryGet("units", type) is null ? $"未知兵种 \"{type}\"" : null;

    internal static IEnumerable<string> CareerIssues(ReadOnlyContent content, JsonObject unit, string type)
    {
        if (unit["career"] is { } careerNode)
        {
            var id = StateJson.Text(careerNode, "level text");
            var career = content.Source.TryGet("careers", id);
            if (career is null)
                yield return $"单位 {type} 使用未知职业 \"{id}\"";
            else if (StateJson.Text(career["unitType"], "level text") != type)
                yield return $"职业 {id} 与兵种 {type} 不匹配";
        }
        foreach (var career in unit["unlockedCareers"] is { } unlocked
            ? StateJson.Array(unlocked, "level values") : Enumerable.Empty<JsonNode?>())
        {
            var id = StateJson.Text(career, "level text");
            if (content.Source.TryGet("careers", id) is null)
                yield return $"单位 {type} 解锁了未知职业 \"{id}\"";
        }
        foreach (var pair in unit["careerMastery"] as JsonObject ?? new JsonObject())
        {
            if (content.Source.TryGet("careers", pair.Key) is null)
                yield return $"单位 {type} 记录了未知职业熟练度 \"{pair.Key}\"";
            var mastery = RulePayloadReferences.Number(pair.Value);
            if (!double.IsFinite(mastery) || mastery < 0)
                yield return $"单位 {type} 的职业熟练度必须 >= 0";
        }
    }
}
