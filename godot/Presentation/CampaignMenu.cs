using System.Globalization;
using System.Text.Json;
using Empire.Application;
using Empire.Localization;
using Empire.Core.Determinism;

namespace Empire.Presentation;

public sealed record CampaignMenuView(string Title, string Kicker, string Description, string ArtAlt,
    string ArtSource, string? ArtResourcePath, bool CanContinue, string ContinueLabel, string? Progress,
    string NewLabel, bool NewPrimary, string ReplacePrompt, string? RejectedNotice, string ArtworkFailureLabel);

/// <summary>Source title-screen facts. Status does not change the source's Continue/New labels.</summary>
public static class CampaignMenu
{
    public static CampaignMenuView Create(GameText text, LoadedCampaign loaded, JsonElement metadata)
    {
        if (metadata.GetProperty("schema").GetInt32() != 3)
            throw new InvalidOperationException("Unsupported campaign menu metadata schema.");
        var menu = metadata.GetProperty("menu");
        var art = metadata.GetProperty("menuArt");
        var exists = loaded.State is not null;
        var total = metadata.GetProperty("battleTotal").GetInt32();
        var completed = loaded.State is null ? 0 : loaded.State["battleHistory"]!.AsArray().Count;
        return new(text.Format(Text(menu, "titleKey")), text.Format(Text(menu, "kickerKey")),
            text.Format(Text(menu, "descriptionKey"), text.Format(Text(metadata, "titleKey")), total), text.Format(Text(menu, "artAltKey")),
            Text(art, "source"), art.GetProperty("resourcePath").ValueKind == JsonValueKind.Null ? null : Text(art, "resourcePath"),
            exists, text.Format(Text(menu, "continueLabelKey")), exists ? $"{completed.ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}" : null,
            text.Format(Text(menu, exists ? "restartLabelKey" : "newLabelKey")), !exists,
            text.Format(Text(menu, "replacePromptKey"), text.Format(Text(metadata, "titleKey"))),
            loaded.Rejected is null ? null : text.Format("error.campaign.save_rejected"), text.Format("error.presentation.artwork_unavailable"));
    }

    private static string Text(JsonElement value, string property) =>
        EcmaJson.ReadString(EcmaJson.Parse(value.GetProperty(property).GetRawText()));
}
