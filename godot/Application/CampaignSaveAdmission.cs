using System.Text.Json.Nodes;
using Empire.Core;
using Empire.Core.Campaign;

namespace Empire.Application;

/// <summary>Admits campaign saves against the same composed unit content as battle definitions.</summary>
internal static class CampaignSaveAdmission
{
    internal static JsonObject Load(Func<JsonObject, IReadOnlyList<string>> unitContentIssues,
        JsonObject definition, JsonNode? document)
    {
        ValidateDefinition(unitContentIssues, definition);
        var state = CampaignSave.Load(document, definition)["state"]!.AsObject();
        var issues = ContentIssues(unitContentIssues, state);
        if (issues.Count > 0)
            throw new StoredDocumentException("Saved campaign roster references invalid content: " + string.Join("; ", issues));
        return state;
    }

    internal static void ValidateDefinition(Func<JsonObject, IReadOnlyList<string>> unitContentIssues, JsonObject definition) =>
        ValidateRuntimeState(unitContentIssues, CampaignValidation.CreateState(definition));

    internal static void ValidateRuntimeState(Func<JsonObject, IReadOnlyList<string>> unitContentIssues, JsonObject state)
    {
        var issues = ContentIssues(unitContentIssues, state);
        if (issues.Count > 0)
            throw new CampaignInvariantException("Campaign roster references invalid content: " + string.Join("; ", issues));
    }

    private static IReadOnlyList<string> ContentIssues(Func<JsonObject, IReadOnlyList<string>> unitContentIssues, JsonObject state)
    {
        var issues = new List<string>();
        foreach (var (id, value) in state["roster"]!.AsObject())
        {
            var unit = value!.AsObject();
            var seed = new JsonObject { ["key"] = id, ["unit"] = unit["unitType"]!.DeepClone() };
            foreach (var field in new[] { "career", "unlockedCareers", "careerMastery", "learnedAbilities" })
                if (unit.ContainsKey(field))
                    seed[field] = unit[field]?.DeepClone();
            issues.AddRange(unitContentIssues(seed).Select(issue => $"{id}: {issue}"));
        }
        return issues;
    }
}
