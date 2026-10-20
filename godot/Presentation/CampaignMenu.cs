using System.Globalization;
using System.Text.Json;
using Empire.Application;
using Empire.Core.Determinism;

namespace Empire.Presentation;

public sealed record CampaignMenuView(string Title, string Kicker, string Description, string ArtAlt,
    string ArtSource, string? ArtResourcePath, bool CanContinue, string ContinueLabel, string? Progress,
    string NewLabel, bool NewPrimary, string ReplacePrompt, string? RejectedNotice);

/// <summary>Source title-screen facts. Status does not change the source's Continue/New labels.</summary>
public static class CampaignMenu
{
    public static CampaignMenuView Create(LoadedCampaign loaded, JsonElement metadata)
    {
        if (metadata.GetProperty("schema").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported campaign menu metadata schema.");
        var menu = metadata.GetProperty("menu");
        var art = metadata.GetProperty("menuArt");
        var exists = loaded.State is not null;
        var total = metadata.GetProperty("battleTotal").GetInt32();
        var completed = loaded.State is null ? 0 : loaded.State["battleHistory"]!.AsArray().Count;
        return new(Text(menu, "title"), Text(menu, "kicker"), Text(menu, "description"), Text(menu, "artAlt"),
            Text(art, "source"), art.GetProperty("resourcePath").ValueKind == JsonValueKind.Null ? null : Text(art, "resourcePath"),
            exists, Text(menu, "continueLabel"), exists ? $"{completed.ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}" : null,
            Text(menu, exists ? "restartLabel" : "newLabel"), !exists, Text(menu, "replacePrompt"),
            loaded.Rejected is null ? null : $"战役存档无法读取，进度已保留但未载入：{loaded.Rejected}");
    }

    private static string Text(JsonElement value, string property) =>
        EcmaJson.ReadString(EcmaJson.Parse(value.GetProperty(property).GetRawText()));
}
